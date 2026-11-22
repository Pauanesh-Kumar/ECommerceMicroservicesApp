using MediatR;
using NotificationService.API.Models;
using NotificationService.Application.Features.Notifications.Commands.MarkAsRead;
using NotificationService.Application.Features.Notifications.Commands.SendNotification;
using NotificationService.Application.Features.Notifications.Queries.GetNotificationById;
using NotificationService.Application.Features.Notifications.Queries.GetNotifications;
using NotificationService.Application.Features.Notifications.Queries.GetNotificationsByUser;
using System.Security.Claims;

namespace NotificationService.API.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        // GET /api/notifications?page=1&pageSize=20
        group.MapGet("/", async (int page, int pageSize, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetNotificationsQuery(page, pageSize), ct)))
            .RequireAuthorization("Admin")
            .WithName("GetNotifications")
            .WithSummary("Get all notifications (paginated, admin/support only)");

        // GET /api/notifications/{id}
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNotificationByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization("Admin")
        .WithName("GetNotificationById")
        .WithSummary("Get a notification by ID (admin/support only)");

        // GET /api/notifications/user/{userId}
        group.MapGet("/user/{userId:guid}", async (Guid userId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetNotificationsByUserQuery(userId), ct)))
            .RequireAuthorization("Admin")
            .WithName("GetNotificationsByUser")
            .WithSummary("Get all notifications for a specific user (admin/support only)");

        // POST /api/notifications
        // NOTE: Disabled in production — notifications are created only via Kafka consumer.
        // Uncomment for local testing if you need to send a notification manually.

        /*group.MapPost("/", async (SendNotificationRequest req, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new SendNotificationCommand(
                req.UserId, req.Recipient, req.Subject, req.Body, req.Channel, req.EventType), ct);
            return Results.CreatedAtRoute("GetNotificationById", new { id }, new { id });
        })
        .RequireAuthorization("Admin")
        .WithName("SendNotification")
        .WithSummary("Send a notification via Email or SMS (testing only)");
        */

        // PUT /api/notifications/{id}/read
        group.MapPut("/{id:guid}/read", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var success = await sender.Send(new MarkAsReadCommand(id), ct);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization("User")
        .WithName("MarkAsRead")
        .WithSummary("Mark a notification as read/acknowledged (self-service)");

        return app;
    }
}
