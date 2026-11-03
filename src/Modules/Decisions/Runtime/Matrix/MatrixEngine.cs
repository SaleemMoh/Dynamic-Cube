using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Decisions.Runtime.Matrix;

/// <summary>
/// The scoring matrix engine (ADR-0050). Input: <c>values</c> (input code → value; missing derived and project inputs are
/// worked out), optionally <c>rows</c> (the row codes to grade; all active rows otherwise) and <c>asOf</c> (the date ages
/// are counted to). Output: the resolved <c>values</c>, the <c>size</c> and, per row, its gates, each column's score,
/// grade and explanation down to every criterion, the provisional, base and final grades, the adjustments applied, the
/// limiting columns and the inputs still pending.
/// </summary>
internal sealed class MatrixEngine(JsonLogicEvaluator logic, TimeProvider clock) : IDecisionEngine
{
    public const string EngineCode = "MATRIX";

    public string Code => EngineCode;

    public string NameAr => "مصفوفة التقييم";

    public string NameEn => "Scoring matrix";

    public IReadOnlyDictionary<string, string> Validate(JsonNode? configuration) => MatrixValidator.Validate(configuration, logic);

    public Task<EngineResult> EvaluateAsync(JsonNode configuration, JsonObject input, CancellationToken cancellationToken)
    {
        var definition = MatrixDefinition.Read(configuration);
        var trace = new JsonArray();
        var asOf = input["asOf"] is JsonValue d && DateOnly.TryParse(d.ToString()[..Math.Min(10, d.ToString().Length)], CultureInfo.InvariantCulture, out var day)
            ? day
            : DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var values = (input["values"] as JsonObject ?? input).DeepClone().AsObject();
        values.Remove("rows");
        values.Remove("asOf");

        var evaluation = new Evaluation(definition, logic, values, asOf);
        evaluation.Resolve(trace);
        var wanted = (input["rows"] as JsonArray)?.Select(r => r?.ToString()).Where(r => r is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new JsonArray();
        foreach (var row in definition.Rows.Where(r => wanted is null ? r.Active : wanted.Contains(r.Code)))
        {
            rows.Add(evaluation.Row(row, trace));
        }

        var output = new JsonObject
        {
            ["size"] = evaluation.Size?.Code,
            ["values"] = values.DeepClone(),
            ["rows"] = rows,
        };
        return Task.FromResult(new EngineResult(output, trace));
    }

    /// <summary>One evaluation: the resolved values, the size and the projects that count.</summary>
    private sealed class Evaluation(MatrixDefinition definition, JsonLogicEvaluator logic, JsonObject values, DateOnly asOf)
    {
        private readonly Dictionary<string, MatrixInput> _inputs = definition.Inputs.GroupBy(i => i.Code, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        public MatrixSize? Size { get; private set; }

        public void Resolve(JsonArray trace)
        {
            var projects = CountedProjects(trace);
            // Derived inputs may read other derived inputs: a few passes settle them.
            for (var pass = 0; pass < 3; pass++)
            {
                foreach (var input in definition.Inputs.Where(i => i.Source.Kind is "derived" or "projects"))
                {
                    if (pass == 0 && values[input.Code] is not null)
                    {
                        // A value given for a computed input wins (entered, overridden or already worked out).
                        values[$"__given_{input.Code}"] = true;
                        continue;
                    }

                    if (values[$"__given_{input.Code}"] is not null)
                    {
                        continue;
                    }

                    var value = input.Source.Kind == "derived"
                        ? Safe(() => logic.Evaluate(input.Source.Rule, values))
                        : Aggregate(input.Source, projects);
                    values[input.Code] = value?.DeepClone();
                    if (pass == 2)
                    {
                        trace.Add(new JsonObject { ["step"] = $"inputs.{input.Code}", ["value"] = value?.DeepClone() });
                    }
                }
            }

            foreach (var key in values.Select(p => p.Key).Where(k => k.StartsWith("__given_", StringComparison.Ordinal)).ToList())
            {
                values.Remove(key);
            }

            if (definition.Sizes is { } sizes && Number(values[sizes.Input]) is { } measure)
            {
                Size = sizes.Categories.FirstOrDefault(c => (c.Min is null || measure >= c.Min) && (c.Max is null || measure <= c.Max));
            }

            trace.Add(new JsonObject { ["step"] = "size", ["value"] = Size?.Code });
        }

        public JsonObject Row(MatrixRow row, JsonArray trace)
        {
            var gates = new JsonArray();
            var eligible = true;
            foreach (var gate in definition.Gates.Where(g => g.Rows.Count == 0 || g.Rows.Contains(row.Code, StringComparer.OrdinalIgnoreCase)))
            {
                var passed = Truthy(gate.Rule);
                eligible &= passed || !gate.Blocking;
                gates.Add(new JsonObject
                {
                    ["code"] = gate.Code,
                    ["passed"] = passed,
                    ["blocking"] = gate.Blocking,
                    ["messageAr"] = gate.MessageAr,
                    ["messageEn"] = gate.MessageEn,
                });
            }

            var columns = new JsonArray();
            var results = new List<(MatrixColumn Column, CellResult Result)>();
            foreach (var column in definition.Columns)
            {
                var result = Cell(row.Code, column.Code, 0);
                results.Add((column, result));
                columns.Add(result.ToJson(column));
            }

            var included = results.Where(r => r.Column.IncludeInFinal && r.Result.Kind != "empty").ToList();
            var pending = included.Where(r => r.Result.Pending).ToList();
            var ungraded = included.Where(r => !r.Result.Pending && r.Result.Grade is null).ToList();
            int? provisional = null;
            if (ungraded.Count == 0 && included.Any(r => !r.Result.Pending))
            {
                provisional = row.Final switch
                {
                    "weighted" => Weighted(included.Where(r => !r.Result.Pending).ToList()),
                    "formula" => (int?)Number(Safe(() => logic.Evaluate(row.FinalRule, FormulaData(results)))),
                    _ => included.Where(r => !r.Result.Pending).Max(r => r.Result.Grade),
                };
            }

            var limiting = pending.Count > 0 || ungraded.Count > 0
                ? ungraded.Concat(pending).Select(r => r.Column.Code).ToList()
                : included.Where(r => r.Result.Grade == provisional).Select(r => r.Column.Code).ToList();
            foreach (var column in columns.OfType<JsonObject>())
            {
                column["limiting"] = limiting.Contains(column["code"]!.GetValue<string>());
            }

            var adjustments = new JsonArray();
            var demote = 0;
            foreach (var adjustment in definition.Adjustments.Where(a => a.Rows.Count == 0 || a.Rows.Contains(row.Code, StringComparer.OrdinalIgnoreCase)))
            {
                var applied = Truthy(adjustment.Rule);
                demote += applied ? adjustment.Demote : 0;
                adjustments.Add(new JsonObject { ["code"] = adjustment.Code, ["nameAr"] = adjustment.NameAr, ["nameEn"] = adjustment.NameEn, ["applied"] = applied, ["demote"] = adjustment.Demote });
            }

            var baseGrade = eligible && pending.Count == 0 ? provisional : null;
            var expected = eligible ? Demote(provisional, demote) : null;
            var rowResult = new JsonObject
            {
                ["code"] = row.Code,
                ["nameAr"] = row.NameAr,
                ["nameEn"] = row.NameEn,
                ["eligible"] = eligible,
                ["size"] = Size?.Code,
                ["gates"] = gates,
                ["columns"] = columns,
                ["provisionalGrade"] = eligible ? provisional : null,
                ["baseGrade"] = baseGrade,
                ["finalGrade"] = Demote(baseGrade, demote),
                ["expectedGrade"] = expected,
                ["adjustments"] = adjustments,
                ["limiting"] = new JsonArray(limiting.Select(l => (JsonNode?)l).ToArray()),
                ["pending"] = new JsonArray(results.SelectMany(r => r.Result.PendingInputs).Distinct().Select(p => (JsonNode?)p).ToArray()),
            };
            trace.Add(new JsonObject { ["step"] = $"rows.{row.Code}", ["value"] = new JsonObject { ["provisional"] = provisional, ["final"] = rowResult["finalGrade"]?.DeepClone(), ["eligible"] = eligible } });
            return rowResult;
        }

        private static int? Weighted(List<(MatrixColumn Column, CellResult Result)> graded)
        {
            var total = graded.Sum(g => g.Column.Weight ?? 1m);
            if (total <= 0)
            {
                return null;
            }

            var mean = graded.Sum(g => (g.Column.Weight ?? 1m) * g.Result.Grade!.Value) / total;
            return (int)Math.Round(mean, MidpointRounding.AwayFromZero);
        }

        private JsonObject FormulaData(List<(MatrixColumn Column, CellResult Result)> results)
        {
            var data = values.DeepClone().AsObject();
            data["columns"] = new JsonObject(results.Select(r => KeyValuePair.Create<string, JsonNode?>(r.Column.Code, new JsonObject { ["grade"] = r.Result.Grade, ["points"] = r.Result.Score })));
            return data;
        }

        private int? Demote(int? grade, int by)
        {
            if (grade is null || by == 0)
            {
                return grade;
            }

            var worst = definition.Grades.Count == 0 ? grade.Value + by : definition.Grades.Max(g => g.Rank);
            var demoted = grade.Value + by;
            return demoted <= worst ? demoted : definition.BelowWorst == "notGraded" ? null : worst;
        }

        private CellResult Cell(string row, string column, int depth)
        {
            var cell = definition.Cells.FirstOrDefault(c => c.Row.Equals(row, StringComparison.OrdinalIgnoreCase) && c.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
            if (cell is null || cell.Kind == "empty")
            {
                return new CellResult { Kind = "empty" };
            }

            if (cell.Kind == "same")
            {
                return depth < 5 && cell.SameRow is not null
                    ? Cell(cell.SameRow, cell.SameColumn ?? column, depth + 1) with { SameAs = $"{cell.SameRow}/{cell.SameColumn ?? column}" }
                    : new CellResult { Kind = "same", Note = "The copied cell is missing." };
            }

            if (cell.Kind == "formula")
            {
                var grade = Number(Safe(() => logic.Evaluate(cell.GradeRule, values)));
                return new CellResult
                {
                    Kind = "formula",
                    Grade = grade is null ? null : (int)grade.Value,
                    Score = cell.ScoreRule is null ? null : Number(Safe(() => logic.Evaluate(cell.ScoreRule, values))),
                    Details = cell.DetailsRule is null ? null : Safe(() => logic.Evaluate(cell.DetailsRule, values))?.DeepClone(),
                };
            }

            var groups = new JsonArray();
            var pendingInputs = new List<string>();
            decimal score = 0, maxScore = 0;
            var minimumMet = true;
            var grades = new List<int>();
            foreach (var group in cell.Groups)
            {
                var criteria = new JsonArray();
                decimal points = 0, groupMax = 0;
                foreach (var criterion in group.Criteria)
                {
                    var c = Criterion(criterion);
                    criteria.Add(c.Json);
                    points += c.Points;
                    groupMax += c.MaxPoints;
                    if (c.Grade is { } g)
                    {
                        grades.Add(g);
                    }

                    if (c.Pending)
                    {
                        pendingInputs.Add(criterion.Input);
                    }
                }

                var max = group.MaxPoints ?? groupMax;
                points = Math.Min(points, max == 0 ? points : max);
                var met = !group.Mandatory || points > group.MinPoints;
                minimumMet &= met;
                score += points;
                maxScore += max;
                groups.Add(new JsonObject
                {
                    ["code"] = group.Code,
                    ["nameAr"] = group.NameAr,
                    ["nameEn"] = group.NameEn,
                    ["points"] = points,
                    ["maxPoints"] = max,
                    ["mandatory"] = group.Mandatory,
                    ["minimumMet"] = met,
                    ["criteria"] = criteria,
                });
            }

            var result = new CellResult { Kind = cell.Kind, Groups = groups, PendingInputs = pendingInputs, MinimumMet = minimumMet };
            if (pendingInputs.Count > 0)
            {
                return result with { Pending = true, Score = cell.Kind == "points" ? score : null };
            }

            if (cell.Kind == "worst")
            {
                return result with { Grade = grades.Count == 0 ? null : grades.Max() };
            }

            var scale = definition.Scales.FirstOrDefault(s => s.Code == cell.Scale);
            var step = minimumMet ? scale?.Steps.FirstOrDefault(s => (s.Min is null || score >= s.Min) && (s.Max is null || score < s.Max)) : null;
            return result with { Score = score, MaxScore = maxScore, Grade = step?.Grade, Note = minimumMet ? null : "minimumNotMet" };
        }

        private (JsonObject Json, decimal Points, decimal MaxPoints, int? Grade, bool Pending) Criterion(MatrixCriterion criterion)
        {
            var value = values[criterion.Input];
            var applicable = criterion.AppliesWhen is null || Truthy(criterion.AppliesWhen);
            var maxPoints = criterion.MaxPoints
                ?? (criterion.Scoring == "options" ? criterion.Options.Max(o => o.Points) : criterion.Bands.Max(b => b.Points)) ?? 0m;
            var json = new JsonObject
            {
                ["code"] = criterion.Code,
                ["nameAr"] = criterion.NameAr,
                ["nameEn"] = criterion.NameEn,
                ["input"] = criterion.Input,
                ["value"] = value?.DeepClone(),
                ["applicable"] = applicable,
                ["maxPoints"] = criterion.Output == "points" ? maxPoints : null,
            };
            var source = _inputs.GetValueOrDefault(criterion.Input)?.Source.Kind;
            if (applicable && value is null && source == "reviewer")
            {
                json["pending"] = true;
                return (json, 0, criterion.Output == "points" ? maxPoints : 0, null, true);
            }

            if (!applicable)
            {
                json["points"] = 0;
                json["note"] = "notApplicable";
                return (json, 0, criterion.Output == "points" ? maxPoints : 0, null, false);
            }

            decimal? points = null;
            int? grade = null;
            switch (criterion.Scoring)
            {
                case "options":
                    {
                        var answer = Text(value);
                        var option = criterion.Options.FirstOrDefault(o => string.Equals(o.Value, answer, StringComparison.OrdinalIgnoreCase));
                        json["option"] = option?.Value;
                        points = option?.Points;
                        grade = option?.Grade;
                        break;
                    }

                case "direct":
                    {
                        var number = Number(value);
                        points = number is null ? null : Math.Clamp(number.Value, 0, maxPoints == 0 ? number.Value : maxPoints);
                        break;
                    }

                default:
                    {
                        var number = criterion.UseOrdinal ? Ordinal(criterion.Input, value) : Number(value);
                        var bands = criterion.Bands.Where(b => b.Size is null || b.Size == Size?.Code).ToList();
                        var index = number is null ? -1 : bands.FindIndex(b => InBand(b, number.Value));
                        if (index >= 0)
                        {
                            json["band"] = index;
                            json["bandLabel"] = bands[index].Label;
                            points = bands[index].Points;
                            grade = bands[index].Grade;
                        }

                        json["number"] = number;
                        break;
                    }
            }

            if (criterion.Output == "grade")
            {
                json["grade"] = grade;
                return (json, 0, 0, grade, false);
            }

            json["points"] = points ?? 0;
            return (json, points ?? 0, maxPoints, null, false);
        }

        private decimal? Ordinal(string input, JsonNode? value)
        {
            var set = _inputs.GetValueOrDefault(input)?.OptionSet;
            var answer = Text(value);
            var item = definition.OptionSets.FirstOrDefault(s => s.Code == set)?.Items.FirstOrDefault(i => string.Equals(i.Code, answer, StringComparison.OrdinalIgnoreCase));
            return item?.Ordinal;
        }

        private static bool InBand(MatrixBand band, decimal value) =>
            (band.Min is null || (band.MinInclusive ? value >= band.Min : value > band.Min))
            && (band.Max is null || (band.MaxInclusive ? value <= band.Max : value < band.Max));

        private List<JsonObject> CountedProjects(JsonArray trace)
        {
            var config = definition.Projects;
            if (config is null || values[config.Input] is not JsonArray list)
            {
                return [];
            }

            var counted = new List<JsonObject>();
            foreach (var project in list.OfType<JsonObject>())
            {
                var row = project.DeepClone().AsObject();
                foreach (var (key, node) in project)
                {
                    if (node is JsonValue v && v.GetValueKind() == JsonValueKind.String && DateOnly.TryParse(v.ToString()[..Math.Min(10, v.ToString().Length)], CultureInfo.InvariantCulture, out var date))
                    {
                        row[$"{key}_yearsAgo"] = Math.Round((asOf.DayNumber - date.DayNumber) / 365.25m, 4);
                    }
                }

                if (config.Rules.Count == 0)
                {
                    counted.Add(row);
                    continue;
                }

                var rule = config.Rules.FirstOrDefault(r => r.Matches is null || logic.EvaluateTruthy(r.Matches, row));
                if (rule is null
                    || (rule.MinCompletion is { } completion && config.CompletionField is { } cf && (Number(row[cf]) ?? 0) < completion)
                    || (rule.MaxAgeYears is { } age && config.DateField is { } df && Number(row[$"{df}_yearsAgo"]) is { } years && years > age)
                    || (rule.MinValue is { } minimum && (Number(row[config.ValueField]) ?? 0) < minimum))
                {
                    continue;
                }

                if (rule.UseCertifiedValue && config.CertifiedValueField is { } certified)
                {
                    row[config.ValueField] = row[certified]?.DeepClone();
                }

                counted.Add(row);
            }

            trace.Add(new JsonObject { ["step"] = "projects", ["value"] = new JsonObject { ["given"] = list.Count, ["counted"] = counted.Count } });
            return counted;
        }

        private JsonNode? Aggregate(MatrixSource source, List<JsonObject> projects)
        {
            var rows = source.Where is null ? projects : projects.Where(p => logic.EvaluateTruthy(source.Where, p)).ToList();
            return source.Function switch
            {
                "count" => rows.Count,
                "distinct" => rows.Select(r => Text(r[source.Column ?? string.Empty])).Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                "max" => rows.Select(r => Number(r[source.Column ?? string.Empty])).Where(n => n is not null).DefaultIfEmpty(0m).Max(),
                _ => rows.Sum(r => Number(r[source.Column ?? string.Empty]) ?? 0m),
            };
        }

        private bool Truthy(JsonNode? rule) => rule is null || (Safe(() => logic.Evaluate(rule, values)) is var v && JsonLogicEvaluator.IsTruthy(v));

        private static JsonNode? Safe(Func<JsonNode?> evaluate)
        {
            try
            {
                return evaluate();
            }
            catch (JsonLogicException)
            {
                return null;
            }
        }
    }

    internal static decimal? Number(JsonNode? node)
    {
        if (node is not JsonValue v)
        {
            return null;
        }

        switch (v.GetValueKind())
        {
            case JsonValueKind.Number:
                if (v.TryGetValue<decimal>(out var d))
                {
                    return d;
                }

                if (v.TryGetValue<double>(out var x) && double.IsFinite(x))
                {
                    return (decimal)x;
                }

                if (v.TryGetValue<long>(out var l))
                {
                    return l;
                }

                return decimal.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            case JsonValueKind.String:
                return decimal.TryParse(v.GetValue<string>(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s) ? s : null;
            case JsonValueKind.True:
                return 1;
            case JsonValueKind.False:
                return 0;
            default:
                return null;
        }
    }

    internal static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.GetValueKind() == JsonValueKind.True => "yes",
        JsonValue v when v.GetValueKind() == JsonValueKind.False => "no",
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        _ => node.ToJsonString(),
    };

    private sealed record CellResult
    {
        public string Kind { get; init; } = "points";

        public decimal? Score { get; init; }

        public decimal? MaxScore { get; init; }

        public int? Grade { get; init; }

        public bool Pending { get; init; }

        public bool MinimumMet { get; init; } = true;

        public List<string> PendingInputs { get; init; } = [];

        public JsonArray? Groups { get; init; }

        public JsonNode? Details { get; init; }

        public string? SameAs { get; init; }

        public string? Note { get; init; }

        public JsonObject ToJson(MatrixColumn column) => new()
        {
            ["code"] = column.Code,
            ["nameAr"] = column.NameAr,
            ["nameEn"] = column.NameEn,
            ["kind"] = Kind,
            ["includeInFinal"] = column.IncludeInFinal,
            ["score"] = Score,
            ["maxScore"] = MaxScore,
            ["grade"] = Grade,
            ["pending"] = Pending,
            ["minimumMet"] = MinimumMet,
            ["sameAs"] = SameAs,
            ["note"] = Note,
            ["details"] = Details?.DeepClone(),
            ["groups"] = Groups?.DeepClone(),
        };
    }
}
