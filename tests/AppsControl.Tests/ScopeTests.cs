using System.Text.Json;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>Entities and business apps: every configuration row is at system, entity or app level, and staff work within theirs.</summary>
public sealed class ScopeTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private static Change App(string code, string? entity = null) =>
        new(AppsControlChangeTypes.BusinessApp, code, ChangeOperation.Create, new { nameAr = "تطبيق " + code, nameEn = "App " + code, entityCode = entity, icon = "apps", companies = new[] { "*" }, branches = new[] { "*" } });

    private static Change Class(string code, string? app = null) =>
        new(AppsControlChangeTypes.Class, code, ChangeOperation.Create, new { nameAr = "تصنيف", nameEn = "Class", attributes = Array.Empty<object>(), businessApp = app });

    private Task<IReadOnlyList<JsonElement>> ListAsync(string user, string type, string? app = null) =>
        _harness.AsAsync(user, sp => sp.GetServices<IConfigApplier>().Single(a => a.EntityType == type).ListAsync(null, CancellationToken.None, app));

    private Task AddAsync(string user, string area, Change change) =>
        _harness.AsAsync(user, async sp =>
        {
            var service = sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>();
            var changeSet = await service.CreateAsync(area, "scope test", CancellationToken.None);
            await service.AddItemAsync(changeSet.Id, change.EntityType, change.EntityCode, change.Operation, JsonSerializer.Serialize(change.Payload), CancellationToken.None);
        });

    [Fact]
    public async Task Staff_of_a_business_app_see_and_change_only_its_rows_and_the_shared_ones()
    {
        var (appA, appB, entity) = (Harness.Unique("APP_A"), Harness.Unique("APP_B"), Harness.Unique("ENT"));
        var (classA, classB, shared) = (Harness.Unique("CLS_A"), Harness.Unique("CLS_B"), Harness.Unique("CLS_SHARED"));
        var role = Harness.Unique("CLERK");
        var clerk = Harness.Unique("clerk").ToLowerInvariant();
        await _harness.PublishAsync(
            AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Entity, entity, ChangeOperation.Create, new { nameAr = "جهة", nameEn = "Second entity" }),
            App(appA),
            App(appB, entity));
        await _harness.PublishAsync(AppsControlAreas.Lookups, Class(classA, appA), Class(classB, appB), Class(shared));
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new
            {
                nameAr = "كاتب",
                nameEn = "Clerk",
                businessApp = appA,
                privileges = new[] { new { resourceCode = AppsControlResources.Classes, actions = new[] { "VIEW", "CREATE", "EDIT" } } },
            }),
            new Change(AppsControlChangeTypes.User, clerk, ChangeOperation.Create, new { nameAr = "كاتب", nameEn = "Clerk", roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } } }));

        // An app inherits its entity; a row of an app takes the app's entity.
        var apps = await ListAsync("admin", AppsControlChangeTypes.BusinessApp);
        apps.Single(a => a.GetProperty("code").GetString() == appA).GetProperty("entityCode").GetString().ShouldBe("ENTITY");
        var classes = await ListAsync("admin", AppsControlChangeTypes.Class);
        classes.Single(c => c.GetProperty("code").GetString() == classB).GetProperty("entityCode").GetString().ShouldBe(entity);

        var access = await _harness.AccessAsync(clerk);
        access.Scope.Apps.ShouldBe([appA]);
        var seen = (await ListAsync(clerk, AppsControlChangeTypes.Class)).Select(c => c.GetProperty("code").GetString()).ToList();
        seen.ShouldContain(classA);
        seen.ShouldContain(shared, "rows of no app are seen by everyone");
        seen.ShouldNotContain(classB);

        // The admin's list narrowed to one app: that app's rows and the shared ones.
        var underB = (await ListAsync("admin", AppsControlChangeTypes.Class, appB)).Select(c => c.GetProperty("code").GetString()).ToList();
        underB.ShouldContain(classB);
        underB.ShouldContain(shared);
        underB.ShouldNotContain(classA);

        // A new row without a level takes the clerk's app; another app or a shared row is refused.
        var mine = Harness.Unique("CLS_MINE");
        await AddAsync(clerk, AppsControlAreas.Lookups, Class(mine));
        var drafted = await _harness.AsAsync(clerk, async sp =>
        {
            var applier = sp.GetServices<IConfigApplier>().Single(a => a.EntityType == AppsControlChangeTypes.Class);
            return applier.Normalize(mine, JsonSerializer.Serialize(new { nameAr = "x", nameEn = "x" }));
        });
        drafted.ShouldNotBeNull();
        (await Should.ThrowAsync<ChangeValidationException>(() => AddAsync(clerk, AppsControlAreas.Lookups, Class(Harness.Unique("CLS_X"), appB)))).Errors.Keys.ShouldContain("businessApp");
        (await Should.ThrowAsync<ChangeValidationException>(() => AddAsync(clerk, AppsControlAreas.Lookups, Class(Harness.Unique("CLS_Y"), "")))).Errors.Keys.ShouldContain("entityCode");
        (await Should.ThrowAsync<ChangeValidationException>(() => AddAsync(
            clerk, AppsControlAreas.Lookups, new Change(AppsControlChangeTypes.Class, shared, ChangeOperation.Update, new { nameAr = "x", nameEn = "x", attributes = Array.Empty<object>() })))).Errors.Keys.ShouldContain("code");

        // An app of another entity cannot be given with this entity.
        var mismatch = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Class, Harness.Unique("CLS_Z"), ChangeOperation.Create, new { nameAr = "x", nameEn = "x", attributes = Array.Empty<object>(), entityCode = "ENTITY", businessApp = appB })));
        mismatch.Errors.Keys.ShouldContain("businessApp");
    }

    [Fact]
    public async Task A_new_row_of_an_app_limited_maker_is_saved_in_their_app()
    {
        var app = Harness.Unique("APP_C");
        var role = Harness.Unique("MAKER_C");
        var maker = Harness.Unique("makerc").ToLowerInvariant();
        await _harness.PublishAsync(AppsControlAreas.System, App(app));
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new { nameAr = "صانع", nameEn = "Maker", businessApp = app }),
            new Change(AppsControlChangeTypes.User, maker, ChangeOperation.Create, new { nameAr = "صانع", nameEn = "Maker", roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } } }));

        var code = Harness.Unique("CLS_C");
        var id = await _harness.AsAsync(maker, async sp =>
        {
            var service = sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>();
            var changeSet = await service.CreateAsync(AppsControlAreas.Lookups, "scope test", CancellationToken.None);
            await service.AddItemAsync(changeSet.Id, AppsControlChangeTypes.Class, code, ChangeOperation.Create, JsonSerializer.Serialize(Class(code).Payload), CancellationToken.None);
            await service.SubmitAsync(changeSet.Id, CancellationToken.None);
            return changeSet.Id;
        });
        await _harness.AsAsync("checker", async sp =>
        {
            var service = sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>();
            await service.ApproveAsync(id, "ok", CancellationToken.None);
            await service.PublishAsync(id, CancellationToken.None);
        });

        // Published by the checker, the row still takes the maker's app.
        var row = (await ListAsync("admin", AppsControlChangeTypes.Class)).Single(c => c.GetProperty("code").GetString() == code);
        row.GetProperty("businessApp").GetString().ShouldBe(app);
        row.GetProperty("entityCode").GetString().ShouldBe("ENTITY");
    }

    [Fact]
    public async Task A_row_may_serve_several_business_apps()
    {
        var (appA, appB, appC) = (Harness.Unique("APP_SA"), Harness.Unique("APP_SB"), Harness.Unique("APP_SC"));
        var (onlyA, both) = (Harness.Unique("CLERK_A"), Harness.Unique("CLERK_AB"));
        var (clerkA, clerkAb) = (Harness.Unique("clerka").ToLowerInvariant(), Harness.Unique("clerkab").ToLowerInvariant());
        await _harness.PublishAsync(AppsControlAreas.System, App(appA), App(appB), App(appC));
        var privileges = new[] { new { resourceCode = AppsControlResources.Classes, actions = new[] { "VIEW", "CREATE", "EDIT" } } };
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, onlyA, ChangeOperation.Create, new { nameAr = "أ", nameEn = "A", businessApp = appA, privileges }),
            new Change(AppsControlChangeTypes.Role, both, ChangeOperation.Create, new { nameAr = "أب", nameEn = "AB", businessApps = new[] { appA, appB }, privileges }),
            new Change(AppsControlChangeTypes.User, clerkA, ChangeOperation.Create, new { nameAr = "أ", nameEn = "A", roles = new[] { new { roleCode = onlyA, companyCode = "*", branches = new[] { "*" } } } }),
            new Change(AppsControlChangeTypes.User, clerkAb, ChangeOperation.Create, new { nameAr = "أب", nameEn = "AB", roles = new[] { new { roleCode = both, companyCode = "*", branches = new[] { "*" } } } }));

        // A role of two apps gives both; a class shared by A and B is listed under each, not under C.
        (await _harness.AccessAsync(clerkAb)).Scope.Apps!.ShouldBe([appA, appB], ignoreOrder: true);
        var shared = Harness.Unique("CLS_SHARED_AB");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Class, shared, ChangeOperation.Create, new { nameAr = "مشترك", nameEn = "Shared", attributes = Array.Empty<object>(), businessApps = new[] { appA, appB } }));
        var row = (await ListAsync("admin", AppsControlChangeTypes.Class, appB)).Single(c => c.GetProperty("code").GetString() == shared);
        row.GetProperty("businessApp").GetString().ShouldBe(appA);
        row.GetProperty("businessApps").EnumerateArray().Select(a => a.GetString()).ShouldBe([appA, appB]);
        (await ListAsync("admin", AppsControlChangeTypes.Class, appC)).ShouldNotContain(c => c.GetProperty("code").GetString() == shared);

        // Staff of A see it; changing it needs both apps.
        (await ListAsync(clerkA, AppsControlChangeTypes.Class)).ShouldContain(c => c.GetProperty("code").GetString() == shared);
        var update = new Change(AppsControlChangeTypes.Class, shared, ChangeOperation.Update, new { nameAr = "مشترك ٢", nameEn = "Shared 2", attributes = Array.Empty<object>() });
        (await Should.ThrowAsync<ChangeValidationException>(() => AddAsync(clerkA, AppsControlAreas.Lookups, update))).Errors.Keys.ShouldContain("code");
        await AddAsync(clerkAb, AppsControlAreas.Lookups, update);
    }
}
