using System.Net;

namespace FinaryExport.Tests.Helpers;

// A fake HttpMessageHandler that returns pre-configured responses.
// Used to mock HTTP layer without actually sending requests.
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _sentRequests = [];

    // <summary>All requests that were sent through this handler, in order.</summary>
    public IReadOnlyList<HttpRequestMessage> SentRequests => _sentRequests;

    // <summary>Enqueue a response to return on the next SendAsync call.</summary>
    public MockHttpMessageHandler Enqueue(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
        return this;
    }

    // <summary>Shorthand: enqueue a 200 OK with JSON body.</summary>
    public MockHttpMessageHandler EnqueueJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }

    // <summary>Enqueue a failure status with optional body.</summary>
    public MockHttpMessageHandler EnqueueError(HttpStatusCode statusCode, string? body = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (body is not null)
            response.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        return Enqueue(response);
    }

    // <summary>Enqueue a timeout (throws TaskCanceledException).</summary>
    public MockHttpMessageHandler EnqueueTimeout()
    {
        _responses.Enqueue(null!); // sentinel
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _sentRequests.Add(request);

        if (_responses.Count == 0)
            throw new InvalidOperationException(
                $"MockHttpMessageHandler: No response enqueued for request #{_sentRequests.Count}: {request.Method} {request.RequestUri}");

		var response = _responses.Dequeue() ?? throw new TaskCanceledException("Request timed out (simulated)");
		return Task.FromResult(response);
    }
}
