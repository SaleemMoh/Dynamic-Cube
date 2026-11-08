using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.DataDictionary;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.DataDictionary;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.AppsControl.Changes;

public sealed class AttributeDefinitionPayload
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public AttributeDataType DataType { get; set; }

    public string? RefClassCode { get; set; }

    public string? RefLovCode { get; set; }

    public bool IsMultiple { get; set; }

    public AttributeGroup AttributeGroup { get; set; }

    public bool IsRequired { get; set; }

    public JsonObject? Validation { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsReportable { get; set; } = true;
}

public sealed class ClassPayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? ParentClassCode { get; set; }

    public bool IsHierarchical { get; set; }

    public bool AllowMultiAssign { get; set; }

    /// <summary>Values may be limited to some companies.</summary>
    public bool ValuesPerCompany { get; set; }

    public List<AttributeDefinitionPayload> Attributes { get; set; } = [];

    /// <summary>Settings of the class itself (named values).</summary>
    public List<ClassSetting> Settings { get; set; } = [];
}

internal sealed class ClassApplier(AppsControlDbContext db) : ConfigApplier<DictionaryClass, ClassPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.Class;

    public override string Area => AppsControlAreas.DataDictionary;

    public override string ResourceCode => AppsControlResources.Classes;

    protected override System.Text.RegularExpressions.Regex CodePattern => ValidationErrors.ClassCode();

    protected override IQueryable<DictionaryClass> Query(AppsControlDbContext context) =>
        context.Classes.Include(c => c.ParentClass).Include(c => c.Attributes);

    protected override Guid? ParentOf(DictionaryClass entity) => entity.ParentClassId;

    protected override async Task ApplyAsync(DictionaryClass entity, ClassPayload payload, RefResolver refs, ValidationErrors errors, DictionaryClass? existing, CancellationToken cancellationToken)
    {
        var viewName = ClassViewGenerator.ViewName(entity.Code);
        var sameView = await refs.Context.Classes.AsNoTracking()
            .Where(c => c.Code != entity.Code && c.Code.Replace("-", "_") == entity.Code.Replace("-", "_"))
            .Select(c => c.Code)
            .FirstOrDefaultAsync(cancellationToken);
        if (sameView is not null)
        {
            errors.Add("code", $"Class '{sameView}' already uses the report view {viewName} ('-' is read as '_').");
        }

        entity.DescriptionAr = payload.DescriptionAr;
        entity.DescriptionEn = payload.DescriptionEn;
        entity.IsHierarchical = payload.IsHierarchical;
        entity.AllowMultiAssign = payload.AllowMultiAssign;
        if (existing is { ValuesPerCompany: true } && !payload.ValuesPerCompany)
        {
            var limited = await refs.Context.Domains.AsNoTracking()
                .Where(d => d.ClassId == existing.Id && d.CompanyCode != null)
                .OrderBy(d => d.Code).Select(d => d.Code).Take(5).ToListAsync(cancellationToken);
            if (limited.Count > 0)
            {
                errors.Add("valuesPerCompany", $"Values {string.Join(", ", limited)} belong to one company; delete them or make them values of every company first.");
            }
        }

        // Values of a class that starts having values per company serve all companies until limited.
        entity.ValuesPerCompany = payload.ValuesPerCompany;
        if (payload.ParentClassCode == entity.Code)
        {
            errors.Add("parentClassCode", "Use 'isHierarchical' for parents within the same class.");
        }
        else
        {
            entity.ParentClassId = await refs.ClassAsync(payload.ParentClassCode, "parentClassCode", errors, cancellationToken);
        }

        ChildSync.RequireUnique(payload.Attributes, a => a.Code, "attributes", errors);
        var definitions = new List<AttributeDefinition>();
        foreach (var (a, i) in payload.Attributes.Select((a, i) => (a, i)))
        {
            var field = $"attributes[{i}]";
            errors.Code(field + ".code", a.Code, ValidationErrors.IdentifierCode());
            var column = ClassViewGenerator.SqlName(a.Code ?? string.Empty);
            if (AttributeValues.ReservedCodes.Contains(column.ToUpperInvariant()))
            {
                errors.Add(field + ".code", $"'{a.Code}' is reserved.");
            }
            else if (payload.Attributes.Take(i).Any(o => o.Code != a.Code && ClassViewGenerator.SqlName(o.Code ?? string.Empty) == column))
            {
                errors.Add(field + ".code", $"'{a.Code}' gives the same report column as another attribute ('-' is read as '_').");
            }

            errors.Required(field + ".nameAr", a.NameAr);
            errors.Required(field + ".nameEn", a.NameEn);
            if (a.DataType == AttributeDataType.DomainRef)
            {
                errors.Required(field + ".refClassCode", a.RefClassCode);
                if (a.RefClassCode != entity.Code)
                {
                    await refs.ClassAsync(a.RefClassCode, field + ".refClassCode", errors, cancellationToken);
                }
            }
            else if (a.DataType == AttributeDataType.LovRef)
            {
                errors.Required(field + ".refLovCode", a.RefLovCode);
                await refs.LovAsync(a.RefLovCode, field + ".refLovCode", errors, cancellationToken);
            }

            if (a.IsMultiple && a.DataType is not (AttributeDataType.DomainRef or AttributeDataType.LovRef))
            {
                errors.Add(field + ".isMultiple", "Only fields taking values of a class or of a list of values can take several.");
            }

            definitions.Add(new AttributeDefinition
            {
                ClassId = entity.Id,
                Code = a.Code ?? string.Empty,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                DataType = a.DataType,
                RefClassCode = a.DataType == AttributeDataType.DomainRef ? a.RefClassCode : null,
                RefLovCode = a.DataType == AttributeDataType.LovRef ? a.RefLovCode : null,
                IsMultiple = a.IsMultiple && (a.DataType is AttributeDataType.DomainRef or AttributeDataType.LovRef),
                AttributeGroup = a.AttributeGroup,
                IsRequired = a.IsRequired,
                Validation = a.Validation is { Count: > 0 } ? a.Validation.ToJsonString() : null,
                DisplayOrder = a.DisplayOrder,
                IsReportable = a.IsReportable,
            });
        }

        ChildSync.RequireUnique(payload.Settings, c => c.Code, "settings", errors);
        foreach (var (setting, i) in payload.Settings.Select((c, i) => (c, i)))
        {
            errors.Code($"settings[{i}].code", setting.Code, ValidationErrors.IdentifierCode());
            errors.Required($"settings[{i}].nameAr", setting.NameAr);
            errors.Required($"settings[{i}].nameEn", setting.NameEn);
            errors.MaxLength($"settings[{i}].value", setting.Value, 4000);
        }

        entity.Settings = [.. payload.Settings.Select(c => new ClassSetting
        {
            Code = c.Code?.Trim() ?? string.Empty,
            NameAr = c.NameAr?.Trim() ?? string.Empty,
            NameEn = c.NameEn?.Trim() ?? string.Empty,
            Value = string.IsNullOrWhiteSpace(c.Value) ? null : c.Value,
        })];

        if (errors.HasErrors)
        {
            return;
        }

        if (existing is not null)
        {
            await CheckExistingValuesAsync(refs, existing, definitions, errors, cancellationToken);
        }

        if (errors.HasErrors || refs.IsValidating)
        {
            return;
        }

        var removed = entity.Attributes.Select(a => a.Code).Except(definitions.Select(d => d.Code)).ToList();
        foreach (var code in removed)
        {
            await refs.Context.Database.ExecuteSqlAsync(
                $"UPDATE apps.dd_domain SET attributes = attributes - {code} WHERE class_id = {entity.Id}",
                cancellationToken);
        }

        ChildSync.Sync(entity.Attributes, definitions, d => d.Code, (from, to) =>
        {
            to.NameAr = from.NameAr;
            to.NameEn = from.NameEn;
            to.DataType = from.DataType;
            to.RefClassCode = from.RefClassCode;
            to.RefLovCode = from.RefLovCode;
            to.IsMultiple = from.IsMultiple;
            to.AttributeGroup = from.AttributeGroup;
            to.IsRequired = from.IsRequired;
            to.Validation = from.Validation;
            to.DisplayOrder = from.DisplayOrder;
            to.IsReportable = from.IsReportable;
        });
    }

    protected override Task<ClassPayload> ToPayloadAsync(AppsControlDbContext context, DictionaryClass entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ClassPayload
        {
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            ParentClassCode = entity.ParentClass?.Code,
            IsHierarchical = entity.IsHierarchical,
            AllowMultiAssign = entity.AllowMultiAssign,
            ValuesPerCompany = entity.ValuesPerCompany,
            Attributes = entity.Attributes
                .OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code)
                .Select(a => new AttributeDefinitionPayload
                {
                    Code = a.Code,
                    NameAr = a.NameAr,
                    NameEn = a.NameEn,
                    DataType = a.DataType,
                    RefClassCode = a.RefClassCode,
                    RefLovCode = a.RefLovCode,
                    IsMultiple = a.IsMultiple,
                    AttributeGroup = a.AttributeGroup,
                    IsRequired = a.IsRequired,
                    Validation = JsonNodeExtensions.ParseOrNull(a.Validation) as JsonObject,
                    DisplayOrder = a.DisplayOrder,
                    IsReportable = a.IsReportable,
                })
                .ToList(),
            Settings = entity.Settings,
        });

    protected override async Task BeforeDeleteAsync(AppsControlDbContext context, DictionaryClass entity, CancellationToken cancellationToken)
    {
        if (await context.Domains.AnyAsync(d => d.ClassId == entity.Id, cancellationToken))
        {
            throw new DynamicCube.Platform.ChangeSets.ChangeSetException($"Class '{entity.Code}' still has values; delete them first.");
        }
    }

    /// <summary>Existing values must stay valid when definitions change (generated views cast them to the declared types).</summary>
    private static async Task CheckExistingValuesAsync(RefResolver refs, DictionaryClass existing, List<AttributeDefinition> definitions, ValidationErrors errors, CancellationToken cancellationToken)
    {
        var before = existing.Attributes.ToDictionary(a => a.Code, StringComparer.Ordinal);
        var changed = definitions
            .Where(d => !before.TryGetValue(d.Code, out var old)
                || old.DataType != d.DataType || old.RefClassCode != d.RefClassCode || old.RefLovCode != d.RefLovCode
                || old.IsMultiple != d.IsMultiple || old.IsRequired != d.IsRequired || old.Validation != d.Validation)
            .ToList();
        if (changed.Count == 0)
        {
            return;
        }

        var domains = await refs.Context.Domains.AsNoTracking()
            .Where(d => d.ClassId == existing.Id)
            .Select(d => new { d.Code, d.Attributes })
            .ToListAsync(cancellationToken);
        var problems = 0;
        foreach (var domain in domains)
        {
            var values = JsonNode.Parse(domain.Attributes) as JsonObject ?? [];
            var relevant = new JsonObject();
            foreach (var definition in changed)
            {
                if (values[definition.Code] is { } value)
                {
                    relevant[definition.Code] = value.DeepClone();
                }
            }

            var domainErrors = new ValidationErrors();
            await AttributeValues.ValidateAsync(changed, relevant, refs, domainErrors, string.Empty, cancellationToken);
            if (domainErrors.HasErrors && problems++ < 5)
            {
                errors.Add("attributes", $"Value '{domain.Code}' does not satisfy the new attribute definitions; update it first.");
            }
        }
    }
}

/// <summary>A lookup value. The entity code is <c>CLASS/CODE</c>.</summary>
public sealed class DomainPayload : ConfigPayload
{
    public string? ClassCode { get; set; }

    public string? ParentCode { get; set; }

    public string? DetailsAr { get; set; }

    public string? DetailsEn { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsDefault { get; set; }

    public JsonObject? Attributes { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    /// <summary>The company of a company value (<c>instid/cmp_num</c>), taken from the key; null for a value of every company.</summary>
    public string? CompanyCode { get; set; }
}

internal sealed class DomainApplier(AppsControlDbContext db, IEnumerable<IDomainRules> rules, IServiceProvider services) : ConfigApplier<DomainValue, DomainPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.Domain;

    public override string Area => AppsControlAreas.DataDictionary;

    public override string ResourceCode => AppsControlResources.Classes;

    protected override IQueryable<DomainValue> Query(AppsControlDbContext context) =>
        context.Domains.Include(d => d.Class).Include(d => d.ParentDomain);

    protected override IQueryable<DomainValue> ListQuery(AppsControlDbContext context, string? parent) =>
        Query(context).Where(d => d.Class!.Code == parent).OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code);

    protected override IQueryable<DomainValue> ExportQuery(AppsControlDbContext context) =>
        Query(context).OrderBy(d => d.Class!.Code).ThenBy(d => d.DisplayOrder).ThenBy(d => d.Code);

    protected override Guid? ParentOf(DomainValue entity) => entity.ParentDomainId;

    protected override Task<DomainValue?> FindAsync(AppsControlDbContext context, string entityCode, CancellationToken cancellationToken)
    {
        if (!DomainKey.TryParse(entityCode, out var classCode, out var companyKey, out var code))
        {
            return Task.FromResult<DomainValue?>(null);
        }

        if (companyKey is null)
        {
            return Query(context).SingleOrDefaultAsync(d => d.Class!.Code == classCode && d.CompanyCode == null && d.Code == code, cancellationToken);
        }

        _ = CompanyKey.TryParse(companyKey, out var instid, out var number);
        return Query(context).SingleOrDefaultAsync(d => d.Class!.Code == classCode && d.EntityCode == instid && d.CompanyCode == number && d.Code == code, cancellationToken);
    }

    protected override void ValidateKey(string entityCode, ValidationErrors errors)
    {
        if (!DomainKey.TryParse(entityCode, out var classCode, out var companyKey, out var code))
        {
            errors.Add("code", "Use CLASS/CODE, or CLASS/instid/cmp_num/CODE for a value of one company.");
            return;
        }

        errors.Code("classCode", classCode, ValidationErrors.ClassCode());
        if (companyKey is not null && CompanyKey.TryParse(companyKey, out var instid, out var number))
        {
            errors.Code("companyCode", instid, ValidationErrors.GeneralCode());
            errors.Code("companyCode", number, ValidationErrors.GeneralCode());
        }

        errors.Code("code", code, ValidationErrors.GeneralCode());
    }

    protected override string CodeFromKey(string entityCode) =>
        DomainKey.TryParse(entityCode, out _, out var code) ? code : entityCode;

    protected override string EntityCodeOf(DomainValue entity) => DomainKey.Format(entity.Class?.Code ?? string.Empty, entity.CompanyKey, entity.Code);

    protected override async Task ApplyAsync(DomainValue entity, DomainPayload payload, RefResolver refs, ValidationErrors errors, DomainValue? existing, CancellationToken cancellationToken)
    {
        var context = refs.Context;
        if (!DomainKey.TryParse(payload.Code!, out var classCode, out var companyKey, out _))
        {
            errors.Add("code", "Use CLASS/CODE, or CLASS/instid/cmp_num/CODE for a value of one company.");
            return;
        }

        var cls = await context.Classes.AsNoTracking().Include(c => c.ParentClass).Include(c => c.Attributes)
            .SingleOrDefaultAsync(c => c.Code == classCode, cancellationToken);
        if (cls is null)
        {
            if (!refs.IsPendingCreate(AppsControlChangeTypes.Class, classCode))
            {
                errors.Add("classCode", $"Class '{classCode}' does not exist.");
            }

            return;
        }

        entity.ClassId = cls.Id;
        entity.DetailsAr = payload.DetailsAr;
        entity.DetailsEn = payload.DetailsEn;
        entity.DisplayOrder = payload.DisplayOrder;
        entity.IsDefault = payload.IsDefault;
        entity.EffectiveFrom = payload.EffectiveFrom;
        entity.EffectiveTo = payload.EffectiveTo;
        if (payload.EffectiveTo < payload.EffectiveFrom)
        {
            errors.Add("effectiveTo", "Must not be before the start date.");
        }

        // A value of one company (key CLASS/instid/cmp_num/CODE) needs a class with values per company; its instance is the
        // company's.
        if (payload.CompanyCode is { Length: > 0 } given && given != companyKey)
        {
            errors.Add("companyCode", $"The value's company is the one in its key ({companyKey ?? "none"}).");
        }

        entity.CompanyCode = null;
        if (companyKey is not null && CompanyKey.TryParse(companyKey, out var instid, out var number))
        {
            if (!cls.ValuesPerCompany)
            {
                errors.Add("companyCode", $"Class {cls.Code} does not have values per company.");
            }

            if (!await context.Companies.AsNoTracking().AnyAsync(c => c.EntityCode == instid && c.Code == number, cancellationToken)
                && !refs.IsPendingCreate(AppsControlChangeTypes.Company, companyKey))
            {
                errors.Add("companyCode", $"Company '{companyKey}' does not exist.");
            }

            entity.EntityCode = instid;
            entity.CompanyCode = number;
        }

        // Parent value: from the parent class when the class has one, otherwise from the same (hierarchical) class.
        entity.ParentDomainId = null;
        if (!string.IsNullOrWhiteSpace(payload.ParentCode))
        {
            if (cls.ParentClass is not null)
            {
                entity.ParentDomainId = await refs.DomainAsync(cls.ParentClass.Code, payload.ParentCode, "parentCode", errors, cancellationToken);
            }
            else if (cls.IsHierarchical)
            {
                if (payload.ParentCode == entity.Code)
                {
                    errors.Add("parentCode", "A value cannot be its own parent.");
                }
                else
                {
                    entity.ParentDomainId = await refs.DomainAsync(cls.Code, payload.ParentCode, "parentCode", errors, cancellationToken);
                    if (existing is not null && entity.ParentDomainId is { } pid && await IsDescendantAsync(context, pid, existing.Id, cancellationToken))
                    {
                        errors.Add("parentCode", "A value cannot be placed under one of its own children.");
                    }
                }
            }
            else
            {
                errors.Add("parentCode", $"Class {cls.Code} has no parent class and is not hierarchical.");
            }
        }

        var attributes = payload.Attributes ?? [];
        await AttributeValues.ValidateAsync(cls.Attributes, attributes, refs, errors, "attributes.", cancellationToken, InListAsync);
        // The owning module's rules (and the defaults they fill in); their problems name attributes like the checks above.
        var ruleErrors = new ValidationErrors();
        foreach (var rule in rules.Where(r => r.ClassCode == cls.Code))
        {
            await rule.ValidateAsync(entity.Code, attributes, ruleErrors, cancellationToken);
        }

        ruleErrors.CopyTo(errors, field => "attributes." + field);

        entity.Attributes = attributes.ToJsonString();

        if (errors.HasErrors || refs.IsValidating)
        {
            return;
        }

        if (payload.IsDefault)
        {
            // Only one default value per class, or per company: a company's default replaces that company's; a value of every
            // company replaces every default.
            var (valueInstid, valueNumber) = (entity.EntityCode, entity.CompanyCode);
            await context.Domains.Where(d => d.ClassId == cls.Id && d.Id != entity.Id && d.IsDefault
                    && (valueNumber == null || d.CompanyCode == null || (d.EntityCode == valueInstid && d.CompanyCode == valueNumber)))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsDefault, false), cancellationToken);
        }
    }

    protected override Task<DomainPayload> ToPayloadAsync(AppsControlDbContext context, DomainValue entity, CancellationToken cancellationToken) =>
        Task.FromResult(new DomainPayload
        {
            ClassCode = entity.Class?.Code,
            ParentCode = entity.ParentDomain?.Code,
            DetailsAr = entity.DetailsAr,
            DetailsEn = entity.DetailsEn,
            DisplayOrder = entity.DisplayOrder,
            IsDefault = entity.IsDefault,
            Attributes = JsonNode.Parse(entity.Attributes) as JsonObject,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            CompanyCode = entity.CompanyKey,
        });

    protected override async Task BeforeDeleteAsync(AppsControlDbContext context, DomainValue entity, CancellationToken cancellationToken)
    {
        if (await context.Domains.AnyAsync(d => d.ParentDomainId == entity.Id, cancellationToken))
        {
            throw new DynamicCube.Platform.ChangeSets.ChangeSetException($"Value '{entity.Code}' is the parent of other values.");
        }

        var classCode = await context.Classes.Where(c => c.Id == entity.ClassId).Select(c => c.Code).SingleAsync(cancellationToken);
        foreach (var rule in rules.Where(r => r.ClassCode == classCode))
        {
            if (await rule.UsedByAsync(entity.Code, cancellationToken) is { } usage)
            {
                throw new DynamicCube.Platform.ChangeSets.ChangeSetException($"Value '{entity.Code}' is used by {usage}.");
            }
        }
    }

    private static async Task<bool> IsDescendantAsync(AppsControlDbContext context, Guid candidate, Guid ancestor, CancellationToken cancellationToken)
    {
        Guid? current = candidate;
        for (var depth = 0; current is not null && depth < 50; depth++)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = await context.Domains.Where(d => d.Id == current).Select(d => d.ParentDomainId).SingleOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    /// <summary>Whether a value is in a list of values (every value of it, whatever the grants of whoever makes the change).</summary>
    private async Task<bool> InListAsync(string lovCode, string value, CancellationToken cancellationToken)
    {
        var access = await services.GetRequiredService<AccessResolver>().GetAsync(cancellationToken);
        try
        {
            var page = await services.GetRequiredService<LovService>().GetValuesAsync(lovCode, access, new LovQuery(null, value, 1, 1000), false, cancellationToken, unrestricted: true);
            return page.Items.Any(i => string.Equals(i.Value, value, StringComparison.Ordinal));
        }
        catch (LovNotFoundException)
        {
            return false;
        }
    }
}
