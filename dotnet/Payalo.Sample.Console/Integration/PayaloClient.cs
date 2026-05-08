using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payalo.Sample.Console.Integration.Models;

namespace Payalo.Sample.Console.Integration;

// Typed HttpClient for the Payalo Merchant Gateway v2.
//
// Registered via AddHttpClient<PayaloClient>() in Program.cs, which adds:
//   - ApiKeyHandler (injects X-Api-Key on every request)
//   - The standard resilience handler (retry + circuit breaker on transient faults)
//
// Each method maps 1:1 to a documented endpoint. We deliberately do **not** retry inside
// the methods themselves — retry policy lives in the handler pipeline so it applies
// uniformly and we never wrap a non-idempotent POST in our own retry loop.
public sealed class PayaloClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<PayaloClient> _log;

    public PayaloClient(HttpClient http, ILogger<PayaloClient> log)
    {
        _http = http;
        _log = log;
    }

    // ------------------------------------------------------------------------------------
    // Payment initiation
    // ------------------------------------------------------------------------------------

    public Task<InitiatePaymentResponseDto> CreateDirectPayInAsync(
        string method, PayInRequest body, CancellationToken ct = default) =>
        PostAsync<PayInRequest, InitiatePaymentResponseDto>($"direct/payin/{method}", body, ct);

    public Task<InitiatePaymentResponseDto> CreateDirectPayOutAsync(
        string method, PayOutRequest body, CancellationToken ct = default) =>
        PostAsync<PayOutRequest, InitiatePaymentResponseDto>($"direct/payout/{method}", body, ct);

    public Task<WebPayInResponseDto> CreateWebPayInAsync(
        string method, WebPayInRequest body, CancellationToken ct = default) =>
        PostAsync<WebPayInRequest, WebPayInResponseDto>($"web/payin/{method}", body, ct);

    public Task<InitiatePaymentResponseDto> CreateQrPayInAsync(
        string method, QrPayInRequest body, CancellationToken ct = default) =>
        PostAsync<QrPayInRequest, InitiatePaymentResponseDto>($"qr/payin/{method}", body, ct);

    public Task<InitiatePaymentResponseDto> CreateDirectTaxPayoutAsync(
        string method, DirectTaxPayoutRequest body, CancellationToken ct = default) =>
        PostAsync<DirectTaxPayoutRequest, InitiatePaymentResponseDto>($"direct/taxpayout/{method}", body, ct);

    // ------------------------------------------------------------------------------------
    // Lookups
    // ------------------------------------------------------------------------------------

    public Task<MmoTransactionDto> GetByGatewayReferenceAsync(
        string gatewayReference, CancellationToken ct = default) =>
        GetAsync<MmoTransactionDto>($"status/{Uri.EscapeDataString(gatewayReference)}", ct);

    public Task<MmoTransactionDto> GetByMerchantReferenceAsync(
        string merchantReference, CancellationToken ct = default) =>
        GetAsync<MmoTransactionDto>($"status/mref/{Uri.EscapeDataString(merchantReference)}", ct);

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

        return GetAsync<RecordsResponseDto>(BuildUrl("records", query), ct);
    }

    // ------------------------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------------------------

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativePath, TRequest body, CancellationToken ct)
    {
        _log.LogInformation("Payalo POST {Path}", relativePath);
        using var response = await _http.PostAsJsonAsync(relativePath, body, JsonOptions, ct);
        return await ReadResponseAsync<TResponse>(response, ct);
    }

    private async Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken ct)
    {
        _log.LogInformation("Payalo GET {Path}", relativePath);
        using var response = await _http.GetAsync(relativePath, ct);
        return await ReadResponseAsync<TResponse>(response, ct);
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(
        HttpResponseMessage response, CancellationToken ct)
    {
        var traceId = response.Headers.TryGetValues("traceparent", out var values)
            ? values.FirstOrDefault()
            : null;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
            return result ?? throw new PayaloApiException(
                response.StatusCode, problem: null,
                rawBody: "Empty success body — gateway contract violation.", traceId);
        }

        var rawBody = await response.Content.ReadAsStringAsync(ct);
        ProblemDetailsDto? problem = null;
        if (LooksLikeJson(response, rawBody))
        {
            try { problem = JsonSerializer.Deserialize<ProblemDetailsDto>(rawBody, JsonOptions); }
            catch (JsonException) { /* surface rawBody instead */ }
        }

        throw new PayaloApiException(response.StatusCode, problem, rawBody, traceId);
    }

    private static bool LooksLikeJson(HttpResponseMessage response, string body) =>
        body.Length > 0 && (
            response.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json"
            || body.TrimStart().StartsWith('{'));

    // Minimal query-string builder. The web sample uses Microsoft.AspNetCore.WebUtilities.QueryHelpers
    // (pulled in by the Web SDK); the console app rolls a small helper to keep deps lean.
    private static string BuildUrl(string path, IDictionary<string, string?> query)
    {
        var sb = new StringBuilder(path);
        var first = true;
        foreach (var (key, value) in query)
        {
            if (value is null) continue;
            sb.Append(first ? '?' : '&');
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
            first = false;
        }
        return sb.ToString();
    }
}
