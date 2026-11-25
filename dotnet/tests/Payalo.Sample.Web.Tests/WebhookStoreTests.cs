using Payalo.Sample.Web.Integration.Models;
using Payalo.Sample.Web.Services;

namespace Payalo.Sample.Web.Tests;

public sealed class WebhookStoreTests
{
    private static MmoTransactionDto Payload(string status) =>
        new() { GatewayReference = "01J1ABCDEF123456", Status = status };

    [Fact]
    public void TryRecord_AcceptsSameReferenceWithChangedStatus()
    {
        var store = new WebhookStore();

        Assert.True(store.TryRecord(Payload("failed"), "{}", "127.0.0.1"));
        Assert.True(store.TryRecord(Payload("success"), "{}", "127.0.0.1"));
        Assert.Equal(2, store.Snapshot().Count);
    }

    [Fact]
    public void TryRecord_RejectsRepeatOfSameReferenceAndStatus()
    {
        var store = new WebhookStore();

        Assert.True(store.TryRecord(Payload("success"), "{}", "127.0.0.1"));
        Assert.False(store.TryRecord(Payload("success"), "{}", "127.0.0.1"));
        Assert.Single(store.Snapshot());
    }
}
