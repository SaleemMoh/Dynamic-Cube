using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Parties.Changes;

public sealed class RecordListPayload : ConfigPayload
{
    public PartyKind PartyKind { get; set; } = PartyKind.Establishment;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Icon { get; set; }

    public List<RecordColumn> Columns { get; set; } = [];

    public bool AllowManual { get; set; } = true;

    public RecordImport? Import { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>Record lists: their columns (unique codes, options for choices) and, when imported, the operation and where each column is.</summary>
internal sealed class RecordListApplier(PartiesDbContext db) : PartyApplier<RecordList, RecordListPayload>(db)
{
    public const int MostColumns = 30;

    public override string EntityType => PartyChangeTypes.RecordList;

    protected override IQueryable<RecordList> Query(PartiesDbContext context) => context.RecordLists;

    protected override IQueryable<RecordList> ListQuery(PartiesDbContext context, string? parent) =>
        Query(context).OrderBy(l => l.PartyKind).ThenBy(l => l.DisplayOrder).ThenBy(l => l.Code);

    protected override Task ApplyAsync(RecordList entity, RecordListPayload payload, ConfigRefs<PartiesDbContext> refs, ValidationErrors errors, RecordList? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        if (payload.Columns.Count == 0)
        {
            errors.Add("columns", "A record list needs at least one column.");
        }

        if (payload.Columns.Count > MostColumns)
        {
            errors.Add("columns", $"At most {MostColumns} columns.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (column, i) in payload.Columns.Select((c, i) => (c, i)))
        {
            var at = $"columns[{i}]";
            column.Code = column.Code?.Trim() ?? string.Empty;
            errors.Code($"{at}.code", column.Code, ValidationErrors.IdentifierCode());
            if (column.Code.Length > 0 && !codes.Add(column.Code))
            {
                errors.Add($"{at}.code", $"Column '{column.Code}' appears twice.");
            }

            errors.Required($"{at}.labelAr", column.LabelAr);
            errors.Required($"{at}.labelEn", column.LabelEn);
            if (column.Type == RecordColumnType.Select)
            {
                if (column.Options.Count == 0)
                {
                    errors.Add($"{at}.options", "A choice column needs options.");
                }

                foreach (var (option, j) in column.Options.Select((o, j) => (o, j)))
                {
                    errors.Required($"{at}.options[{j}].value", option.Value);
                    errors.Required($"{at}.options[{j}].labelAr", option.LabelAr);
                    errors.Required($"{at}.options[{j}].labelEn", option.LabelEn);
                }
            }
            else
            {
                column.Options = [];
            }
        }

        var import = payload.Import is { } given && !string.IsNullOrWhiteSpace(given.OperationCode) ? given : null;
        if (import is not null)
        {
            import.OperationCode = import.OperationCode.Trim();
            errors.Code("import.operationCode", import.OperationCode, ValidationErrors.IdentifierCode());
            Path(errors, "import.itemsPath", import.ItemsPath, required: true);
            import.ItemsPath = import.ItemsPath?.Trim() ?? string.Empty;
            if (!codes.Contains(import.KeyColumn ?? string.Empty))
            {
                errors.Add("import.keyColumn", "Choose the column that identifies an imported record.");
            }

            if (import.Inputs.Count == 0)
            {
                errors.Add("import.inputs", "Say which identifier of the party is passed to the operation.");
            }

            var allowed = payload.PartyKind == PartyKind.Individual ? IdentifierTypes.Individual : IdentifierTypes.Establishment;
            foreach (var (input, i) in import.Inputs.Select((b, i) => (b, i)))
            {
                errors.Required($"import.inputs[{i}].parameter", input.Parameter);
                if (!allowed.Contains(input.IdentifierType))
                {
                    errors.Add($"import.inputs[{i}].identifierType", $"Use one of {string.Join(", ", allowed)}.");
                }
            }

            if (import.Fields.Count == 0)
            {
                errors.Add("import.fields", "Say where each column is in an imported item.");
            }

            foreach (var (field, i) in import.Fields.Select((f, i) => (f, i)))
            {
                if (!codes.Contains(field.Column ?? string.Empty))
                {
                    errors.Add($"import.fields[{i}].column", "Not a column of the list.");
                }

                Path(errors, $"import.fields[{i}].path", field.Path, required: true);
            }
        }
        else if (!payload.AllowManual)
        {
            errors.Add("allowManual", "A list that is not imported must let the applicant add records.");
        }

        entity.PartyKind = payload.PartyKind;
        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.Icon = Blank(payload.Icon);
        entity.Columns = payload.Columns;
        entity.AllowManual = payload.AllowManual;
        entity.Import = import;
        entity.DisplayOrder = payload.DisplayOrder;
        return Task.CompletedTask;
    }

    protected override Task<RecordListPayload> ToPayloadAsync(PartiesDbContext context, RecordList entity, CancellationToken cancellationToken) =>
        Task.FromResult(new RecordListPayload
        {
            PartyKind = entity.PartyKind,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Icon = entity.Icon,
            Columns = entity.Columns,
            AllowManual = entity.AllowManual,
            Import = entity.Import,
            DisplayOrder = entity.DisplayOrder,
        });
}
