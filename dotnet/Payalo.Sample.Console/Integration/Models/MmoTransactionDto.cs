namespace Payalo.Sample.Console.Integration.Models;

// The full transaction representation returned by GET /status/{ref}, GET /status/mref/{ref},
// each entry of GET /records, AND the body of inbound webhook callbacks — per the v2 docs
// they all share the same wire shape.
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

    // Null for push (provider-initiated) transactions.
    public string? MerchantReference { get; set; }

    // Null for push transactions.
    public string? ReconciliationReference { get; set; }

    // Payment provider's own transaction reference. Null until routed.
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

    // What resolved the transaction. Documented values: "webhook" | "poll" | "manual".
    public string? CompletionSource { get; set; }

    // Populated only when Status == "failed".
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public ProviderDataDto? ProviderData { get; set; }
}
