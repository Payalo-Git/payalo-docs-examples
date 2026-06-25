using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Web.Integration.Models;

// Body of POST /web/payin/{method}.
//
// Extends PayInRequest with browser-redirect URLs. After the gateway returns `pageUrl`,
// you redirect the end user there; on completion, the gateway redirects them back to
// `successRedirectUrl` or `errorRedirectUrl`. Always rely on the callback to `resultUrl`
// for the *authoritative* status — the redirects are a UX convenience only.
public sealed class WebPayInRequest
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

    [Url, StringLength(2048)]
    public string? SuccessRedirectUrl { get; set; }

    [Url, StringLength(2048)]
    public string? ErrorRedirectUrl { get; set; }

    [StringLength(255)]
    public string? ReconciliationReference { get; set; }

    public Dictionary<string, string>? Labels { get; set; }

    // Optional BCP-47 `language-REGION` tag (e.g. "fr-FR") that sets the hosted/redirection
    // page language. An unrecognised or language-only value falls back to English server-side.
    public string? PreferredLanguage { get; set; }
}
