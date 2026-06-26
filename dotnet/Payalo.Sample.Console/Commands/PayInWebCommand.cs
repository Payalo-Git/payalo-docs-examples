using Microsoft.Extensions.Options;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;
using Payalo.Sample.Console.Integration.Models;

namespace Payalo.Sample.Console.Commands;

// `payalo payin-web` — POST /web/payin/{method}. Hosted Payment Page flow.
internal sealed class PayInWebCommand
{
    private readonly PayaloClient _client;
    private readonly IOptions<PayaloOptions> _options;

    public PayInWebCommand(PayaloClient client, IOptions<PayaloOptions> options)
    {
        _client = client;
        _options = options;
    }

    public async Task ExecuteAsync(ParsedArgs args, CancellationToken ct)
    {
        Output.Banner("Web Pay-In (Hosted Payment Page)");

        var common = SubmitInputs.GatherCommon(args, _options);
        var payer  = SubmitInputs.GatherParty(args, "Payer");

        var request = new WebPayInRequest
        {
            Amount                  = common.Amount,
            Payer                   = payer,
            Country                 = common.Country,
            ResultUrl               = common.ResultUrl,
            MerchantReference       = common.MerchantReference,
            ReconciliationReference = common.ReconciliationReference,
            SuccessRedirectUrl      = args.Get("--success-url"),
            ErrorRedirectUrl        = args.Get("--error-url"),
            PreferredLanguage       = args.Get("--preferred-language"),
        };

        Output.Info($"Submitting via method '{common.Method}'…");
        var response = await _client.CreateWebPayInAsync(common.Method, request, ct);

        Output.Success($"Accepted. Redirect the end user to: {response.PageUrl}");
        Output.Json(response);
    }
}
