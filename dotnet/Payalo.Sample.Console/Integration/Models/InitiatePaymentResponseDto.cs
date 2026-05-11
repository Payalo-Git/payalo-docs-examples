namespace Payalo.Sample.Console.Integration.Models;

// Standard success body for direct payment initiation endpoints.
// Status is always "pending" — the transaction has been accepted but not yet processed.
// Save GatewayReference; you'll need it to look up status and to deduplicate callbacks.
public class InitiatePaymentResponseDto
{
    public string Status { get; set; } = "";
    public string GatewayReference { get; set; } = "";
    public string MerchantReference { get; set; } = "";
    public string ReconciliationReference { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
