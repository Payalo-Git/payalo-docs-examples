using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using PayAlo.Sample.Web.Integration.Models;

namespace PayAlo.Sample.Web.Integration;

// Typed HttpClient for the PayAlo Merchant Gateway v2.
//
// Registered via AddHttpClient<PayAloClient>() in Program.cs, which adds:
//   - ApiKeyHandler (injects X-Api-Key on every request)
//   - The standard resilience handler (retry + circuit breaker on transient faults)
//
// Each method maps 1:1 to a documented endpoint. We deliberately do **not** retry inside
// the methods themselves — retry policy lives in the handler pipeline so it applies
// uniformly and we never wrap a non-idempotent POST in our own retry loop.
public sealed class PayAloClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Serialize null optional fields as omitted, not "field": null. Keeps wire payloads
        // closer to the examples in the docs.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<PayAloClient> _log;

    public PayAloClient(HttpClient http, ILogger<PayAloClient> log)
    {
        _http = http;
        _log = log;
    }

    // ------------------------------------------------------------------------------------
    // Payment initiation
    // ------------------------------------------------------------------------------------

    // POST /direct/payin/{method} — server-to-server deposit (e.g. M-Pesa STK push).
    public Task<InitiatePaymentResponseDto> CreateDirectPayInAsync(
        string method, PayInRequest body, CancellationToken ct = default) =>
        PostAsync<PayInRequest, InitiatePaymentResponseDto>($"direct/payin/{method}", body, ct);

    // POST /direct/payout/{method} — server-to-server disbursement to an end user.
    public Task<InitiatePaymentResponseDto> CreateDirectPayOutAsync(
        string method, PayOutRequest body, CancellationToken ct = default) =>
        PostAsync<PayOutRequest, InitiatePaymentResponseDto>($"direct/payout/{method}", body, ct);

    // POST /web/payin/{method} — Hosted Payment Page. Returns `pageUrl` to redirect to.
    public Task<WebPayInResponseDto> CreateWebPayInAsync(
        string method, WebPayInRequest body, CancellationToken ct = default) =>
        PostAsync<WebPayInRequest, WebPayInResponseDto>($"web/payin/{method}", body, ct);

    // POST /qr/payin/{method} — QR-code deposit. Body must include `displayQrUrl`.
    public Task<InitiatePaymentResponseDto> CreateQrPayInAsync(
        string method, QrPayInRequest body, CancellationToken ct = default) =>
        PostAsync<QrPayInRequest, InitiatePaymentResponseDto>($"qr/payin/{method}", body, ct);

    // POST /direct/taxpayout/{method} — payout to a tax authority. No payer/payee.
    public Task<InitiatePaymentResponseDto> CreateDirectTaxPayoutAsync(
        string method, DirectTaxPayoutRequest body, CancellationToken ct = default) =>
        PostAsync<DirectTaxPayoutRequest, InitiatePaymentResponseDto>($"direct/taxpayout/{method}", body, ct);

    // ------------------------------------------------------------------------------------
    // Lookups
    // ------------------------------------------------------------------------------------

    // GET /status/{gatewayReference}
    public Task<MmoTransactionDto> GetByGatewayReferenceAsync(
        string gatewayReference, CancellationToken ct = default) =>
        GetAsync<MmoTransactionDto>($"status/{Uri.EscapeDataString(gatewayReference)}", ct);

    // GET /status/mref/{merchantReference}
    public Task<MmoTransactionDto> GetByMerchantReferenceAsync(
        string merchantReference, CancellationToken ct = default) =>
        GetAsync<MmoTransactionDto>($"status/mref/{Uri.EscapeDataString(merchantReference)}", ct);

    // GET /records — paginated. Pass the cursor from the previous response in `pageCursor`.
    public Task<RecordsResponseDto> GetRecordsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? type = null,
        string? method = null,
        string? status = null,
        int? pageSize = null,
        string? pageCursor = null,
        CancellationToken ct = default)
    {
        // The cursor encodes the original filter set, so on follow-up calls we *only*
        // send `page`. Sending mismatched filters with a cursor returns 400.
        var query = new Dictionary<string, string?>();
        if (pageCursor is { Length: > 0 })
        {
            query["page"] = pageCursor;
        }
        else
        {
            query["from"] = from.UtcDateTime.ToString("o");
            query["to"]   = to.UtcDateTime.ToString("o");
            if (type is not null)     query["type"] = type;
            if (method is not null)   query["method"] = method;
            if (status is not null)   query["status"] = status;
            if (pageSize is not null) query["pageSize"] = pageSize.Value.ToString();
        }

        var url = QueryHelpers.AddQueryString("records", query);
        return GetAsync<RecordsResponseDto>(url, ct);
    }

    // ------------------------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------------------------

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativePath, TRequest body, CancellationToken ct)
    {
        _log.LogInformation("PayAlo POST {Path}", relativePath);

        // The HttpClient.BaseAddress already ends with /v2/, so we use a relative path
        // and let HttpClient resolve it. JsonContent uses our shared JsonOptions so
        // payloads come out as documented camelCase.
        using var response = await _http.PostAsJsonAsync(relativePath, body, JsonOptions, ct);
        return await ReadResponseAsync<TResponse>(response, ct);
    }

    private async Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken ct)
    {
        _log.LogInformation("PayAlo GET {Path}", relativePath);
        using var response = await _http.GetAsync(relativePath, ct);
        return await ReadResponseAsync<TResponse>(response, ct);
    }

    // Centralised response reader: throws PayAloApiException on non-2xx, parses Problem
    // Details when present, returns the typed response otherwise.
    private static async Task<TResponse> ReadResponseAsync<TResponse>(
        HttpResponseMessage response, CancellationToken ct)
    {
        var traceId = response.Headers.TryGetValues("traceparent", out var values)
            ? values.FirstOrDefault()
            : null;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
            return result ?? throw new PayAloApiException(
                response.StatusCode, problem: null,
                rawBody: "Empty success body — gateway contract violation.", traceId);
        }

        // Tolerate non-JSON error bodies (proxy timeouts, gateway misconfiguration).
        var rawBody = await response.Content.ReadAsStringAsync(ct);
        ProblemDetailsDto? problem = null;
        if (LooksLikeJson(response, rawBody))
        {
            try { problem = JsonSerializer.Deserialize<ProblemDetailsDto>(rawBody, JsonOptions); }
            catch (JsonException) { /* surface rawBody instead */ }
        }

        throw new PayAloApiException(response.StatusCode, problem, rawBody, traceId);
    }

    private static bool LooksLikeJson(HttpResponseMessage response, string body) =>
        body.Length > 0 && (
            response.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json"
            || body.TrimStart().StartsWith('{'));
}
