using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using ProductService.Contracts;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Persistence;
using Xunit;

namespace ProductService.Infrastructure.Tests;

public sealed class InventoryTransactionTests
{
    [Fact]
    public async Task TransactionRollsBackOnOutboxFailureAndDeduplicatesReservationAndRelease()
    {
        var name = "ecommerce-inventory-test-" + Guid.NewGuid().ToString("N")[..12];
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var started = false;
        try
        {
            await Docker("run", "--detach", "--name", name, "--publish", $"127.0.0.1:{port}:27017", "--tmpfs", "/data/db", "mongo:7-jammy", "mongod", "--replSet", "test-rs", "--bind_ip_all");
            started = true;
            await WaitUntilAsync(async () =>
            {
                try { await Docker("exec", name, "mongosh", "--quiet", "--eval", "db.adminCommand({ping:1}).ok"); return true; }
                catch (InvalidOperationException) { return false; }
            });
            await Docker("exec", name, "mongosh", "--quiet", "--eval", "rs.initiate({_id:'test-rs',members:[{_id:0,host:'localhost:27017'}]})");
            await WaitUntilAsync(async () => (await Docker("exec", name, "mongosh", "--quiet", "--eval", "print(db.hello().isWritablePrimary)")).Trim() == "true");

            if (!BsonClassMap.IsClassMapRegistered(typeof(Product)))
                BsonClassMap.RegisterClassMap<Product>(map => { map.AutoMap(); map.MapIdMember(p => p.Id).SetSerializer(new GuidSerializer(GuidRepresentation.Standard)); });
            var client = new MongoClient($"mongodb://127.0.0.1:{port}/?directConnection=true");
            var db = client.GetDatabase("inventory_test");
            await new ProductIndexInitializer(db, NullLogger<ProductIndexInitializer>.Instance).StartAsync(default);
            var product = Product.Create("SKU", "Product", "Description", "Category", 10m, "INR", 5);
            await db.GetCollection<Product>("products").InsertOneAsync(product);
            var processor = new TransactionalInventoryProcessor(client, db);
            var orderId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var items = new[] { new OrderCreatedLineItem(product.Id, 2) };
            var created = JsonSerializer.Serialize(new OrderCreatedIntegrationEvent(orderId, Guid.NewGuid(), items, DateTime.UtcNow));
            await db.RunCommandAsync<BsonDocument>(new BsonDocument { { "collMod", "inventory_outbox" }, { "validator", new BsonDocument("$expr", new BsonDocument("$eq", new BsonArray { 1, 0 })) } });
            await Assert.ThrowsAnyAsync<MongoException>(() => processor.ProcessAsync("order-created", created, eventId, true, default));
            Assert.Equal(5, (await db.GetCollection<Product>("products").Find(p => p.Id == product.Id).SingleAsync()).Stock);
            Assert.Equal(0, await db.GetCollection<BsonDocument>("inventory_inbox").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(0, await db.GetCollection<BsonDocument>("inventory_reservations").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            await db.RunCommandAsync<BsonDocument>(new BsonDocument { { "collMod", "inventory_outbox" }, { "validator", new BsonDocument() } });
            await processor.ProcessAsync("order-created", created, eventId, true, default);
            await processor.ProcessAsync("order-created", created, eventId, true, default);
            await processor.ProcessAsync("order-created", created, Guid.NewGuid(), true, default);
            var reserved = await db.GetCollection<Product>("products").Find(p => p.Id == product.Id).SingleAsync();
            Assert.Equal(3, reserved.Stock);
            Assert.Equal(2, reserved.ReservedStock);
            Assert.Equal(1, await db.GetCollection<BsonDocument>("inventory_outbox").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            var release = JsonSerializer.Serialize(new StockReleaseRequestedEvent(orderId, items, DateTime.UtcNow));
            await processor.ProcessAsync("stock-release", release, Guid.NewGuid(), false, default);
            await processor.ProcessAsync("stock-release", release, Guid.NewGuid(), false, default);
            var restored = await db.GetCollection<Product>("products").Find(p => p.Id == product.Id).SingleAsync();
            Assert.Equal(5, restored.Stock);
            Assert.Equal(0, restored.ReservedStock);

            var conflicting = JsonSerializer.Serialize(new OrderCreatedIntegrationEvent(orderId, Guid.NewGuid(), [new(product.Id, 1)], DateTime.UtcNow));
            await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync("order-created", conflicting, eventId, true, default));
            await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync("order-created", conflicting, Guid.NewGuid(), true, default));
            var legacyEventId = Guid.NewGuid();
            await db.GetCollection<InboxRecord>("inbox").InsertOneAsync(new InboxRecord { EventId = legacyEventId });
            var legacy = JsonSerializer.Serialize(new OrderCreatedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), items, DateTime.UtcNow));
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync("order-created", legacy, legacyEventId, true, default));
            Assert.Equal(5, (await db.GetCollection<Product>("products").Find(p => p.Id == product.Id).SingleAsync()).Stock);
        }
        finally
        {
            if (started) await Docker("rm", "--force", name);
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(1000);
        }
        throw new TimeoutException("Timed out waiting for the isolated MongoDB replica set.");
    }

    private static async Task<string> Docker(params string[] args)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Docker.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await process.WaitForExitAsync(timeout.Token);
        var stdout = await output;
        var stderr = await error;
        if (process.ExitCode != 0) throw new InvalidOperationException($"Docker {args[0]} failed: {stderr}");
        return stdout;
    }
}
