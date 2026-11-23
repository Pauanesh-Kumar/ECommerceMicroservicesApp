using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CartService.Application.Interfaces;
using CartService.Infrastructure.Clients;
using Microsoft.Extensions.Logging.Abstractions;

namespace CartService.Tests;

public class OrderServiceClientTests
{
    [Fact]
    public async Task ForwardsBearerTokenIdempotencyKeyAndOrderLines()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var handler = new RecordingHandler(orderId);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://order.test") };
        var orderClient = new OrderServiceClient(new ClientFactory(client), NullLogger<OrderServiceClient>.Instance);

        var result = await orderClient.CreateOrderAsync(userId, [new OrderLineForCreation(productId, "Item", 15m, 2)], "stable-key", "test-token");

        Assert.True(result.Success);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal("stable-key", handler.IdempotencyKey);
        Assert.Equal("/api/orders", handler.Path);
        using var json = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(productId, json.RootElement.GetProperty("items")[0].GetProperty("productId").GetGuid());
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Guid orderId) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
            Path = request.RequestUri?.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { orderId }) };
        }
    }
}
