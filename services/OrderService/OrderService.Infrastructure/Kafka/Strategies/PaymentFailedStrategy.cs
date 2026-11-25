using MediatR;
using OrderService.Application.Commands;
using OrderService.Contracts;
using System.Text.Json;

namespace OrderService.Infrastructure.Kafka.Strategies;

public sealed class PaymentFailedStrategy(ISender mediator) : IOrderEventStrategy
{
    public string Topic => "payment-status:PaymentFailed";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task HandleAsync(string rawJson, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<PaymentFailedIntegrationEvent>(rawJson, Json);
        if (evt is null) return;
        // Payment failed AFTER stock was reserved (order was AwaitingPayment) — so this
        // path DOES need the compensating stock release, unlike StockReservationFailedStrategy.
        await mediator.Send(new CancelOrderCommand(evt.OrderId, evt.Reason, ReleaseStock: true, PaymentFailed: true), ct);
    }
}
