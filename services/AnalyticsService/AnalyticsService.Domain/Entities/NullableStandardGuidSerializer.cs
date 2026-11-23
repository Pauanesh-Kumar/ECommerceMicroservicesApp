using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AnalyticsService.Domain.Entities;

public sealed class NullableStandardGuidSerializer : SerializerBase<Guid?>
{
    private static readonly NullableSerializer<Guid> Serializer = new(new GuidSerializer(GuidRepresentation.Standard));

    public override Guid? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        => Serializer.Deserialize(context, args);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid? value)
        => Serializer.Serialize(context, args, value);
}
