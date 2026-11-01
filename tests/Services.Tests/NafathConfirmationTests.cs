using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.Nafath;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>Versions that ask for it are submitted only once the applicant confirmed the request with Nafath, as themselves (ADR-0043).</summary>
public sealed class NafathConfirmationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly ServiceSamples _sample = new();
    private readonly FakeNafath _nafath = new();
    private Applicant _applicant = null!;

    public async ValueTask InitializeAsync()
    {
        _applicant = await Applicant.CreateAsync(fixture, configure: s => s.AddSingleton<INafathVerifications>(_nafath));
        var definition = _sample.Definition();
        definition.ConfirmWithNafath = true;
        await _applicant.Host.PublishAsync(ServiceChangeTypes.Area, [.. _sample.Questions(), _sample.ServiceChange(), _sample.Version(1, definition)]);
    }

    public async ValueTask DisposeAsync() => await _applicant.DisposeAsync();

    [Fact]
    public async Task A_request_is_submitted_only_with_the_applicants_own_nafath_confirmation()
    {
        var id = await DraftAsync();

        var missing = await Should.ThrowAsync<RequestValidationException>(() => Requests(s => s.SubmitAsync(id, new SubmitRequest(true), CancellationToken.None)));
        missing.Errors.ShouldContain(e => e.Field == "nafath");

        var challenge = await Requests(s => s.StartNafathAsync(id, "10.0.0.1", CancellationToken.None));
        challenge.Random.ShouldBe("42");
        _nafath.Sent[challenge.CheckId].ShouldBe(_applicant.Identity.NationalId, "sent to the applicant's own app");
        (await Requests(s => s.NafathStatusAsync(id, challenge.CheckId, null, CancellationToken.None))).Status.ShouldBe("WAITING");
        await Should.ThrowAsync<RequestValidationException>(() => Requests(s => s.SubmitAsync(id, new SubmitRequest(true, challenge.CheckId), CancellationToken.None)));

        // Someone else answering does not confirm it.
        _nafath.Answer(challenge.CheckId, "1999999999");
        await Should.ThrowAsync<RequestValidationException>(() => Requests(s => s.SubmitAsync(id, new SubmitRequest(true, challenge.CheckId), CancellationToken.None)));

        _nafath.Answer(challenge.CheckId, _applicant.Identity.NationalId!);
        (await Requests(s => s.NafathStatusAsync(id, challenge.CheckId, null, CancellationToken.None))).Status.ShouldBe("VERIFIED");
        var submitted = await Requests(s => s.SubmitAsync(id, new SubmitRequest(true, challenge.CheckId), CancellationToken.None));
        submitted.Request.Status.ShouldNotBe(StatusCategory.Draft);
        submitted.Timeline.Select(t => t.Type).ShouldContain(RequestEventTypes.NafathConfirmed);
        var stored = await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Requests.SingleAsync(r => r.Id == id));
        stored.NafathCheckId.ShouldBe(challenge.CheckId);
        stored.NafathConfirmedAt.ShouldNotBeNull();

        // A confirmation belongs to its request.
        var other = await DraftAsync();
        await Should.ThrowAsync<RequestNotFoundException>(() => Requests(s => s.NafathStatusAsync(other, challenge.CheckId, null, CancellationToken.None)));
        await Should.ThrowAsync<RequestValidationException>(() => Requests(s => s.SubmitAsync(other, new SubmitRequest(true, challenge.CheckId), CancellationToken.None)));
    }

    private async Task<Guid> DraftAsync()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));
        var answers = new JsonObject
        {
            [_sample.Sectors] = new JsonArray("ICT"),
            [_sample.IctCategory] = "GOLD",
            [_sample.Employees] = 25,
            [_sample.Revenue] = 20_000_000,
        };
        await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(answers, "PREMIUM", null), CancellationToken.None));
        return draft.Request.Id;
    }

    private Task<T> Requests<T>(Func<RequestService, Task<T>> action) => _applicant.AsAsync(sp => action(sp.GetRequiredService<RequestService>()));

    /// <summary>Nafath verifications answered by the test.</summary>
    private sealed class FakeNafath : INafathVerifications
    {
        private readonly ConcurrentDictionary<Guid, NafathCheck> _checks = new();

        public ConcurrentDictionary<Guid, string> Sent { get; } = new();

        public bool IsConfigured => true;

        public Task<NafathChallenge> StartAsync(string nationalId, string? purpose, string subjectType, Guid? subjectId, string? userIp, CancellationToken cancellationToken)
        {
            var id = Guid.CreateVersion7();
            var expires = DateTimeOffset.UtcNow.AddMinutes(3);
            Sent[id] = nationalId;
            _checks[id] = new NafathCheck(id, subjectType, subjectId, NafathStatus.Waiting, "42", expires, null, null);
            return Task.FromResult(new NafathChallenge(id, "42", expires));
        }

        public Task<NafathCheck?> CheckAsync(Guid id, string? userIp, CancellationToken cancellationToken) =>
            Task.FromResult(_checks.TryGetValue(id, out var check) ? check : null);

        public void Answer(Guid id, string identifier) =>
            _checks[id] = _checks[id] with { Status = NafathStatus.Verified, Person = new NafathPerson(identifier, "مقدم الطلب", null, "SA", null, null, null), CompletedAt = DateTimeOffset.UtcNow };
    }
}
