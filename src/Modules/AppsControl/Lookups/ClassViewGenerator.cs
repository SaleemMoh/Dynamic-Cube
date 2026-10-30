using System.Text;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Lookups;

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

    internal static string BuildSql(LookupClass cls)
    {
        var sql = new StringBuilder();
        sql.Append("CREATE VIEW apps.").Append(Quote(ViewName(cls.Code))).Append(" AS SELECT d.id, d.code, c.code AS class_code, d.name_ar, d.name_en, ")
            .Append("d.details_ar, d.details_en, p.code AS parent_code, d.display_order, d.is_default, d.is_active, d.effective_from, d.effective_to");
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

        sql.Append(" FROM apps.domain d JOIN apps.class c ON c.id = d.class_id LEFT JOIN apps.domain p ON p.id = d.parent_domain_id WHERE c.code = ")
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
