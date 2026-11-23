using AnalyticsService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace AnalyticsService.Tests;

public class AnalyticsEventSerializationTests
{
    [Fact]
    public void EventIdentityAndGuidUseStableMongoRepresentation()
    {
        var orderId = Guid.NewGuid();
        var evt = new AnalyticsEvent
        {
            SourceEventId = "order-created:event:123",
            SourceTopic = "order-created",
            EventType = "order-created",
            OrderId = orderId,
            RawPayload = "{}"
        };

        var document = evt.ToBsonDocument();
        Assert.Equal(BsonBinarySubType.UuidStandard, document["OrderId"].AsBsonBinaryData.SubType);
        var restored = BsonSerializer.Deserialize<AnalyticsEvent>(document);
        Assert.Equal(orderId, restored.OrderId);
        Assert.Equal(evt.SourceEventId, restored.SourceEventId);
    }

    [Fact]
    public void LegacyEventWithoutSourceIdentityStillDeserializes()
    {
        var document = new AnalyticsEvent
        {
            SourceTopic = "legacy", EventType = "legacy", RawPayload = "{}"
        }.ToBsonDocument();
        Assert.False(document.Contains("SourceEventId"));
        Assert.Null(BsonSerializer.Deserialize<AnalyticsEvent>(document).SourceEventId);
    }
}
