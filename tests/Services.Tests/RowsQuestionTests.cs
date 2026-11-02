using System.Text;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>Rows questions: a list of rows with their own columns, checked row by row; files kept per column; rules over rows.</summary>
public sealed class RowsQuestionTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly string _service = TestHost.Unique("ROWS");
    private readonly string _projects = TestHost.Unique("PROJECTS");
    private Applicant _applicant = null!;

    private static readonly JsonNode Government = ServiceSamples.Rule("""{"==": [{"var": "OWNER"}, "GOV"]}""");

    public async ValueTask InitializeAsync()
    {
        _applicant = await Applicant.CreateAsync(fixture);
        var definition = new VersionDefinition
        {
            Steps =
            [
                new()
                {
                    Code = "PROJECTS",
                    Type = StepType.Form,
                    TitleAr = "المشاريع",
                    TitleEn = "Projects",
                    Sections = [new() { Code = "MAIN", TitleAr = "المشاريع", TitleEn = "Projects", Questions = [new() { Code = _projects, Required = true }] }],
                },
                new() { Code = "DECLARATION", Type = StepType.Declaration, TitleAr = "الإقرار", TitleEn = "Declaration", TextAr = "أقر", TextEn = "I confirm" },
            ],
            // The total value of the projects must reach 10.
            Validations =
            [
                new()
                {
                    Code = "TOTAL",
                    Step = "PROJECTS",
                    Rule = ServiceSamples.Rule($$"""{">=": [{"reduce": [{"var": "data.{{_projects}}"}, {"+": [{"var": "accumulator"}, {"var": "current.VALUE"}]}, 0]}, 10]}"""),
                    MessageAr = "مجموع القيم أقل من 10",
                    MessageEn = "The total value is below 10",
                },
            ],
        };
        await _applicant.Host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Question, _projects, ChangeOperation.Create, Projects()),
            new Change(ServiceChangeTypes.Service, _service, ChangeOperation.Create, new ServicePayload
            {
                NameAr = "خدمة بمشاريع",
                NameEn = "Service with projects",
                Archetype = "ISSUANCE",
                PartyKinds = [PartyKind.Establishment],
                NumberPrefix = "ROW",
            }),
            new Change(ServiceChangeTypes.Version, $"{_service}/1", ChangeOperation.Create, new ServiceVersionPayload { NameAr = "الإصدار 1", NameEn = "Version 1", Definition = definition }));
    }

    private static QuestionPayload Projects() => new()
    {
        NameAr = "المشاريع",
        NameEn = "Projects",
        Type = QuestionType.Rows,
        Validation = new QuestionValidation { MaxRows = 3 },
        Columns =
        [
            new() { Code = "NAME", LabelAr = "الاسم", LabelEn = "Name", Type = QuestionType.Text, Required = true, InTable = true },
            new()
            {
                Code = "OWNER",
                LabelAr = "المالك",
                LabelEn = "Owner",
                Type = QuestionType.Select,
                Required = true,
                Options = [new() { Value = "GOV", LabelAr = "حكومي", LabelEn = "Government" }, new() { Value = "PRIVATE", LabelAr = "خاص", LabelEn = "Private" }],
            },
            new() { Code = "VALUE", LabelAr = "القيمة", LabelEn = "Value", Type = QuestionType.Number, Required = true, Validation = new QuestionValidation { Min = 0 } },
            // Only government projects need the approval letter.
            new() { Code = "APPROVAL", LabelAr = "خطاب الموافقة", LabelEn = "Approval letter", Type = QuestionType.File, VisibleWhen = Government, RequiredWhen = Government },
        ],
    };

    public async ValueTask DisposeAsync() => await _applicant.DisposeAsync();

    private Task<T> Requests<T>(Func<RequestService, Task<T>> action) => _applicant.AsAsync(sp => action(sp.GetRequiredService<RequestService>()));

    private Task<RequestView> SaveAsync(Guid request, JsonArray rows) =>
        Requests(s => s.SaveAsync(request, new SaveRequest(new JsonObject { [_projects] = rows }, null, "PROJECTS", "PROJECTS"), CancellationToken.None));

    private static JsonObject Row(string name, string owner, decimal value, string? approval = null)
    {
        var row = new JsonObject { ["NAME"] = name, ["OWNER"] = owner, ["VALUE"] = value };
        if (approval is not null)
        {
            row["APPROVAL"] = new JsonArray(approval);
        }

        return row;
    }

    [Fact]
    public async Task Rows_are_checked_row_by_row_with_files_and_rules_over_them()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_service), CancellationToken.None));
        var id = draft.Request.Id;

        // A government project needs its approval letter (row 2), a negative value is refused (row 1).
        var missing = await SaveAsync(id, [Row("School", "PRIVATE", 4), Row("Hospital", "GOV", 7)]);
        var error = missing.StepErrors.Single();
        (error.Field, error.Code).ShouldBe((_projects, "REQUIRED"));
        error.MessageEn.ShouldBe("Row 2, Approval letter: This field is required.");
        (await SaveAsync(id, [Row("School", "PRIVATE", -1)])).StepErrors.Single().MessageEn.ShouldStartWith("Row 1, Value:");

        // The letter is uploaded under the column and kept in the row; a file of another column or request is dropped.
        var letter = await Requests(s => s.UploadFileAsync(id, $"{_projects}/APPROVAL", "letter.pdf", 10, new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 10))), CancellationToken.None));
        (await Should.ThrowAsync<RequestValidationException>(() =>
            Requests(s => s.UploadFileAsync(id, $"{_projects}/NAME", "x.pdf", 10, new MemoryStream(new byte[10]), CancellationToken.None)))).Errors.Single().Code.ShouldBe("INVALID");
        var saved = await SaveAsync(id, [Row("School", "PRIVATE", 4, Guid.NewGuid().ToString()), Row("Hospital", "GOV", 7, letter.Id.ToString())]);
        saved.StepErrors.ShouldBeEmpty();
        var rows = saved.Data[_projects]!.AsArray();
        rows.Count.ShouldBe(2);
        rows[0]!.AsObject().ContainsKey("APPROVAL").ShouldBeFalse();
        RequestFiles.Ids(rows[1]!["APPROVAL"]).ShouldBe([letter.Id.ToString()]);

        // The version's rule adds the rows up; more rows than the question takes are refused.
        (await SaveAsync(id, [Row("School", "PRIVATE", 4)])).StepErrors.Single().Code.ShouldBe("TOTAL");
        (await SaveAsync(id, [Row("A", "PRIVATE", 4), Row("B", "PRIVATE", 4), Row("C", "PRIVATE", 4), Row("D", "PRIVATE", 4)])).StepErrors.Single().Code.ShouldBe("MAX_ROWS");

        // Removing the file takes it out of its row.
        await Requests(async s =>
        {
            await s.DeleteFileAsync(id, letter.Id, CancellationToken.None);
            return true;
        });
        var after = await Requests(s => s.GetAsync(id, CancellationToken.None));
        after.Data[_projects]!.AsArray()[1]!.AsObject().ContainsKey("APPROVAL").ShouldBeFalse();
    }

    [Fact]
    public async Task A_rows_question_needs_valid_columns()
    {
        var bad = Projects();
        bad.Columns = [new() { Code = "A", LabelAr = "أ", LabelEn = "A", Type = QuestionType.Rows }, new() { Code = "A", LabelAr = "ب", LabelEn = "B", Type = QuestionType.Select }];
        var refused = await Should.ThrowAsync<ChangeValidationException>(() =>
            _applicant.Host.PublishAsync(ServiceChangeTypes.Area, new Change(ServiceChangeTypes.Question, TestHost.Unique("BAD_ROWS"), ChangeOperation.Create, bad)));
        refused.Errors.Keys.ShouldContain("columns[0].type");
        refused.Errors.Keys.ShouldContain("columns[1].code");
        refused.Errors.Keys.ShouldContain("columns[1].options");

        var empty = Projects();
        empty.Columns = [];
        (await Should.ThrowAsync<ChangeValidationException>(() =>
            _applicant.Host.PublishAsync(ServiceChangeTypes.Area, new Change(ServiceChangeTypes.Question, TestHost.Unique("NO_COLUMNS"), ChangeOperation.Create, empty)))).Errors.Keys.ShouldContain("columns");
    }
}
