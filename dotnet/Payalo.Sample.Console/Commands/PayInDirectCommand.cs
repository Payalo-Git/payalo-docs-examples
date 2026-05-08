using Microsoft.Extensions.Options;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;
using Payalo.Sample.Console.Integration.Models;

namespace Payalo.Sample.Console.Commands;

// `payalo payin-direct` — POST /direct/payin/{method}.
internal sealed class PayInDirectCommand
{
    private readonly PayaloClient _client;
    private readonly IOptions<PayaloOptions> _options;

    public PayInDirectCommand(PayaloClient client, IOptions<PayaloOptions> options)
    {
        _client = client;
        _options = options;
    }

    public async Task ExecuteAsync(ParsedArgs args, CancellationToken ct)
    {
        Output.Banner("Direct Pay-In");

        var common = SubmitInputs.GatherCommon(args, _options);
        var payer  = SubmitInputs.GatherParty(args, "Payer");

        var request = new PayInRequest
        {
            Amount                 = common.Amount,
            Payer                  = payer,
            Country                = common.Country,
            ResultUrl              = common.ResultUrl,
            MerchantReference      = common.MerchantReference,
            ReconciliationReference = common.ReconciliationReference,
        };

        Output.Info($"Submitting via method '{common.Method}'…");
        var response = await _client.CreateDirectPayInAsync(common.Method, request, ct);

        Output.Success($"Accepted. Gateway reference: {response.GatewayReference}");
        Output.Json(response);
    }
}
