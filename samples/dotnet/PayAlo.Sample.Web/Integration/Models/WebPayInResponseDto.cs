namespace PayAlo.Sample.Web.Integration.Models;

// Success body for the Hosted Payment Page flow. Adds `PageUrl` (where to redirect the
// end user) on top of the standard initiate-payment response.
public sealed class WebPayInResponseDto : InitiatePaymentResponseDto
{
    public string PageUrl { get; set; } = "";

    // Currently always "redirect" per the v2 spec.
    public string PageOpenMode { get; set; } = "";
}
