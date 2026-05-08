using Microsoft.Extensions.Options;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;
using Payalo.Sample.Console.Integration.Models;

namespace Payalo.Sample.Console.Commands;

// `payalo payin-qr` — POST /qr/payin/{method}.
internal sealed class PayInQrCommand
{
    private readonly PayaloClient _client;
    private readonly IOptions<PayaloOptions> _options;

    public PayInQrCommand(PayaloClient client, IOptions<PayaloOptions> options)
    {
        _client = client;
        _options = options;
    }

    public async Task ExecuteAsync(ParsedArgs args, CancellationToken ct)
    {
        Output.Banner("QR Pay-In");

        var common = SubmitInputs.GatherCommon(args, _options);
        var payer  = SubmitInputs.GatherParty(args, "Payer");

        var displayQrUrl = args.Get("--display-qr-url")
            ?? Prompt.AskString("Display QR URL (where the gateway POSTs QR data)");

        var request = new QrPayInRequest
        {
            Amount                  = common.Amount,
            Payer                   = payer,
            Country                 = common.Country,
            ResultUrl               = common.ResultUrl,
            MerchantReference       = common.MerchantReference,
            ReconciliationReference = common.ReconciliationReference,
            DisplayQrUrl            = displayQrUrl,
        };

        Output.Info($"Submitting via method '{common.Method}'…");
        var response = await _client.CreateQrPayInAsync(common.Method, request, ct);

        Output.Success($"Accepted. Gateway reference: {response.GatewayReference}");
        Output.Info("Wait for the gateway to POST QR data to your displayQrUrl, then render it.");
        Output.Json(response);
    }
}
