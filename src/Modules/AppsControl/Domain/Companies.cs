namespace DynamicCube.Modules.AppsControl.Domain;

/// <summary>
/// The explicit choice of every company or every branch (ADR-0036). Where companies or branches are chosen (a user's roles
/// and personal privileges, an app), "all" is this value: an empty choice is not valid and never means all.
/// </summary>
public static class CompanyScope
{
    public const string All = "*";

    /// <summary>Whether a list of codes is the choice of all of them.</summary>
    public static bool IsAll(IReadOnlyCollection<string> codes) => codes.Count == 1 && codes.Contains(All);
}

/// <summary>
/// A company is keyed like the legacy Apps Control: its instance (<c>instid</c>) and its number in that instance
/// (<c>cmp_num</c>). Wherever a company is named — change sets, the API, the current company, a user's roles, requests —
/// it is that pair written <c>instid/cmp_num</c> (e.g. <c>5/2</c>).
/// </summary>
public static class CompanyKey
{
    public static string Format(string? instid, string cmpNum) => instid is null ? cmpNum : $"{instid}/{cmpNum}";

    /// <summary>Splits a key into its instance and number (false when it is not <c>instid/cmp_num</c>).</summary>
    public static bool TryParse(string? key, out string instid, out string cmpNum)
    {
        var index = key?.IndexOf('/', StringComparison.Ordinal) ?? -1;
        if (key is null || index <= 0 || index == key.Length - 1 || key.IndexOf('/', index + 1) >= 0)
        {
            instid = cmpNum = string.Empty;
            return false;
        }

        instid = key[..index];
        cmpNum = key[(index + 1)..];
        return true;
    }
}

/// <summary>
/// A legal entity of an organization (<see cref="ConfigEntity.EntityCode"/>, the <see cref="GovernmentEntity"/>): it holds
/// branches, and requests, tasks and documents belong to one of its branches (ADR-0036).
/// </summary>
public sealed class Company : ConfigEntity
{
    public string? CrNumber { get; set; }

    public string? UnifiedNumber { get; set; }

    public string? VatNumber { get; set; }

    /// <summary>The company's short name, e.g. on documents (legacy <c>CMP_ABBREVIATION</c>); optional.</summary>
    public string? Abbreviation { get; set; }

    /// <summary>The company's default business unit in its ERP (legacy <c>DEFAULT_BU</c>); optional.</summary>
    public string? DefaultBusinessUnit { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>The company's key, <c>instid/cmp_num</c> (see <see cref="CompanyKey"/>).</summary>
    public string Key => CompanyKey.Format(EntityCode, Code);
}

/// <summary>A business unit of a company (head office, regional office); its organization is the company's.</summary>
public sealed class Branch : ConfigEntity
{
    /// <summary>The company's number (<c>cmp_num</c>); with the branch's instance it names the company.</summary>
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>The company's key, <c>instid/cmp_num</c>.</summary>
    public string CompanyKey => Domain.CompanyKey.Format(EntityCode, CompanyCode);

    /// <summary>Code of the org unit that serves the branch (tasks of its requests are routed under it); optional.</summary>
    public string? OrgUnitCode { get; set; }

    /// <summary>Domain code of class <c>REGION</c> (where the branch is), optional.</summary>
    public string? RegionCode { get; set; }

    public string? City { get; set; }

    /// <summary>The company's default branch: used when nothing else says which one. One per company.</summary>
    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }
}
