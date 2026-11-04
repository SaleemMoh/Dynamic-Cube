using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.DataDictionary;

/// <summary>
/// The values of a class (ADR-0042): active and in effect today, serving the call's company and allowed by the caller's data
/// grants (<see cref="ApiClassValues"/>), in display order —
/// get all or get some: optionally only the children of a parent value, and only the values whose codes are given
/// (separated by commas). An unknown class is not found.
/// </summary>
internal sealed class ClassValuesFunction(AppsControlDbContext context, ApiClassValues classValues) : IApiFunction
{
    public const string Code = "CLASS_VALUES";

    public ApiFunctionInfo Info { get; } = new(
        Code,
        "قيم تصنيف",
        "Class values",
        "قيم التصنيف الفعالة اليوم في شركة الاستدعاء والتي تسمح بها صلاحيات بيانات العميل، بترتيب العرض، ويمكن قصرها على أبناء قيمة.",
        "The class's values in effect today that serve the company of the call and that the client's data permissions allow, in display order; optionally only the children of a value.",
        [
            new("class", true, "رمز التصنيف (مثل REGIONS)", "The class code (e.g. REGIONS)"),
            new("parent", false, "رمز القيمة الأب لعرض أبنائها فقط", "A parent value's code, to list only its children"),
            new("codes", false, "رموز القيم المطلوبة مفصولة بفواصل؛ فارغ: كل القيم", "The codes of the values wanted, separated by commas; empty: all values"),
        ],
        new JsonArray(
            new JsonObject { ["code"] = "RIYADH", ["nameAr"] = "الرياض", ["nameEn"] = "Riyadh", ["parentCode"] = null, ["detailsAr"] = null, ["detailsEn"] = null },
            new JsonObject { ["code"] = "MAKKAH", ["nameAr"] = "مكة المكرمة", ["nameEn"] = "Makkah", ["parentCode"] = null, ["detailsAr"] = null, ["detailsEn"] = null }));

    public async Task<ApiFunctionResult> InvokeAsync(IReadOnlyDictionary<string, string?> inputs, ApiCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(caller);
        var classCode = inputs.GetValueOrDefault("class")?.Trim();
        if (string.IsNullOrEmpty(classCode))
        {
            throw new ApiInputException(new Dictionary<string, string[]> { ["class"] = ["The class is required."] });
        }

        if (!await context.Classes.AsNoTracking().AnyAsync(c => c.Code == classCode, cancellationToken))
        {
            return ApiFunctionResult.NotFound;
        }

        var parent = inputs.GetValueOrDefault("parent")?.Trim();
        var codes = LookupsFunction.Codes(inputs.GetValueOrDefault("codes"));
        var values = (await classValues.ValuesAsync(classCode, caller, cancellationToken))
            .Where(v => string.IsNullOrEmpty(parent) || string.Equals(v.ParentCode, parent, StringComparison.Ordinal))
            .Where(v => codes.Count == 0 || codes.Contains(v.Code));
        return new ApiFunctionResult(new JsonArray([.. values.Select(v => (JsonNode)new JsonObject
        {
            ["code"] = v.Code,
            ["nameAr"] = v.NameAr,
            ["nameEn"] = v.NameEn,
            ["parentCode"] = v.ParentCode,
            ["detailsAr"] = v.DetailsAr,
            ["detailsEn"] = v.DetailsEn,
        })]));
    }
}
