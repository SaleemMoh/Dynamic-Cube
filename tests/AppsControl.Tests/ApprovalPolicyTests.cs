using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

public sealed class ApprovalPolicyTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    [Fact]
    public async Task Policy_rows_are_seeded_from_configuration_and_the_policy_itself_always_needs_approval()
    {
        var (uiTexts, lookups, governance) = await _harness.AsAsync("admin", async sp =>
        {
            var policy = sp.GetRequiredService<IChangeApprovalPolicy>();
            return (
                await policy.RequiresApprovalAsync(AppsControlAreas.UiTexts, CancellationToken.None),
                await policy.RequiresApprovalAsync(AppsControlAreas.Lookups, CancellationToken.None),
                await policy.RequiresApprovalAsync(AppsControlAreas.Governance, CancellationToken.None));
        });

        uiTexts.ShouldBeFalse();
        lookups.ShouldBeTrue();
        governance.ShouldBeTrue();
    }

    [Fact]
    public async Task Save_and_publish_applies_at_once_and_keeps_before_and_after()
    {
        var key = Harness.Unique("home.title").ToLowerInvariant();
        await SaveAsync(AppsControlAreas.UiTexts, AppsControlChangeTypes.UiText, key, ChangeOperation.Create, """{"nameAr":"مرحبا","nameEn":"Hello"}""");
        var changeSet = await SaveAsync(AppsControlAreas.UiTexts, AppsControlChangeTypes.UiText, key, ChangeOperation.Update, """{"nameAr":"أهلا","nameEn":"Welcome"}""");

        changeSet.Status.ShouldBe(ChangeSetStatus.Published);
        var item = changeSet.Items.ShouldHaveSingleItem();
        item.Before.ShouldNotBeNull().ShouldContain("Hello");
        item.After.ShouldNotBeNull().ShouldContain("Welcome");

        var text = await _harness.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().UiTexts.SingleAsync(t => t.Code == key));
        text.NameEn.ShouldBe("Welcome");
    }

    [Fact]
    public async Task Save_and_publish_is_refused_where_approval_is_required_and_stores_nothing_when_invalid()
    {
        await Should.ThrowAsync<ChangeSetException>(() =>
            SaveAsync(AppsControlAreas.Lookups, AppsControlChangeTypes.Class, Harness.Unique("CLS"), ChangeOperation.Create, """{"nameAr":"أ","nameEn":"A"}"""));

        var bad = Harness.Unique("bad").ToLowerInvariant();
        await Should.ThrowAsync<ChangeValidationException>(() =>
            SaveAsync(AppsControlAreas.UiTexts, AppsControlChangeTypes.UiText, bad, ChangeOperation.Create, """{"nameAr":"","nameEn":"x"}"""));
        (await CountChangeSetsAsync(bad)).ShouldBe(0);
    }

    [Fact]
    public async Task Switching_an_area_to_no_approval_goes_through_approval_first()
    {
        var area = AppsControlAreas.Menus;
        await _harness.PublishAsync(
            AppsControlAreas.Governance,
            new Change(AppsControlChangeTypes.ChangeArea, area, ChangeOperation.Update, new { nameAr = "القوائم", nameEn = "Menus", requiresApproval = false }));

        var requires = await _harness.AsAsync("maker", sp => sp.GetRequiredService<IChangeApprovalPolicy>().RequiresApprovalAsync(area, CancellationToken.None));
        requires.ShouldBeFalse();

        await _harness.PublishAsync(
            AppsControlAreas.Governance,
            new Change(AppsControlChangeTypes.ChangeArea, area, ChangeOperation.Update, new { nameAr = "القوائم", nameEn = "Menus", requiresApproval = true }));

        await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Governance,
            new Change(AppsControlChangeTypes.ChangeArea, "apps.unknown", ChangeOperation.Create, new { nameAr = "س", nameEn = "X" })));
    }

    private Task<ChangeSet> SaveAsync(string area, string type, string code, ChangeOperation operation, string payload) =>
        _harness.AsAsync("maker", async sp =>
        {
            var service = sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>();
            var changeSet = await service.SaveAndPublishAsync(area, code, type, code, operation, payload, CancellationToken.None);
            return await service.GetAsync(changeSet.Id, CancellationToken.None);
        });

    private Task<int> CountChangeSetsAsync(string title) =>
        _harness.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().ChangeSets.CountAsync(c => c.Title == title));
}
