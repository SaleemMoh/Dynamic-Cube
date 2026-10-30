using System.Text.Json;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>Menu trees (ADR-0036): each channel's, where Dynamic Cube apps add items, and each connected app's own.</summary>
public sealed class MenuTreeTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private static Change App(string code, string kind, params string[] channels) =>
        new(AppsControlChangeTypes.BusinessApp, code, ChangeOperation.Create, new { nameAr = "تطبيق", nameEn = "App " + code, kind, channels, companies = new[] { "*" }, branches = new[] { "*" } });

    private static Change Group(string code, string? app, string? channel) =>
        new(AppsControlChangeTypes.MenuGroup, code, ChangeOperation.Create, new { nameAr = "مجموعة", nameEn = "Group " + code, businessApp = app, appCode = channel });

    // Hidden: the tests share the internal system's menu with other tests that check it exactly.
    private static Change Item(string code, string? app, string? channel, string? group = null, string? parent = null, string type = "GROUP", ChangeOperation operation = ChangeOperation.Create) =>
        new(AppsControlChangeTypes.Menu, code, operation, new
        {
            nameAr = "عنصر",
            nameEn = "Item " + code,
            businessApp = app,
            appCode = channel,
            groupCode = group,
            parentCode = parent,
            itemType = type,
            target = type == "GROUP" ? null : "/hr/" + code,
            isVisible = false,
        });

    private async Task<IEnumerable<string>> Problems(params Change[] changes) =>
        (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Menus, changes))).Errors.Keys;

    [Fact]
    public async Task A_connected_app_has_its_own_tree_of_any_depth_without_cycles()
    {
        var hr = Harness.Unique("HRVAULT");
        await _harness.PublishAsync(AppsControlAreas.System, App(hr, "CONNECTED"));
        var (root, board, sub, digital) = (Harness.Unique("M010"), Harness.Unique("M010_000"), Harness.Unique("M010_010"), Harness.Unique("M010_010_010"));
        await _harness.PublishAsync(
            AppsControlAreas.Menus,
            Group(root, hr, null),
            Item(board, hr, null, group: root),
            Item(sub, hr, null, parent: board),
            Item(digital, hr, null, parent: sub, type: "SCREEN"));

        var tree = await _harness.AsAsync("admin", sp => sp.GetServices<IConfigApplier>().Single(a => a.EntityType == AppsControlChangeTypes.Menu).ListAsync(hr, CancellationToken.None));
        tree.Select(m => m.GetProperty("code").GetString()).ShouldBe([board, sub, digital], ignoreOrder: true);
        tree.Single(m => m.GetProperty("code").GetString() == digital).GetProperty("parentCode").GetString().ShouldBe(sub);
        tree.ShouldAllBe(m => m.GetProperty("appCode").ValueKind == JsonValueKind.Null);

        (await Problems(Item(Harness.Unique("X"), hr, "INTERNAL", group: root))).ShouldContain("appCode", "a connected app's menu has no channel");
        (await Problems(Item(board, hr, null, parent: digital, operation: ChangeOperation.Update))).ShouldContain("parentCode", "an item cannot hang under its own items");
        (await Problems(Item(Harness.Unique("Y"), null, "INTERNAL", parent: sub))).ShouldContain("parentCode", "the parent is in another tree");
    }

    [Fact]
    public async Task A_dynamic_cube_app_adds_items_only_on_the_channels_it_ticks()
    {
        var app = Harness.Unique("CLS");
        await _harness.PublishAsync(AppsControlAreas.System, App(app, "DYNAMIC_CUBE", "INTERNAL"));

        (await Problems(Item(Harness.Unique("P"), app, "EXTERNAL", type: "SCREEN"))).ShouldContain("appCode");
        (await Problems(Item(Harness.Unique("Q"), null, null, type: "SCREEN"))).ShouldContain("appCode", "a platform item needs its channel");
        await _harness.PublishAsync(AppsControlAreas.Menus, Item(Harness.Unique("R"), app, "INTERNAL", type: "SCREEN"));
    }
}
