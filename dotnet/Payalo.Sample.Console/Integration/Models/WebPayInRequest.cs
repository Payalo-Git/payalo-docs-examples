using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Console.Integration.Models;

// Body of POST /web/payin/{method}.
//
// Extends a regular pay-in with browser-redirect URLs. After the gateway returns
// `pageUrl`, the merchant redirects the end user there; on completion the gateway
// redirects them back to `successRedirectUrl` or `errorRedirectUrl`. Always rely on
// the callback to `resultUrl` for authoritative status — the redirects are UX only.
public sealed class WebPayInRequest
{
    [Required]
    public MoneyDto Amount { get; set; } = new();

    [Required]
    public MsisdnPartyDto Payer { get; set; } = new();

    [Required, StringLength(10, MinimumLength = 2)]
    public string Country { get; set; } = "";

    [Required, Url, StringLength(255)]
    public string ResultUrl { get; set; } = "";

    [Required, StringLength(100)]
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
