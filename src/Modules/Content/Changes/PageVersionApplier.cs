using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Content.Domain;
using DynamicCube.Modules.Content.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Content.Changes;

public sealed class PageVersionPayload : ConfigPayload
{
    /// <summary>
    /// The layout: <c>{ "version": 1, "sections": [ { "layout": "1-1", "background": "light", "columns": [[blocks], [blocks]] } ] }</c>,
    /// where each block has a <c>kind</c> (heading, text, image, button, cards, callout, divider, spacer) and its texts in
    /// both languages.
    /// </summary>
    public JsonObject? Design { get; set; }
}

/// <summary>
/// Versions of a page's designed layout, keyed <c>PAGE/N</c>. A new version takes the next number; afterwards only its
/// names may change, so a published version always shows what was approved.
/// </summary>
internal sealed partial class ContentPageVersionApplier(ContentDbContext db)
    : ConfigApplier<ContentDbContext, ConfigRefs<ContentDbContext>, ContentPageVersion, PageVersionPayload>(db)
{
    private const int MaxDesign = 1_500_000;
    private static readonly string[] Layouts = ["1", "1-1", "1-1-1", "2-1", "1-2"];
    private static readonly string[] Backgrounds = ["none", "light", "brand", "dark"];
    private static readonly string[] Kinds = ["heading", "text", "image", "button", "cards", "callout", "divider", "spacer"];

    public override string EntityType => ContentChangeTypes.PageVersion;

    public override string Area => ContentChangeTypes.Area;

    public override string ResourceCode => ContentResources.Config;

    protected override Regex CodePattern => Key();

    protected override ConfigRefs<ContentDbContext> CreateRefs(ContentDbContext context, ChangeSet? draft) => new(context, draft);

    protected override IQueryable<ContentPageVersion> Query(ContentDbContext context) => context.PageVersions.Include(v => v.Page);

    protected override IQueryable<ContentPageVersion> ListQuery(ContentDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(v => v.Page!.Code == parent)).OrderBy(v => v.Page!.Code).ThenByDescending(v => v.Number);

    // Oldest first: a package creates versions in number order.
    protected override IQueryable<ContentPageVersion> ExportQuery(ContentDbContext context) =>
        Query(context).OrderBy(v => v.Page!.Code).ThenBy(v => v.Number);

    protected override async Task ApplyAsync(ContentPageVersion entity, PageVersionPayload payload, ConfigRefs<ContentDbContext> refs, ValidationErrors errors, ContentPageVersion? existing, CancellationToken cancellationToken)
    {
        var (pageCode, number) = Parse(entity.Code);
        var design = payload.Design ?? [];
        var serialized = design.ToJsonString();
        if (existing is not null)
        {
            if (!JsonElement.DeepEquals(JsonDocument.Parse(serialized).RootElement, JsonDocument.Parse(existing.Design).RootElement))
            {
                errors.Add("design", $"Versions do not change once added; add version {pageCode}/{number + 1} instead.");
            }

            return;
        }

        entity.PageId = await refs.ResolveAsync(refs.Context.Pages, ContentChangeTypes.Page, pageCode, "code", errors, cancellationToken) ?? Guid.Empty;
        entity.Number = number;
        entity.Design = serialized;

        var latest = await refs.Context.PageVersions.Where(v => v.Page!.Code == pageCode).MaxAsync(v => (int?)v.Number, cancellationToken) ?? 0;
        var pendingBefore = refs.IsValidating ? Enumerable.Range(1, Math.Max(0, number - 1)).Count(n => refs.IsPendingCreate(ContentChangeTypes.PageVersion, $"{pageCode}/{n}")) : 0;
        if (number != latest + 1 + pendingBefore)
        {
            errors.Add("code", $"The next version of {pageCode} is {latest + 1 + pendingBefore}.");
        }

        Validate(design, serialized, errors);
    }

    /// <summary>The layout's shape, kinds and addresses (links: site paths, https or mailto; images: https or embedded).</summary>
    internal static void Validate(JsonObject design, string serialized, ValidationErrors errors)
    {
        if (serialized.Length > MaxDesign)
        {
            errors.Add("design", "The layout is too large; use image addresses instead of embedded images.");
            return;
        }

        if (design["sections"] is not JsonArray sections || sections.Count == 0)
        {
            errors.Add("design", "Add at least one section.");
            return;
        }

        if (sections.Count > 100)
        {
            errors.Add("design", "A page has at most 100 sections.");
        }

        for (var s = 0; s < sections.Count; s++)
        {
            var field = $"design.sections[{s}]";
            if (sections[s] is not JsonObject section)
            {
                errors.Add(field, "Each section is an object.");
                continue;
            }

            if (Text(section, "layout") is { } layout && !Layouts.Contains(layout))
            {
                errors.Add(field + ".layout", $"Use one of {string.Join(", ", Layouts)}.");
            }

            if (Text(section, "background") is { } background && !Backgrounds.Contains(background))
            {
                errors.Add(field + ".background", $"Use one of {string.Join(", ", Backgrounds)}.");
            }

            if (section["columns"] is not JsonArray columns)
            {
                errors.Add(field + ".columns", "Give the section's columns.");
                continue;
            }

            for (var c = 0; c < columns.Count; c++)
            {
                if (columns[c] is not JsonArray blocks)
                {
                    errors.Add($"{field}.columns[{c}]", "Each column is a list of blocks.");
                    continue;
                }

                for (var b = 0; b < blocks.Count; b++)
                {
                    var blockField = $"{field}.columns[{c}][{b}]";
                    if (blocks[b] is not JsonObject block || Text(block, "kind") is not { } kind || !Kinds.Contains(kind))
                    {
                        errors.Add(blockField, $"Each block has a kind: {string.Join(", ", Kinds)}.");
                        continue;
                    }

                    if (Text(block, "url") is { } url && !IsLink(url))
                    {
                        errors.Add(blockField + ".url", "Use a site path (/…), an https address or mailto:.");
                    }

                    if (Text(block, "src") is { } src && !IsImage(src))
                    {
                        errors.Add(blockField + ".src", "Use an https address or an uploaded image.");
                    }

                    if (block["items"] is JsonArray items)
                    {
                        for (var i = 0; i < items.Count; i++)
                        {
                            if (items[i] is JsonObject item && Text(item, "url") is { } itemUrl && !IsLink(itemUrl))
                            {
                                errors.Add($"{blockField}.items[{i}].url", "Use a site path (/…), an https address or mailto:.");
                            }
                        }
                    }
                }
            }
        }
    }

    private static string? Text(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    private static bool IsLink(string url) =>
        (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private static bool IsImage(string src) =>
        src.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || EmbeddedImage().IsMatch(src);

    protected override Task<PageVersionPayload> ToPayloadAsync(ContentDbContext context, ContentPageVersion entity, CancellationToken cancellationToken) =>
        Task.FromResult(new PageVersionPayload { Design = JsonNode.Parse(entity.Design) as JsonObject });

    protected override Task BeforeDeleteAsync(ContentDbContext context, ContentPageVersion entity, CancellationToken cancellationToken) =>
        entity.Page?.PublishedVersion == entity.Number
            ? throw new ChangeSetException($"Version {entity.Code} is published; publish another version first.")
            : Task.CompletedTask;

    internal static (string Page, int Number) Parse(string key)
    {
        var slash = key.LastIndexOf('/');
        return slash > 0 && int.TryParse(key[(slash + 1)..], System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? (key[..slash], number)
            : (key, 0);
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_\\-]{0,50}/[1-9][0-9]{0,3}$")]
    private static partial Regex Key();

    [GeneratedRegex("^data:image/(png|jpeg|webp);base64,[A-Za-z0-9+/=]+$")]
    private static partial Regex EmbeddedImage();
}
