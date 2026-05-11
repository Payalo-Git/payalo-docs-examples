using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Web.Integration.Models;

// Body of POST /direct/payin/{method}.
//
// `merchantReference` acts as an idempotency key. The gateway tracks submitted IDs in
// its primary deduplication store for ~15 minutes; downstream services may reject
// duplicates for longer (e.g. once a transaction has settled). Always generate a fresh
// ULID/UUID per attempt — never reuse a reference, even if the previous transaction failed.
public sealed class PayInRequest
{
    // Nested DataAnnotations on these complex types are validated per-field as the user
    // types (Blazor's DataAnnotationsValidator does not recurse on submit). The gateway
    // performs authoritative validation server-side.
    [Required]
    public MoneyDto Amount { get; set; } = new();

    [Required]
    public MsisdnPartyDto Payer { get; set; } = new();

    [Required, StringLength(10, MinimumLength = 2)]
    public string Country { get; set; } = "";

    // The gateway will POST the terminal status to this URL once the transaction
    // resolves. Must be HTTPS in production.
    [Required, Url, StringLength(2048)]
    public string ResultUrl { get; set; } = "";

    [Required, StringLength(255)]
    public string MerchantReference { get; set; } = "";

    [StringLength(255)]
    public string? ReconciliationReference { get; set; }

    public Dictionary<string, string>? Labels { get; set; }
}
