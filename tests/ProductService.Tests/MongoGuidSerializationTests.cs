using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Extensions;
using ProductService.Infrastructure.Persistence;

namespace ProductService.Tests;

public class MongoGuidSerializationTests
{
    [Fact]
    public void ProductAndInboxUseStandardUuidRepresentation()
    {
        var services = new ServiceCollection();
        services.AddProductInfrastructure(new ConfigurationBuilder().Build());
        var product = Product.Create("SKU", "Product", "Description", "Category", 5m, "INR", 2);
        var productDocument = product.ToBsonDocument();
        var eventId = Guid.NewGuid();
        var inboxDocument = new InboxRecord { EventId = eventId }.ToBsonDocument();

        Assert.Equal(BsonBinarySubType.UuidStandard, productDocument["_id"].AsBsonBinaryData.SubType);
        Assert.Equal(product.Id, BsonSerializer.Deserialize<Product>(productDocument).Id);
        Assert.Equal(BsonBinarySubType.UuidStandard, inboxDocument["_id"].AsBsonBinaryData.SubType);
        Assert.Equal(eventId, BsonSerializer.Deserialize<InboxRecord>(inboxDocument).EventId);
    }
}
