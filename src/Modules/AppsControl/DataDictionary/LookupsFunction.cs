using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.DataDictionary;

/// <summary>
/// Several classes and lists of values in one call (ADR-0042): each class's values in effect today that serve the call's
/// company and that the caller's data grants allow, and the values of each list marked public (the lists the portal serves
/// without sign-in) for that company, as the caller's data grants allow them. Codes come separated by commas (or as
/// repeated parameters); an unknown class or a list that is not public is refused.
/// </summary>
internal sealed class LookupsFunction(AppsControlDbContext context, ApiClassValues classValues, ApiAccess access, LovService lovs) : IApiFunction
{
    public const string Code = "LOOKUPS";

    /// <summary>Most classes, and most lists, one call may ask for.</summary>
    public const int MaxCodes = 20;

    /// <summary>Most values returned for one list.</summary>
    public const int MaxListValues = 2000;

    public ApiFunctionInfo Info { get; } = new(
        Code,
        "قيم تصنيفات وقوائم",
        "Classes and lists",
        "قيم عدة تصنيفات (الفعالة اليوم في شركة الاستدعاء وبحسب صلاحيات بيانات العميل) وقيم عدة قوائم عامة في استدعاء واحد.",
        "The values of several classes (in effect today, in the company of the call, as the client's data permissions allow) and of several public lists of values, in one call.",
        [
            new("classes", false, "رموز التصنيفات مفصولة بفواصل (مثل REGION,SVC_CATEGORY)", "Class codes separated by commas (e.g. REGION,SVC_CATEGORY)"),
            new("lovs", false, "رموز القوائم العامة مفصولة بفواصل (مثل REGIONS)", "Public list codes separated by commas (e.g. REGIONS)"),
        ],
        new JsonObject
        {
            ["classes"] = new JsonObject
            {
                ["REGION"] = new JsonArray(new JsonObject { ["code"] = "RIYADH", ["nameAr"] = "منطقة الرياض", ["nameEn"] = "Riyadh", ["parentCode"] = null, ["detailsAr"] = null, ["detailsEn"] = null }),
            },
            ["lovs"] = new JsonObject
            {
                ["REGIONS"] = new JsonArray(new JsonObject { ["value"] = "RIYADH", ["labelAr"] = "منطقة الرياض", ["labelEn"] = "Riyadh", ["parent"] = null }),
            },
        });

    public async Task<ApiFunctionResult> InvokeAsync(IReadOnlyDictionary<string, string?> inputs, ApiCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(caller);
        var classCodes = Codes(inputs.GetValueOrDefault("classes"));
        var lovCodes = Codes(inputs.GetValueOrDefault("lovs"));
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (classCodes.Count == 0 && lovCodes.Count == 0)
        {
            throw new ApiInputException(new Dictionary<string, string[]> { ["classes"] = ["Give at least one class or list."] });
        }

        if (classCodes.Count > MaxCodes || lovCodes.Count > MaxCodes)
        {
            throw new ApiInputException(new Dictionary<string, string[]> { [classCodes.Count > MaxCodes ? "classes" : "lovs"] = [$"Ask for at most {MaxCodes} at a time."] });
        }

        var knownClasses = classCodes.Count == 0
            ? []
            : await context.Classes.AsNoTracking().Where(c => classCodes.Contains(c.Code)).Select(c => c.Code).ToListAsync(cancellationToken);
        var unknownClasses = classCodes.Except(knownClasses, StringComparer.Ordinal).ToList();
        if (unknownClasses.Count > 0)
        {
            errors["classes"] = [.. unknownClasses.Select(c => $"There is no class {c}.")];
        }

        var publicLists = lovCodes.Count == 0
            ? []
            : await context.Lovs.AsNoTracking().Where(l => lovCodes.Contains(l.Code) && l.IsPublic && l.IsActive).Select(l => l.Code).ToListAsync(cancellationToken);
        var refusedLists = lovCodes.Except(publicLists, StringComparer.Ordinal).ToList();
        if (refusedLists.Count > 0)
        {
            errors["lovs"] = [.. refusedLists.Select(l => $"There is no public list {l}.")];
        }

        if (errors.Count > 0)
        {
            throw new ApiInputException(errors);
        }

        var classes = new JsonObject();
        foreach (var code in classCodes)
        {
            var values = await classValues.ValuesAsync(code, caller, cancellationToken);
            classes[code] = new JsonArray([.. values.Select(v => (JsonNode)new JsonObject
            {
                ["code"] = v.Code,
                ["nameAr"] = v.NameAr,
                ["nameEn"] = v.NameEn,
                ["parentCode"] = v.ParentCode,
                ["detailsAr"] = v.DetailsAr,
                ["detailsEn"] = v.DetailsEn,
            })]);
        }

        var lists = new JsonObject();
        foreach (var code in lovCodes)
        {
            lists[code] = new JsonArray([.. (await ListValuesAsync(code, caller, cancellationToken)).Select(i => (JsonNode)new JsonObject
            {
                ["value"] = i.Value,
                ["labelAr"] = i.LabelAr,
                ["labelEn"] = i.LabelEn,
                ["parent"] = i.Parent,
            })]);
        }

        var result = new JsonObject();
        if (classCodes.Count > 0)
        {
            result["classes"] = classes;
        }

        if (lovCodes.Count > 0)
        {
            result["lovs"] = lists;
        }

        return new ApiFunctionResult(result);
    }

    /// <summary>
    /// A public list's values as the portal serves them, in the call's company, with the caller's data grants (the designer's
    /// try as administrator: as the portal serves them to anyone, in every company).
    /// </summary>
    private async Task<List<LovItem>> ListValuesAsync(string code, ApiCaller caller, CancellationToken cancellationToken)
    {
        var items = new List<LovItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = await access.SnapshotAsync(caller, cancellationToken) ?? AccessSnapshot.Anonymous("API", caller.Company);
        for (var page = 1; items.Count < MaxListValues; page++)
        {
            var values = await lovs.GetValuesAsync(code, snapshot, new LovQuery(Page: page), publicOnly: true, cancellationToken);
            items.AddRange(values.Items.Where(i => seen.Add(i.Value)));
            if (!values.HasMore || values.Items.Count == 0)
            {
                break;
            }
        }

        return items.Count > MaxListValues ? items[..MaxListValues] : items;
    }

    /// <summary>Codes separated by commas, trimmed, each once, in the order given.</summary>
    internal static List<string> Codes(string? text) =>
        [.. (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];
}
