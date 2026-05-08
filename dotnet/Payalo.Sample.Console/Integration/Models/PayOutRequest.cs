using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Console.Integration.Models;

// Body of POST /direct/payout/{method}.
//
// Functionally identical to PayInRequest except the party field is named `payee`
// (funds flow *to* the end user). Kept as a separate type so JSON serialization emits
// the correct field name and IntelliSense stays honest at the call site.
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
}
