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

    /// <summary>
    /// The version of the designed layout the portal shows (<see cref="ContentPageVersion.Number"/>); none: the page shows
    /// its Markdown body.
    /// </summary>
    public int? PublishedVersion { get; set; }
}

/// <summary>
/// A version of a page's designed layout (sections of blocks, JSON), keyed <c>PAGE/N</c>. Versions never change once
/// added; the page shows the one it publishes, and an earlier one can be published again.
/// </summary>
public sealed class ContentPageVersion : ConfigEntity
{
    public Guid PageId { get; set; }

    public ContentPage? Page { get; set; }

    public int Number { get; set; }

    public string Design { get; set; } = "{}";
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

/// <summary>
/// One answer to "Was this page helpful?". A visitor answers once per page and may change the answer: signed-in users
/// by their subject, others by the random visitor id their browser keeps.
/// </summary>
public sealed class PageFeedback
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Which page: <c>home</c>, <c>service:CODE</c>, <c>page:slug</c>, <c>faq</c>…</summary>
    public string PageKey { get; set; } = string.Empty;

    /// <summary><c>user:SUBJECT</c> or <c>visitor:ID</c>.</summary>
    public string VoterKey { get; set; } = string.Empty;

    public bool Helpful { get; set; }

    public DateTimeOffset AnsweredAt { get; set; }
}

/// <summary>A signed-in user's rating of a service, one to five stars; rating again replaces it.</summary>
public sealed class ServiceRating
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string ServiceCode { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public int Stars { get; set; }

    public DateTimeOffset RatedAt { get; set; }
}
