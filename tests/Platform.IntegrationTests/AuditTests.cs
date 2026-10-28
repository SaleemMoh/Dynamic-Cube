using System.Text.Json.Nodes;
using DynamicCube.Platform.Auditing;
using DynamicCube.Platform.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Platform.IntegrationTests;

public sealed class AuditTests(PostgresFixture database)
{
    [Fact]
    public async Task Create_update_delete_are_audited_with_changed_values_and_audit_columns()
    {
        await using var services = TestServices.Build(database.RequireConnectionString());
        var clock = services.GetRequiredService<TestClock>();
        var user = services.GetRequiredService<TestUser>();
        var ct = TestContext.Current.CancellationToken;
        var setting = new SampleSetting { Code = "AUDIT-" + Guid.NewGuid().ToString("N")[..8], ValueAr = "قيمة", ValueEn = "value" };

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
            db.SampleSettings.Add(setting);
            await db.SaveChangesAsync(ct);
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        user.UserId = "editor";
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
            var loaded = await db.SampleSettings.SingleAsync(s => s.Id == setting.Id, ct);
            loaded.ValueEn = "changed";
            await db.SaveChangesAsync(ct);

            loaded.CreatedBy.ShouldBe("maker");
            loaded.UpdatedBy.ShouldBe("editor");
            loaded.UpdatedAt.ShouldBe(clock.GetUtcNow());

            // Saving again without changes must not add an audit entry.
            await db.SaveChangesAsync(ct);

            db.SampleSettings.Remove(loaded);
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
            var entries = await db.AuditEntries
                .Where(a => a.EntityType == nameof(SampleSetting) && a.EntityId == setting.Id.ToString())
                .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id)
                .ToListAsync(ct);

            entries.Select(e => e.Action).ShouldBe([AuditAction.Created, AuditAction.Updated, AuditAction.Deleted]);
            entries[0].UserId.ShouldBe("maker");
            entries[0].AppCode.ShouldBe("TEST");
            JsonNode.Parse(entries[0].Changes)!["ValueAr"]!["new"]!.GetValue<string>().ShouldBe("قيمة");

            var updated = JsonNode.Parse(entries[1].Changes)!.AsObject();
            updated["ValueEn"]!["old"]!.GetValue<string>().ShouldBe("value");
            updated["ValueEn"]!["new"]!.GetValue<string>().ShouldBe("changed");
            updated.ContainsKey("ValueAr").ShouldBeFalse();
            entries[1].UserId.ShouldBe("editor");
        }
    }
}
