using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Payalo.Sample.Web.Integration;
using Payalo.Sample.Web.Integration.Models;
using Payalo.Sample.Web.Services;

namespace Payalo.Sample.Web.Endpoints;

// Minimal-API endpoint that receives webhook callbacks from the gateway.
//
// Per the v2 docs the gateway:
//   - POSTs application/json to your `resultUrl`,
//   - includes the `X-API-KEY` header set to *your* brand API key,
//   - delivers the same MmoTransactionDto shape returned by GET /status/{ref},
//   - expects any 2xx response within 15 seconds; the response body is optional and informational only,
//   - makes ONE attempt per terminal state change and does not retry: reconcile with Status or Records
//     if a delivery fails,
//   - sends another callback when an operator changes a final status (be idempotent, key on reference + status).
public static class WebhookEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapPayaloWebhook(this IEndpointRouteBuilder routes)
    {
        var options = routes.ServiceProvider.GetRequiredService<IOptions<PayaloOptions>>().Value;

        routes.MapPost($"{options.WebhookPath.TrimEnd('/')}/qr", HandleQrDataAsync)
            .WithName("PayaloQrData")
            .DisableAntiforgery();

        routes.MapPost(options.WebhookPath, HandleAsync)
            .WithName("PayaloWebhook")
            // Webhooks are not browser-driven and never carry an antiforgery token.
            // Excluding the endpoint here is required, otherwise the global UseAntiforgery
            // middleware would 400 every callback.
            .DisableAntiforgery();

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IOptionsMonitor<PayaloOptions> optionsMonitor,
        WebhookStore store,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var log = loggerFactory.CreateLogger("Payalo.Webhook");
        var options = optionsMonitor.CurrentValue;

        // 1) Authenticate. The only signal we have is the X-API-KEY header echoing our
        //    own brand key. Use a fixed-time comparison so an attacker cannot probe the
        //    expected value byte-by-byte via response timing.
        var presented = context.Request.Headers["X-API-KEY"].ToString();
        if (string.IsNullOrEmpty(presented) || !FixedTimeEquals(presented, options.ApiKey))
        {
            log.LogWarning("Rejected webhook from {Ip} — X-API-KEY missing or mismatched",
                context.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }

        // 2) Buffer the raw body. We need it both for parsing and for displaying in the
        //    demo UI alongside the parsed view.
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(ct);
        context.Request.Body.Position = 0;

        // The gateway delivers the V2 transaction object — exactly the same shape as
        // GET /status/{ref}, so we reuse MmoTransactionDto and don't need a separate type.
        MmoTransactionDto? payload;
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (IsPushCallback(document.RootElement))
            {
                log.LogInformation("Push callback received: read GET /status/{{id}} for the V2 transaction");
                return Results.Ok(new CallbackResponse("ok"));
            }

            payload = document.RootElement.Deserialize<MmoTransactionDto>(JsonOptions);
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "Webhook body was not valid JSON");
            // The gateway does not retry a failed delivery, so answering 400 only records the
            // failure in its logs. Reconcile with Status or Records.
            return Results.BadRequest(new CallbackResponse("error"));
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.GatewayReference))
        {
            log.LogWarning("Webhook body parsed but is missing required `gatewayReference` field");
            return Results.BadRequest(new CallbackResponse("error"));
        }

        // 3) Persist (idempotently). TryRecord returns false on duplicate delivery.
        //    Per the v2 callbacks docs the deduplication key is `gatewayReference` + `status`,
        //    because an operator can change a final status and that sends another callback.
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isNew = store.TryRecord(payload, raw, sourceIp);

        log.LogInformation(
            "Webhook {Action}: gatewayReference={GatewayReference} status={PaymentStatus} duplicate={Duplicate}",
            isNew ? "accepted" : "deduplicated",
            payload.GatewayReference,
            payload.Status,
            !isNew);

        // 4) Always reply "ok" within 15 s. Heavy work (DB writes, fulfillment) belongs
        //    in a background queue, not in this handler. Acknowledge duplicates as "ok" too.
        return Results.Ok(new CallbackResponse("ok"));
    }

    private static async Task<IResult> HandleQrDataAsync(
        HttpContext context,
        IOptionsMonitor<PayaloOptions> optionsMonitor,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var log = loggerFactory.CreateLogger("Payalo.Webhook");
        var presented = context.Request.Headers["X-API-KEY"].ToString();
        if (string.IsNullOrEmpty(presented) || !FixedTimeEquals(presented, optionsMonitor.CurrentValue.ApiKey))
        {
            log.LogWarning("Rejected QR data request from {Ip} — X-API-KEY missing or mismatched",
                context.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(ct);
        QrDataRequest? data;
        try
        {
            data = JsonSerializer.Deserialize<QrDataRequest>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "QR data request body was not valid JSON");
            return Results.BadRequest(new CallbackResponse("error"));
        }

        log.LogInformation(
            "QR data received: gatewayReference={GatewayReference} qrCode={HasQrCode} deepLink={HasDeepLink} expiresAt={ExpiresAt}",
            data?.GatewayReference,
            string.IsNullOrEmpty(data?.QrCode) ? "no" : "yes",
            string.IsNullOrEmpty(data?.DeepLink) ? "no" : "yes",
            data?.ExpiresAt);

        return Results.Ok(new CallbackResponse("ok"));
    }

    private static bool IsPushCallback(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && !root.TryGetProperty("gatewayReference", out _)
        && root.TryGetProperty("id", out _);

    // Constant-time byte comparison. Use SHA-256 hashes so the comparison is over a
    // fixed-length input regardless of secret length.
    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var bBytes = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private sealed record CallbackResponse(string Status);

    private sealed record QrDataRequest(
        string? GatewayReference,
        string? MerchantReference,
        string? QrCode,
        string? DeepLink,
        string? Message,
        string? ExpiresAt);
}
