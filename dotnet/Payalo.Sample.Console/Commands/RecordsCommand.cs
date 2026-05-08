using System.Globalization;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;

namespace Payalo.Sample.Console.Commands;

// `payalo records` — GET /records.
//
// On the first page we send the full filter set; on follow-up pages we send *only*
// the cursor — the cursor encodes the original query and the gateway will reject
// mismatched filters with a 400.
internal sealed class RecordsCommand
{
    private readonly PayaloClient _client;

    public RecordsCommand(PayaloClient client) => _client = client;

    public async Task ExecuteAsync(ParsedArgs args, CancellationToken ct)
    {
        Output.Banner("Records (paginated)");

        var pageCursor = args.Get("--page-cursor");

        DateTimeOffset from, to;
        string? type, method, status;
        int? pageSize;

        if (pageCursor is not null)
        {
            // When paging, filters are ignored — the cursor carries them. Stub them out.
            from = default;
            to = default;
            type = method = status = null;
            pageSize = null;
        }
        else
        {
            from = ParseTimestamp(args.Get("--from")) ?? Prompt.AskDateTime("From",
                DateTimeOffset.UtcNow.AddDays(-1));
            to = ParseTimestamp(args.Get("--to")) ?? Prompt.AskDateTime("To",
                DateTimeOffset.UtcNow);
            type     = args.Get("--type");
            method   = args.Get("--method");
            status   = args.Get("--status");
            pageSize = args.Get("--page-size") is { } sizeStr
                && int.TryParse(sizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        var page = await _client.GetRecordsAsync(from, to, type, method, status, pageSize, pageCursor, ct);

        Output.Success($"Returned {page.Data.Count} record(s).");
        if (page.Data.Count > 0)
        {
            foreach (var tx in page.Data.Take(10))
            {
                Output.Info($"  [{tx.Status,-7}] {tx.Type,-6} {tx.Method,-12} {tx.GatewayReference} " +
                            $"{tx.RequestedAmount.Value} {tx.RequestedAmount.Currency} @ {tx.CreatedAt:o}");
            }
            if (page.Data.Count > 10)
                Output.Info($"  … plus {page.Data.Count - 10} more on this page.");
        }

        if (!string.IsNullOrEmpty(page.Pages.Next))
            Output.Info($"\nNext page cursor: {page.Pages.Next}");
        if (!string.IsNullOrEmpty(page.Pages.Previous))
            Output.Info($"Previous page cursor: {page.Pages.Previous}");

        Output.Json(page);
    }

    private static DateTimeOffset? ParseTimestamp(string? input) =>
        DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed) ? parsed : null;
}
