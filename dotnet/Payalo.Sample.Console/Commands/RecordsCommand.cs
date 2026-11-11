using System.Globalization;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;

namespace Payalo.Sample.Console.Commands;

// `payalo records` — GET /records.
//
// Every call sends from, to and the filters. A follow-up page also sends the cursor, which is
// only valid for the same query: the gateway rejects a cursor with different values (400).
internal sealed class RecordsCommand
{
    private readonly PayaloClient _client;

    public RecordsCommand(PayaloClient client) => _client = client;

    public async Task ExecuteAsync(ParsedArgs args, CancellationToken ct)
    {
        Output.Banner("Records (paginated)");

        var pageCursor = args.Get("--page-cursor");

        var from = ParseTimestamp(args.Get("--from")) ?? Prompt.AskDateTime("From",
            DateTimeOffset.UtcNow.AddDays(-1));
        var to = ParseTimestamp(args.Get("--to")) ?? Prompt.AskDateTime("To",
            DateTimeOffset.UtcNow);
        var type = args.Get("--type");
        var method = args.Get("--method");
        var status = args.Get("--status");
        int? pageSize = args.Get("--page-size") is { } sizeStr
            && int.TryParse(sizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

        var page = await _client.GetRecordsAsync(from, to, type, method, status, pageSize, pageCursor, ct);

        Output.Success($"Returned {page.Data.Count} record(s).");
        if (page.Data.Count > 0)
        {
            foreach (var tx in page.Data.Take(10))
            {
                Output.Info($"  [{tx.Status,-7}] {tx.Type,-6} {tx.Method,-12} {tx.GatewayReference} " +
                            $"{tx.RequestedAmount?.Value} {tx.RequestedAmount?.Currency} @ {tx.CreatedAt:o}");
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
