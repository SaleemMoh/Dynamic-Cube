using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace DynamicCube.Tests.Infrastructure;

/// <summary>A request the provider received; <paramref name="Headers"/> holds request and content headers (values joined).</summary>
public sealed record RecordedRequest(HttpMethod Method, Uri Url, string? Authorization, string? Body, IReadOnlyDictionary<string, string>? Headers = null)
{
    public string? Header(string name) => Headers is not null && Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>Stands in for the provider's gateway: answers requests with a test-supplied function and records them.</summary>
public sealed class FakeProvider : HttpMessageHandler
{
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public Func<RecordedRequest, (HttpStatusCode Status, string Body)> Respond { get; set; } = _ => (HttpStatusCode.OK, "{}");

    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    public int CountTo(string pathFragment) => Requests.Count(r => r.Url.AbsolutePath.Contains(pathFragment, StringComparison.Ordinal));

    public void Reset() => _requests.Clear();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
            request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase));
        _requests.Enqueue(recorded);
        var (status, body) = Respond(recorded);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
