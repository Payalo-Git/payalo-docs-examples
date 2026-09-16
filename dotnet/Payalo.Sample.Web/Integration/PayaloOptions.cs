namespace Payalo.Sample.Web.Integration;

// Strongly-typed configuration bound to the "Payalo" section in appsettings.json.
// Values can be overridden by environment variables (Payalo__BaseUrl, Payalo__ApiKey, ...)
// or by `dotnet user-secrets set Payalo:ApiKey ...` in development.
public sealed class PayaloOptions
{
    public const string SectionName = "Payalo";

    // Base URL of the Merchant Gateway v2, e.g. https://api.payalo.com/gateway/mmo/v2
    public string BaseUrl { get; set; } = "";

    // Your brand API key. Sent as the X-Api-Key header on every outbound request.
    // Also used to authenticate inbound webhook callbacks (the gateway echoes the same key).
    public string ApiKey { get; set; } = "";

    // The path our app exposes for inbound webhook callbacks.
    // The full URL (e.g. https://your-host/webhooks/payalo) is what you put in `resultUrl`
    // when you initiate a payment.
    public string WebhookPath { get; set; } = "/webhooks/payalo";

    // The publicly reachable origin of this app. Used to build the absolute `resultUrl`
    // we send to the gateway. In local development this typically points at your ngrok
    // tunnel — the gateway can't reach http://localhost:5000.
    public string PublicBaseUrl { get; set; } = "";

    // True once the API key looks plausibly populated. Used to gate the demo pages
    // and surface a helpful "configure me" hint on the home page.
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
