namespace Payalo.Sample.Web.Integration.Models;

// The full transaction representation returned by GET /status/{ref}, GET /status/mref/{ref},
// each entry of GET /records, AND the body of inbound webhook callbacks — per the v2 docs
// they all share the same wire shape, so one DTO covers every read path.
//
// The `Status` field follows the lifecycle: pending -> success | failed (terminal).
// Once a transaction is success/failed it is **immutable** — no further changes will occur.
public sealed class MmoTransactionDto
{
    public string Status { get; set; } = "";

    // "payin" | "payout" | "tax"
    public string Type { get; set; } = "";

    // "direct" | "web" | "qr" | "push"
    public string Flow { get; set; } = "";

    public string GatewayReference { get; set; } = "";

    // Null for push (provider-initiated) transactions — there was no merchant request
    // to echo. Always present for direct/web/qr flows.
    public string? MerchantReference { get; set; }

    // Null for push transactions. For merchant-initiated flows it's either the value
    // submitted on create, or the gateway's fallback to GatewayReference.
    public string? ReconciliationReference { get; set; }

    // Payment provider's own transaction reference. Null until the gateway has routed
    // the transaction to the provider.
    public string? ProviderReference { get; set; }

    public MsisdnPartyDto Party { get; set; } = new();
    public string Method { get; set; } = "";
    public string Country { get; set; } = "";

    public MoneyDto RequestedAmount { get; set; } = new();

    // Final settled amount. Null while pending or if the transaction failed before settling.
    public MoneyDto? FinalAmount { get; set; }

    public Dictionary<string, string>? Labels { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    // What resolved the transaction. Documented values: "webhook" (provider callback),
    // "poll" (gateway polled the provider), "manual" (operator override). Null while
    // pending. Treat unknown values as informational.
    public string? CompletionSource { get; set; }

    // Populated only when Status == "failed". Use PayaloErrorMessages.Describe to map to UX copy.
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public ProviderDataDto? ProviderData { get; set; }
}
