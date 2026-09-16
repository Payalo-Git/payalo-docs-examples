namespace Payalo.Sample.Console.Integration;

// Strongly-typed configuration bound to the "Payalo" section in appsettings.json.
// Values can be overridden by environment variables (Payalo__BaseUrl, Payalo__ApiKey, ...)
// or by `dotnet user-secrets set Payalo:ApiKey ...` in development.
public sealed class PayaloOptions
{
    public const string SectionName = "Payalo";

    // Base URL of the Merchant Gateway v2, e.g. https://api.payalo.com/gateway/mmo/v2
    public string BaseUrl { get; set; } = "";

    // Your brand API key. Sent as the X-Api-Key header on every outbound request.
    public string ApiKey { get; set; } = "";

    // Default `resultUrl` populated on submit requests when the caller does not pass
    // --result-url. The console app does not host a webhook receiver — point this at a
    // public endpoint you control (e.g. the dotnet/nodejs/python web samples behind ngrok)
    // if you want to observe terminal callbacks.
    public string DefaultResultUrl { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
