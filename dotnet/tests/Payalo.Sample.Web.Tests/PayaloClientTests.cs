using System.Net;
using System.Text.Json;
using Payalo.Sample.Web.Integration;
using Payalo.Sample.Web.Integration.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Payalo.Sample.Web.Tests;

// Happy-path, error-path, and web pay-in request-serialization tests. Enough to
// demonstrate the pattern; the consumer can grow the suite from here. We talk to a
// WireMock fake gateway rather than the real one so the test is hermetic.
public sealed class PayaloClientTests : IDisposable
{
    private readonly WireMockServer _gateway = WireMockServer.Start();
    private readonly HttpClient _http;
    private readonly PayaloClient _client;

    public PayaloClientTests()
    {
        _http = new HttpClient { BaseAddress = new Uri(_gateway.Url! + "/") };
        _client = new PayaloClient(
            _http,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<PayaloClient>());
    }

    public void Dispose()
    {
        _http.Dispose();
        _gateway.Stop();
    }

    [Fact]
    public async Task DirectPayIn_ReturnsPendingResponse_OnHttp200()
    {
        _gateway
            .Given(Request.Create().WithPath("/direct/payin/mpesa-ke").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                      "status": "pending",
                      "gatewayReference": "01J1ABCDEF123456",
                      "merchantReference": "dep-test-1",
                      "reconciliationReference": "INV-1",
                      "createdAt": "2026-04-28T10:00:00+00:00"
                    }
                    """));

        var response = await _client.CreateDirectPayInAsync("mpesa-ke", BuildPayInRequest());

        Assert.Equal("pending", response.Status);
        Assert.Equal("01J1ABCDEF123456", response.GatewayReference);
    }

    [Fact]
    public async Task DirectPayIn_ThrowsPayaloApiException_WithParsedProblemDetailsDto_On422()
    {
        _gateway
            .Given(Request.Create().WithPath("/direct/payin/mpesa-ke").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(422)
                .WithHeader("Content-Type", "application/problem+json")
                .WithBody("""
                    {
                      "type": "https://docs.payalo.com/errors/merchant_transactionid_duplicate",
                      "title": "Business logic error",
                      "status": 422,
                      "detail": "Duplicate reference detected in merchant request.",
                      "errorCode": "merchant_transactionid_duplicate"
                    }
                    """));

        var ex = await Assert.ThrowsAsync<PayaloApiException>(() =>
            _client.CreateDirectPayInAsync("mpesa-ke", BuildPayInRequest()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal("merchant_transactionid_duplicate", ex.ErrorCode);
        // 422 with a specific business-rule violation is not retryable — the caller
        // would burn the merchantReference if it blindly retried.
        Assert.False(ex.IsRetryable);
    }

    [Fact]
    public async Task WebPayIn_SendsPreferredLanguageAsCamelCase_WhenProvided()
    {
        StubWebPayInOk();

        await _client.CreateWebPayInAsync("card-eu", BuildWebPayInRequest("fr-FR"));

        using var body = JsonDocument.Parse(CapturedRequestBody());
        Assert.Equal("fr-FR", body.RootElement.GetProperty("preferredLanguage").GetString());
    }

    [Fact]
    public async Task WebPayIn_OmitsPreferredLanguage_WhenNull()
    {
        StubWebPayInOk();

        await _client.CreateWebPayInAsync("card-eu", BuildWebPayInRequest(preferredLanguage: null));

        using var body = JsonDocument.Parse(CapturedRequestBody());
        Assert.False(
            body.RootElement.TryGetProperty("preferredLanguage", out _),
            "preferredLanguage must be omitted from the request body when null.");
    }

    private static PayInRequest BuildPayInRequest() => new()
    {
        Amount = new MoneyDto { Value = 500m, Currency = "KES" },
        Payer = new MsisdnPartyDto { Id = "user-42", Msisdn = "+254712345678" },
        Country = "KE",
        ResultUrl = "https://merchant.example.com/webhooks/payalo",
        MerchantReference = "dep-test",
    };

    private static WebPayInRequest BuildWebPayInRequest(string? preferredLanguage) => new()
    {
        Amount = new MoneyDto { Value = 25m, Currency = "EUR" },
        Payer = new MsisdnPartyDto { Id = "user-42", Msisdn = "+491234567890" },
        Country = "DE",
        ResultUrl = "https://merchant.example.com/webhooks/payalo",
        MerchantReference = "web-test-1",
        PreferredLanguage = preferredLanguage,
    };

    private void StubWebPayInOk() =>
        _gateway
            .Given(Request.Create().WithPath("/web/payin/card-eu").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                      "status": "pending",
                      "gatewayReference": "01J1WEBPAYIN0001",
                      "merchantReference": "web-test-1",
                      "reconciliationReference": "web-test-1",
                      "createdAt": "2026-04-28T10:00:00+00:00",
                      "pageUrl": "https://hpp.payalo.com/pay/session-xyz",
                      "pageOpenMode": "redirect"
                    }
                    """));

    private string CapturedRequestBody() =>
        _gateway.LogEntries.Single().RequestMessage?.Body
            ?? throw new InvalidOperationException("No request body was captured.");
}
