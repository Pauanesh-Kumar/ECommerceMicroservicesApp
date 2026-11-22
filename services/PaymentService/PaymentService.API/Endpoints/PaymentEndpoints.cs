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
            HttpRequest http, IPaymentGateway gateway, ISender mediator, IPaymentUnitOfWork uow, CancellationToken ct) =>
        {
            using var reader = new StreamReader(http.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var signature = http.Headers["X-Razorpay-Signature"].FirstOrDefault() ?? string.Empty;

            if (!gateway.VerifySignature(rawBody, signature))
                return Results.Unauthorized(); // do NOT process unverified webhook payloads

            var payload = JsonSerializer.Deserialize<JsonElement>(rawBody);
            var eventId = payload.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();

            // Inbox dedupe — Razorpay retries webhooks on any non-2xx response.
            uow.RecordInboxEvent(eventId);
            if (!await uow.TrySaveChangesAsync(ct))
                return Results.Ok(); // already processed — ack anyway

            var eventType = payload.GetProperty("event").GetString(); // e.g. "payment.captured" / "payment.failed"
            var entity = payload.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var providerOrderId = entity.GetProperty("order_id").GetString()!;
            var providerPaymentId = entity.GetProperty("id").GetString()!;

            var success = eventType == "payment.captured";
            var failureReason = success ? null : entity.TryGetProperty("error_description", out var e) ? e.GetString() : "Declined";

            await mediator.Send(new ConfirmPaymentCommand(providerOrderId, providerPaymentId, success, failureReason), ct);
            return Results.Ok();
        })
        .AllowAnonymous()
        .WithSummary("Razorpay webhook — HMAC-SHA256 signature verified, idempotent via Inbox");

        return app;
    }
}
