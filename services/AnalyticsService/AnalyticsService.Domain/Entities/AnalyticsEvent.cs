using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AnalyticsService.Domain.Entities;

/// <summary>
/// Deliberately schema-loose raw sink — every event from every topic lands here
/// verbatim (RawPayload). This is the append-only "bronze layer"; the nightly
/// projection job reads from here and writes the curated star-schema tables in
/// Postgres that Power BI actually connects to. Never point Power BI at this
/// collection directly.
/// </summary>
public sealed class AnalyticsEvent
{
    [BsonId] public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    [BsonIgnoreIfNull] public string? SourceEventId { get; init; }
    public string SourceTopic { get; init; } = default!;
    public string EventType { get; init; } = default!;
    [BsonSerializer(typeof(NullableStandardGuidSerializer))] public Guid? OrderId { get; init; }
    [BsonSerializer(typeof(NullableStandardGuidSerializer))] public Guid? UserId { get; init; }
    public decimal? Amount { get; init; }
    public string RawPayload { get; init; } = default!;
    public DateTime IngestedAtUtc { get; init; } = DateTime.UtcNow;
    public bool ProjectedToReporting { get; set; }
}
