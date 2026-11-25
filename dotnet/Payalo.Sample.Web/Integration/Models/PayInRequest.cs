using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Web.Integration.Models;

// Body of POST /direct/payin/{method}.
//
// `merchantReference` acts as an idempotency key. The gateway keeps a registered reference
// used permanently. A request that was rejected (400, 401, or a 422 other than a duplicate)
// registers nothing, so you can send it again with the same reference after you fix it.
// After a 500 or a timeout, read Payment Status by merchantReference before you send again.
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
    [Required, Url, StringLength(255)]
    public string ResultUrl { get; set; } = "";

    [Required, StringLength(100)]
    public string MerchantReference { get; set; } = "";

    [StringLength(255)]
    public string? ReconciliationReference { get; set; }

    public Dictionary<string, string>? Labels { get; set; }

    // Optional BCP-47 `language-REGION` tag (e.g. "fr-FR") that sets the hosted/redirection
    // page language. An unrecognised or language-only value falls back to English server-side.
    public string? PreferredLanguage { get; set; }
}
