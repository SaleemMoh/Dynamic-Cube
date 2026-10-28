using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>Key of a setting or UI text: <c>KEY</c> (global) or <c>APP/KEY</c> (one app).</summary>
internal static class AppScopedKey
{
    public static (string? App, string Key) Parse(string entityCode)
    {
        var index = entityCode.IndexOf('/', StringComparison.Ordinal);
        return index < 0 ? (null, entityCode) : (entityCode[..index], entityCode[(index + 1)..]);
    }

    public static string Format(string? app, string key) => app is null ? key : $"{app}/{key}";

    public static void Validate(string entityCode, ValidationErrors errors)
    {
        var (app, key) = Parse(entityCode);
        if (app is not null)
        {
            errors.Code("appCode", app, ValidationErrors.IdentifierCode());
        }

        errors.Code("code", key, ValidationErrors.GeneralCode());
    }
}

public sealed class SettingPayload : ConfigPayload
{
    public string? AppCode { get; set; }

    public SettingDataType DataType { get; set; }

    public JsonNode? Value { get; set; }

    public string? Description { get; set; }

    public bool IsSecret { get; set; }

    public bool IsPublic { get; set; }
}

internal sealed class SettingApplier : ConfigApplier<Setting, SettingPayload>
{
    public const string Masked = "********";

    public override string EntityType => AppsControlChangeTypes.Setting;

    public override string Area => AppsControlAreas.Settings;

    public override string ResourceCode => AppsControlResources.Settings;

    protected override IQueryable<Setting> Query(AppsControlDbContext context) => context.Settings.Include(s => s.App);

    protected override IQueryable<Setting> ListQuery(AppsControlDbContext context, string? parent) =>
        Query(context).OrderBy(s => s.Code).ThenBy(s => s.App!.Code);

    protected override Task<Setting?> FindAsync(AppsControlDbContext context, string entityCode, CancellationToken cancellationToken)
    {
        var (app, key) = AppScopedKey.Parse(entityCode);
        return Query(context).SingleOrDefaultAsync(s => s.Code == key && (app == null ? s.AppId == null : s.App!.Code == app), cancellationToken);
    }

    protected override void ValidateKey(string entityCode, ValidationErrors errors) => AppScopedKey.Validate(entityCode, errors);

    protected override string CodeFromKey(string entityCode) => AppScopedKey.Parse(entityCode).Key;

    protected override string EntityCodeOf(Setting entity) => AppScopedKey.Format(entity.App?.Code, entity.Code);

    protected override async Task ApplyAsync(Setting entity, SettingPayload payload, RefResolver refs, ValidationErrors errors, Setting? existing, CancellationToken cancellationToken)
    {
        var (app, _) = AppScopedKey.Parse(payload.Code!);
        entity.AppId = await refs.AppAsync(app, "appCode", errors, cancellationToken);
        if (payload.IsSecret && payload.IsPublic)
        {
            errors.Add("isPublic", "Secret settings cannot be public.");
        }

        // A masked secret in an update keeps the stored value.
        var keepSecret = existing is { IsSecret: true } && payload.Value?.GetValueKind() == JsonValueKind.String && payload.Value.GetValue<string>() == Masked;
        var value = keepSecret ? JsonNode.Parse(existing!.Value) : payload.Value;
        var kind = value?.GetValueKind() ?? JsonValueKind.Null;
        var valid = payload.DataType switch
        {
            SettingDataType.Text => kind is JsonValueKind.String or JsonValueKind.Null,
            SettingDataType.Number => kind is JsonValueKind.Number or JsonValueKind.Null,
            SettingDataType.Bool => kind is JsonValueKind.True or JsonValueKind.False,
            _ => true,
        };
        if (!valid)
        {
            errors.Add("value", $"Expected a {payload.DataType.ToString().ToUpperInvariant()} value.");
        }

        entity.DataType = payload.DataType;
        entity.Value = value?.ToJsonString() ?? "null";
        entity.Description = payload.Description;
        entity.IsSecret = payload.IsSecret;
        entity.IsPublic = payload.IsPublic;
    }

    protected override Task<SettingPayload> ToPayloadAsync(AppsControlDbContext context, Setting entity, CancellationToken cancellationToken) =>
        Task.FromResult(new SettingPayload
        {
            AppCode = entity.App?.Code,
            DataType = entity.DataType,
            Value = entity.IsSecret ? JsonValue.Create(Masked) : JsonNode.Parse(entity.Value),
            Description = entity.Description,
            IsSecret = entity.IsSecret,
            IsPublic = entity.IsPublic,
        });
}

public sealed class UiTextPayload : ConfigPayload
{
    public string? AppCode { get; set; }
}

/// <summary>UI text override; <c>nameAr</c> / <c>nameEn</c> hold the texts.</summary>
internal sealed class UiTextApplier : ConfigApplier<UiText, UiTextPayload>
{
    public override string EntityType => AppsControlChangeTypes.UiText;

    public override string Area => AppsControlAreas.UiTexts;

    public override string ResourceCode => AppsControlResources.UiTexts;

    protected override int NameMaxLength => 4000;

    protected override IQueryable<UiText> Query(AppsControlDbContext context) => context.UiTexts.Include(t => t.App);

    protected override IQueryable<UiText> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(t => t.App!.Code == parent || t.AppId == null)).OrderBy(t => t.Code);

    protected override Task<UiText?> FindAsync(AppsControlDbContext context, string entityCode, CancellationToken cancellationToken)
    {
        var (app, key) = AppScopedKey.Parse(entityCode);
        return Query(context).SingleOrDefaultAsync(t => t.Code == key && (app == null ? t.AppId == null : t.App!.Code == app), cancellationToken);
    }

    protected override void ValidateKey(string entityCode, ValidationErrors errors) => AppScopedKey.Validate(entityCode, errors);

    protected override string CodeFromKey(string entityCode) => AppScopedKey.Parse(entityCode).Key;

    protected override string EntityCodeOf(UiText entity) => AppScopedKey.Format(entity.App?.Code, entity.Code);

    protected override async Task ApplyAsync(UiText entity, UiTextPayload payload, RefResolver refs, ValidationErrors errors, UiText? existing, CancellationToken cancellationToken)
    {
        var (app, _) = AppScopedKey.Parse(payload.Code!);
        entity.AppId = await refs.AppAsync(app, "appCode", errors, cancellationToken);
    }

    protected override Task<UiTextPayload> ToPayloadAsync(AppsControlDbContext context, UiText entity, CancellationToken cancellationToken) =>
        Task.FromResult(new UiTextPayload { AppCode = entity.App?.Code });
}
