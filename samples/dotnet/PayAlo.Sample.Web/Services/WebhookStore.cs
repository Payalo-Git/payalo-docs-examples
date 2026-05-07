using System.Collections.Concurrent;
using PayAlo.Sample.Web.Integration.Models;

namespace PayAlo.Sample.Web.Services;

// In-memory log of received gateway callbacks. DEMO ONLY — wipes on restart.
//
// A production integration writes each entry to a database keyed on the gateway
// `gatewayReference` so callbacks are durable, dedupable across replicas, and
// reconcilable via /records.
public sealed class WebhookStore
{
    private const int MaxEntries = 200;

    private readonly ConcurrentDictionary<string, WebhookEntry> _byReference = new();
    private readonly LinkedList<string> _insertionOrder = new();
    private readonly object _orderLock = new();

    public event Action? Changed;

    // Atomic insert-if-absent. Returns true when newly stored, false on duplicate.
    // The `gatewayReference` field is the gateway-assigned transaction identifier and
    // is the recommended deduplication key per the v2 callbacks docs.
    public bool TryRecord(MmoTransactionDto payload, string rawBody, string sourceIp)
    {
        if (string.IsNullOrWhiteSpace(payload.GatewayReference)) return false;

        var entry = new WebhookEntry(DateTimeOffset.UtcNow, sourceIp, payload, rawBody);
        if (!_byReference.TryAdd(payload.GatewayReference, entry)) return false;

        lock (_orderLock)
        {
            _insertionOrder.AddFirst(payload.GatewayReference);
            while (_insertionOrder.Count > MaxEntries)
            {
                var oldest = _insertionOrder.Last!.Value;
                _insertionOrder.RemoveLast();
                _byReference.TryRemove(oldest, out _);
            }
        }

        Changed?.Invoke();
        return true;
    }

    public IReadOnlyList<WebhookEntry> Snapshot()
    {
        lock (_orderLock)
        {
            return _insertionOrder
                .Select(reference => _byReference.TryGetValue(reference, out var e) ? e : null)
                .Where(e => e is not null)
                .Select(e => e!)
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_orderLock)
        {
            _insertionOrder.Clear();
            _byReference.Clear();
        }
        Changed?.Invoke();
    }
}

public sealed record WebhookEntry(
    DateTimeOffset ReceivedAt,
    string SourceIp,
    MmoTransactionDto Payload,
    string RawBody);
