using System.Text.Json;
using System.Text.Json.Nodes;

namespace DynamicCube.Modules.Decisions.Runtime.Matrix;

/// <summary>
/// Turns a classification guide exported from the reviewed classification system (rule set version, metrics, option
/// sets, sizes, grades, scales, sectors with tracks, groups, criteria and bands, gates, adjustments, project rules) into a
/// scoring matrix: sectors become rows, tracks columns, each sector's track a cell. Formulas are converted to JSONLogic;
/// the coded ICT category handler becomes a formula cell built from its settings. Form layout, attachments and project
/// fields are kept under <see cref="MatrixImportResult.Extras"/> for the service form.
/// </summary>
public static class GuideImport
{
    public static MatrixImportResult Convert(JsonObject export)
    {
        var warnings = new List<string>();
        var version = export["ruleSetVersion"] as JsonObject ?? [];
        var projectFields = (export["projectFields"] as JsonArray ?? []).Select(f => S(f, "code")).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);
        // Project values in millions are worked out from the fields in riyals.
        projectFields.UnionWith(["value_sar_m", "certified_value_sar_m"]);

        var definition = new MatrixDefinition
        {
            BelowWorst = S(version, "demoteBelowLowest").Equals("Stay", StringComparison.OrdinalIgnoreCase) ? "stay" : "notGraded",
            Grades = Items(export, "gradeLevels").Select(g => new MatrixGrade { Rank = I(g, "rank") ?? 0, Code = S(g, "code"), NameAr = S(g, "nameAr"), NameEn = S(g, "nameEn") }).ToList(),
            OptionSets = Items(export, "optionSets").Select(s => new MatrixOptionSet
            {
                Code = S(s, "code"),
                NameAr = S(s, "nameAr"),
                NameEn = S(s, "nameEn"),
                Items = Items(s, "items").Select(i => new MatrixOption { Code = S(i, "code"), LabelAr = S(i, "labelAr"), LabelEn = S(i, "labelEn"), Ordinal = I(i, "ordinal") ?? 0 }).ToList(),
            }).ToList(),
            Scales = Items(export, "gradeScales").Select(s => new MatrixScale
            {
                Code = S(s, "code"),
                NameAr = S(s, "nameAr"),
                NameEn = S(s, "nameEn"),
                Steps = Items(s, "steps").Select(t => new MatrixScaleStep { Min = D(t, "minScore"), Max = D(t, "maxScore"), Grade = I(t, "gradeRank") ?? 0 }).ToList(),
            }).ToList(),
        };

        var sizeInput = S(version, "sizeMetricCode");
        var sizes = Items(export, "sizeCategories").ToList();
        if (sizeInput.Length > 0 && sizes.Count > 0)
        {
            definition.Sizes = new MatrixSizes
            {
                Input = sizeInput,
                Categories = sizes.Select(c => new MatrixSize { Code = S(c, "code"), NameAr = S(c, "nameAr"), NameEn = S(c, "nameEn"), Min = D(c, "minEmployees"), Max = D(c, "maxEmployees") }).ToList(),
            };
        }

        foreach (var metric in Items(export, "metrics"))
        {
            definition.Inputs.Add(Input(metric, projectFields, warnings));
        }

        // Columns: the tracks in the order of the first sector that has them.
        var sectors = Items(export, "sectors").ToList();
        foreach (var track in sectors.SelectMany(s => Items(s, "tracks")))
        {
            if (definition.Columns.Any(c => c.Code == S(track, "code")))
            {
                continue;
            }

            definition.Columns.Add(new MatrixColumn
            {
                Code = S(track, "code"),
                NameAr = S(track, "nameAr"),
                NameEn = S(track, "nameEn"),
                IncludeInFinal = B(track, "includeInFinal") ?? true,
                Weight = D(track, "weight"),
            });
        }

        foreach (var sector in sectors)
        {
            var code = S(sector, "code");
            definition.Rows.Add(new MatrixRow
            {
                Code = code,
                NameAr = S(sector, "nameAr"),
                NameEn = S(sector, "nameEn"),
                Active = (B(sector, "isActive") ?? true) && (B(sector, "isEnabled") ?? true),
                Final = S(sector, "finalRule") switch
                {
                    "WorstOfTracks" or "" => "worst",
                    "Weighted" or "WeightedAverage" => "weighted",
                    var other => Warn(warnings, $"Sector {code}: final rule '{other}' read as worst of tracks.", "worst"),
                },
                NotesAr = N(sector, "notesAr"),
                NotesEn = N(sector, "notesEn"),
                Settings = new JsonObject
                {
                    ["authority"] = S(sector, "authorityCode"),
                    ["authorityAr"] = N(sector, "authorityAr"),
                    ["authorityEn"] = N(sector, "authorityEn"),
                    ["certificateScope"] = N(sector, "certificateScope"),
                    ["activityFloor"] = sector["activityFloor"]?.DeepClone(),
                    ["activities"] = sector["activities"]?.DeepClone(),
                    ["form"] = new JsonArray(Items(sector, "inputFields").Select(f => (JsonNode?)new JsonObject
                    {
                        ["input"] = S(f, "metricCode"),
                        ["section"] = N(f, "sectionCode"),
                        ["required"] = B(f, "isRequired") ?? false,
                        ["visibleWhen"] = Rule(N(f, "visibilityExpression"), warnings, $"{code} form {S(f, "metricCode")}"),
                        ["requiredWhen"] = Rule(N(f, "requiredWhen"), warnings, $"{code} form {S(f, "metricCode")}"),
                        ["column"] = N(f, "trackCode"),
                        ["order"] = I(f, "sortOrder"),
                    }).ToArray()),
                },
            });

            foreach (var track in Items(sector, "tracks"))
            {
                definition.Cells.Add(Cell(code, track, warnings));
            }
        }

        foreach (var gate in Items(export, "eligibilityRules"))
        {
            definition.Gates.Add(new MatrixGate
            {
                Code = S(gate, "code"),
                Rows = Scope(gate),
                Rule = Rule(N(gate, "conditionExpression"), warnings, $"gate {S(gate, "code")}"),
                MessageAr = S(gate, "messageAr"),
                MessageEn = S(gate, "messageEn"),
                Blocking = B(gate, "blocking") ?? true,
            });
        }

        foreach (var adjustment in Items(export, "adjustmentRules"))
        {
            if (S(adjustment, "effect") != "DemoteGrades")
            {
                warnings.Add($"Adjustment {S(adjustment, "code")}: effect '{S(adjustment, "effect")}' is not supported and was left out.");
                continue;
            }

            definition.Adjustments.Add(new MatrixAdjustment
            {
                Code = S(adjustment, "code"),
                NameAr = S(adjustment, "nameAr"),
                NameEn = S(adjustment, "nameEn"),
                Rows = Scope(adjustment),
                Rule = Rule(N(adjustment, "conditionExpression"), warnings, $"adjustment {S(adjustment, "code")}"),
                Demote = I(adjustment, "effectValue") ?? 1,
            });
        }

        definition.Projects = new MatrixProjects
        {
            Input = "projects",
            ValueField = "value_sar_m",
            CertifiedValueField = "certified_value_sar_m",
            CompletionField = "completion_pct",
            DateField = "start_date",
            SplitThreshold = D(export, "projectSplitThresholdSarM"),
            Rules = Items(export, "projectEligibilityRules").Select(r => new MatrixProjectRule
            {
                Code = S(r, "code"),
                Matches = new JsonObject
                {
                    ["and"] = new JsonArray(
                        new JsonObject { ["=="] = new JsonArray(new JsonObject { ["var"] = "location" }, S(r, "location")) },
                        new JsonObject { ["=="] = new JsonArray(new JsonObject { ["var"] = "project_type" }, S(r, "projectType")) }),
                },
                MinCompletion = D(r, "minCompletionPct"),
                MaxAgeYears = D(r, "maxAgeYears"),
                MinValue = D(r, "minValueSarM"),
                UseCertifiedValue = B(r, "useCertifiedValue") ?? false,
            }).ToList(),
            Fields = Items(export, "projectFields").Select(f => new MatrixInput
            {
                Code = S(f, "code"),
                NameAr = S(f, "nameAr"),
                NameEn = S(f, "nameEn"),
                Type = Type(S(f, "dataType")),
                Source = new MatrixSource { Kind = "given" },
            }).ToList(),
        };

        var extras = new JsonObject
        {
            ["code"] = S(version, "code"),
            ["nameAr"] = S(version, "nameAr"),
            ["nameEn"] = S(version, "nameEn"),
            ["effectiveFrom"] = N(version, "effectiveFrom"),
            ["formSections"] = export["formSections"]?.DeepClone(),
            ["attachmentTypes"] = export["attachmentTypes"]?.DeepClone(),
            ["attachmentRules"] = export["attachmentRules"]?.DeepClone(),
        };
        return new MatrixImportResult(definition, extras, warnings);
    }

    private static MatrixInput Input(JsonObject metric, ISet<string> projectFields, List<string> warnings)
    {
        var code = S(metric, "code");
        var input = new MatrixInput
        {
            Code = code,
            NameAr = S(metric, "nameAr"),
            NameEn = S(metric, "nameEn"),
            Type = Type(S(metric, "dataType")),
            Unit = N(metric, "unit"),
            OptionSet = N(metric, "optionSetCode"),
            Min = D(metric, "minValue"),
            Max = D(metric, "maxValue"),
        };
        var expression = N(metric, "expression");
        var provider = N(metric, "externalProvider");
        var enteredBy = N(metric, "enteredBy");
        input.Source = S(metric, "source") switch
        {
            _ when GuideExpressions.ProjectSource(expression, projectFields) is { } projects => projects,
            "Derived" => new MatrixSource { Kind = "derived", Rule = Rule(expression, warnings, $"input {code}") },
            // Entered by staff (possibly from a provider they consult, such as the credit rating): it waits for the reviewer.
            _ when string.Equals(enteredBy, "Staff", StringComparison.OrdinalIgnoreCase) => new MatrixSource { Kind = "reviewer", Field = code, Operation = provider?.ToUpperInvariant() },
            _ when provider is not null => new MatrixSource
            {
                Kind = "integration",
                Operation = provider.ToUpperInvariant(),
                Output = N(metric, "externalField") ?? code,
                AllowOverride = B(metric, "allowOverride") ?? false,
            },
            _ => new MatrixSource { Kind = "applicant", Question = code, AllowOverride = B(metric, "allowOverride") ?? false },
        };
        return input;
    }

    private static MatrixCell Cell(string row, JsonObject track, List<string> warnings)
    {
        var aggregation = S(track, "aggregation");
        var cell = new MatrixCell
        {
            Row = row,
            Column = S(track, "code"),
            Kind = aggregation switch
            {
                "SumToScale" => "points",
                "WorstGrade" => "worst",
                "Custom" => "formula",
                _ => Warn(warnings, $"{row}/{S(track, "code")}: aggregation '{aggregation}' read as points.", "points"),
            },
            Scale = N(track, "gradeScaleCode"),
            NoteAr = N(track, "note"),
        };

        foreach (var group in Items(track, "groups"))
        {
            cell.Groups.Add(new MatrixGroup
            {
                Code = S(group, "code"),
                NameAr = S(group, "nameAr"),
                NameEn = S(group, "nameEn"),
                MaxPoints = D(group, "maxPoints"),
                Mandatory = B(group, "isMandatory") ?? false,
                Criteria = Items(group, "criteria").Select(c => Criterion(c, warnings, row)).ToList(),
            });
        }

        if (cell.Kind == "formula")
        {
            var handler = S(track, "customHandler");
            if (handler == "IctCategory" && track["settings"] is JsonObject settings)
            {
                IctCategory(cell, settings);
            }
            else
            {
                warnings.Add($"{row}/{cell.Column}: custom handler '{handler}' has no formula; set the cell's grade rule.");
                cell.GradeRule = null;
            }
        }

        return cell;
    }

    /// <summary>
    /// The ICT category handler as a formula: the grade from the overall score by the handler's scale (below the floor:
    /// not graded); the category shown with it (platinum needs research and development resources in the Kingdom).
    /// </summary>
    private static void IctCategory(MatrixCell cell, JsonObject settings)
    {
        var score = new JsonObject { ["var"] = S(settings, "overallMetric") };
        var floor = D(settings, "overallFloor") ?? 0;
        var steps = (settings["gradeScale"] as JsonArray ?? []).OfType<JsonArray>()
            .Select(s => (Min: s[0]!.GetValue<decimal>(), Grade: s[1]!.GetValue<int>()))
            .OrderByDescending(s => s.Min)
            .ToList();
        var grade = new JsonArray { new JsonObject { ["<"] = new JsonArray(score.DeepClone(), floor) }, null };
        foreach (var step in steps)
        {
            grade.Add(new JsonObject { [">="] = new JsonArray(score.DeepClone(), step.Min) });
            grade.Add(step.Grade);
        }

        grade.Add(null);
        cell.GradeRule = new JsonObject { ["if"] = grade };
        cell.ScoreRule = score.DeepClone();

        var hasRd = new JsonObject { ["var"] = S(settings, "hasRdMetric") };
        var category = new JsonArray();
        foreach (var c in (settings["categories"] as JsonArray ?? []).OfType<JsonObject>().OrderByDescending(c => D(c, "min")))
        {
            JsonNode condition = new JsonObject { [">="] = new JsonArray(score.DeepClone(), D(c, "min")) };
            if (B(c, "requiresRD") == true)
            {
                condition = new JsonObject { ["and"] = new JsonArray(condition, new JsonObject { ["=="] = new JsonArray(hasRd.DeepClone(), true) }) };
            }

            category.Add(condition);
            category.Add(new JsonObject { ["category"] = S(c, "id"), ["labelAr"] = S(c, "label"), ["chain"] = S(c, "chain") });
        }

        category.Add(null);
        cell.DetailsRule = new JsonObject { ["if"] = category };
        cell.NoteEn = "Imported from the ICT category handler: grade by the overall score; category with its value chain.";
    }

    private static MatrixCriterion Criterion(JsonObject c, List<string> warnings, string row)
    {
        var scoring = S(c, "scoringType");
        return new MatrixCriterion
        {
            Code = S(c, "code"),
            NameAr = S(c, "nameAr"),
            NameEn = S(c, "nameEn"),
            Input = S(c, "metricCode"),
            Scoring = scoring switch
            {
                "Bands" => "bands",
                "Options" => "options",
                "Direct" => "direct",
                _ => Warn(warnings, $"{row} criterion {S(c, "code")}: scoring '{scoring}' read as bands.", "bands"),
            },
            Output = S(c, "outputType") == "Grade" ? "grade" : "points",
            UseOrdinal = S(c, "valueSource") == "OptionOrdinal",
            MaxPoints = D(c, "maxPoints"),
            AppliesWhen = Rule(N(c, "applicabilityExpression"), warnings, $"{row} criterion {S(c, "code")}"),
            AlternativeKey = N(c, "minGroupKey"),
            Note = N(c, "note"),
            Bands = Items(c, "bands").OrderBy(b => I(b, "bandIndex")).Select(b => new MatrixBand
            {
                Size = N(b, "size"),
                Min = D(b, "minValue"),
                MinInclusive = B(b, "minInclusive") ?? true,
                Max = D(b, "maxValue"),
                MaxInclusive = B(b, "maxInclusive") ?? false,
                Points = D(b, "points"),
                Grade = I(b, "gradeRank"),
                Label = N(b, "label"),
            }).ToList(),
            Options = Items(c, "options").Select(o => new MatrixOptionScore
            {
                Value = S(o, "optionCode"),
                Points = D(o, "points"),
                Grade = I(o, "gradeRank"),
                Attachment = B(o, "requiresAttachment") == true ? N(o, "attachmentTypeCode") : null,
            }).ToList(),
        };
    }

    private static JsonNode? Rule(string? expression, List<string> warnings, string where)
    {
        try
        {
            return GuideExpressions.Convert(expression);
        }
        catch (FormatException ex)
        {
            warnings.Add($"{where}: formula '{expression}' could not be read ({ex.Message}).");
            return null;
        }
    }

    private static List<string> Scope(JsonObject rule) =>
        S(rule, "scope") == "AllSectors" ? [] : (rule["sectorCodes"] as JsonArray ?? []).Select(s => s?.ToString() ?? string.Empty).Where(s => s.Length > 0).ToList();

    private static string Type(string dataType) => dataType switch
    {
        "Integer" or "Decimal" or "Money" or "Percent" or "Years" => "number",
        "Option" => "option",
        "Boolean" or "YesNo" => "boolean",
        "Date" => "date",
        "Table" => "list",
        _ => "text",
    };

    private static T Warn<T>(List<string> warnings, string message, T value)
    {
        warnings.Add(message);
        return value;
    }

    private static IEnumerable<JsonObject> Items(JsonNode? node, string key) => (node?[key] as JsonArray ?? []).OfType<JsonObject>();

    private static string S(JsonNode? node, string key) => node?[key] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : string.Empty;

    private static string? N(JsonNode? node, string key) => S(node, key) is { Length: > 0 } s ? s : null;

    private static decimal? D(JsonNode? node, string key) => MatrixEngine.Number(node?[key]);

    private static int? I(JsonNode? node, string key) => D(node, key) is { } d ? (int)d : null;

    private static bool? B(JsonNode? node, string key) => node?[key] is JsonValue v && v.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? v.GetValue<bool>() : null;
}

/// <summary>An imported guide: the matrix, what the service form needs (sections, attachments) and what could not be read.</summary>
public sealed record MatrixImportResult(MatrixDefinition Definition, JsonObject Extras, IReadOnlyList<string> Warnings);
