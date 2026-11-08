using System.Text;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.DataDictionary;

/// <summary>
/// Maintains <c>apps.v_class_&lt;code&gt;</c>: one typed view per class with the fixed domain columns plus every reportable
/// attribute as a column of its declared type, so reports and data objects see ordinary columns instead of JSON.
/// </summary>
internal sealed class ClassViewGenerator : IChangeSetPublishHook<AppsControlDbContext>
{
    public static string ViewName(string classCode) => "v_class_" + SqlName(classCode);

    /// <summary>Column or view-name part for a code: lower case, <c>-</c> as <c>_</c>, so reports need no quoting.</summary>
    public static string SqlName(string code) => code.ToLowerInvariant().Replace('-', '_');

    public async Task OnPublishedAsync(AppsControlDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        var classItems = changeSet.Items.Where(i => i.EntityType == AppsControlChangeTypes.Class).ToList();
        foreach (var item in classItems.Where(i => i.Operation == ChangeOperation.Delete))
        {
            await DropAsync(context, item.EntityCode, cancellationToken);
        }

        await RegenerateAsync(context, classItems.Where(i => i.Operation != ChangeOperation.Delete).Select(i => i.EntityCode).ToList(), cancellationToken);
    }

    /// <summary>Recreates the views of the given classes (all classes when null).</summary>
    public static async Task RegenerateAsync(AppsControlDbContext context, IReadOnlyCollection<string>? classCodes, CancellationToken cancellationToken)
    {
        var query = context.Classes.AsNoTracking().Include(c => c.Attributes).AsQueryable();
        if (classCodes is not null)
        {
            query = query.Where(c => classCodes.Contains(c.Code));
        }

        foreach (var cls in await query.ToListAsync(cancellationToken))
        {
            await DropAsync(context, cls.Code, cancellationToken);
#pragma warning disable EF1002 // Identifiers are validated class / attribute codes; the class code is also passed as a literal.
            await context.Database.ExecuteSqlRawAsync(BuildSql(cls), cancellationToken);
#pragma warning restore EF1002
        }
    }

    internal static string BuildSql(DictionaryClass cls)
    {
        var sql = new StringBuilder();
        // The view keeps its own column names (code, name_ar…), which lists of values and reports use, over the
        // legacy-named table columns (value, domain_desc_ntv…; see AppsControlNaming).
        sql.Append("CREATE VIEW apps.").Append(Quote(ViewName(cls.Code)))
            .Append(" AS SELECT d.id, d.value AS code, c.class_code AS class_code, d.domain_desc_ntv AS name_ar, d.domain_desc_frn AS name_en, ")
            .Append("d.domain_desc_detail_ntv AS details_ar, d.domain_desc_detail_frn AS details_en, p.value AS parent_code, d.orderby AS display_order, ")
            .Append("d.is_default, d.active_flag AS is_active, d.effective_from, d.effective_to, d.cmp_nums AS companies");
        foreach (var attribute in cls.Attributes.Where(a => a.IsReportable).OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code))
        {
            var key = Literal(attribute.Code);
            var expression = attribute.IsMultiple ? $"d.attributes->{key}" : attribute.DataType switch
            {
                AttributeDataType.Number => $"(d.attributes->>{key})::numeric",
                AttributeDataType.Date => $"(d.attributes->>{key})::date",
                AttributeDataType.Bool => $"(d.attributes->>{key})::boolean",
                AttributeDataType.Json => $"d.attributes->{key}",
                _ => $"d.attributes->>{key}",
            };
            sql.Append(", ").Append(expression).Append(" AS ").Append(Quote(SqlName(attribute.Code)));
        }

        sql.Append(" FROM apps.dd_domain d JOIN apps.dd_classes c ON c.id = d.class_id LEFT JOIN apps.dd_domain p ON p.id = d.parent_domain_id WHERE c.class_code = ")
            .Append(Literal(cls.Code));
        return sql.ToString();
    }

    private static Task<int> DropAsync(AppsControlDbContext context, string classCode, CancellationToken cancellationToken) =>
#pragma warning disable EF1002 // See above.
        context.Database.ExecuteSqlRawAsync($"DROP VIEW IF EXISTS apps.{Quote(ViewName(classCode))}", cancellationToken);
#pragma warning restore EF1002

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
