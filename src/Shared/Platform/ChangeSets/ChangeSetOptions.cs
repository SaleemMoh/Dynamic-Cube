namespace DynamicCube.Platform.ChangeSets;

/// <summary>Maker-checker policy per configuration area.</summary>
public sealed class ChangeSetOptions
{
    public const string SectionName = "ChangeSets";

    /// <summary>Applies to areas not listed in <see cref="Areas"/>.</summary>
    public bool RequireApprovalByDefault { get; set; } = true;

    /// <summary>Area code → whether approval by a second user is required.</summary>
    public Dictionary<string, bool> Areas { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool RequiresApproval(string area) =>
        Areas.TryGetValue(area, out var required) ? required : RequireApprovalByDefault;
}
