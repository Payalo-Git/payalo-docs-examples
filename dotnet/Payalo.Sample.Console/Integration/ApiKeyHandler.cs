using Microsoft.Extensions.Options;

namespace Payalo.Sample.Console.Integration;

// DelegatingHandler that injects the X-Api-Key header on every outbound request.
//
// We resolve PayaloOptions through IOptionsMonitor so that a key change picked up via
// reload is applied without restarting. Centralising the header in a handler means the
// key is set in exactly one place and no code path can accidentally produce a 401.
internal sealed class ApiKeyHandler(IOptionsMonitor<PayaloOptions> options)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var apiKey = options.CurrentValue.ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Remove("X-Api-Key");
            request.Headers.Add("X-Api-Key", apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
