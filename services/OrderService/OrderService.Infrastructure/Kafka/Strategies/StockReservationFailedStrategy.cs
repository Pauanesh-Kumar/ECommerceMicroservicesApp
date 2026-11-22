using MediatR;
using OrderService.Application.Commands;
using OrderService.Contracts;
using System.Text.Json;

namespace OrderService.Infrastructure.Kafka.Strategies;

public sealed class StockReservationFailedStrategy(ISender mediator) : IOrderEventStrategy
{
    public string Topic => "product-stock:StockReservationFailed";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task HandleAsync(string rawJson, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<StockReservationFailedIntegrationEvent>(rawJson, Json);
        if (evt is null) return;
        // Stock was never reserved for the unavailable lines (Product already compensated
        // any partial reservation internally) — so ReleaseStock=false here.
        await mediator.Send(new CancelOrderCommand(evt.OrderId, evt.Reason, ReleaseStock: false), ct);
    }
}
