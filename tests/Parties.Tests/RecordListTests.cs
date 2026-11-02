using System.Net;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Parties.Tests;

/// <summary>Record lists: records typed by hand and checked against the columns, or imported and updated by their key.</summary>
public sealed class RecordListTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly TestApplicant _applicant = new() { NationalId = PartyTestSupport.NationalId(), Name = "Sara Ali" };
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = PartyTestSupport.Host(fixture, _applicant);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<T> Records<T>(Func<RecordService, Task<T>> action) => _host.AsAsync("admin", sp => action(sp.GetRequiredService<RecordService>()));

    private static List<RecordColumn> Columns() =>
    [
        new() { Code = "CR", LabelAr = "السجل", LabelEn = "CR", Type = RecordColumnType.Text, Required = true, InTable = true },
        new() { Code = "NAME", LabelAr = "الاسم", LabelEn = "Name", Type = RecordColumnType.Text, Required = true },
        new() { Code = "VALUE", LabelAr = "القيمة", LabelEn = "Value", Type = RecordColumnType.Number },
        new() { Code = "SINCE", LabelAr = "منذ", LabelEn = "Since", Type = RecordColumnType.Date },
        new()
        {
            Code = "STATUS",
            LabelAr = "الحالة",
            LabelEn = "Status",
            Type = RecordColumnType.Select,
            Options = [new() { Value = "Active", LabelAr = "نشط", LabelEn = "Active" }, new() { Value = "Inactive", LabelAr = "غير نشط", LabelEn = "Inactive" }],
        },
    ];

    [Fact]
    public async Task Records_typed_by_hand_are_checked_and_kept_per_party()
    {
        var list = TestHost.Unique("NOTES_LIST");
        await _host.PublishAsync(PartyChangeTypes.Area, new Change(PartyChangeTypes.RecordList, list, ChangeOperation.Create, new RecordListPayload
        {
            NameAr = "سجلات",
            NameEn = "Records",
            PartyKind = PartyKind.Individual,
            Columns = Columns(),
        }));
        var individual = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None));

        var invalid = await Should.ThrowAsync<RecordValidationException>(() =>
            Records(s => s.AddAsync(individual.Id, list, new JsonObject { ["CR"] = "1", ["VALUE"] = "many", ["SINCE"] = "yesterday", ["STATUS"] = "Gone" }, CancellationToken.None)));
        invalid.Errors.Keys.ShouldBe(["NAME", "VALUE", "SINCE", "STATUS"], ignoreOrder: true);

        var added = await Records(s => s.AddAsync(individual.Id, list, new JsonObject { ["CR"] = "1010", ["NAME"] = "Shop", ["VALUE"] = "12.5", ["SINCE"] = "2024-03-01T00:00:00", ["OTHER"] = "dropped" }, CancellationToken.None));
        added.Data.ToJsonString().ShouldBe("""{"CR":"1010","NAME":"Shop","VALUE":12.5,"SINCE":"2024-03-01"}""");
        added.Source.ShouldBe(RecordSource.Manual);

        var changed = await Records(s => s.UpdateAsync(individual.Id, added.Id, new JsonObject { ["CR"] = "1010", ["NAME"] = "Big shop", ["STATUS"] = "Active" }, CancellationToken.None));
        changed.Data["NAME"]!.GetValue<string>().ShouldBe("Big shop");
        (await Records(s => s.ListAsync(individual.Id, list, CancellationToken.None))).Records.Count.ShouldBe(1);

        // Another party's record cannot be reached through this party.
        await Should.ThrowAsync<PartyNotFoundException>(() => Records(async s =>
        {
            await s.DeleteAsync(Guid.NewGuid(), added.Id, CancellationToken.None);
            return true;
        }));
        await Records(async s =>
        {
            await s.DeleteAsync(individual.Id, added.Id, CancellationToken.None);
            return true;
        });
        (await Records(s => s.ListAsync(individual.Id, list, CancellationToken.None))).Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Imported_records_are_updated_by_their_key_and_kept_apart_from_typed_ones()
    {
        var prefix = "/v1/" + TestHost.Unique("R").ToLowerInvariant();
        var operation = await MomrahSamples.PublishAsync(_host, retryCount: 0, operation: (c, e) => MomrahSamples.CrByIdNumber(c, e, path: prefix + "/{idNumber}"));
        var list = TestHost.Unique("MY_CRS");
        await _host.PublishAsync(PartyChangeTypes.Area, new Change(PartyChangeTypes.RecordList, list, ChangeOperation.Create, new RecordListPayload
        {
            NameAr = "سجلاتي التجارية",
            NameEn = "My commercial registrations",
            PartyKind = PartyKind.Individual,
            Columns = Columns(),
            Import = new RecordImport
            {
                OperationCode = operation,
                Inputs = [new() { Parameter = "idNumber", IdentifierType = IdentifierTypes.NationalId }],
                ItemsPath = "Licenses",
                KeyColumn = "CR",
                Fields = [new() { Column = "CR", Path = "Commercial_NO" }, new() { Column = "NAME", Path = "Commercial_Name" }, new() { Column = "STATUS", Path = "Status" }],
            },
        }));
        var individual = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None));
        await Records(s => s.AddAsync(individual.Id, list, new JsonObject { ["CR"] = "999", ["NAME"] = "Typed" }, CancellationToken.None));

        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, PartyTestSupport.Licenses(("4030001", "Alpha", "1", "Active"), ("4030002", "Beta", "1", "Unknown"))));
        var first = await Records(s => s.ImportAsync(individual.Id, list, CancellationToken.None));
        (first.Outcome, first.Added, first.Updated, first.Removed).ShouldBe((IntegrationOutcome.Success, 2, 0, 0));

        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, PartyTestSupport.Licenses(("4030001", "Alpha renamed", "1", "Inactive"))));
        var second = await Records(s => s.ImportAsync(individual.Id, list, CancellationToken.None));
        (second.Added, second.Updated, second.Removed).ShouldBe((0, 1, 1));

        var records = (await Records(s => s.ListAsync(individual.Id, list, CancellationToken.None))).Records;
        records.Select(r => (r.Source, r.Data["NAME"]!.GetValue<string>())).ShouldBe([(RecordSource.Manual, "Typed"), (RecordSource.Integration, "Alpha renamed")], ignoreOrder: true);
        var imported = records.Single(r => r.Source == RecordSource.Integration);
        imported.Key.ShouldBe("4030001");
        imported.Data["STATUS"]!.GetValue<string>().ShouldBe("Inactive");

        // An imported record changes only through a new import.
        await Should.ThrowAsync<PartyConflictException>(() => Records(s => s.UpdateAsync(individual.Id, imported.Id, new JsonObject { ["CR"] = "x", ["NAME"] = "x" }, CancellationToken.None)));
    }

    [Fact]
    public async Task A_record_list_is_checked_when_it_is_saved()
    {
        var refused = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(PartyChangeTypes.Area, new Change(PartyChangeTypes.RecordList, TestHost.Unique("BAD_LIST"), ChangeOperation.Create, new RecordListPayload
        {
            NameAr = "س",
            NameEn = "x",
            AllowManual = false,
            Columns =
            [
                new() { Code = "A", LabelAr = "أ", LabelEn = "A", Type = RecordColumnType.Select },
                new() { Code = "A", LabelAr = "ب", LabelEn = "B" },
            ],
        })));
        refused.Errors.Keys.ShouldBe(["columns[0].options", "columns[1].code", "allowManual"], ignoreOrder: true);

        var badImport = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(PartyChangeTypes.Area, new Change(PartyChangeTypes.RecordList, TestHost.Unique("BAD_IMPORT"), ChangeOperation.Create, new RecordListPayload
        {
            NameAr = "س",
            NameEn = "x",
            Columns = [new() { Code = "A", LabelAr = "أ", LabelEn = "A" }],
            Import = new RecordImport { OperationCode = "SOME_OP", ItemsPath = "a..b", KeyColumn = "Z", Fields = [new() { Column = "Q", Path = "x" }] },
        })));
        badImport.Errors.Keys.ShouldBe(["import.itemsPath", "import.keyColumn", "import.inputs", "import.fields[0].column"], ignoreOrder: true);
    }
}
