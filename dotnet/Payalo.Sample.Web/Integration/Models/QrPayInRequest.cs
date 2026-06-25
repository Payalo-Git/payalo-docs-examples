using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Web.Integration.Models;

// Body of POST /qr/payin/{method}.
//
// Same shape as Direct Pay-In with one extra **required** field, `displayQrUrl` —
// a publicly reachable HTTPS URL where the gateway POSTs the raw QR data once it has
// generated it. Your application is responsible for fetching from that URL (or
// receiving the push, depending on your wiring) and rendering the QR code for the
// end user to scan.
public sealed class QrPayInRequest
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

    // Where the gateway delivers QR data for you to render. Required for QR pay-ins.
    [Required, Url, StringLength(2048)]
    public string DisplayQrUrl { get; set; } = "";

    public Dictionary<string, string>? Labels { get; set; }

    // Optional BCP-47 `language-REGION` tag (e.g. "fr-FR") that sets the hosted/redirection
    // page language. An unrecognised or language-only value falls back to English server-side.
    public string? PreferredLanguage { get; set; }
}
