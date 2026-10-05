using System.Net;
using System.Text;
using BloodDonationNetwork.Api.Http;

namespace BloodDonationNetwork.UnitTests.Agents;

public class AgentServiceWakeUpHandlerTests
{
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _runWorkflowResponses;
        private readonly Queue<HttpStatusCode> _healthResponses;

        public ScriptedHandler(HttpStatusCode[] runWorkflow, HttpStatusCode[] health)
        {
            _runWorkflowResponses = new Queue<HttpStatusCode>(runWorkflow);
            _healthResponses = new Queue<HttpStatusCode>(health);
        }

        public List<(HttpMethod Method, string Path, string? Body, string? Secret)> Calls { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("X-Internal-Secret", out var secrets);
            Calls.Add((request.Method, request.RequestUri!.AbsolutePath, body, secrets?.FirstOrDefault()));

            var queue = request.RequestUri.AbsolutePath == "/health" ? _healthResponses : _runWorkflowResponses;
            var status = queue.Count > 0 ? queue.Dequeue() : HttpStatusCode.OK;
            return new HttpResponseMessage(status);
        }
    }

    private static HttpClient ClientFor(ScriptedHandler inner, TimeSpan? wakeUpLimit = null)
    {
        var handler = new AgentServiceWakeUpHandler(wakeUpLimit ?? TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1))
        {
            InnerHandler = inner,
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example") };
        client.DefaultRequestHeaders.Add("X-Internal-Secret", "s3cret");
        return client;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Success_IsReturnedWithoutRetrying()
    {
        var inner = new ScriptedHandler(new[] { HttpStatusCode.OK }, Array.Empty<HttpStatusCode>());

        var response = await ClientFor(inner).PostAsync("/run-workflow", Json("{\"a\":1}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(inner.Calls);
    }

    [Fact]
    public async Task GatewayError_WaitsForHealth_ThenResendsSameRequest()
    {
        var inner = new ScriptedHandler(
            new[] { HttpStatusCode.BadGateway, HttpStatusCode.OK },
            new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK });

        var response = await ClientFor(inner).PostAsync("/run-workflow", Json("{\"bloodRequestId\":\"r1\"}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new[] { "/run-workflow", "/health", "/health", "/run-workflow" },
            inner.Calls.Select(c => c.Path).ToArray());

        var first = inner.Calls[0];
        var retry = inner.Calls[3];
        Assert.Equal(HttpMethod.Post, retry.Method);
        Assert.Equal(first.Body, retry.Body);
        Assert.Equal("s3cret", retry.Secret);
    }

    [Fact]
    public async Task ClientErrors_AreNotRetried()
    {
        var inner = new ScriptedHandler(new[] { HttpStatusCode.Unauthorized }, Array.Empty<HttpStatusCode>());

        var response = await ClientFor(inner).PostAsync("/run-workflow", Json("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(inner.Calls);
    }

    [Fact]
    public async Task ServiceThatNeverWakes_ReturnsOriginalGatewayError()
    {
        var inner = new ScriptedHandler(
            new[] { HttpStatusCode.BadGateway },
            Enumerable.Repeat(HttpStatusCode.BadGateway, 10_000).ToArray());

        var response = await ClientFor(inner, TimeSpan.FromMilliseconds(50)).PostAsync("/run-workflow", Json("{}"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(1, inner.Calls.Count(c => c.Path == "/run-workflow"));
    }
}
