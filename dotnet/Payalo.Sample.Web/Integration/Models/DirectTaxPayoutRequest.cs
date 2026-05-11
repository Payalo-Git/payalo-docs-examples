using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Web.Integration.Models;

// Body of POST /direct/taxpayout/{method}.
//
// Tax payouts have no payer/payee — the recipient is implicitly the tax authority
// associated with the configured payment method. Two extra differences from a
// regular pay-out:
//   - `reconciliationReference` is REQUIRED (not optional).
//   - There is no party object on the request.
public sealed class DirectTaxPayoutRequest
{
    [Required]
    public MoneyDto Amount { get; set; } = new();

    [Required, StringLength(10, MinimumLength = 2)]
    public string Country { get; set; } = "";

    [Required, Url, StringLength(2048)]
    public string ResultUrl { get; set; } = "";

    [Required, StringLength(255)]
    public string MerchantReference { get; set; } = "";

    [Required, StringLength(255)]
    public string ReconciliationReference { get; set; } = "";

    public Dictionary<string, string>? Labels { get; set; }
}
