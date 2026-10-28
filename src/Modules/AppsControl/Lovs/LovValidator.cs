using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Platform.Lovs;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>
/// Server-side check of LOV answers with the same grants and public/internal rules as the LOV values endpoint, so a
/// tampered request cannot submit a value the user could not pick.
/// </summary>
internal sealed class LovValidator(AccessResolver resolver, LovService lovs, AppsControlHostOptions host) : ILovValidator
{
    public async Task<bool> IsAllowedAsync(string lovCode, string value, string? parent, CancellationToken cancellationToken)
    {
        var access = await resolver.GetAsync(cancellationToken);
        try
        {
            // Every source also searches the value column, so the exact value is among the matches when allowed.
            var page = await lovs.GetValuesAsync(lovCode, access, new LovQuery(parent, value, 1, 1000), publicOnly: !host.ManagesUsers, cancellationToken);
            return page.Items.Any(i => string.Equals(i.Value, value, StringComparison.Ordinal));
        }
        catch (LovNotFoundException)
        {
            return false;
        }
    }
}
