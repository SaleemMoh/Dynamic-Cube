using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>A problem in a query definition, at a path of the payload (e.g. <c>query.joins[0].on[0].left</c>).</summary>
public sealed record LovQueryProblem(string Field, string Message);

/// <summary>SQL with its bound parameters. Identifiers in it are quoted registered names; values are all parameters.</summary>
/// <param name="Fields">For a query of chosen columns (a public endpoint): their names and how each reads, in order.</param>
public sealed record LovCompiledQuery(string Sql, IReadOnlyDictionary<string, object?> Parameters, IReadOnlyList<(string Name, LovColumnCategory Category)>? Fields = null);

/// <summary>A column a public endpoint returns: its name in the response and the column (<c>alias.column</c>) it reads.</summary>
public sealed record LovQueryField(string Name, string Column);

/// <summary>What a run adds to the definition: the parent and search typed in, the user's grant, the page.</summary>
public sealed record LovQueryRun(
    LovUserFacts User,
    string? Parent = null,
    string? Search = null,
    GrantSet? Grant = null,
    int Limit = 50,
    int Offset = 0,
    IReadOnlyDictionary<string, IReadOnlyList<DataScopeFilter>>? Scopes = null,
    IReadOnlyDictionary<string, string?>? Inputs = null);

/// <summary>
/// Checks a <see cref="LovQueryDefinition"/> against the registered sources and turns it into one SELECT returning
/// <c>value</c>, <c>label_ar</c>, <c>label_en</c> and <c>parent</c> as text. Classes are read through their typed views
/// (<c>apps.v_class_*</c>) and keep the values a class LOV shows: active, in effect today and serving the company.
/// </summary>
public static partial class LovQueryCompiler
{
    public const int MaxJoins = 5;
    public const int MaxConditions = 30;
    public const int MaxDepth = 3;
    public const int MaxSorts = 5;

    public static readonly string[] Operators =
        ["EQ", "NE", "GT", "GE", "LT", "LE", "CONTAINS", "STARTS_WITH", "ENDS_WITH", "IN", "NOT_IN", "IS_EMPTY", "IS_NOT_EMPTY"];

    private static readonly string[] UserOperators = ["EQ", "NE", "IN", "NOT_IN"];

    /// <summary>Most columns a public endpoint returns.</summary>
    public const int MaxFields = 60;

    /// <summary>Problems of the definition; empty when it can run.</summary>
    public static IReadOnlyList<LovQueryProblem> Check(LovQueryDefinition? query, Func<string, string, LovQuerySourceInfo?> source)
    {
        var problems = new List<LovQueryProblem>();
        _ = Build(query, source, null, problems);
        return problems;
    }

    /// <summary>The SQL of a valid definition for one run, or the problems that prevent it.</summary>
    public static (LovCompiledQuery? Query, IReadOnlyList<LovQueryProblem> Problems) Compile(LovQueryDefinition? query, Func<string, string, LovQuerySourceInfo?> source, LovQueryRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var problems = new List<LovQueryProblem>();
        var compiled = Build(query, source, run, problems);
        return problems.Count == 0 ? (compiled, problems) : (null, problems);
    }

    /// <summary>Problems of a public endpoint's query (ADR-0042): chosen <paramref name="fields"/> instead of a value and labels; conditions may use the <paramref name="inputs"/>.</summary>
    public static IReadOnlyList<LovQueryProblem> CheckRows(LovQueryDefinition? query, Func<string, string, LovQuerySourceInfo?> source, IReadOnlyList<LovQueryField> fields, IReadOnlyCollection<string> inputs)
    {
        var problems = new List<LovQueryProblem>();
        _ = Build(query, source, null, problems, fields, inputs);
        return problems;
    }

    /// <summary>The kind of value each chosen field returns (for the endpoint's OpenAPI document); null when the query has problems.</summary>
    public static IReadOnlyList<(string Name, LovColumnCategory Category)>? DescribeRows(
        LovQueryDefinition? query, Func<string, string, LovQuerySourceInfo?> source, IReadOnlyList<LovQueryField> fields, IReadOnlyCollection<string> inputs)
    {
        var problems = new List<LovQueryProblem>();
        var compiled = Build(query, source, null, problems, fields, inputs);
        return problems.Count == 0 ? compiled?.Fields : null;
    }

    /// <summary>The SQL of a public endpoint's query for one run (its inputs, caller and page), or the problems that prevent it.</summary>
    public static (LovCompiledQuery? Query, IReadOnlyList<LovQueryProblem> Problems) CompileRows(
        LovQueryDefinition? query, Func<string, string, LovQuerySourceInfo?> source, IReadOnlyList<LovQueryField> fields, LovQueryRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var problems = new List<LovQueryProblem>();
        var compiled = Build(query, source, run, problems, fields, run.Inputs?.Keys.ToList());
        return problems.Count == 0 ? (compiled, problems) : (null, problems);
    }

    private static LovCompiledQuery? Build(
        LovQueryDefinition? query,
        Func<string, string, LovQuerySourceInfo?> source,
        LovQueryRun? run,
        List<LovQueryProblem> problems,
        IReadOnlyList<LovQueryField>? fields = null,
        IReadOnlyCollection<string>? inputs = null)
    {
        if (query is null)
        {
            problems.Add(new("query", "Build the query: choose where the values come from."));
            return null;
        }

        var builder = new Builder(source, run, problems, fields, inputs);
        return builder.Compile(query);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex FieldPattern();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,19}$")]
    private static partial Regex AliasPattern();

    [GeneratedRegex("^[0-9]{4}-[0-9]{2}-[0-9]{2}$")]
    private static partial Regex DatePattern();

    internal static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>How a database type compares.</summary>
    public static LovColumnCategory CategoryOf(string dataType)
    {
        var type = dataType.ToUpperInvariant();
        if (type.Contains("JSON", StringComparison.Ordinal) || type.Contains("ARRAY", StringComparison.Ordinal) || type.EndsWith("[]", StringComparison.Ordinal))
        {
            return LovColumnCategory.Json;
        }

        if (type is "BOOLEAN" or "BOOL")
        {
            return LovColumnCategory.Bool;
        }

        if (type.StartsWith("DATE", StringComparison.Ordinal) || type.StartsWith("TIMESTAMP", StringComparison.Ordinal))
        {
            return LovColumnCategory.Date;
        }

        return type.Contains("INT", StringComparison.Ordinal) || type.Contains("NUMERIC", StringComparison.Ordinal) || type.Contains("DECIMAL", StringComparison.Ordinal)
            || type.Contains("REAL", StringComparison.Ordinal) || type.Contains("DOUBLE", StringComparison.Ordinal) || type == "NUMBER"
            ? LovColumnCategory.Number
            : LovColumnCategory.Text;
    }

    private sealed class Builder(
        Func<string, string, LovQuerySourceInfo?> resolve,
        LovQueryRun? run,
        List<LovQueryProblem> problems,
        IReadOnlyList<LovQueryField>? fields,
        IReadOnlyCollection<string>? inputs)
    {
        private readonly Dictionary<string, (LovQuerySourceInfo Info, Dictionary<string, LovSourceColumn> Columns)> _aliases = new(StringComparer.Ordinal);

        // Sources that could not be resolved (already reported): their columns are not reported again.
        private readonly HashSet<string> _unresolved = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> _parameters = new(StringComparer.Ordinal);
        private int _conditions;

        public LovCompiledQuery? Compile(LovQueryDefinition query)
        {
            var from = Source(query.From, "query.from");
            if (query.Joins.Count > MaxJoins)
            {
                problems.Add(new("query.joins", $"Join at most {MaxJoins} sources."));
            }

            // Sources first, so conditions and joins can refer to any of them.
            var joins = query.Joins.Take(MaxJoins).Select((j, i) => (Join: j, Info: Source(j, $"query.joins[{i}]"), Index: i)).ToList();
            var sql = new StringBuilder();
            var fromSql = from is null ? null : $"{Quote(from.Schema)}.{Quote(from.DbObject)} AS {Quote(query.From.Alias)}";
            var joinSql = new StringBuilder();
            foreach (var (join, info, i) in joins)
            {
                var type = join.Type?.ToUpperInvariant();
                if (type is not ("INNER" or "LEFT"))
                {
                    problems.Add(new($"query.joins[{i}].type", "Choose INNER (only matching rows) or LEFT (every row)."));
                }

                if (join.On.Count == 0)
                {
                    problems.Add(new($"query.joins[{i}].on", "Say which columns match."));
                }

                var on = new List<string>();
                foreach (var (pair, k) in join.On.Select((p, k) => (p, k)))
                {
                    var left = Column(pair.Left, $"query.joins[{i}].on[{k}].left");
                    var right = Column(pair.Right, $"query.joins[{i}].on[{k}].right");
                    if (left is not null && right is not null)
                    {
                        on.Add($"{left.Value.Sql}::text = {right.Value.Sql}::text");
                    }
                }

                if (info is not null)
                {
                    on.AddRange(BuiltIn(info, join.Alias));
                    joinSql.Append(type == "INNER" ? " JOIN " : " LEFT JOIN ")
                        .Append(Quote(info.Schema)).Append('.').Append(Quote(info.DbObject)).Append(" AS ").Append(Quote(join.Alias))
                        .Append(" ON ").Append(on.Count == 0 ? "TRUE" : string.Join(" AND ", on));
                }
            }

            // A public endpoint returns chosen columns; a list returns a value, its labels and its parent.
            var selected = fields is null ? null : Fields(fields);
            var value = fields is null ? MapColumn(query.Map.Value, "query.map.value", required: true, "Choose the column that holds the value.") : null;
            var labelAr = fields is null ? MapColumn(query.Map.LabelAr, "query.map.labelAr", required: true, "Choose the column that holds the Arabic label.") : null;
            var labelEn = fields is null ? MapColumn(query.Map.LabelEn, "query.map.labelEn", required: true, "Choose the column that holds the English label.") : null;
            var parent = fields is null ? MapColumn(query.Map.Parent, "query.map.parent", required: false, null) : null;

            var where = new List<string>();
            if (from is not null)
            {
                where.AddRange(BuiltIn(from, query.From.Alias));
            }

            if (query.Where is not null)
            {
                var condition = Group(query.Where, "query.where", 1);
                if (condition is not null)
                {
                    where.Add(condition);
                }
            }

            foreach (var scope in run?.Scopes ?? new Dictionary<string, IReadOnlyList<DataScopeFilter>>())
            {
                if (!_aliases.ContainsKey(scope.Key))
                {
                    continue;
                }

                foreach (var filter in scope.Value.Where(f => !f.Grant.All))
                {
                    var name = Parameter(filter.Grant.Values.ToArray());
                    where.Add($"{Quote(scope.Key)}.{Quote(filter.Column)}::text = ANY({name}::text[])");
                }
            }

            if (query.Sort.Count > MaxSorts)
            {
                problems.Add(new("query.sort", $"Sort by at most {MaxSorts} columns."));
            }

            var sorts = new List<(string Sql, bool Descending)>();
            foreach (var (sort, i) in query.Sort.Take(MaxSorts).Select((s, i) => (s, i)))
            {
                var column = Column(sort.Column, $"query.sort[{i}].column");
                var direction = sort.Direction?.ToUpperInvariant();
                if (direction is not ("ASC" or "DESC"))
                {
                    problems.Add(new($"query.sort[{i}].direction", "Sort ASC or DESC."));
                }

                if (column is not null)
                {
                    sorts.Add((column.Value.Sql, direction == "DESC"));
                }
            }

            if (selected is not null)
            {
                return problems.Count > 0 || fromSql is null ? null : Rows(query, selected, sorts, fromSql, joinSql.ToString(), where);
            }

            if (problems.Count > 0 || fromSql is null || value is null || labelAr is null || labelEn is null)
            {
                return null;
            }

            // The inner query maps and filters; the outer one applies the parent, the search, the grant and the page.
            sql.Append("SELECT q.value, q.label_ar, q.label_en, q.parent FROM (SELECT ").Append(query.Distinct ? "DISTINCT " : string.Empty)
                .Append(value).Append("::text AS value, ").Append(labelAr).Append("::text AS label_ar, ").Append(labelEn).Append("::text AS label_en, ")
                .Append(parent is null ? "NULL::text" : parent + "::text").Append(" AS parent");
            for (var i = 0; i < sorts.Count; i++)
            {
                sql.Append(", ").Append(sorts[i].Sql).Append(" AS s").Append(i.ToString(CultureInfo.InvariantCulture));
            }

            sql.Append(" FROM ").Append(fromSql).Append(joinSql);
            if (where.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", where.Select(w => "(" + w + ")")));
            }

            sql.Append(") AS q");
            if (run is not null)
            {
                var outer = new List<string>();
                if (run.Parent is not null)
                {
                    outer.Add($"q.parent = {Parameter(run.Parent)}");
                }

                if (!string.IsNullOrWhiteSpace(run.Search))
                {
                    var pattern = Parameter($"%{SqlLovRunner.LikeEscape(run.Search.Trim())}%");
                    outer.Add($"(q.label_ar ILIKE {pattern} OR q.label_en ILIKE {pattern} OR q.value ILIKE {pattern})");
                }

                if (run.Grant is { All: false } grant)
                {
                    outer.Add($"q.value = ANY({Parameter(grant.Values.ToArray())}::text[])");
                }

                if (outer.Count > 0)
                {
                    sql.Append(" WHERE ").Append(string.Join(" AND ", outer));
                }
            }

            sql.Append(" ORDER BY ");
            sql.Append(sorts.Count == 0
                ? "q.label_ar"
                : string.Join(", ", sorts.Select((s, i) => $"q.s{i.ToString(CultureInfo.InvariantCulture)}{(s.Descending ? " DESC" : string.Empty)} NULLS LAST")) + ", q.label_ar");
            if (run is not null)
            {
                sql.Append(" LIMIT ").Append(Math.Clamp(run.Limit, 1, 5001).ToString(CultureInfo.InvariantCulture))
                    .Append(" OFFSET ").Append(Math.Max(0, run.Offset).ToString(CultureInfo.InvariantCulture));
            }

            return new LovCompiledQuery(sql.ToString(), _parameters);
        }

        /// <summary>The chosen columns of a public endpoint, checked: valid names, used once, registered columns.</summary>
        private List<(string Name, string Sql, LovColumnCategory Category)>? Fields(IReadOnlyList<LovQueryField> chosen)
        {
            if (chosen.Count == 0)
            {
                problems.Add(new("fields", "Choose the columns the endpoint returns."));
                return null;
            }

            if (chosen.Count > MaxFields)
            {
                problems.Add(new("fields", $"Return at most {MaxFields} columns."));
                return null;
            }

            var result = new List<(string, string, LovColumnCategory)>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (field, i) in chosen.Select((f, i) => (f, i)))
            {
                if (!FieldPattern().IsMatch(field.Name ?? string.Empty))
                {
                    problems.Add(new($"fields[{i}].name", "Use a name of English letters, digits and _, starting with a letter."));
                }
                else if (!names.Add(field.Name!))
                {
                    problems.Add(new($"fields[{i}].name", $"'{field.Name}' is used twice."));
                }

                var column = Column(field.Column, $"fields[{i}].column");
                if (column is not null)
                {
                    result.Add((field.Name ?? string.Empty, column.Value.Sql, column.Value.Column.Category));
                }
            }

            return result;
        }

        /// <summary>The chosen columns, filtered and sorted, one page of them (sorted by the first column when no sort is set).</summary>
        private LovCompiledQuery Rows(LovQueryDefinition query, List<(string Name, string Sql, LovColumnCategory Category)> selected, List<(string Sql, bool Descending)> sorts, string fromSql, string joinSql, List<string> where)
        {
            var sql = new StringBuilder("SELECT ");
            sql.Append(string.Join(", ", selected.Select((_, i) => $"q.f{i.ToString(CultureInfo.InvariantCulture)}")));
            sql.Append(" FROM (SELECT ").Append(query.Distinct ? "DISTINCT " : string.Empty);
            sql.Append(string.Join(", ", selected.Select((f, i) => $"{f.Sql} AS f{i.ToString(CultureInfo.InvariantCulture)}")));
            for (var i = 0; i < sorts.Count; i++)
            {
                sql.Append(", ").Append(sorts[i].Sql).Append(" AS s").Append(i.ToString(CultureInfo.InvariantCulture));
            }

            sql.Append(" FROM ").Append(fromSql).Append(joinSql);
            if (where.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", where.Select(w => "(" + w + ")")));
            }

            sql.Append(") AS q ORDER BY ");
            sql.Append(sorts.Count == 0
                ? "q.f0"
                : string.Join(", ", sorts.Select((s, i) => $"q.s{i.ToString(CultureInfo.InvariantCulture)}{(s.Descending ? " DESC" : string.Empty)} NULLS LAST")));
            if (run is not null)
            {
                sql.Append(" LIMIT ").Append(Math.Clamp(run.Limit, 1, 1001).ToString(CultureInfo.InvariantCulture))
                    .Append(" OFFSET ").Append(Math.Max(0, run.Offset).ToString(CultureInfo.InvariantCulture));
            }

            return new LovCompiledQuery(sql.ToString(), _parameters, [.. selected.Select(f => (f.Name, f.Category))]);
        }

        private LovQuerySourceInfo? Source(LovQuerySource source, string path)
        {
            var alias = source.Alias ?? string.Empty;
            if (!AliasPattern().IsMatch(alias))
            {
                problems.Add(new($"{path}.alias", "Use a short lower-case name (letters, digits, _), starting with a letter."));
                return null;
            }

            if (_aliases.ContainsKey(alias))
            {
                problems.Add(new($"{path}.alias", $"The name '{alias}' is already used by another source."));
                return null;
            }

            var kind = source.Kind?.ToUpperInvariant() ?? string.Empty;
            if (kind is not (LovQueryKinds.Class or LovQueryKinds.DataObject or LovQueryKinds.Table))
            {
                problems.Add(new($"{path}.kind", "Choose a class or a table."));
                _unresolved.Add(alias);
                return null;
            }

            var info = string.IsNullOrWhiteSpace(source.Code) ? null : resolve(kind, source.Code);
            if (info is null)
            {
                problems.Add(new($"{path}.code", string.IsNullOrWhiteSpace(source.Code)
                    ? "Choose the class or table."
                    : kind switch
                    {
                        LovQueryKinds.Class => $"Class '{source.Code}' does not exist.",
                        LovQueryKinds.Table => $"Table '{source.Code}' is not registered; save the list from the query builder, which registers it.",
                        _ => $"'{source.Code}' is not a registered data object.",
                    }));
                _unresolved.Add(alias);
                return null;
            }

            _aliases[alias] = (info, info.Columns.ToDictionary(c => c.Name, StringComparer.Ordinal));
            return info;
        }

        /// <summary>The conditions a class keeps: active values, in effect today, serving the user's company.</summary>
        private List<string> BuiltIn(LovQuerySourceInfo info, string alias)
        {
            if (info.Kind != LovQueryKinds.Class)
            {
                return [];
            }

            var a = Quote(alias);
            var company = run?.User.Company;
            var conditions = new List<string>
            {
                $"{a}.is_active",
                $"({a}.effective_from IS NULL OR {a}.effective_from <= CURRENT_DATE)",
                $"({a}.effective_to IS NULL OR {a}.effective_to >= CURRENT_DATE)",
            };
            if (run?.User is { AllCompanies: false, Companies: { } companies })
            {
                // A caller of several companies (a public API client): values serving any of them.
                conditions.Add($"(cardinality({a}.companies) = 0 OR '*' = ANY({a}.companies) OR {a}.companies && {Parameter(companies.ToArray())}::text[])");
            }
            else if (company is not null && run?.User.AllCompanies != true)
            {
                conditions.Add($"(cardinality({a}.companies) = 0 OR '*' = ANY({a}.companies) OR {Parameter(company)}::text = ANY({a}.companies))");
            }

            return conditions;
        }

        private (string Sql, LovSourceColumn Column)? Column(string? reference, string path)
        {
            var text = reference ?? string.Empty;
            var dot = text.IndexOf('.', StringComparison.Ordinal);
            if (dot <= 0 || dot == text.Length - 1)
            {
                problems.Add(new(path, "Choose a column."));
                return null;
            }

            var alias = text[..dot];
            var name = text[(dot + 1)..];
            if (!_aliases.TryGetValue(alias, out var source))
            {
                if (!_unresolved.Contains(alias))
                {
                    problems.Add(new(path, $"'{alias}' is not one of the sources of the query."));
                }

                return null;
            }

            if (!source.Columns.TryGetValue(name, out var column))
            {
                problems.Add(new(path, $"'{name}' is not a column of {source.Info.Code}."));
                return null;
            }

            return ($"{Quote(alias)}.{Quote(name)}", column);
        }

        private string? MapColumn(string? reference, string path, bool required, string? missing)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                if (required)
                {
                    problems.Add(new(path, missing!));
                }

                return null;
            }

            var column = Column(reference, path);
            if (column?.Column.Category == LovColumnCategory.Json)
            {
                problems.Add(new(path, "A list or JSON column cannot be a value or a label."));
                return null;
            }

            return column?.Sql;
        }

        private string? Group(LovQueryGroup group, string path, int depth)
        {
            if (depth > MaxDepth)
            {
                problems.Add(new(path, $"Nest groups at most {MaxDepth} deep."));
                return null;
            }

            var match = group.Match?.ToUpperInvariant();
            if (match is not ("ALL" or "ANY"))
            {
                problems.Add(new($"{path}.match", "Match ALL or ANY of the conditions."));
                return null;
            }

            var parts = new List<string>();
            foreach (var (condition, i) in group.Conditions.Select((c, i) => (c, i)))
            {
                if (++_conditions > MaxConditions)
                {
                    problems.Add(new(path, $"Use at most {MaxConditions} conditions."));
                    return null;
                }

                var sql = Condition(condition, $"{path}.conditions[{i}]");
                if (sql is not null)
                {
                    parts.Add(sql);
                }
            }

            foreach (var (nested, i) in group.Groups.Select((g, i) => (g, i)))
            {
                var sql = Group(nested, $"{path}.groups[{i}]", depth + 1);
                if (sql is not null)
                {
                    parts.Add(sql);
                }
            }

            return parts.Count == 0 ? null : "(" + string.Join(match == "ALL" ? " AND " : " OR ", parts) + ")";
        }

        private string? Condition(LovQueryCondition condition, string path)
        {
            var column = Column(condition.Column, $"{path}.column");
            var op = condition.Op?.ToUpperInvariant() ?? string.Empty;
            if (!Operators.Contains(op))
            {
                problems.Add(new($"{path}.op", "Choose a comparison."));
                return null;
            }

            if (column is null)
            {
                return null;
            }

            var (sql, info) = column.Value;
            if (!string.IsNullOrWhiteSpace(condition.User))
            {
                return UserCondition(condition.User, op, sql, path);
            }

            // Compared with an endpoint's input: checked against the inputs while saving; left out when not given.
            var value = condition.Value;
            var valuePath = $"{path}.value";
            if (!string.IsNullOrWhiteSpace(condition.Param))
            {
                if (inputs is null || !inputs.Contains(condition.Param, StringComparer.Ordinal))
                {
                    problems.Add(new($"{path}.param", inputs is null ? "Only a public endpoint's query compares with inputs." : $"There is no input '{condition.Param}'."));
                    return null;
                }

                if (op is "IS_EMPTY" or "IS_NOT_EMPTY")
                {
                    problems.Add(new($"{path}.op", "Compare an input with equals, greater or less than, contains, one of…"));
                    return null;
                }

                var given = run?.Inputs?.GetValueOrDefault(condition.Param);
                if (run is null || string.IsNullOrWhiteSpace(given))
                {
                    return run is null && info.Category == LovColumnCategory.Json ? JsonInput(path) : "TRUE";
                }

                value = JsonValue.Create(given.Trim());
                valuePath = $"inputs.{condition.Param}";
            }

            if (info.Category == LovColumnCategory.Json && op is not ("IS_EMPTY" or "IS_NOT_EMPTY"))
            {
                problems.Add(new($"{path}.op", "A list or JSON column can only be checked for being empty."));
                return null;
            }

            switch (op)
            {
                case "IS_EMPTY":
                    return info.Category == LovColumnCategory.Text ? $"({sql} IS NULL OR {sql}::text = '')" : $"{sql} IS NULL";
                case "IS_NOT_EMPTY":
                    return info.Category == LovColumnCategory.Text ? $"({sql} IS NOT NULL AND {sql}::text <> '')" : $"{sql} IS NOT NULL";
                case "IN" or "NOT_IN":
                    var values = Values(value, valuePath);
                    if (values is null)
                    {
                        return null;
                    }

                    var list = Parameter(values);
                    return op == "IN" ? $"{sql}::text = ANY({list}::text[])" : $"({sql} IS NULL OR NOT ({sql}::text = ANY({list}::text[])))";
                case "CONTAINS" or "STARTS_WITH" or "ENDS_WITH":
                    var text = Text(value, valuePath);
                    if (text is null)
                    {
                        return null;
                    }

                    var escaped = SqlLovRunner.LikeEscape(text);
                    var pattern = op switch { "CONTAINS" => $"%{escaped}%", "STARTS_WITH" => $"{escaped}%", _ => $"%{escaped}" };
                    return $"{sql}::text ILIKE {Parameter(pattern)}";
                default:
                    if (op is "GT" or "GE" or "LT" or "LE" && info.Category is LovColumnCategory.Bool)
                    {
                        problems.Add(new($"{path}.op", "Yes/no columns are compared with equals or not equals."));
                        return null;
                    }

                    var typed = Typed(value, info.Category, valuePath);
                    if (typed is null)
                    {
                        return null;
                    }

                    var symbol = op switch { "EQ" => "=", "NE" => "<>", "GT" => ">", "GE" => ">=", "LT" => "<", _ => "<=" };
                    var left = info.Category switch
                    {
                        LovColumnCategory.Number => $"{sql}::numeric",
                        LovColumnCategory.Date => $"{sql}::date",
                        LovColumnCategory.Bool => $"{sql}::boolean",
                        _ => $"{sql}::text",
                    };
                    var cast = info.Category switch
                    {
                        LovColumnCategory.Number => "::numeric",
                        LovColumnCategory.Date => "::date",
                        LovColumnCategory.Bool => "::boolean",
                        _ => "::text",
                    };
                    return op == "NE" ? $"({sql} IS NULL OR {left} {symbol} {Parameter(typed)}{cast})" : $"{left} {symbol} {Parameter(typed)}{cast}";
            }
        }

        private string? JsonInput(string path)
        {
            problems.Add(new($"{path}.op", "A list or JSON column can only be checked for being empty."));
            return null;
        }

        /// <summary>The column compared with one of the user's facts; a fact the user holds in full (all branches) passes.</summary>
        private string? UserCondition(string fact, string op, string sql, string path)
        {
            var name = fact.ToUpperInvariant();
            if (!LovUserFacts.Names.Contains(name))
            {
                problems.Add(new($"{path}.user", "Choose what of the user to compare with."));
                return null;
            }

            if (!UserOperators.Contains(op))
            {
                problems.Add(new($"{path}.op", "Compare with the user's data by equals, not equals, in or not in."));
                return null;
            }

            if (run is null)
            {
                return "TRUE";
            }

            var values = run.User.Get(name);
            if (values is null)
            {
                return op is "EQ" or "IN" ? "TRUE" : "FALSE";
            }

            var list = Parameter(values.ToArray());
            return op is "EQ" or "IN" ? $"{sql}::text = ANY({list}::text[])" : $"({sql} IS NULL OR NOT ({sql}::text = ANY({list}::text[])))";
        }

        private string? Text(JsonNode? value, string path)
        {
            var text = value is JsonValue v && v.GetValueKind() is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : null;
            if (string.IsNullOrEmpty(text))
            {
                problems.Add(new(path, "Give the text to look for."));
                return null;
            }

            return text;
        }

        private string[]? Values(JsonNode? value, string path)
        {
            var values = value switch
            {
                JsonArray array => array.Select(x => x?.ToString()).OfType<string>().Where(x => x.Length > 0).ToArray(),
                JsonValue v when v.GetValueKind() is JsonValueKind.String => v.GetValue<string>().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                _ => [],
            };
            if (values.Length == 0)
            {
                problems.Add(new(path, "Give one or more values."));
                return null;
            }

            return values;
        }

        private string? Typed(JsonNode? value, LovColumnCategory category, string path)
        {
            var raw = value is JsonValue v ? v.GetValueKind() switch
            {
                JsonValueKind.String => v.GetValue<string>(),
                JsonValueKind.Number => v.ToJsonString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            } : null;
            if (raw is null || raw.Length == 0)
            {
                problems.Add(new(path, "Give the value to compare with."));
                return null;
            }

            var valid = category switch
            {
                LovColumnCategory.Number => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
                LovColumnCategory.Date => DatePattern().IsMatch(raw) && DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                LovColumnCategory.Bool => raw is "true" or "false",
                _ => true,
            };
            if (!valid)
            {
                problems.Add(new(path, category switch
                {
                    LovColumnCategory.Number => "Give a number.",
                    LovColumnCategory.Date => "Give a date as YYYY-MM-DD.",
                    _ => "Choose yes or no.",
                }));
                return null;
            }

            return raw;
        }

        private string Parameter(object? value)
        {
            var name = "@q" + _parameters.Count.ToString(CultureInfo.InvariantCulture);
            _parameters[name[1..]] = value;
            return name;
        }
    }
}
