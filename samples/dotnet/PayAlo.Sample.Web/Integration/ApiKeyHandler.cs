using Microsoft.Extensions.Options;

namespace PayAlo.Sample.Web.Integration;

// DelegatingHandler that injects the X-Api-Key header on every outbound request.
//
// We resolve PayAloOptions through IOptionsMonitor so that a key change in user-secrets
// or environment variables is picked up without restarting the process.
//
// Keeping the header injection in a handler (rather than passing the key into every
// PayAloClient method) means:
//   - The key is set in exactly one place.
//   - No code path can accidentally forget it and produce a 401.
//   - The key value never appears in our higher-level client / page code.
internal sealed class ApiKeyHandler(IOptionsMonitor<PayAloOptions> options)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var apiKey = options.CurrentValue.ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Per the v2 spec: "X-Api-Key: <your-api-key>". Header name is case-insensitive
            // server-side, but we use the canonical casing from the docs.
            request.Headers.Remove("X-Api-Key");
            request.Headers.Add("X-Api-Key", apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
