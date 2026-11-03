using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace DynamicCube.Modules.Decisions.Runtime.Matrix;

/// <summary>
/// Converts the classification guide's formulas (<c>[metric] &gt; 0 &amp;&amp; [other] == 'yes'</c>,
/// <c>IIF(c, a, b)</c>, arithmetic, <c>null</c>/<c>true</c>/<c>false</c>) to JSONLogic. Inside project conditions bare
/// names are project fields (or literal words such as <c>gov</c>) and <c>field &gt;= as_of - 3y</c> becomes
/// <c>field_yearsAgo &lt;= 3</c>.
/// </summary>
public static class GuideExpressions
{
    public static JsonNode? Convert(string? expression, ISet<string>? projectFields = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var parser = new Parser(Tokenize(expression), projectFields);
        var node = parser.Or();
        parser.End();
        return node;
    }

    private enum Kind
    {
        Number,
        Text,
        Metric,
        Name,
        Years,
        Symbol,
    }

    private sealed record Token(Kind Kind, string Value);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '[')
            {
                var end = text.IndexOf(']', i);
                if (end < 0)
                {
                    throw new FormatException($"Missing ] in '{text}'.");
                }

                tokens.Add(new Token(Kind.Metric, text[(i + 1)..end].Trim()));
                i = end + 1;
            }
            else if (c is '\'' or '"')
            {
                var end = text.IndexOf(c, i + 1);
                if (end < 0)
                {
                    throw new FormatException($"Missing quote in '{text}'.");
                }

                tokens.Add(new Token(Kind.Text, text[(i + 1)..end]));
                i = end + 1;
            }
            else if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                {
                    i++;
                }

                if (i < text.Length && text[i] == 'y' && (i + 1 == text.Length || !char.IsLetterOrDigit(text[i + 1])))
                {
                    tokens.Add(new Token(Kind.Years, text[start..i]));
                    i++;
                }
                else
                {
                    tokens.Add(new Token(Kind.Number, text[start..i]));
                }
            }
            else if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new Token(Kind.Name, text[start..i]));
            }
            else
            {
                var two = i + 1 < text.Length ? text.Substring(i, 2) : string.Empty;
                if (two is "&&" or "||" or "==" or "!=" or ">=" or "<=")
                {
                    tokens.Add(new Token(Kind.Symbol, two));
                    i += 2;
                }
                else if ("<>+-*/!(),".Contains(c, StringComparison.Ordinal))
                {
                    tokens.Add(new Token(Kind.Symbol, c.ToString()));
                    i++;
                }
                else
                {
                    throw new FormatException($"Unexpected '{c}' in '{text}'.");
                }
            }
        }

        return tokens;
    }

    private sealed class Parser(List<Token> tokens, ISet<string>? projectFields)
    {
        private int _position;

        private Token? Peek => _position < tokens.Count ? tokens[_position] : null;

        private bool Accept(string symbol)
        {
            if (Peek is { Kind: Kind.Symbol } t && t.Value == symbol)
            {
                _position++;
                return true;
            }

            return false;
        }

        private void Expect(string symbol)
        {
            if (!Accept(symbol))
            {
                throw new FormatException($"Expected '{symbol}' at {Describe()}.");
            }
        }

        private string Describe() => Peek is { } t ? $"'{t.Value}'" : "the end";

        public void End()
        {
            if (Peek is not null)
            {
                throw new FormatException($"Unexpected {Describe()}.");
            }
        }

        public JsonNode? Or()
        {
            var left = And();
            var items = new List<JsonNode?> { left };
            while (Accept("||"))
            {
                items.Add(And());
            }

            return items.Count == 1 ? left : new JsonObject { ["or"] = new JsonArray(items.ToArray()) };
        }

        private JsonNode? And()
        {
            var left = Not();
            var items = new List<JsonNode?> { left };
            while (Accept("&&"))
            {
                items.Add(Not());
            }

            return items.Count == 1 ? left : new JsonObject { ["and"] = new JsonArray(items.ToArray()) };
        }

        private JsonNode? Not() => Accept("!") ? new JsonObject { ["!"] = new JsonArray(Not()) } : Compare();

        private JsonNode? Compare()
        {
            var left = Additive(out var leftName);
            foreach (var op in new[] { "==", "!=", ">=", "<=", ">", "<" })
            {
                if (!Accept(op))
                {
                    continue;
                }

                // A project date compared with "as_of - Ny": its age in years.
                if (projectFields is not null && leftName is not null && Peek is { Kind: Kind.Name, Value: "as_of" })
                {
                    _position++;
                    Expect("-");
                    var years = Peek is { Kind: Kind.Years } y ? decimal.Parse(y.Value, CultureInfo.InvariantCulture) : throw new FormatException("Expected years such as 3y after as_of -.");
                    _position++;
                    var age = new JsonObject { ["var"] = $"{leftName}_yearsAgo" };
                    var flipped = op switch { ">=" => "<=", ">" => "<", "<=" => ">=", "<" => ">", _ => op };
                    return new JsonObject { [flipped] = new JsonArray(age, years) };
                }

                var right = Additive(out _);
                return new JsonObject { [op] = new JsonArray(left, right) };
            }

            return left;
        }

        private JsonNode? Additive(out string? name)
        {
            var left = Multiplicative(out name);
            while (Peek is { Kind: Kind.Symbol, Value: "+" or "-" } t)
            {
                _position++;
                var right = Multiplicative(out _);
                left = new JsonObject { [t.Value] = new JsonArray(left, right) };
                name = null;
            }

            return left;
        }

        private JsonNode? Multiplicative(out string? name)
        {
            var left = Unary(out name);
            while (Peek is { Kind: Kind.Symbol, Value: "*" or "/" } t)
            {
                _position++;
                var right = Unary(out _);
                left = new JsonObject { [t.Value] = new JsonArray(left, right) };
                name = null;
            }

            return left;
        }

        private JsonNode? Unary(out string? name)
        {
            if (Accept("-"))
            {
                name = null;
                return new JsonObject { ["-"] = new JsonArray(Unary(out _)) };
            }

            return Primary(out name);
        }

        private JsonNode? Primary(out string? name)
        {
            name = null;
            var token = Peek ?? throw new FormatException("Unexpected end of the formula.");
            _position++;
            switch (token.Kind)
            {
                case Kind.Number:
                    return JsonValue.Create(decimal.Parse(token.Value, CultureInfo.InvariantCulture));
                case Kind.Text:
                    return JsonValue.Create(token.Value);
                case Kind.Metric:
                    name = token.Value;
                    return new JsonObject { ["var"] = token.Value };
                case Kind.Years:
                    throw new FormatException("Years are only used as 'field >= as_of - Ny'.");
                case Kind.Symbol when token.Value == "(":
                    {
                        var inner = Or();
                        Expect(")");
                        return inner;
                    }

                case Kind.Name:
                    {
                        switch (token.Value.ToLowerInvariant())
                        {
                            case "null":
                                return null;
                            case "true":
                                return JsonValue.Create(true);
                            case "false":
                                return JsonValue.Create(false);
                        }

                        if (Accept("("))
                        {
                            var args = new List<JsonNode?>();
                            if (!Accept(")"))
                            {
                                do
                                {
                                    args.Add(Or());
                                }
                                while (Accept(","));
                                Expect(")");
                            }

                            return token.Value.ToUpperInvariant() switch
                            {
                                "IIF" when args.Count == 3 => new JsonObject { ["if"] = new JsonArray(args.ToArray()) },
                                "MIN" => new JsonObject { ["min"] = new JsonArray(args.ToArray()) },
                                "MAX" => new JsonObject { ["max"] = new JsonArray(args.ToArray()) },
                                _ => throw new FormatException($"Unknown function {token.Value}."),
                            };
                        }

                        // Inside project conditions a bare name is a project field, or a literal word.
                        if (projectFields is not null)
                        {
                            if (projectFields.Contains(token.Value))
                            {
                                name = token.Value;
                                return new JsonObject { ["var"] = token.Value };
                            }

                            // Still a name for "x >= as_of - Ny" (a date field the guide does not list).
                            name = token.Value;
                            return JsonValue.Create(token.Value);
                        }

                        name = token.Value;
                        return new JsonObject { ["var"] = token.Value };
                    }

                default:
                    throw new FormatException($"Unexpected '{token.Value}'.");
            }
        }
    }

    /// <summary>A project function (<c>PROJECTS_SUM(col)</c>, <c>PROJECTS_COUNT(cond)</c>, …) as a projects source; null when not one.</summary>
    public static MatrixSource? ProjectSource(string? expression, ISet<string> projectFields)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var text = expression.Trim();
        var open = text.IndexOf('(', StringComparison.Ordinal);
        if (!text.StartsWith("PROJECTS_", StringComparison.OrdinalIgnoreCase) || open < 0 || !text.EndsWith(')'))
        {
            return null;
        }

        var function = text[9..open].ToLowerInvariant();
        var args = SplitArgs(text[(open + 1)..^1]);
        var source = new MatrixSource { Kind = "projects", Function = function };
        switch (function)
        {
            case "count":
                source.Where = args.Count > 0 ? Convert(args[0], projectFields) : null;
                break;
            case "sum" or "max" or "distinct":
                source.Column = args.Count > 0 ? args[0].Trim() : null;
                source.Where = args.Count > 1 ? Convert(args[1], projectFields) : null;
                break;
            default:
                return null;
        }

        return source;
    }

    private static List<string> SplitArgs(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = new StringBuilder();
        foreach (var c in text)
        {
            if (c == ',' && depth == 0)
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            depth += c == '(' ? 1 : c == ')' ? -1 : 0;
            current.Append(c);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }
}
