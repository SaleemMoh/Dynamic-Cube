using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Lookups;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Changes;

/// <summary>The Services module's classes in Class/Domain.</summary>
public static class ServiceClasses
{
    /// <summary>Catalog categories (edited as <c>svc.category</c>): icon and descriptions as attributes.</summary>
    public const string Category = "SVC_CATEGORY";

    internal static IReadOnlyList<CatalogClass> All { get; } =
    [
        new(Category, "تصنيفات الخدمات", "Service categories",
        [
            new("ICON", "الأيقونة", "Icon", LookupAttributeTypes.Text),
            new("DESCRIPTION_AR", "الوصف بالعربية", "Arabic description", LookupAttributeTypes.Text),
            new("DESCRIPTION_EN", "الوصف بالإنجليزية", "English description", LookupAttributeTypes.Text),
        ],
        [],
        "تجمع الخدمات في دليل البوابة (تبويبات الصفحة الرئيسية وتصفية البحث).",
        "Group services in the portal catalog (home page tabs and the search filter)."),
        new("SVC_QUESTION_TYPE", "أنواع الإجابات", "Answer types", [],
        [
            new("TEXT", "نص", "Text"),
            new("TEXTAREA", "نص طويل", "Long text"),
            new("NUMBER", "رقم", "Number"),
            new("DATE", "تاريخ", "Date"),
            new("SELECT", "اختيار", "Choice"),
            new("CHECKBOX", "نعم/لا", "Yes/no"),
            new("FILE", "رفع ملفات", "File upload"),
        ]),
    ];
}

/// <summary>A category cannot be deleted while services are in it.</summary>
internal sealed class ServiceCategoryRules(ServicesDbContext context) : IDomainRules
{
    public string ClassCode => ServiceClasses.Category;

    public Task ValidateAsync(string code, System.Text.Json.Nodes.JsonObject attributes, ValidationErrors errors, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<string?> UsedByAsync(string code, CancellationToken cancellationToken)
    {
        var services = await context.Services.AsNoTracking().Where(s => s.CategoryCode == code).Select(s => s.Code).Take(5).ToListAsync(cancellationToken);
        return services.Count == 0 ? null : "services " + string.Join(", ", services);
    }
}
