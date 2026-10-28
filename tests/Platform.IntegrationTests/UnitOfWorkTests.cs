using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.IntegrationTests.Infrastructure;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Platform.IntegrationTests;

public sealed class UnitOfWorkTests(PostgresFixture database)
{
    [Fact]
    public async Task Changes_of_two_module_contexts_commit_or_roll_back_together()
    {
        await using var services = TestServices.Build(database.RequireConnectionString());
        var ct = TestContext.Current.CancellationToken;
        var kept = "UOW-" + Guid.NewGuid().ToString("N")[..8];
        var dropped = "UOW-" + Guid.NewGuid().ToString("N")[..8];

        await SaveBothAsync(services, kept, commit: true, ct);
        await SaveBothAsync(services, dropped, commit: false, ct);

        await using var scope = services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await module.SampleSettings.AnyAsync(s => s.Code == kept, ct)).ShouldBeTrue();
        (await platform.ChangeSets.AnyAsync(c => c.Title == kept, ct)).ShouldBeTrue();
        (await module.SampleSettings.AnyAsync(s => s.Code == dropped, ct)).ShouldBeFalse();
        (await platform.ChangeSets.AnyAsync(c => c.Title == dropped, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_inner_unit_of_work_joins_the_outer_one_and_contexts_save_normally_afterwards()
    {
        await using var services = TestServices.Build(database.RequireConnectionString());
        var ct = TestContext.Current.CancellationToken;
        var code = "UOW-" + Guid.NewGuid().ToString("N")[..8];
        await using var scope = services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var module = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();

        await using (var outer = await unitOfWork.BeginAsync(ct))
        {
            await using (var inner = await unitOfWork.BeginAsync(ct))
            {
                module.SampleSettings.Add(new SampleSetting { Code = code, ValueAr = "أ", ValueEn = "a" });
                await module.SaveChangesAsync(ct);
                await inner.CommitAsync(ct);
            }

            // The inner commit left the transaction open: other connections cannot see the row yet.
            await using var other = services.CreateAsyncScope();
            (await other.ServiceProvider.GetRequiredService<TestModuleDbContext>().SampleSettings.AnyAsync(s => s.Code == code, ct)).ShouldBeFalse();
            await outer.CommitAsync(ct);
        }

        module.SampleSettings.Add(new SampleSetting { Code = code + "-2", ValueAr = "ب", ValueEn = "b" });
        await module.SaveChangesAsync(ct);
        (await module.SampleSettings.CountAsync(s => s.Code.StartsWith(code), ct)).ShouldBe(2);
    }

    private static async Task SaveBothAsync(ServiceProvider services, string code, bool commit, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await module.SampleSettings.CountAsync(ct);
        await using var work = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginAsync(ct);
        module.SampleSettings.Add(new SampleSetting { Code = code, ValueAr = "أ", ValueEn = "a" });
        await module.SaveChangesAsync(ct);
        platform.ChangeSets.Add(ChangeSet.Create("test.area", code, requiresApproval: false, "maker", TimeProvider.System.GetUtcNow()));
        await platform.SaveChangesAsync(ct);
        if (commit)
        {
            await work.CommitAsync(ct);
        }
    }
}
