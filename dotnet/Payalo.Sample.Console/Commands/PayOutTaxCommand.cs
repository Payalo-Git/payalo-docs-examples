using Microsoft.Extensions.Options;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;
using Payalo.Sample.Console.Integration.Models;

namespace Payalo.Sample.Console.Commands;

// `payalo payout-tax` — POST /direct/taxpayout/{method}.
//
// Tax payouts have no party object and require a reconciliation reference.
internal sealed class PayOutTaxCommand
{
    private readonly PayaloClient _client;
    private readonly IOptions<PayaloOptions> _options;

    public PayOutTaxCommand(PayaloClient client, IOptions<PayaloOptions> options)
    {
        _client = client;
        _options = options;
    }

    public async Task ExecuteAsync(ParsedArgs args, CancellationToken ct)
    {
        Output.Banner("Direct Tax Payout");

        var common = SubmitInputs.GatherCommon(args, _options);

        // Override the optional reconciliation gather: this flow makes it required.
        var reconRef = common.ReconciliationReference
            ?? Prompt.AskString("Reconciliation reference (required for tax payouts)");

        var request = new DirectTaxPayoutRequest
        {
            Amount                  = common.Amount,
            Country                 = common.Country,
            ResultUrl               = common.ResultUrl,
            MerchantReference       = common.MerchantReference,
            ReconciliationReference = reconRef,
        };

        Output.Info($"Submitting via method '{common.Method}'…");
        var response = await _client.CreateDirectTaxPayoutAsync(common.Method, request, ct);

        Output.Success($"Accepted. Gateway reference: {response.GatewayReference}");
        Output.Json(response);
    }
}
