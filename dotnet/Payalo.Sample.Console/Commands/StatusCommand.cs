using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;

namespace Payalo.Sample.Console.Commands;

// `payalo status` — GET /status/{ref} or /status/mref/{ref}.
internal sealed class StatusCommand
{
    private readonly PayaloClient _client;

    public StatusCommand(PayaloClient client) => _client = client;

    public async Task ExecuteAsync(ParsedArgs args, CancellationToken ct)
    {
        Output.Banner("Transaction status lookup");

        var gref = args.Get("--gref");
        var mref = args.Get("--mref");

        if (gref is null && mref is null)
        {
            Output.Info("Provide one of: --gref <gateway-ref>, --mref <merchant-ref>.");
            var choice = Prompt.AskString("Look up by (g)ateway or (m)erchant reference?", defaultValue: "g")
                .ToLowerInvariant();

            if (choice.StartsWith('m'))
                mref = Prompt.AskString("Merchant reference");
            else
                gref = Prompt.AskString("Gateway reference");
        }
        else if (gref is not null && mref is not null)
        {
            Output.Warn("Both --gref and --mref supplied; using --gref.");
            mref = null;
        }

        var transaction = gref is not null
            ? await _client.GetByGatewayReferenceAsync(gref, ct)
            : await _client.GetByMerchantReferenceAsync(mref!, ct);

        Output.Success($"Status: {transaction.Status} | Type: {transaction.Type} | Method: {transaction.Method}");

        if (transaction.Status == "failed" && !string.IsNullOrEmpty(transaction.ErrorCode))
        {
            var friendly = PayaloErrorMessages.Describe(transaction.ErrorCode, transaction.ErrorMessage);
            Output.Warn($"Error [{transaction.ErrorCode}]: {friendly}");
        }

        Output.Json(transaction);
    }
}
