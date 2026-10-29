namespace DynamicCube.Modules.Content.Domain;

/// <summary>
/// A page of the portal (terms of use, privacy, about): the name is its title, the body Markdown in both languages. Its
/// address is the code in lower case with <c>-</c> for <c>_</c> (<c>TERMS_OF_USE</c> is <c>/pages/terms-of-use</c>).
/// </summary>
public sealed class ContentPage : ConfigEntity
{
    public string BodyAr { get; set; } = string.Empty;

    public string BodyEn { get; set; } = string.Empty;

    /// <summary>Linked from the portal footer, in <see cref="Order"/>.</summary>
    public bool ShowInFooter { get; set; }

    public int Order { get; set; }
}

/// <summary>A frequently asked question: the name is the question, grouped on the portal by topic.</summary>
public sealed class Faq : ConfigEntity
{
    public string AnswerAr { get; set; } = string.Empty;

    public string AnswerEn { get; set; } = string.Empty;

    public string? TopicAr { get; set; }

    public string? TopicEn { get; set; }

    /// <summary>Shown with this service's questions too (its service page links to them).</summary>
    public string? ServiceCode { get; set; }

    public int Order { get; set; }
}
