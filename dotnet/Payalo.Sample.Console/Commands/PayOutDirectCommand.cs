using Microsoft.Extensions.Options;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;
using Payalo.Sample.Console.Integration.Models;

namespace Payalo.Sample.Console.Commands;

// `payalo payout-direct` — POST /direct/payout/{method}.
internal sealed class PayOutDirectCommand
{
    private readonly PayaloClient _client;
    private readonly IOptions<PayaloOptions> _options;

    public PayOutDirectCommand(PayaloClient client, IOptions<PayaloOptions> options)
    {
        _client = client;
        _options = options;
    }

    public async Task ExecuteAsync(ParsedArgs args, CancellationToken ct)
    {
        Output.Banner("Direct Pay-Out");

        var common = SubmitInputs.GatherCommon(args, _options);
        var payee  = SubmitInputs.GatherParty(args, "Payee");

        var request = new PayOutRequest
        {
            Amount                  = common.Amount,
            Payee                   = payee,
            Country                 = common.Country,
            ResultUrl               = common.ResultUrl,
            MerchantReference       = common.MerchantReference,
            ReconciliationReference = common.ReconciliationReference,
        };

        Output.Info($"Submitting via method '{common.Method}'…");
        var response = await _client.CreateDirectPayOutAsync(common.Method, request, ct);

        Output.Success($"Accepted. Gateway reference: {response.GatewayReference}");
        Output.Json(response);
    }
}
