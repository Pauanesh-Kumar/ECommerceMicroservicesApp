using MediatR;
using PaymentService.Application.Commands.ConfirmPayment;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Queries.GetPayments;
using PaymentService.Domain.Repositories;
using System.Text.Json;

namespace PaymentService.API.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Payments").WithOpenApi();

        payments.MapGet("/order/{orderId:guid}", async (Guid orderId, IPaymentRepository repo, CancellationToken ct) =>
        {
            var payment = await repo.GetByOrderIdAsync(orderId, ct);
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        })
        .RequireAuthorization("User")
        .WithSummary("Get payment status for an order");

        // Cursor/keyset pagination — admin ledger view.
        payments.MapGet("/", async (string? cursor, int? limit, Guid? userId, ISender mediator, CancellationToken ct) =>
        {
            var page = await mediator.Send(new GetPaymentsQuery(cursor, limit ?? 20, userId), ct);
            return Results.Ok(new { items = page.Items, nextCursor = page.NextCursor, hasMore = page.HasMore });
        })
        .RequireAuthorization("Admin")
        .WithSummary("Cursor-paginated payment ledger (admin only)");

        // ── Razorpay webhook — server-to-server, no user JWT, signature-authenticated instead ──
        // Configure this exact path in Razorpay Dashboard → Settings → Webhooks.
        payments.MapPost("/webhooks/razorpay", async (
            HttpRequest http, IPaymentGateway gateway, ISender mediator, CancellationToken ct) =>
        {
            using var reader = new StreamReader(http.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var signature = http.Headers["X-Razorpay-Signature"].FirstOrDefault() ?? string.Empty;

            if (!gateway.VerifySignature(rawBody, signature))
                return Results.Unauthorized(); // do NOT process unverified webhook payloads

            var eventId = http.Headers["X-Razorpay-Event-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(eventId))
                return Results.BadRequest(new { error = "X-Razorpay-Event-Id header is required." });

            JsonElement payload;
            try { payload = JsonSerializer.Deserialize<JsonElement>(rawBody); }
            catch (JsonException) { return Results.BadRequest(new { error = "Invalid webhook JSON." }); }

            if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("event", out var eventValue) || eventValue.ValueKind != JsonValueKind.String)
                return Results.BadRequest(new { error = "Webhook event is required." });
            var eventType = eventValue.GetString();
            if (eventType is not ("payment.captured" or "payment.failed"))
                return Results.Ok();

            if (!payload.TryGetProperty("payload", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("payment", out var payment) || payment.ValueKind != JsonValueKind.Object
                || !payment.TryGetProperty("entity", out var entity) || entity.ValueKind != JsonValueKind.Object
                || !entity.TryGetProperty("order_id", out var orderId) || orderId.ValueKind != JsonValueKind.String
                || !entity.TryGetProperty("id", out var paymentId) || paymentId.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(orderId.GetString()) || string.IsNullOrWhiteSpace(paymentId.GetString()))
                return Results.BadRequest(new { error = "Webhook payment and order IDs are required." });

            var success = eventType == "payment.captured";
            var failureReason = !success && entity.TryGetProperty("error_description", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString() : null;

            await mediator.Send(new ConfirmPaymentCommand(eventId, orderId.GetString()!, paymentId.GetString()!, success, failureReason), ct);
            return Results.Ok();
        })
        .AllowAnonymous()
        .WithSummary("Razorpay webhook — HMAC-SHA256 signature verified, idempotent via Inbox");

        return app;
    }
}
