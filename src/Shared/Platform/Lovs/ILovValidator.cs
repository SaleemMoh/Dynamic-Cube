namespace DynamicCube.Platform.Lovs;

/// <summary>Checks submitted values against Apps Control LOVs, with the grants of the current user (implemented by Apps Control).</summary>
public interface ILovValidator
{
    /// <summary>True when <paramref name="value"/> is one of the values the current user may choose in the LOV.</summary>
    Task<bool> IsAllowedAsync(string lovCode, string value, string? parent, CancellationToken cancellationToken);
}
