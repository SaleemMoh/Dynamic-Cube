using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Platform.IntegrationTests;

public sealed class ChangeSetPublishTests(PostgresFixture database)
{
    private const string EntityType = "test.sample-setting";

    [Fact]
    public async Task Approved_change_set_is_applied_and_published()
    {
        await using var services = TestServices.Build(database.RequireConnectionString());
        var user = services.GetRequiredService<TestUser>();
        var ct = TestContext.Current.CancellationToken;
        var code = "CS-" + Guid.NewGuid().ToString("N")[..8];

        Guid id;
        await using (var scope = services.CreateAsyncScope())
        {
            var changes = scope.ServiceProvider.GetRequiredService<ChangeSetService<TestModuleDbContext>>();
            var changeSet = await changes.CreateAsync("test.settings", "Add setting", ct);
            id = changeSet.Id;
            await changes.AddItemAsync(id, EntityType, code, ChangeOperation.Create, null, """{ "valueAr": "نعم", "valueEn": "yes" }""", ct);
            await changes.SubmitAsync(id, ct);
        }

        user.UserId = "checker";
        await using (var scope = services.CreateAsyncScope())
        {
            var changes = scope.ServiceProvider.GetRequiredService<ChangeSetService<TestModuleDbContext>>();
            await changes.ApproveAsync(id, "Looks good", ct);
            await changes.PublishAsync(id, ct);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
            var setting = await db.SampleSettings.SingleAsync(s => s.Code == code, ct);
            setting.ValueAr.ShouldBe("نعم");
            setting.CreatedBy.ShouldBe("checker");

            var changeSet = await db.ChangeSets.Include(c => c.Items).SingleAsync(c => c.Id == id, ct);
            changeSet.Status.ShouldBe(ChangeSetStatus.Published);
            changeSet.CreatedBy.ShouldBe("maker");
            changeSet.ReviewedBy.ShouldBe("checker");
            changeSet.Items.Count.ShouldBe(1);

            (await db.AuditEntries.CountAsync(a => a.EntityId == setting.Id.ToString(), ct)).ShouldBe(1);
        }
    }

    [Fact]
    public async Task Failing_item_rolls_back_the_whole_change_set()
    {
        await using var services = TestServices.Build(database.RequireConnectionString(), s =>
            s.Configure<ChangeSetOptions>(o => o.Areas["test.unchecked"] = false));
        var ct = TestContext.Current.CancellationToken;
        var code = "OK-" + Guid.NewGuid().ToString("N")[..8];

        Guid id;
        await using (var scope = services.CreateAsyncScope())
        {
            var changes = scope.ServiceProvider.GetRequiredService<ChangeSetService<TestModuleDbContext>>();
            var changeSet = await changes.CreateAsync("test.unchecked", "Two settings", ct);
            id = changeSet.Id;
            await changes.AddItemAsync(id, EntityType, code, ChangeOperation.Create, null, """{ "valueAr": "أ", "valueEn": "a" }""", ct);
            await changes.AddItemAsync(id, EntityType, "BOOM", ChangeOperation.Create, null, """{ "valueAr": "ب", "valueEn": "b" }""", ct);
            await changes.SubmitAsync(id, ct);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var changes = scope.ServiceProvider.GetRequiredService<ChangeSetService<TestModuleDbContext>>();
            await Should.ThrowAsync<InvalidOperationException>(() => changes.PublishAsync(id, ct));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
            (await db.SampleSettings.AnyAsync(s => s.Code == code, ct)).ShouldBeFalse();
            (await db.ChangeSets.SingleAsync(c => c.Id == id, ct)).Status.ShouldBe(ChangeSetStatus.Approved);
        }
    }

    [Fact]
    public async Task Item_for_unknown_entity_type_is_rejected()
    {
        await using var services = TestServices.Build(database.RequireConnectionString());
        var ct = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var changes = scope.ServiceProvider.GetRequiredService<ChangeSetService<TestModuleDbContext>>();
        var changeSet = await changes.CreateAsync("test.settings", "Bad item", ct);

        await Should.ThrowAsync<ChangeSetException>(() =>
            changes.AddItemAsync(changeSet.Id, "unknown.type", "X", ChangeOperation.Create, null, "{}", ct));
    }
}
