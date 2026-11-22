using ApiGateway.Aggregators;
using ApiGateway.Extensions;
using ApiGateway.Middleware;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// ── Bootstrap logger ──────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting API Gateway...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables(); // highest priority — overrides all above

    // ── Serilog (mirrors UserService / NotificationService template for uniform logs) ──
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithCorrelationId()           // Serilog.Enrichers.CorrelationId
        .Enrich.WithProperty("Service", "ApiGateway")
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

    // ── Modular service registrations (each Extension owns its own concern) ───
    builder.Services
        .AddGatewayAuth(builder.Configuration)           // Extensions/AuthExtensions
        .AddGatewayCors(builder.Configuration)           // Extensions/CorsExtensions
        .AddGatewayKafka(builder.Configuration)          // Extensions/KafkaExtensions
        .AddDownstreamClients(builder.Configuration)     // Extensions/HttpClientExtensions
        .AddGatewayHealthChecks(builder.Configuration);  // Extensions/HealthCheckExtensions

    // ── YARP (routes /api/users/**, /api/auth/**, /api/notifications/** to services) ──
    builder.Services
        .AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    // ── Aggregator (scoped — owns IHttpClientFactory + IProducer, no static state) ──
    builder.Services.AddScoped<UserNotificationAggregator>();

    // ── OpenAPI + Scalar UI — documents Gateway-native endpoints (aggregator, health) ──
    // NOTE: YARP-proxied routes (/api/users/**, /api/notifications/**) are NOT auto-documented
    // here because YARP forwards raw HTTP and does not generate OpenAPI metadata for proxy routes.
    // Use UserService's own Scalar UI (http://localhost:5042/scalar/v1) to test register/login,
    // then use THIS Gateway Scalar UI to test the /api/aggregate endpoint with the resulting JWT.
    builder.Services.AddOpenApi(opt =>
    {
        opt.AddDocumentTransformer((doc, ctx, ct) =>
        {
            doc.Info.Title = "API Gateway";
            doc.Info.Version = "v1";
            doc.Info.Description =
                "Gateway-native endpoints only (Aggregator, Health). " +
                "Proxied routes (/api/users/**, /api/notifications/**) are forwarded via YARP " +
                "and are best tested through UserService/NotificationService's own Scalar UIs, " +
                "or with the same JWT against this Gateway's base URL directly.";
            return Task.CompletedTask;
        });
    });

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // Order matters: error handler first → wraps everything
    app.UseGatewayErrorHandling();   // Middleware/ErrorHandlingMiddleware
    app.UseCorrelationId();          // Middleware/CorrelationIdMiddleware

    app.UseSerilogRequestLogging(o =>
        o.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms");

    app.UseCors(CorsExtensions.PolicyName);
    app.UseAuthentication();
    app.UseAuthorization();

    // ── OpenAPI + Scalar ─────────────────────────────────────────────────────
    app.MapOpenApi();
    app.MapScalarApiReference(opt =>
    {
        opt.WithTitle("API Gateway")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    // ── Health ────────────────────────────────────────────────────────────────
    app.MapHealthChecks("/health");

    // ── Aggregator endpoint (/api/aggregate/** bypasses YARP, runs custom orchestration) ──
    app.MapGroup("/api/aggregate")
       .RequireAuthorization()
       .MapGet("/users/{userId:guid}", async (
           Guid userId,
           UserNotificationAggregator aggregator,
           HttpContext ctx,
           CancellationToken ct) =>
           await aggregator.GetUserWithNotificationsAsync(userId, ctx, ct))
       .WithName("GetUserWithNotifications")
       .WithSummary("Returns merged user profile + notifications (aggregator pattern — single round-trip)");

    // ── YARP handles everything else ──────────────────────────────────────────
    app.MapReverseProxy();

    Log.Information("API Gateway ready");
    Log.Information("  Scalar UI   → http://localhost:8000/scalar/v1");
    Log.Information("  Proxy       → http://localhost:8000/api/users/**");
    Log.Information("  Proxy       → http://localhost:8000/api/notifications/**");
    Log.Information("  Aggregator  → http://localhost:8000/api/aggregate/users/{{userId}}");
    Log.Information("  Health      → http://localhost:8000/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API Gateway terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}