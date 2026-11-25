using AnalyticsService.Application.Queries.Reporting;
using AnalyticsService.Infrastructure.Reporting;

namespace AnalyticsService.Tests;

public class ReportingProjectionTests
{
    private static readonly DateTime Ingested = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void OrderCreatedTopicMapsUsingEventTimestamp()
    {
        var json = $$"""{"orderId":"{{OrderId}}","userId":"{{UserId}}","items":[],"occurredOnUtc":"2026-03-04T10:00:00Z"}""";
        var change = Assert.IsType<OrderCreatedChange>(ReportingEventMapper.Map("order-created", json, Ingested));
        Assert.Equal(OrderId, change.OrderId);
        Assert.Equal(UserId, change.UserId);
        Assert.Equal(new DateTime(2026, 3, 4, 10, 0, 0, DateTimeKind.Utc), change.AtUtc);
    }

    [Theory]
    [InlineData("order-confirmed")]
    [InlineData("OrderConfirmedIntegrationEvent")]
    [InlineData("OrderConfirmed")]
    public void OrderConfirmedMatchesTopicAndTypeSpellings(string eventType)
    {
        var json = $$"""{"orderId":"{{OrderId}}","userId":"{{UserId}}","amount":149.5,"occurredOnUtc":"2026-03-04T10:00:00Z"}""";
        var change = Assert.IsType<OrderConfirmedChange>(ReportingEventMapper.Map(eventType, json, Ingested));
        Assert.Equal(149.5m, change.Amount);
    }

    [Fact]
    public void OrderCancelledTopicMaps()
    {
        var json = $$"""{"orderId":"{{OrderId}}","userId":"{{UserId}}","reason":"x","occurredOnUtc":"2026-03-04T10:00:00Z"}""";
        Assert.IsType<OrderCancelledChange>(ReportingEventMapper.Map("order-cancelled", json, Ingested));
    }

    [Fact]
    public void PaymentEnvelopeWithStringPayloadMapsToSucceededPayment()
    {
        var paymentId = Guid.NewGuid();
        var inner = $$"""{"orderId":"{{OrderId}}","paymentId":"{{paymentId}}","providerReference":"pay_1","occurredOnUtc":"2026-03-04T10:00:00Z"}""";
        var envelope = System.Text.Json.JsonSerializer.Serialize(new { eventType = "PaymentProcessed", payload = inner });

        var change = Assert.IsType<PaymentChange>(ReportingEventMapper.Map("PaymentProcessed", envelope, Ingested));
        Assert.Equal(paymentId, change.PaymentId);
        Assert.Equal(OrderId, change.OrderId);
        Assert.Equal("Succeeded", change.Status);
    }

    [Fact]
    public void MissingTimestampFallsBackToIngestionTime()
    {
        var json = $$"""{"orderId":"{{OrderId}}"}""";
        var change = Assert.IsType<OrderCreatedChange>(ReportingEventMapper.Map("order-created", json, Ingested));
        Assert.Equal(Ingested, change.AtUtc);
    }

    [Theory]
    [InlineData("product-stock", """{"orderId":"00000000-0000-0000-0000-000000000001"}""")]
    [InlineData("order-created", "not json")]
    [InlineData("order-created", """{"userId":"00000000-0000-0000-0000-000000000001"}""")]
    [InlineData("PaymentFailed", """{"orderId":"00000000-0000-0000-0000-000000000001"}""")]
    public void UnmappableEventsAreIgnored(string eventType, string json)
        => Assert.Null(ReportingEventMapper.Map(eventType, json, Ingested));

    [Fact]
    public void ReportWindowDefaultsToThirtyDaysAndCapsAtOneYear()
    {
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var def = ReportWindow.Normalize(null, null, now);
        Assert.Equal(now, def.ToUtc);
        Assert.Equal(now.AddDays(-30), def.FromUtc);

        var wide = ReportWindow.Normalize(now.AddYears(-5), now, now);
        Assert.Equal(now.AddDays(-366), wide.FromUtc);

        var swapped = ReportWindow.Normalize(now, now.AddDays(-2), now);
        Assert.True(swapped.FromUtc <= swapped.ToUtc);
    }
}
