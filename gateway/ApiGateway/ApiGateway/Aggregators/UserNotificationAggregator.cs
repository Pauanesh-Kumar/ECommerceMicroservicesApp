using System.Text.Json;
using System.Text.Json.Nodes;
using ApiGateway.Configuration;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace ApiGateway.Aggregators;

/// <summary>
/// Aggregator pattern: fetches User (UserService /api/users/{id}) and
/// their Notifications (NotificationService /api/notifications/user/{userId}) in parallel,
/// merges into a single response — eliminating client round-trips.
///
/// Endpoint path on NotificationService matches existing:
///   NotificationEndpoints.MapNotificationEndpoints → GET /api/notifications/user/{userId:guid}
///   → GetNotificationsByUserQuery
///
/// After a successful fetch, publishes a lightweight audit event on topic "user-updated"
/// (matches UserService KafkaOptions.UserUpdatedTopic and NotificationService KafkaSettings.UserUpdatedTopic).
/// NotificationService ignores unknown EventType values gracefully via EventTypeMapper.Resolve().
/// </summary>
public sealed class UserNotificationAggregator(
    IHttpClientFactory httpClientFactory,
    IProducer<string, string> kafkaProducer,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<UserNotificationAggregator> logger)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<IResult> GetUserWithNotificationsAsync(
        Guid userId,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userClient = httpClientFactory.CreateClient("UserService");
        var notifClient = httpClientFactory.CreateClient("NotificationService");

        PropagateHeaders(httpContext, userClient, notifClient);

        // Parallel downstream calls — no sequential blocking
        var userTask = userClient.GetAsync($"/api/users/{userId}", ct);
        var notifTask = notifClient.GetAsync($"/api/notifications/user/{userId}", ct);

        await Task.WhenAll(userTask, notifTask);

        var userResponse = userTask.Result;
        var notifResponse = notifTask.Result;

        if (!userResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("UserService returned {Status} for userId={UserId}",
                userResponse.StatusCode, userId);
            return Results.StatusCode((int)userResponse.StatusCode);
        }

        var userJson = JsonNode.Parse(
            await userResponse.Content.ReadAsStringAsync(ct));

        JsonNode? notifJson = null;
        if (notifResponse.IsSuccessStatusCode)
        {
            notifJson = JsonNode.Parse(
                await notifResponse.Content.ReadAsStringAsync(ct));
        }
        else
        {
            logger.LogWarning(
                "NotificationService returned {Status} for userId={UserId} — returning empty list",
                notifResponse.StatusCode, userId);
        }

        var aggregate = new JsonObject
        {
            ["user"] = userJson,
            ["notifications"] = notifJson ?? new JsonArray()
        };

        // Fire-and-forget audit event — never blocks the response
        _ = PublishAuditEventAsync(userId, ct);

        return Results.Ok(aggregate);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void PropagateHeaders(
        HttpContext ctx,
        params HttpClient[] clients)
    {
        // Forward JWT and Correlation-ID to downstream services
        string[] headersToForward = ["Authorization", "X-Correlation-ID"];

        foreach (var header in headersToForward)
        {
            if (!ctx.Request.Headers.TryGetValue(header, out var value)) continue;
            foreach (var client in clients)
            {
                client.DefaultRequestHeaders.Remove(header);
                client.DefaultRequestHeaders.TryAddWithoutValidation(header, value.ToString());
            }
        }
    }

    private async Task PublishAuditEventAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // EventType "GatewayAggregation" — EventTypeMapper.Resolve() returns Unknown,
            // NotificationService logs a warning and skips it gracefully (no crash).
            var payload = JsonSerializer.Serialize(new
            {
                eventType = "GatewayAggregation",
                userId,
                occurredOn = DateTime.UtcNow
            }, JsonOpts);

            await kafkaProducer.ProduceAsync(
                kafkaOptions.Value.UserUpdatedTopic,
                new Message<string, string> { Key = userId.ToString(), Value = payload },
                ct);

            logger.LogDebug("Audit event published for userId={UserId}", userId);
        }
        catch (Exception ex)
        {
            // Kafka failure MUST NOT affect the HTTP response — log only
            logger.LogError(ex, "Failed to publish audit event for userId={UserId}", userId);
        }
    }
}