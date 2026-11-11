using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Payalo.Sample.Web.Tests;

// One auth-rejection test, one happy-path test. Enough to demonstrate the
// `WebApplicationFactory` pattern; the consumer can grow from there.
public sealed class WebhookEndpointTests : IClassFixture<WebhookTestFactory>
{
    private const string ApiKey = "test-key-123";

    private readonly HttpClient _client;

    public WebhookEndpointTests(WebhookTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Webhook_Returns401_WhenApiKeyHeaderIsMissing()
    {
        using var content = new StringContent(
            """{"gatewayReference":"01J1ABCDEF123456","status":"failed"}""",
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/webhooks/payalo", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_Returns200Ok_WithValidApiKey()
    {
        using var content = new StringContent(
            """
            {
              "status": "success",
              "type": "payin",
              "flow": "direct",
              "gatewayReference": "01J1ABCDEF123456",
              "merchantReference": "dep-test-1",
              "reconciliationReference": "INV-2024-001",
              "providerReference": "MPESA-REC-99887766",
              "party": {
                "id": "user-42",
                "msisdn": "+254712345678",
                "firstName": "Jane",
                "lastName": "Doe"
              },
              "method": "mpesa-ke",
              "country": "KE",
              "requestedAmount": { "value": 500.00, "currency": "KES" },
              "finalAmount": { "value": 500.00, "currency": "KES" },
              "createdAt": "2026-04-28T10:00:00+00:00",
              "completedAt": "2026-04-28T10:00:30+00:00",
              "completionSource": "webhook",
              "errorCode": null,
              "errorMessage": null,
              "providerData": {
                "name": "mpesa",
                "title": "M-Pesa Kenya",
                "fee": { "value": 10.00, "currency": "KES" }
              }
            }
            """,
            Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/payalo")
        {
            Content = content,
        };
        request.Headers.Add("X-API-KEY", ApiKey);

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"ok\"", body);
    }

    [Fact]
    public async Task Webhook_Returns200Ok_ForPushCallbackWithNumericRequestedAmount()
    {
        using var content = new StringContent(
            """{"id":"b2p123","status":"Success","requestedAmount":100.5}""",
            Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/payalo") { Content = content };
        request.Headers.Add("X-API-KEY", ApiKey);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task QrData_Returns400_WhenBodyIsNotValidJson()
    {
        using var content = new StringContent("not json", Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/payalo/qr") { Content = content };
        request.Headers.Add("X-API-KEY", ApiKey);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class WebhookTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payalo:ApiKey"] = "test-key-123",
                ["Payalo:PublicBaseUrl"] = "https://test.example.com",
            });
        });
    }
}
