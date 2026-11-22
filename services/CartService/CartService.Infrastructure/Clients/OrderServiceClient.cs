using System.Net.Http.Json;
using CartService.Application.Interfaces;
using CartService.Contracts;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CartService.Infrastructure.Clients;

/// <summary>
/// Concrete IOrderServiceClient — owns the actual HTTP call against the "OrderService"
/// named HttpClient (registered with AddStandardResilienceHandler in
/// InfrastructureServiceExtensions). All resilience-library exception types are caught
/// and translated HERE into a plain CreateOrderClientResult, so the Application layer
/// (CheckoutCommandHandler) never needs a reference to Polly at all — that's the whole
/// point of the IOrderServiceClient boundary.
/// </summary>
public sealed class OrderServiceClient(
    IHttpClientFactory httpClientFactory, ILogger<OrderServiceClient> logger) : IOrderServiceClient
{
    public async Task<CreateOrderClientResult> CreateOrderAsync(
        Guid userId, IReadOnlyList<OrderLineForCreation> items, string idempotencyKey, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("OrderService");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest(
                userId, items.Select(i => new CreateOrderLineRequest(i.ProductId, i.Name, i.UnitPrice, i.Quantity)).ToList()))
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return new CreateOrderClientResult(false, null, $"OrderService returned {response.StatusCode}");

            var body = await response.Content.ReadFromJsonAsync<CreateOrderResponse>(cancellationToken: ct);
            return body is null
                ? new CreateOrderClientResult(false, null, "Malformed OrderService response.")
                : new CreateOrderClientResult(true, body.OrderId, null);
        }
        // ── Circuit is open: OrderService has already failed enough recent calls that
        // we're not even attempting the network hop. Fail fast with a clear, distinct
        // message rather than let the exception surface as an unhandled 500 — this is
        // the whole reason the breaker exists: give the caller (and Order) a clean
        // signal to back off instead of piling more load onto a struggling service.
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(ex, "OrderService circuit is open — failing checkout fast for UserId={UserId}", userId);
            return new CreateOrderClientResult(false, null, "Checkout is temporarily unavailable. Please try again shortly.");
        }
        catch (TimeoutRejectedException ex)
        {
            logger.LogWarning(ex, "OrderService call timed out (all retries exhausted) for UserId={UserId}", userId);
            return new CreateOrderClientResult(false, null, "Checkout is taking longer than expected. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "OrderService call failed for UserId={UserId}", userId);
            return new CreateOrderClientResult(false, null, "Unable to reach order service.");
        }
    }
}
