namespace PayAlo.Sample.Web.Integration.Models;

// Provider-specific details attached to a transaction once it has been routed.
// Null in the parent transaction until routing completes.
public sealed class ProviderDataDto
{
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public MoneyDto? Fee { get; set; }
    public Dictionary<string, string>? PartyData { get; set; }

    // Provider's *native* error code/message (raw, not mapped). For our friendly text use the
    // top-level errorCode on MmoTransactionDto / PaymentDto — that's the gateway-normalized one.
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
