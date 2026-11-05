using System.Net;
using System.Text;

namespace Ohmline.Mobile.Core.Tests;

/// <summary>
/// Charging's API as the app's tests play it: an <see cref="HttpMessageHandler"/> that answers
/// each request with the next of its queued answers, and remembers what it was sent. A queued
/// exception is a network failure, as a phone in a car park sees one.
/// </summary>
internal sealed class FakeServer : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _answers = new();

    public static Uri Address { get; } = new("http://10.0.2.2:5180");

    public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];

    public HttpClient Client() => new(this) { BaseAddress = Address };

    public FakeServer Answers(HttpStatusCode status, string? json = null)
    {
        _answers.Enqueue(() => new HttpResponseMessage(status)
        {
            Content = json is null
                ? null
                : new StringContent(json, Encoding.UTF8, "application/json"),
        });
        return this;
    }

    public FakeServer IsUnreachable()
    {
        _answers.Enqueue(() => throw new HttpRequestException("Network is unreachable"));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!.PathAndQuery, body));
        return _answers.Dequeue()();
    }
}
