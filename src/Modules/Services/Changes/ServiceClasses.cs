using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.DataDictionary;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Changes;

/// <summary>The Services module's classes in Class/Domain.</summary>
public static class ServiceClasses
{
    /// <summary>Catalog categories (values of class <c>SVC_CATEGORY</c>, edited in Classes &amp; domains): icon and descriptions as attributes.</summary>
    public const string Category = "SVC_CATEGORY";

    internal static IReadOnlyList<CatalogClass> All { get; } =
    [
        new(Category, "تصنيفات الخدمات", "Service categories",
        [
            new("ICON", "الأيقونة", "Icon", DictionaryAttributeTypes.Text),
            new("DESCRIPTION_AR", "الوصف بالعربية", "Arabic description", DictionaryAttributeTypes.Text),
            new("DESCRIPTION_EN", "الوصف بالإنجليزية", "English description", DictionaryAttributeTypes.Text),
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
            new("ROWS", "صفوف متكررة", "Repeating rows"),
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
