namespace DynamicCube.Platform.Localization;

/// <summary>A bilingual text value (Arabic / English) as stored in <c>name_ar</c> / <c>name_en</c> columns.</summary>
public sealed record LocalizedText(string Ar, string En)
{
    /// <summary>Returns the text for <paramref name="language"/>, falling back to the other language when empty.</summary>
    public string Get(Language language)
    {
        var primary = language == Language.Ar ? Ar : En;
        var fallback = language == Language.Ar ? En : Ar;
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
    }

    public override string ToString() => Get(Language.Ar);
}
