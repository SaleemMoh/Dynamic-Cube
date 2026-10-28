using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DynamicCube.Platform.Expressions;

/// <summary>
/// Evaluates JSONLogic rules (https://jsonlogic.com) with the semantics of the reference JavaScript implementation,
/// so the server and the Angular apps produce identical results. Both implementations are verified against the
/// shared conformance cases in <c>tests/shared/jsonlogic-cases.json</c>.
/// </summary>
public sealed partial class JsonLogicEvaluator
{
    private delegate JsonNode? LazyOperation(JsonLogicEvaluator evaluator, IReadOnlyList<JsonNode?> args, JsonNode? data);

    private delegate JsonNode? EagerOperation(IReadOnlyList<JsonNode?> args, JsonNode? data);

    private static readonly Dictionary<string, LazyOperation> LazyOperations = new(StringComparer.Ordinal)
    {
        ["if"] = If,
        ["?:"] = If,
        ["and"] = And,
        ["or"] = Or,
        ["map"] = Map,
        ["filter"] = Filter,
        ["reduce"] = Reduce,
        ["all"] = All,
        ["none"] = None,
        ["some"] = Some,
    };

    private static readonly Dictionary<string, EagerOperation> BuiltInOperations = new(StringComparer.Ordinal)
    {
        ["var"] = Var,
        ["missing"] = (a, d) => Missing(a, d),
        ["missing_some"] = MissingSome,
        ["=="] = (a, _) => LooseEquals(Arg(a, 0), Arg(a, 1)),
        ["==="] = (a, _) => StrictEquals(Arg(a, 0), Arg(a, 1)),
        ["!="] = (a, _) => !LooseEquals(Arg(a, 0), Arg(a, 1)),
        ["!=="] = (a, _) => !StrictEquals(Arg(a, 0), Arg(a, 1)),
        ["!"] = (a, _) => !IsTruthy(Arg(a, 0)),
        ["!!"] = (a, _) => IsTruthy(Arg(a, 0)),
        [">"] = (a, _) => Compare(Arg(a, 0), Arg(a, 1), c => c > 0),
        [">="] = (a, _) => Compare(Arg(a, 0), Arg(a, 1), c => c >= 0),
        ["<"] = (a, _) => a.Count == 3
            ? Compare(a[0], a[1], c => c < 0) && Compare(a[1], a[2], c => c < 0)
            : Compare(Arg(a, 0), Arg(a, 1), c => c < 0),
        ["<="] = (a, _) => a.Count == 3
            ? Compare(a[0], a[1], c => c <= 0) && Compare(a[1], a[2], c => c <= 0)
            : Compare(Arg(a, 0), Arg(a, 1), c => c <= 0),
        ["+"] = (a, _) => Number(a.Aggregate(0d, (sum, x) => sum + ParseFloat(x))),
        ["*"] = Multiply,
        ["-"] = (a, _) => a.Count == 1 ? Number(-ToNumber(a[0])) : Number(ToNumber(Arg(a, 0)) - ToNumber(Arg(a, 1))),
        ["/"] = (a, _) => Number(ToNumber(Arg(a, 0)) / ToNumber(Arg(a, 1))),
        ["%"] = (a, _) => Number(ToNumber(Arg(a, 0)) % ToNumber(Arg(a, 1))),
        ["max"] = (a, _) => a.Count == 0 ? null : Number(a.Select(ToNumber).Aggregate(double.NegativeInfinity, MaxJs)),
        ["min"] = (a, _) => a.Count == 0 ? null : Number(a.Select(ToNumber).Aggregate(double.PositiveInfinity, MinJs)),
        ["cat"] = (a, _) => string.Concat(a.Select(x => x is null ? string.Empty : ToJsString(x))),
        ["substr"] = Substr,
        ["in"] = In,
        ["merge"] = Merge,
        ["log"] = (a, _) => Clone(Arg(a, 0)),
    };

    private readonly Dictionary<string, EagerOperation> _customOperations = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a platform-specific operation (e.g. working-day arithmetic). Arguments are evaluated before the call.
    /// The same operation must be registered in the TypeScript evaluator.
    /// </summary>
    public JsonLogicEvaluator AddOperation(string name, Func<IReadOnlyList<JsonNode?>, JsonNode?, JsonNode?> operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(operation);
        if (LazyOperations.ContainsKey(name) || BuiltInOperations.ContainsKey(name))
        {
            throw new ArgumentException($"'{name}' is a built-in JSONLogic operation and cannot be replaced.", nameof(name));
        }

        _customOperations[name] = (args, data) => operation(args, data);
        return this;
    }

    /// <summary>Evaluates <paramref name="rule"/> against <paramref name="data"/>.</summary>
    public JsonNode? Evaluate(JsonNode? rule, JsonNode? data)
    {
        if (rule is JsonArray array)
        {
            var result = new JsonArray();
            foreach (var item in array)
            {
                result.Add(Clone(Evaluate(item, data)));
            }

            return result;
        }

        if (!IsLogic(rule))
        {
            return Clone(rule);
        }

        var (name, rawArgs) = SplitOperation((JsonObject)rule!);

        if (LazyOperations.TryGetValue(name, out var lazy))
        {
            return lazy(this, rawArgs, data);
        }

        var args = rawArgs.Select(arg => Evaluate(arg, data)).ToList();

        if (BuiltInOperations.TryGetValue(name, out var eager) || _customOperations.TryGetValue(name, out eager))
        {
            return eager(args, data);
        }

        throw new JsonLogicException($"Unrecognized JSONLogic operation: '{name}'.");
    }

    /// <summary>Evaluates a rule and returns its truthiness.</summary>
    public bool EvaluateTruthy(JsonNode? rule, JsonNode? data) => IsTruthy(Evaluate(rule, data));

    /// <summary>JSONLogic truthiness: <c>null</c>, <c>false</c>, <c>0</c>, <c>""</c> and <c>[]</c> are falsy.</summary>
    public static bool IsTruthy(JsonNode? value) => value switch
    {
        null => false,
        JsonArray array => array.Count > 0,
        JsonObject => true,
        _ => value.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetValue<string>().Length > 0,
            JsonValueKind.Number => GetDouble(value) is var d && d != 0 && !double.IsNaN(d),
            _ => false,
        },
    };

    private static bool IsLogic(JsonNode? node) => node is JsonObject obj && obj.Count == 1;

    private static (string Name, IReadOnlyList<JsonNode?> Args) SplitOperation(JsonObject rule)
    {
        var (name, value) = rule.First();
        IReadOnlyList<JsonNode?> args = value is JsonArray array ? array.ToList() : [value];
        return (name, args);
    }

    // ---- Lazy operations ------------------------------------------------------------------------

    private static JsonNode? If(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        int i;
        for (i = 0; i < args.Count - 1; i += 2)
        {
            if (IsTruthy(e.Evaluate(args[i], data)))
            {
                return e.Evaluate(args[i + 1], data);
            }
        }

        return args.Count == i + 1 ? e.Evaluate(args[i], data) : null;
    }

    private static JsonNode? And(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        JsonNode? current = null;
        foreach (var arg in args)
        {
            current = e.Evaluate(arg, data);
            if (!IsTruthy(current))
            {
                return current;
            }
        }

        return current;
    }

    private static JsonNode? Or(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        JsonNode? current = null;
        foreach (var arg in args)
        {
            current = e.Evaluate(arg, data);
            if (IsTruthy(current))
            {
                return current;
            }
        }

        return current;
    }

    private static JsonArray Map(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        var result = new JsonArray();
        if (e.Evaluate(Arg(args, 0), data) is JsonArray items)
        {
            foreach (var item in items)
            {
                result.Add(Clone(e.Evaluate(Arg(args, 1), item)));
            }
        }

        return result;
    }

    private static JsonArray Filter(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        var result = new JsonArray();
        if (e.Evaluate(Arg(args, 0), data) is JsonArray items)
        {
            foreach (var item in items)
            {
                if (IsTruthy(e.Evaluate(Arg(args, 1), item)))
                {
                    result.Add(Clone(item));
                }
            }
        }

        return result;
    }

    private static JsonNode? Reduce(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        var accumulator = e.Evaluate(Arg(args, 2), data);
        if (e.Evaluate(Arg(args, 0), data) is not JsonArray items)
        {
            return accumulator;
        }

        foreach (var item in items)
        {
            var scope = new JsonObject { ["current"] = Clone(item), ["accumulator"] = Clone(accumulator) };
            accumulator = e.Evaluate(Arg(args, 1), scope);
        }

        return accumulator;
    }

    private static JsonNode? All(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        if (e.Evaluate(Arg(args, 0), data) is not JsonArray { Count: > 0 } items)
        {
            return false;
        }

        return items.All(item => IsTruthy(e.Evaluate(Arg(args, 1), item)));
    }

    private static JsonNode? None(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data) =>
        Filter(e, args, data).Count == 0;

    private static JsonNode? Some(JsonLogicEvaluator e, IReadOnlyList<JsonNode?> args, JsonNode? data) =>
        Filter(e, args, data).Count > 0;

    // ---- Data access ----------------------------------------------------------------------------

    private static JsonNode? Var(IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        var path = Arg(args, 0);
        var notFound = Arg(args, 1);
        var pathText = path is null ? string.Empty : ToJsString(path);
        if (pathText.Length == 0)
        {
            return Clone(data);
        }

        var current = data;
        foreach (var segment in pathText.Split('.'))
        {
            if (current is null)
            {
                return Clone(notFound);
            }

            switch (current)
            {
                case JsonObject obj when obj.TryGetPropertyValue(segment, out var child):
                    current = child;
                    break;
                case JsonArray array when int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < array.Count:
                    current = array[index];
                    break;
                default:
                    return Clone(notFound);
            }
        }

        return Clone(current);
    }

    private static JsonArray Missing(IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        IEnumerable<JsonNode?> keys = Arg(args, 0) is JsonArray first ? first : args;
        var missing = new JsonArray();
        foreach (var key in keys)
        {
            var value = Var([key, null], data);
            if (value is null || (value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Length == 0))
            {
                missing.Add(Clone(key));
            }
        }

        return missing;
    }

    private static JsonArray MissingSome(IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        var need = ToNumber(Arg(args, 0));
        var keys = Arg(args, 1) as JsonArray ?? [];
        var missing = Missing([keys], data);
        return keys.Count - missing.Count >= need ? new JsonArray() : missing;
    }

    // ---- Comparison -----------------------------------------------------------------------------

    private static bool LooseEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a is JsonArray or JsonObject || b is JsonArray or JsonObject)
        {
            if ((a is JsonArray or JsonObject) && (b is JsonArray or JsonObject))
            {
                return ReferenceEquals(a, b);
            }

            return a is JsonArray or JsonObject
                ? LooseEquals(JsonValue.Create(ToJsString(a)), b)
                : LooseEquals(a, JsonValue.Create(ToJsString(b)));
        }

        var ka = a.GetValueKind();
        var kb = b.GetValueKind();
        if (ka == kb || (IsBool(ka) && IsBool(kb)))
        {
            return StrictEquals(a, b);
        }

        if (IsBool(ka))
        {
            return LooseEquals(JsonValue.Create(ToNumber(a)), b);
        }

        if (IsBool(kb))
        {
            return LooseEquals(a, JsonValue.Create(ToNumber(b)));
        }

        // number <-> string
        return ToNumber(a) == ToNumber(b);
    }

    private static bool StrictEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a is JsonArray or JsonObject || b is JsonArray or JsonObject)
        {
            return ReferenceEquals(a, b);
        }

        var ka = a.GetValueKind();
        var kb = b.GetValueKind();
        return (ka, kb) switch
        {
            (JsonValueKind.Number, JsonValueKind.Number) => GetDouble(a) == GetDouble(b),
            (JsonValueKind.String, JsonValueKind.String) => string.Equals(a.GetValue<string>(), b.GetValue<string>(), StringComparison.Ordinal),
            _ when IsBool(ka) && IsBool(kb) => ka == kb,
            _ => false,
        };
    }

    private static bool Compare(JsonNode? a, JsonNode? b, Func<int, bool> predicate)
    {
        var pa = ToPrimitive(a);
        var pb = ToPrimitive(b);
        if (pa is not null && pb is not null && pa.GetValueKind() == JsonValueKind.String && pb.GetValueKind() == JsonValueKind.String)
        {
            return predicate(string.CompareOrdinal(pa.GetValue<string>(), pb.GetValue<string>()));
        }

        var na = ToNumber(pa);
        var nb = ToNumber(pb);
        if (double.IsNaN(na) || double.IsNaN(nb))
        {
            return false;
        }

        return predicate(na.CompareTo(nb));
    }

    private static bool IsBool(JsonValueKind kind) => kind is JsonValueKind.True or JsonValueKind.False;

    // ---- Arithmetic & strings -------------------------------------------------------------------

    private static JsonNode? Multiply(IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        if (args.Count == 0)
        {
            return null;
        }

        if (args.Count == 1)
        {
            return Clone(args[0]);
        }

        var product = ParseFloat(args[0]);
        for (var i = 1; i < args.Count; i++)
        {
            product *= ParseFloat(args[i]);
        }

        return Number(product);
    }

    private static JsonNode? Substr(IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        var source = ToJsString(Arg(args, 0));
        var start = (int)ToInteger(Arg(args, 1));
        var temp = JsSubstr(source, start, null);
        if (args.Count < 3 || Arg(args, 2) is null)
        {
            return temp;
        }

        var end = (int)ToInteger(args[2]);
        return end < 0 ? JsSubstr(temp, 0, Math.Max(0, temp.Length + end)) : JsSubstr(source, start, end);
    }

    private static string JsSubstr(string value, int start, int? length)
    {
        if (start < 0)
        {
            start = Math.Max(value.Length + start, 0);
        }

        start = Math.Min(start, value.Length);
        var available = value.Length - start;
        var count = length is null ? available : Math.Clamp(length.Value, 0, available);
        return value.Substring(start, count);
    }

    private static JsonNode? In(IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        var needle = Arg(args, 0);
        return Arg(args, 1) switch
        {
            JsonArray haystack => haystack.Any(item => StrictEquals(item, needle)),
            JsonValue value when value.GetValueKind() == JsonValueKind.String =>
                value.GetValue<string>().Contains(ToJsString(needle), StringComparison.Ordinal),
            _ => false,
        };
    }

    private static JsonArray Merge(IReadOnlyList<JsonNode?> args, JsonNode? data)
    {
        var result = new JsonArray();
        foreach (var arg in args)
        {
            if (arg is JsonArray array)
            {
                foreach (var item in array)
                {
                    result.Add(Clone(item));
                }
            }
            else
            {
                result.Add(Clone(arg));
            }
        }

        return result;
    }

    // ---- JavaScript coercion helpers ------------------------------------------------------------

    internal static double ToNumber(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return 0;
            case JsonArray array:
                return array.Count switch
                {
                    0 => 0,
                    1 => ToNumber(JsonValue.Create(ToJsString(array[0] ?? JsonValue.Create(string.Empty)))),
                    _ => double.NaN,
                };
            case JsonObject:
                return double.NaN;
        }

        return node.GetValueKind() switch
        {
            JsonValueKind.Number => GetDouble(node),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            JsonValueKind.String => StringToNumber(node.GetValue<string>()),
            _ => double.NaN,
        };
    }

    private static double StringToNumber(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return 0;
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
    }

    private static double ToInteger(JsonNode? node)
    {
        var number = ToNumber(node);
        return double.IsNaN(number) ? 0 : Math.Truncate(number);
    }

    /// <summary>JavaScript <c>parseFloat</c>: parses the leading numeric prefix of the string form.</summary>
    internal static double ParseFloat(JsonNode? node)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.Number)
        {
            return GetDouble(value);
        }

        var match = FloatPrefix().Match(node is null ? "null" : ToJsString(node));
        return match.Success ? double.Parse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture) : double.NaN;
    }

    private static JsonNode? ToPrimitive(JsonNode? node) =>
        node is JsonArray or JsonObject ? JsonValue.Create(ToJsString(node)) : node;

    /// <summary>JavaScript <c>String(value)</c>.</summary>
    internal static string ToJsString(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return "null";
            case JsonArray array:
                return string.Join(',', array.Select(item => item is null ? string.Empty : ToJsString(item)));
            case JsonObject:
                return "[object Object]";
        }

        return node.GetValueKind() switch
        {
            JsonValueKind.String => node.GetValue<string>(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => FormatNumber(GetDouble(node)),
            _ => string.Empty,
        };
    }

    private static string FormatNumber(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "Infinity" : "-Infinity";
        }

        if (value == 0)
        {
            return "0";
        }

        if (Math.Abs(value) < 1e21 && value == Math.Floor(value))
        {
            return value.ToString("0", CultureInfo.InvariantCulture);
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static double GetDouble(JsonNode node)
    {
        var value = node.AsValue();
        if (value.TryGetValue(out double d))
        {
            return d;
        }

        if (value.TryGetValue(out long l))
        {
            return l;
        }

        if (value.TryGetValue(out int i))
        {
            return i;
        }

        if (value.TryGetValue(out decimal m))
        {
            return (double)m;
        }

        if (value.TryGetValue(out JsonElement element) && element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDouble();
        }

        return double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static double MaxJs(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : Math.Max(a, b);

    private static double MinJs(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : Math.Min(a, b);

    /// <summary>Numbers that JSON cannot represent (NaN, ±Infinity) become null, as with <c>JSON.stringify</c>.</summary>
    private static JsonValue? Number(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? null : JsonValue.Create(value);

    private static JsonNode? Arg(IReadOnlyList<JsonNode?> args, int index) => index < args.Count ? args[index] : null;

    private static JsonNode? Clone(JsonNode? node) => node?.DeepClone();

    [GeneratedRegex(@"^\s*[+-]?(Infinity|(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?)")]
    private static partial Regex FloatPrefix();
}
