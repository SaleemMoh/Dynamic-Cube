using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Services;

public sealed record RecordView(Guid Id, JsonObject Data, RecordSource Source, string? Key, DateTimeOffset? ImportedAt, DateTimeOffset UpdatedAt);

public sealed record RecordListView(
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    string? Icon,
    IReadOnlyList<RecordColumn> Columns,
    bool AllowManual,
    bool CanImport,
    IReadOnlyList<RecordView> Records);

public sealed record RecordImportResult(IntegrationOutcome Outcome, string? MessageAr, string? MessageEn, int Added, int Updated, int Removed);

/// <summary>A record's values were not valid for its list (field: column code).</summary>
public sealed class RecordValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("The record is not valid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>
/// The records a party keeps in its record lists: typed by hand (when the list allows it) or imported from the list's
/// operation, where a later import updates the records by their key and removes the ones no longer returned.
/// </summary>
internal sealed class RecordService(PartiesDbContext context, IIntegrationGateway gateway, TimeProvider clock)
{
    /// <summary>The most records a party keeps in one list.</summary>
    public const int MostRecords = 500;

    public async Task<IReadOnlyList<RecordListView>> ListsAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var party = await context.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new PartyNotFoundException($"Party {partyId} does not exist.");
        var lists = await context.RecordLists.AsNoTracking().Where(l => l.IsActive && l.PartyKind == party.Kind)
            .OrderBy(l => l.DisplayOrder).ThenBy(l => l.Code).ToListAsync(cancellationToken);
        var records = await context.Records.AsNoTracking().Where(r => r.PartyId == partyId).OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);
        return [.. lists.Select(l => View(l, records.Where(r => r.ListCode == l.Code)))];
    }

    public async Task<RecordListView> ListAsync(Guid partyId, string listCode, CancellationToken cancellationToken)
    {
        var (_, list) = await LoadAsync(partyId, listCode, cancellationToken);
        var records = await context.Records.AsNoTracking().Where(r => r.PartyId == partyId && r.ListCode == list.Code).OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);
        return View(list, records);
    }

    public async Task<RecordView> AddAsync(Guid partyId, string listCode, JsonObject? data, CancellationToken cancellationToken)
    {
        var (_, list) = await LoadAsync(partyId, listCode, cancellationToken);
        if (!list.AllowManual)
        {
            throw new PartyConflictException($"Records of '{list.Code}' are imported, not added by hand.");
        }

        if (await context.Records.CountAsync(r => r.PartyId == partyId && r.ListCode == list.Code, cancellationToken) >= MostRecords)
        {
            throw new PartyConflictException($"A list keeps at most {MostRecords} records.");
        }

        var record = new PartyRecord { PartyId = partyId, ListCode = list.Code, Data = Check(list, data).ToJsonString(), Source = RecordSource.Manual };
        context.Records.Add(record);
        await context.SaveChangesAsync(cancellationToken);
        return View(record);
    }

    public async Task<RecordView> UpdateAsync(Guid partyId, Guid recordId, JsonObject? data, CancellationToken cancellationToken)
    {
        var record = await ManualAsync(partyId, recordId, cancellationToken);
        var (_, list) = await LoadAsync(partyId, record.ListCode, cancellationToken);
        record.Data = Check(list, data).ToJsonString();
        await context.SaveChangesAsync(cancellationToken);
        return View(record);
    }

    public async Task DeleteAsync(Guid partyId, Guid recordId, CancellationToken cancellationToken)
    {
        context.Records.Remove(await ManualAsync(partyId, recordId, cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Calls the list's operation with the party's identifiers and keeps its items as the party's imported records: a record
    /// whose key comes again is updated, a new key is added, an imported record no longer returned is removed (records typed
    /// by hand are kept).
    /// </summary>
    public async Task<RecordImportResult> ImportAsync(Guid partyId, string listCode, CancellationToken cancellationToken)
    {
        var (party, list) = await LoadAsync(partyId, listCode, cancellationToken);
        var import = list.Import ?? throw new PartyConflictException($"'{list.Code}' is not imported from another system.");
        var inputs = new Dictionary<string, string?>();
        foreach (var binding in import.Inputs)
        {
            inputs[binding.Parameter] = party.Identifiers.FirstOrDefault(i => i.Type == binding.IdentifierType)?.Value;
        }

        if (inputs.Values.Any(v => v is null))
        {
            return new RecordImportResult(IntegrationOutcome.BusinessError, "لا يتوفر المعرّف المطلوب لهذا الطرف.", "The party has no identifier of the required type.", 0, 0, 0);
        }

        var result = await gateway.InvokeAsync(import.OperationCode, inputs, cancellationToken);
        if (result.Outcome is not (IntegrationOutcome.Success or IntegrationOutcome.NotFound))
        {
            return new RecordImportResult(result.Outcome, result.MessageAr, result.MessageEn, 0, 0, 0);
        }

        var items = result.Outcome == IntegrationOutcome.NotFound ? [] : (JsonPaths.Read(result.Data, import.ItemsPath) as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var existing = await context.Records.Where(r => r.PartyId == partyId && r.ListCode == list.Code && r.Source == RecordSource.Integration).ToListAsync(cancellationToken);
        var byKey = existing.Where(r => r.Key is not null).GroupBy(r => r.Key!).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var now = clock.GetUtcNow();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int added = 0, updated = 0;
        foreach (var item in items.Take(MostRecords))
        {
            var data = new JsonObject();
            foreach (var field in import.Fields)
            {
                var value = JsonPaths.Read(item, field.Path);
                if (value is not null)
                {
                    data[field.Column] = value.DeepClone();
                }
            }

            var clean = Tidy(list, data);
            var key = JsonPaths.Text(clean[import.KeyColumn]);
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
            {
                continue;
            }

            if (byKey.TryGetValue(key, out var record))
            {
                updated++;
            }
            else
            {
                record = new PartyRecord { PartyId = partyId, ListCode = list.Code, Key = key, Source = RecordSource.Integration };
                context.Records.Add(record);
                added++;
            }

            record.Data = clean.ToJsonString();
            record.ImportedAt = now;
        }

        var gone = existing.Where(r => r.Key is null || !seen.Contains(r.Key)).ToList();
        context.Records.RemoveRange(gone);
        await context.SaveChangesAsync(cancellationToken);
        return new RecordImportResult(result.Outcome, result.MessageAr, result.MessageEn, added, updated, gone.Count);
    }

    private async Task<(Party Party, RecordList List)> LoadAsync(Guid partyId, string listCode, CancellationToken cancellationToken)
    {
        var party = await context.Parties.AsNoTracking().Include(p => p.Identifiers).SingleOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new PartyNotFoundException($"Party {partyId} does not exist.");
        var list = await context.RecordLists.AsNoTracking().SingleOrDefaultAsync(l => l.Code == listCode && l.IsActive && l.PartyKind == party.Kind, cancellationToken)
            ?? throw new PartyNotFoundException($"Record list '{listCode}' does not exist for {party.Kind} parties.");
        return (party, list);
    }

    private async Task<PartyRecord> ManualAsync(Guid partyId, Guid recordId, CancellationToken cancellationToken)
    {
        var record = await context.Records.SingleOrDefaultAsync(r => r.Id == recordId && r.PartyId == partyId, cancellationToken)
            ?? throw new PartyNotFoundException($"Record {recordId} does not exist.");
        return record.Source == RecordSource.Manual ? record : throw new PartyConflictException("Imported records change only when they are imported again.");
    }

    /// <summary>The values of a record typed by hand, checked against the list's columns (unknown columns are dropped).</summary>
    private static JsonObject Check(RecordList list, JsonObject? data)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var clean = new JsonObject();
        foreach (var column in list.Columns)
        {
            var value = data?[column.Code];
            if (Empty(value))
            {
                if (column.Required)
                {
                    errors[column.Code] = ["Required."];
                }

                continue;
            }

            var (kept, problem) = Value(column, value!);
            if (problem is not null)
            {
                errors[column.Code] = [problem];
            }
            else
            {
                clean[column.Code] = kept;
            }
        }

        return errors.Count > 0 ? throw new RecordValidationException(errors) : clean;
    }

    /// <summary>The values of an imported record: those of the right type are kept, the others left out.</summary>
    private static JsonObject Tidy(RecordList list, JsonObject data)
    {
        var clean = new JsonObject();
        foreach (var column in list.Columns)
        {
            if (!Empty(data[column.Code]) && Value(column, data[column.Code]!) is ({ } kept, null))
            {
                clean[column.Code] = kept;
            }
        }

        return clean;
    }

    private static (JsonNode? Value, string? Problem) Value(RecordColumn column, JsonNode value)
    {
        var text = JsonPaths.Text(value);
        switch (column.Type)
        {
            case RecordColumnType.Text:
                return text is { Length: <= 2000 } ? (JsonValue.Create(text), null) : (null, "Text of at most 2000 characters.");
            case RecordColumnType.Number:
                return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? (JsonValue.Create(number), null) : (null, "A number.");
            case RecordColumnType.Date:
                return text is not null && DateOnly.TryParseExact(text[..Math.Min(10, text.Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? (JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), null)
                    : (null, "A date as yyyy-MM-dd.");
            case RecordColumnType.Checkbox:
                return value.GetValueKind() is JsonValueKind.True or JsonValueKind.False
                    ? (value.DeepClone(), null)
                    : bool.TryParse(text, out var flag) ? (JsonValue.Create(flag), null) : (null, "Yes or no.");
            case RecordColumnType.Select:
                return column.Options.Any(o => o.Value == text) ? (JsonValue.Create(text), null) : (null, "One of the column's options.");
            default:
                return (null, "Not a valid value.");
        }
    }

    private static bool Empty(JsonNode? value) => value is null || (value is JsonValue v && v.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s));

    private static RecordListView View(RecordList list, IEnumerable<PartyRecord> records) =>
        new(list.Code, list.NameAr, list.NameEn, list.DescriptionAr, list.DescriptionEn, list.Icon, list.Columns, list.AllowManual, list.Import is not null, [.. records.Select(View)]);

    private static RecordView View(PartyRecord record) =>
        new(record.Id, JsonNode.Parse(record.Data)!.AsObject(), record.Source, record.Key, record.ImportedAt, record.UpdatedAt ?? record.CreatedAt);
}
