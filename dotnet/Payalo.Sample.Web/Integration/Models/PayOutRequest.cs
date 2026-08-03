using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Web.Integration.Models;

// Body of POST /direct/payout/{method}.
//
// Functionally identical to a pay-in request except the party field is named `payee`
// (funds flow *to* the end user). We keep the two as separate types so JSON serialization
// emits the correct field name and to keep IntelliSense honest at the call site.
public sealed class PayOutRequest
{
    [Required]
    public MoneyDto Amount { get; set; } = new();

    [Required]
    public MsisdnPartyDto Payee { get; set; } = new();

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
