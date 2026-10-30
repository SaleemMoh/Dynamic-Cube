using System.Text.RegularExpressions;
using DynamicCube.Modules.Content.Domain;
using DynamicCube.Modules.Content.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Content.Changes;

public sealed class ContentPagePayload : ConfigPayload
{
    public string? BodyAr { get; set; }

    public string? BodyEn { get; set; }

    public bool ShowInFooter { get; set; }

    public int Order { get; set; }

    /// <summary>The version of the designed layout shown on the portal; none: the Markdown body.</summary>
    public int? PublishedVersion { get; set; }
}

public sealed class FaqPayload : ConfigPayload
{
    public string? AnswerAr { get; set; }

    public string? AnswerEn { get; set; }

    public string? TopicAr { get; set; }

    public string? TopicEn { get; set; }

    public string? ServiceCode { get; set; }

    public int Order { get; set; }
}

internal static class ContentLimits
{
    public const int Body = 50_000;
    public const int Answer = 10_000;
    public const int Topic = 200;

    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void Order(ValidationErrors errors, int order)
    {
        if (order is < 0 or > 9999)
        {
            errors.Add("order", "Must be between 0 and 9999.");
        }
    }
}

internal sealed class ContentPageApplier(ContentDbContext db)
    : ConfigApplier<ContentDbContext, ConfigRefs<ContentDbContext>, ContentPage, ContentPagePayload>(db)
{
    public override string EntityType => ContentChangeTypes.Page;

    public override string Area => ContentChangeTypes.Area;

    public override string ResourceCode => ContentResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<ContentDbContext> CreateRefs(ContentDbContext context, ChangeSet? draft) => new(context, draft);

    protected override IQueryable<ContentPage> Query(ContentDbContext context) => context.Pages;

    protected override async Task ApplyAsync(ContentPage entity, ContentPagePayload payload, ConfigRefs<ContentDbContext> refs, ValidationErrors errors, ContentPage? existing, CancellationToken cancellationToken)
    {
        // A designed page shows its published version; the Markdown body is then optional.
        if (payload.PublishedVersion is { } version)
        {
            var key = $"{entity.Code}/{version}";
            var exists = await refs.Context.PageVersions.AnyAsync(v => v.Page!.Code == entity.Code && v.Number == version, cancellationToken);
            if (!exists && !refs.IsPendingCreate(ContentChangeTypes.PageVersion, key))
            {
                errors.Add("publishedVersion", $"Version {key} does not exist.");
            }
        }
        else
        {
            errors.Required("bodyAr", payload.BodyAr);
            errors.Required("bodyEn", payload.BodyEn);
        }

        entity.PublishedVersion = payload.PublishedVersion;
        errors.MaxLength("bodyAr", payload.BodyAr, ContentLimits.Body);
        errors.MaxLength("bodyEn", payload.BodyEn, ContentLimits.Body);
        ContentLimits.Order(errors, payload.Order);

        entity.BodyAr = payload.BodyAr?.Trim() ?? string.Empty;
        entity.BodyEn = payload.BodyEn?.Trim() ?? string.Empty;
        entity.ShowInFooter = payload.ShowInFooter;
        entity.Order = payload.Order;
    }

    protected override Task<ContentPagePayload> ToPayloadAsync(ContentDbContext context, ContentPage entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ContentPagePayload
        {
            BodyAr = entity.BodyAr,
            BodyEn = entity.BodyEn,
            ShowInFooter = entity.ShowInFooter,
            Order = entity.Order,
            PublishedVersion = entity.PublishedVersion,
        });
}

internal sealed class FaqApplier(ContentDbContext db)
    : ConfigApplier<ContentDbContext, ConfigRefs<ContentDbContext>, Faq, FaqPayload>(db)
{
    public override string EntityType => ContentChangeTypes.Faq;

    public override string Area => ContentChangeTypes.Area;

    public override string ResourceCode => ContentResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<ContentDbContext> CreateRefs(ContentDbContext context, ChangeSet? draft) => new(context, draft);

    protected override IQueryable<Faq> Query(ContentDbContext context) => context.Faqs;

    protected override Task ApplyAsync(Faq entity, FaqPayload payload, ConfigRefs<ContentDbContext> refs, ValidationErrors errors, Faq? existing, CancellationToken cancellationToken)
    {
        errors.Required("answerAr", payload.AnswerAr);
        errors.Required("answerEn", payload.AnswerEn);
        errors.MaxLength("answerAr", payload.AnswerAr, ContentLimits.Answer);
        errors.MaxLength("answerEn", payload.AnswerEn, ContentLimits.Answer);
        errors.MaxLength("topicAr", payload.TopicAr, ContentLimits.Topic);
        errors.MaxLength("topicEn", payload.TopicEn, ContentLimits.Topic);
        if ((ContentLimits.Blank(payload.TopicAr) is null) != (ContentLimits.Blank(payload.TopicEn) is null))
        {
            errors.Add("topicEn", "Give the topic in both languages, or neither.");
        }

        // Services live in another module: the code is kept as given (a question about a service not yet configured is fine).
        if (ContentLimits.Blank(payload.ServiceCode) is { } service)
        {
            errors.Code("serviceCode", service, ValidationErrors.IdentifierCode());
        }
        ContentLimits.Order(errors, payload.Order);

        entity.AnswerAr = payload.AnswerAr?.Trim() ?? string.Empty;
        entity.AnswerEn = payload.AnswerEn?.Trim() ?? string.Empty;
        entity.TopicAr = ContentLimits.Blank(payload.TopicAr);
        entity.TopicEn = ContentLimits.Blank(payload.TopicEn);
        entity.ServiceCode = ContentLimits.Blank(payload.ServiceCode);
        entity.Order = payload.Order;
        return Task.CompletedTask;
    }

    protected override Task<FaqPayload> ToPayloadAsync(ContentDbContext context, Faq entity, CancellationToken cancellationToken) =>
        Task.FromResult(new FaqPayload
        {
            AnswerAr = entity.AnswerAr,
            AnswerEn = entity.AnswerEn,
            TopicAr = entity.TopicAr,
            TopicEn = entity.TopicEn,
            ServiceCode = entity.ServiceCode,
            Order = entity.Order,
        });
}
