using System.Net;
using System.Text;

namespace PersonalVidIQ.UnitTests.YouTube;

/// <summary>Returns queued responses in order and records every request URL.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<Uri> Requests { get; } = [];

    public FakeHttpHandler Respond(HttpStatusCode status, string body)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        return this;
    }

    public FakeHttpHandler RespondFixture(string name, HttpStatusCode status = HttpStatusCode.OK) =>
        Respond(status, Fixture.Read(name));

    public FakeHttpHandler Throw(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}

internal static class Fixture
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "youtube", name + ".json"));
}
