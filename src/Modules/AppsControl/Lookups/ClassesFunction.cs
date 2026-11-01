using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Lookups;

/// <summary>
/// Classes and their values (ADR-0042) — get all or get some: with no class codes, every active class; with codes
/// (separated by commas, or the parameter repeated), only those, in the order asked. Each class with its names and, unless
/// <c>values</c> is <c>false</c>, its values in effect today that serve the caller's companies. An unknown class is refused.
/// </summary>
internal sealed class ClassesFunction(AppsControlDbContext context, ILookupDirectory lookups) : IApiFunction
{
    public const string Code = "CLASSES";

    public ApiFunctionInfo Info { get; } = new(
        Code,
        "التصنيفات",
        "Classes",
        "التصنيفات وقيمها: كلها إذا لم تُحدد رموز، أو المحددة فقط. القيم الفعالة اليوم والتي تخدم جهات العميل.",
        "Classes and their values: all of them when no codes are given, or only those given. Values in effect today that serve the client's companies.",
        [
            new("classes", false, "رموز التصنيفات مفصولة بفواصل؛ فارغ: كل التصنيفات", "Class codes separated by commas; empty: all classes"),
            new("values", false, "false لإرجاع التصنيفات دون قيمها", "false to return the classes without their values"),
        ],
        new JsonArray(new JsonObject
        {
            ["code"] = "REGION",
            ["nameAr"] = "المناطق الإدارية",
            ["nameEn"] = "Administrative regions",
            ["values"] = new JsonArray(new JsonObject { ["code"] = "RIYADH", ["nameAr"] = "منطقة الرياض", ["nameEn"] = "Riyadh", ["parentCode"] = null, ["detailsAr"] = null, ["detailsEn"] = null }),
        }));

    public async Task<ApiFunctionResult> InvokeAsync(IReadOnlyDictionary<string, string?> inputs, ApiCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(caller);
        var asked = LookupsFunction.Codes(inputs.GetValueOrDefault("classes"));
        var withValues = !string.Equals(inputs.GetValueOrDefault("values")?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
        var query = context.Classes.AsNoTracking().Where(c => c.IsActive);
        if (asked.Count > 0)
        {
            query = query.Where(c => asked.Contains(c.Code));
        }

        var classes = await query.OrderBy(c => c.Code).Select(c => new { c.Code, c.NameAr, c.NameEn }).ToListAsync(cancellationToken);
        var unknown = asked.Except(classes.Select(c => c.Code), StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new ApiInputException(new Dictionary<string, string[]> { ["classes"] = [.. unknown.Select(c => $"There is no class {c}.")] });
        }

        // Asked for some: in the order asked; all: by code.
        var ordered = asked.Count > 0 ? asked.Select(code => classes.First(c => c.Code == code)).ToList() : classes;
        var result = new JsonArray();
        foreach (var c in ordered)
        {
            var item = new JsonObject { ["code"] = c.Code, ["nameAr"] = c.NameAr, ["nameEn"] = c.NameEn };
            if (withValues)
            {
                var values = (await lookups.ValuesAsync(c.Code, cancellationToken)).Where(v => caller.Companies is null || caller.Companies.Any(v.Serves));
                item["values"] = new JsonArray([.. values.Select(v => (JsonNode)new JsonObject
                {
                    ["code"] = v.Code,
                    ["nameAr"] = v.NameAr,
                    ["nameEn"] = v.NameEn,
                    ["parentCode"] = v.ParentCode,
                    ["detailsAr"] = v.DetailsAr,
                    ["detailsEn"] = v.DetailsEn,
                })]);
            }

            result.Add(item);
        }

        return new ApiFunctionResult(result);
    }
}
