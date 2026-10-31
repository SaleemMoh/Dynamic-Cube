using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>Follow-up services: requested for one of the applicant's documents, within a window, and amending that document.</summary>
public sealed class FollowUpServiceTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_follow_up_request_is_made_for_a_document_within_its_window_and_amends_it()
    {
        await using var applicant = await Applicant.CreateAsync(fixture);
        var host = applicant.Host;
        var type = await DocumentTypeAsync(host);
        var issued = await host.AsAsync("admin", sp => sp.GetRequiredService<IDocumentIssuer>().IssueAsync(
            new DocumentIssue(type, applicant.EstablishmentId, new JsonObject { ["grade"] = 2 }, [], "TEST", Guid.CreateVersion7(), null, "First issue"), CancellationToken.None));

        var number = TestHost.Unique("CERT_NO");
        var amend = TestHost.Unique("AMEND");
        var renew = TestHost.Unique("RENEW");
        await host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Question, number, ChangeOperation.Create, new QuestionPayload { NameAr = "رقم الشهادة", NameEn = "Certificate number", Type = QuestionType.Text, Prefill = "document.number" }),
            Service(amend, type),
            Version(amend, new AvailabilityDefinition { From = new DateBound { Of = DocumentDate.Issued, Days = 0 } }, number),
            Service(renew, type),
            Version(renew, new AvailabilityDefinition { From = new DateBound { Of = DocumentDate.Expires, Days = -90 }, To = new DateBound { Of = DocumentDate.Expires, Days = 0 } }, number));

        // The amendment's workflow renews the document the request was made for.
        await host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Version, amend + "/2", ChangeOperation.Create, new
        {
            nameAr = "تجديد",
            nameEn = "Renew",
            definition = new
            {
                nodes = new object[]
                {
                    new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "AMEND" },
                    new { code = "AMEND", type = "DOCUMENT", nameAr = "تعديل", nameEn = "Amend", documentType = type, target = "SOURCE", validity = "RENEW", data = new { renewedBy = new { var = "number" } }, next = "END" },
                    new { code = "END", type = "END", nameAr = "نهاية", nameEn = "End", outcome = "APPROVED" },
                },
            },
        }));

        // The document page lists both services: the amendment open now, the renewal from 90 days before expiry.
        var followUps = await applicant.AsAsync(sp => Requests(sp).FollowUpsAsync(issued.Id, CancellationToken.None));
        var open = followUps.Single(f => f.Service.Code == amend);
        open.Document.IsAvailable.ShouldBeTrue();
        open.Service.IsEligible.ShouldBeTrue();
        var later = followUps.Single(f => f.Service.Code == renew);
        later.Document.IsAvailable.ShouldBeFalse();
        later.Document.Reasons.Single().Code.ShouldBe("NOT_YET_OPEN");
        later.Document.OpensOn.ShouldBe(DateOnly.FromDateTime(issued.ExpiresAt!.Value.UtcDateTime).AddDays(-90));
        later.Document.ClosesOn.ShouldBe(DateOnly.FromDateTime(issued.ExpiresAt!.Value.UtcDateTime));

        var view = await applicant.AsAsync(sp => Requests(sp).GetServiceAsync(amend, CancellationToken.None));
        view.Documents.Single().Number.ShouldBe(issued.Number);
        view.Service.DocumentType.ShouldBe(type);

        // A request names its document; the answers can start from it.
        await Should.ThrowAsync<RequestValidationException>(() => applicant.AsAsync(sp => Requests(sp).CreateAsync(new CreateRequest(amend), CancellationToken.None)));
        await Should.ThrowAsync<RequestNotFoundException>(() => applicant.AsAsync(sp => Requests(sp).CreateAsync(new CreateRequest(amend, Guid.CreateVersion7()), CancellationToken.None)));
        var draft = await applicant.AsAsync(sp => Requests(sp).CreateAsync(new CreateRequest(amend, issued.Id), CancellationToken.None));
        draft.Data[number]!.GetValue<string>().ShouldBe(issued.Number);
        draft.Request.SourceDocumentNumber.ShouldBe(issued.Number);

        // One open request per document and service.
        var twice = await Should.ThrowAsync<NotEligibleException>(() => applicant.AsAsync(sp => Requests(sp).CreateAsync(new CreateRequest(amend, issued.Id), CancellationToken.None)));
        twice.Reasons.Single().Code.ShouldBe("OPEN_REQUEST");
        var locked = await Should.ThrowAsync<NotEligibleException>(() => applicant.AsAsync(sp => Requests(sp).CreateAsync(new CreateRequest(renew, issued.Id), CancellationToken.None)));
        locked.Reasons.Single().Code.ShouldBe("NOT_YET_OPEN");

        // Approval renews the same document: same number, a new version, a full period added to its expiry.
        var submitted = await applicant.AsAsync(sp => Requests(sp).SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        submitted.Request.Status.ShouldBe(StatusCategory.Approved);
        var document = (await host.AsAsync("admin", sp => sp.GetRequiredService<IDocumentDirectory>().GetAsync(issued.Id, CancellationToken.None)))!;
        document.Number.ShouldBe(issued.Number);
        document.Version.ShouldBe(2);
        document.ExpiresAt!.Value.ShouldBe(issued.ExpiresAt.Value.AddMonths(12), TimeSpan.FromMilliseconds(1));
        document.Data["grade"]!.GetValue<int>().ShouldBe(2);
        document.Data["renewedBy"]!.GetValue<string>().ShouldBe(submitted.Request.Number);

        // Someone else's document is not theirs to use, and the catalog says why a service is closed.
        await Should.ThrowAsync<RequestNotFoundException>(() => applicant.AsAsync(sp => Requests(sp).FollowUpsAsync(issued.Id, CancellationToken.None), self: true));
        var catalog = await applicant.AsAsync(sp => Requests(sp).CatalogAsync(CancellationToken.None));
        catalog.Single(e => e.Code == renew).Reasons.Single().Code.ShouldBe("NOT_YET_OPEN");
        catalog.Single(e => e.Code == amend).IsEligible.ShouldBeTrue("the earlier request is closed");
        var self = await applicant.AsAsync(sp => Requests(sp).CatalogAsync(CancellationToken.None), self: true);
        self.Single(e => e.Code == amend).Reasons.ShouldContain(r => r.Code == "NO_DOCUMENT");
    }

    [Fact]
    public async Task Only_services_with_a_document_type_have_an_availability()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        var service = TestHost.Unique("PLAIN");
        var question = TestHost.Unique("Q");
        Change Question() => new(ServiceChangeTypes.Question, question, ChangeOperation.Create, new QuestionPayload { NameAr = "س", NameEn = "Q", Type = QuestionType.Text });

        var unknown = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Service, service, ChangeOperation.Create, new ServicePayload { NameAr = "خدمة", NameEn = "Service", PartyKinds = [PartyKind.Establishment], DocumentType = "NO_SUCH_TYPE" })));
        unknown.Errors.Keys.ShouldContain("documentType");

        var type = await DocumentTypeAsync(host);
        var window = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(
            ServiceChangeTypes.Area,
            Question(),
            Service(service, type),
            Version(service, new AvailabilityDefinition { Statuses = ["LOST"], From = new DateBound { Of = DocumentDate.Expires, Days = 10 }, To = new DateBound { Of = DocumentDate.Expires, Days = -10 } }, question)));
        window.Errors.Keys.ShouldContain("definition.availability.statuses");
        window.Errors.Keys.ShouldContain("definition.availability.to");

        var plain = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(
            ServiceChangeTypes.Area,
            Question(),
            new Change(ServiceChangeTypes.Service, service, ChangeOperation.Create, new ServicePayload { NameAr = "خدمة", NameEn = "Service", PartyKinds = [PartyKind.Establishment] }),
            Version(service, new AvailabilityDefinition(), question)));
        plain.Errors["definition.availability"].Single().ShouldContain("does not act on a document");
    }

    private static RequestService Requests(IServiceProvider services) => services.GetRequiredService<RequestService>();

    private static Change Service(string code, string documentType) =>
        new(ServiceChangeTypes.Service, code, ChangeOperation.Create, new ServicePayload
        {
            NameAr = "خدمة لاحقة",
            NameEn = "Follow-up service",
            Archetype = "AMENDMENT",
            PartyKinds = [PartyKind.Establishment],
            DocumentType = documentType,
        });

    private static Change Version(string service, AvailabilityDefinition availability, string question) =>
        new(ServiceChangeTypes.Version, service + "/1", ChangeOperation.Create, new ServiceVersionPayload
        {
            NameAr = "الإصدار 1",
            NameEn = "Version 1",
            Definition = new VersionDefinition
            {
                Availability = availability,
                Steps =
                [
                    new StepDefinition
                    {
                        Code = "DETAILS",
                        Type = StepType.Form,
                        TitleAr = "البيانات",
                        TitleEn = "Details",
                        Sections = [new SectionDefinition { Code = "CERT", TitleAr = "الشهادة", TitleEn = "Certificate", Questions = [new QuestionUse { Code = question, Required = true, Readonly = true }] }],
                    },
                ],
            },
        });

    private static async Task<string> DocumentTypeAsync(TestHost host)
    {
        var template = TestHost.Unique("TPL");
        var type = TestHost.Unique("CERT");
        await host.PublishAsync(
            DocumentChangeTypes.Area,
            new Change(DocumentChangeTypes.Template, template, ChangeOperation.Create, new { nameAr = "قالب", nameEn = "Template", html = "<p>{{ document.number }}</p>" }),
            new Change(DocumentChangeTypes.DocumentType, type, ChangeOperation.Create, new
            {
                nameAr = "شهادة",
                nameEn = "certificate",
                numberPrefix = "FU" + type[^4..],
                validityMonths = 12,
                onePerHolder = true,
                templateCode = template,
            }));
        return type;
    }
}
