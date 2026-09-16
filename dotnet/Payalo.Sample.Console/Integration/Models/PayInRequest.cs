using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Console.Integration.Models;

// Body of POST /direct/payin/{method}.
//
// `merchantReference` acts as an idempotency key. The gateway tracks submitted IDs in
// its primary deduplication store for ~15 minutes; downstream services may reject
// duplicates for longer. Always generate a fresh ULID/UUID per attempt — never reuse
// a reference, even if the previous transaction failed.
public sealed class PayInRequest
{
    [Required]
    public MoneyDto Amount { get; set; } = new();

    [Required]
    public MsisdnPartyDto Payer { get; set; } = new();

    [Required, StringLength(10, MinimumLength = 2)]
    public string Country { get; set; } = "";

    [Required, Url, StringLength(2048)]
    public string ResultUrl { get; set; } = "";

    [Required, StringLength(255)]
    public string MerchantReference { get; set; } = "";

    [StringLength(255)]
    public string? ReconciliationReference { get; set; }

    public Dictionary<string, string>? Labels { get; set; }

    // Optional BCP-47 language tag (e.g. "fr-FR", or language-only "fr") that sets the
    // hosted/redirection page language. Omit it for English; an unrecognised tag is
    // rejected server-side with 400 invalid_language_code.
    public string? PreferredLanguage { get; set; }
}
