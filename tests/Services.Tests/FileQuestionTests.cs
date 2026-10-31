using System.Text;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>File questions: uploads checked against the question, kept with the request, required only for some answers.</summary>
public sealed class FileQuestionTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly string _service = TestHost.Unique("FILES");
    private readonly string _hasLicense = TestHost.Unique("HAS_LICENSE");
    private readonly string _license = TestHost.Unique("LICENSE_FILE");
    private readonly string _photos = TestHost.Unique("PHOTOS");
    private Applicant _applicant = null!;

    public async ValueTask InitializeAsync()
    {
        _applicant = await Applicant.CreateAsync(fixture);
        var licensed = ServiceSamples.Rule($$"""{"==": [{"var": "{{_hasLicense}}"}, "YES"]}""");
        var definition = new VersionDefinition
        {
            Steps =
            [
                new()
                {
                    Code = "DOCS",
                    Type = StepType.Form,
                    TitleAr = "المرفقات",
                    TitleEn = "Attachments",
                    Sections =
                    [
                        new()
                        {
                            Code = "MAIN",
                            TitleAr = "المرفقات",
                            TitleEn = "Attachments",
                            Questions =
                            [
                                new() { Code = _hasLicense, Required = true },
                                new() { Code = _license, RequiredWhen = licensed, VisibleWhen = licensed },
                                new() { Code = _photos },
                            ],
                        },
                    ],
                },
                new() { Code = "DECLARATION", Type = StepType.Declaration, TitleAr = "الإقرار", TitleEn = "Declaration", TextAr = "أقر", TextEn = "I confirm" },
            ],
        };
        await _applicant.Host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Question, _hasLicense, ChangeOperation.Create, new QuestionPayload
            {
                NameAr = "لديك ترخيص؟",
                NameEn = "Do you have a license?",
                Type = QuestionType.Select,
                Options = [new() { Value = "YES", LabelAr = "نعم", LabelEn = "Yes" }, new() { Value = "NO", LabelAr = "لا", LabelEn = "No" }],
            }),
            new Change(ServiceChangeTypes.Question, _license, ChangeOperation.Create, new QuestionPayload
            {
                NameAr = "صورة الترخيص",
                NameEn = "License copy",
                Type = QuestionType.File,
                Validation = new QuestionValidation { Extensions = [".PDF"], MaxSizeMb = 1 },
            }),
            new Change(ServiceChangeTypes.Question, _photos, ChangeOperation.Create, new QuestionPayload
            {
                NameAr = "صور الموقع",
                NameEn = "Site photos",
                Type = QuestionType.File,
                Multiple = true,
                Validation = new QuestionValidation { MaxFiles = 2 },
            }),
            new Change(ServiceChangeTypes.Service, _service, ChangeOperation.Create, new ServicePayload
            {
                NameAr = "خدمة بمرفقات",
                NameEn = "Service with attachments",
                Archetype = "ISSUANCE",
                PartyKinds = [PartyKind.Establishment],
                NumberPrefix = "FIL",
            }),
            new Change(ServiceChangeTypes.Version, $"{_service}/1", ChangeOperation.Create, new ServiceVersionPayload { NameAr = "الإصدار 1", NameEn = "Version 1", Definition = definition }));
    }

    public async ValueTask DisposeAsync() => await _applicant.DisposeAsync();

    private Task<T> Requests<T>(Func<RequestService, Task<T>> action) => _applicant.AsAsync(sp => action(sp.GetRequiredService<RequestService>()));

    private Task<RequestFileInfo> UploadAsync(Guid request, string question, string name, int bytes = 100) =>
        Requests(s => s.UploadFileAsync(request, question, name, bytes, new MemoryStream(Encoding.UTF8.GetBytes(new string('x', bytes))), CancellationToken.None));

    private Task<RequestView> SaveAsync(Guid request, JsonObject data) =>
        Requests(s => s.SaveAsync(request, new SaveRequest(data, null, "DOCS", "DOCS"), CancellationToken.None));

    [Fact]
    public async Task A_file_is_required_for_one_answer_and_checked_against_the_question()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_service), CancellationToken.None));
        var id = draft.Request.Id;

        // "Yes" makes the license copy required.
        var missing = await SaveAsync(id, new JsonObject { [_hasLicense] = "YES" });
        missing.StepErrors.Select(e => (e.Field, e.Code)).ShouldBe([(_license, "REQUIRED")]);

        // Only PDF, at most 1 MB.
        (await Should.ThrowAsync<RequestValidationException>(() => UploadAsync(id, _license, "license.exe"))).Errors.Single().Code.ShouldBe("FILE_TYPE");
        (await Should.ThrowAsync<RequestValidationException>(() => UploadAsync(id, _license, "license.pdf", 2 * 1024 * 1024))).Errors.Single().Code.ShouldBe("FILE_SIZE");
        (await Should.ThrowAsync<RequestValidationException>(() => UploadAsync(id, _hasLicense, "license.pdf"))).Errors.Single().Code.ShouldBe("INVALID");

        // One file: a second upload replaces the first.
        await UploadAsync(id, _license, "old.pdf");
        var file = await UploadAsync(id, _license, "license.pdf");
        file.ContentType.ShouldBe("application/pdf");
        var view = await Requests(s => s.GetAsync(id, CancellationToken.None));
        view.Files!.Where(f => f.QuestionCode == _license).Select(f => f.FileName).ShouldBe(["license.pdf"]);
        RequestFiles.Ids(view.Data[_license]).ShouldBe([file.Id.ToString()]);

        // Answers keep only this request's files.
        var forged = await SaveAsync(id, new JsonObject { [_hasLicense] = "YES", [_license] = new JsonArray(file.Id.ToString(), Guid.NewGuid().ToString()) });
        forged.StepErrors.ShouldBeEmpty();
        RequestFiles.Ids(forged.Data[_license]).ShouldBe([file.Id.ToString()]);

        // Several files up to the question's limit.
        await UploadAsync(id, _photos, "front.jpg");
        await UploadAsync(id, _photos, "back.png");
        (await Should.ThrowAsync<RequestValidationException>(() => UploadAsync(id, _photos, "side.jpg"))).Errors.Single().Code.ShouldBe("FILE_COUNT");

        var submitted = await Requests(s => s.SubmitAsync(id, new SubmitRequest(true), CancellationToken.None));
        submitted.Request.Status.ShouldNotBe(StatusCategory.Draft);

        // Staff see and open the files; the applicant can no longer change them.
        var detail = await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<RequestAdminService>().GetAsync(id, CancellationToken.None));
        detail.View.Files!.Count.ShouldBe(3);
        var (content, contentType, name) = await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<RequestAdminService>().OpenFileAsync(id, file.Id, CancellationToken.None));
        await using (content)
        {
            (contentType, name).ShouldBe(("application/pdf", "license.pdf"));
        }

        await Should.ThrowAsync<RequestConflictException>(() => Requests(async s =>
        {
            await s.DeleteFileAsync(id, file.Id, CancellationToken.None);
            return true;
        }));
    }

    [Fact]
    public async Task No_file_is_asked_for_the_other_answer()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_service), CancellationToken.None));
        var view = await SaveAsync(draft.Request.Id, new JsonObject { [_hasLicense] = "NO" });

        view.StepErrors.ShouldBeEmpty();
    }
}
