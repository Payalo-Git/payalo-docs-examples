namespace Payalo.Sample.Console.Integration.Models;

// Body of GET /records — one page of transactions plus opaque cursors.
//
// Cursor stability: `Pages.Next`/`Pages.Previous` are only valid for the query that produced
// them, so repeat from, to and the filters, and add `page`, on follow-up calls.
public sealed class RecordsResponseDto
{
    public List<MmoTransactionDto> Data { get; set; } = new();
    public PageCursors Pages { get; set; } = new();
}

public sealed class PageCursors
{
    public string? Next { get; set; }
    public string? Previous { get; set; }
}
