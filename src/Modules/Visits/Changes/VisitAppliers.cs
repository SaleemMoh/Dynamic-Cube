using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Integration;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Visits.Changes;

/// <summary>Entity types, change-set area and guarding resources of visits.</summary>
public static class VisitChangeTypes
{
    public const string Type = "vis.visit-type";

    public const string Area = "vis.visits";
}

public static class VisitResources
{
    /// <summary>The visits register; EDIT allows scheduling, cancelling and recording results.</summary>
    public const string Registry = "visits.registry";

    /// <summary>The field app: the inspector's own visits.</summary>
    public const string Field = "visits.field";

    public const string Config = "visits.config";
}

public sealed class VisitTypePayload : ConfigPayload
{
    public VisitMode Mode { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public List<VisitOutcome> Outcomes { get; set; } = [];

    public List<VisitField> Fields { get; set; } = [];

    public int PhotosRequired { get; set; }

    public bool RequireLocation { get; set; }

    public bool RequireSignature { get; set; }

    public string? InspectorRole { get; set; }

    public int DueDays { get; set; } = 5;

    public ExternalVisitSettings? External { get; set; }
}

internal sealed partial class VisitTypeApplier(VisitsDbContext db, JsonLogicEvaluator evaluator)
    : ConfigApplier<VisitsDbContext, ConfigRefs<VisitsDbContext>, VisitType, VisitTypePayload>(db)
{
    /// <summary>Most photos a report may carry.</summary>
    public const int MaxPhotos = 8;

    public override string EntityType => VisitChangeTypes.Type;

    public override string Area => VisitChangeTypes.Area;

    public override string ResourceCode => VisitResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<VisitsDbContext> CreateRefs(VisitsDbContext context, ChangeSet? draft) => new(context, draft);

    protected override IQueryable<VisitType> Query(VisitsDbContext context) => context.Types;

    protected override Task ApplyAsync(VisitType entity, VisitTypePayload payload, ConfigRefs<VisitsDbContext> refs, ValidationErrors errors, VisitType? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        errors.MaxLength("inspectorRole", payload.InspectorRole, 100);
        if (payload.DueDays is < 1 or > 90)
        {
            errors.Add("dueDays", "Must be between 1 and 90.");
        }

        if (payload.PhotosRequired is < 0 or > MaxPhotos)
        {
            errors.Add("photosRequired", $"Must be between 0 and {MaxPhotos}.");
        }

        Outcomes(payload.Outcomes, errors);
        Fields(payload.Fields, errors);
        if (payload.Mode == VisitMode.External)
        {
            External(payload.External, payload.Outcomes, errors);
        }

        entity.Mode = payload.Mode;
        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.Outcomes = payload.Outcomes;
        entity.Fields = payload.Fields;
        entity.PhotosRequired = payload.PhotosRequired;
        entity.RequireLocation = payload.RequireLocation;
        entity.RequireSignature = payload.RequireSignature;
        entity.InspectorRole = Blank(payload.InspectorRole);
        entity.DueDays = payload.DueDays;
        entity.External = payload.Mode == VisitMode.External ? payload.External : null;
        return Task.CompletedTask;
    }

    protected override Task<VisitTypePayload> ToPayloadAsync(VisitsDbContext context, VisitType entity, CancellationToken cancellationToken) =>
        Task.FromResult(new VisitTypePayload
        {
            Mode = entity.Mode,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Outcomes = entity.Outcomes,
            Fields = entity.Fields,
            PhotosRequired = entity.PhotosRequired,
            RequireLocation = entity.RequireLocation,
            RequireSignature = entity.RequireSignature,
            InspectorRole = entity.InspectorRole,
            DueDays = entity.DueDays,
            External = entity.External,
        });

    protected override async Task BeforeDeleteAsync(VisitsDbContext context, VisitType entity, CancellationToken cancellationToken)
    {
        if (await context.Visits.AnyAsync(v => v.TypeId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Visit type '{entity.Code}' has visits; deactivate it instead.");
        }
    }

    private static void Outcomes(List<VisitOutcome> outcomes, ValidationErrors errors)
    {
        if (outcomes.Count == 0)
        {
            errors.Add("outcomes", "Give at least one outcome.");
        }

        for (var i = 0; i < outcomes.Count; i++)
        {
            var outcome = outcomes[i];
            errors.Code($"outcomes[{i}].code", outcome.Code, ValidationErrors.IdentifierCode());
            errors.Required($"outcomes[{i}].nameAr", outcome.NameAr);
            errors.Required($"outcomes[{i}].nameEn", outcome.NameEn);
            errors.MaxLength($"outcomes[{i}].nameAr", outcome.NameAr, 200);
            errors.MaxLength($"outcomes[{i}].nameEn", outcome.NameEn, 200);
        }

        foreach (var duplicate in outcomes.GroupBy(o => o.Code).Where(g => g.Count() > 1))
        {
            errors.Add("outcomes", $"'{duplicate.Key}' is used more than once.");
        }
    }

    private static void Fields(List<VisitField> fields, ValidationErrors errors)
    {
        if (fields.Count > 50)
        {
            errors.Add("fields", "At most 50 fields.");
        }

        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (string.IsNullOrEmpty(field.Code) || !FieldCode().IsMatch(field.Code))
            {
                errors.Add($"fields[{i}].code", "Use letters, digits and _, starting with a letter.");
            }

            errors.Required($"fields[{i}].labelAr", field.LabelAr);
            errors.Required($"fields[{i}].labelEn", field.LabelEn);
            errors.MaxLength($"fields[{i}].labelAr", field.LabelAr, 200);
            errors.MaxLength($"fields[{i}].labelEn", field.LabelEn, 200);
            if (field.Type == VisitFieldType.Select && field.Options.Count == 0)
            {
                errors.Add($"fields[{i}].options", "A list needs its choices.");
            }

            if (field.Type != VisitFieldType.Select && field.Options.Count > 0)
            {
                errors.Add($"fields[{i}].options", "Only lists have choices.");
            }

            for (var o = 0; o < field.Options.Count; o++)
            {
                errors.Code($"fields[{i}].options[{o}].code", field.Options[o].Code, ValidationErrors.IdentifierCode());
                errors.Required($"fields[{i}].options[{o}].labelAr", field.Options[o].LabelAr);
                errors.Required($"fields[{i}].options[{o}].labelEn", field.Options[o].LabelEn);
            }
        }

        foreach (var duplicate in fields.GroupBy(f => f.Code).Where(g => g.Count() > 1))
        {
            errors.Add("fields", $"'{duplicate.Key}' is used more than once.");
        }
    }

    private void External(ExternalVisitSettings? external, List<VisitOutcome> outcomes, ValidationErrors errors)
    {
        if (external is null)
        {
            errors.Add("external", "Say how the external system is called.");
            return;
        }

        errors.Required("external.createOperation", external.CreateOperation);
        errors.MaxLength("external.createOperation", external.CreateOperation, 100);
        errors.MaxLength("external.resultOperation", external.ResultOperation, 100);
        errors.Required("external.referenceOutput", external.ReferenceOutput);
        if (external.PollMinutes is < 1 or > 1440)
        {
            errors.Add("external.pollMinutes", "Must be between 1 and 1440.");
        }

        if (external.CallbackSecret is { Length: > 0 } secret && !SecretName().IsMatch(secret))
        {
            errors.Add("external.callbackSecret", "A secret name: upper-case letters, digits and _.");
        }

        if (string.IsNullOrWhiteSpace(external.ResultOperation) && string.IsNullOrWhiteSpace(external.CallbackSecret))
        {
            errors.Add("external", "Results must come somehow: give a result operation, a callback secret, or both.");
        }

        var result = external.Result ?? new ExternalResultFields();
        foreach (var (field, path) in new[] { ("reference", result.Reference), ("status", result.Status), ("outcome", result.Outcome) })
        {
            if (!JsonPaths.IsValid(path))
            {
                errors.Add($"external.result.{field}", "Not a valid path.");
            }
        }

        foreach (var (field, path) in new[] { ("data", result.Data), ("report", result.Report) })
        {
            if (path is { Length: > 0 } && !JsonPaths.IsValid(path))
            {
                errors.Add($"external.result.{field}", "Not a valid path.");
            }
        }

        errors.Required("external.result.completedValue", result.CompletedValue);
        external.Result = result;
        foreach (var (name, rule) in external.Inputs)
        {
            try
            {
                evaluator.Evaluate(rule, new JsonObject { ["visit"] = new JsonObject(), ["data"] = new JsonObject(), ["party"] = new JsonObject() });
            }
            catch (JsonLogicException ex)
            {
                errors.Add($"external.inputs.{name}", $"Invalid rule: {ex.Message}");
            }
        }

        var codes = outcomes.Select(o => o.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var (value, code) in external.OutcomeMap)
        {
            if (!codes.Contains(code))
            {
                errors.Add($"external.outcomeMap.{value}", $"'{code}' is not one of the outcomes.");
            }
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex FieldCode();

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,99}$")]
    private static partial Regex SecretName();
}
