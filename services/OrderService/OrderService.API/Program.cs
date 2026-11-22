using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrderService.API.Endpoints;
//using OrderService.Application.Commands.CreateOrder;
using OrderService.Application.Commands;
using OrderService.Infrastructure.Extensions;
using ECommerce.Shared.Infrastructure.Extensions;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting OrderService...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    // ========== 1. LOGGING FIRST ==========
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "OrderService")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}"));

    // ========== 2. TRACING - instruments entire pipeline ==========
    builder.Services.AddOpenTelemetryTracing(builder.Configuration, "OrderService");

    // ========== 3. INFRASTRUCTURE SERVICES - Kafka, Redis, DbContext ==========
    builder.Services.AddOrderInfrastructure(builder.Configuration);
    builder.Services.AddKafkaProducer(builder.Configuration);
    builder.Services.AddKafkaConsumer(builder.Configuration, groupId: "order-service-payment-events");
    builder.Services.AddSharedRedis(builder.Configuration);

    // ========== 4. APPLICATION SERVICES - MediatR, FluentValidation ==========
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateOrderCommand>());
    builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderCommandValidator>();

    // ========== 5. AUTHENTICATION & AUTHORIZATION ==========
    var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured. Set Jwt__Key env var.");
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt => opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "userservice",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ecommerce-api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        });
    builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy("Admin", p => p.RequireRole("Admin"));
        opts.AddPolicy("User", p => p.RequireRole("Admin", "User"));
    });

    // ========== 6. CONTROLLERS & HEALTH CHECKS ==========
    builder.Services.AddControllers();
    builder.Services
        .AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("OrderServiceDb")!, name: "postgres-order", tags: ["db", "postgres"]);

    // ========== 7. API DOCUMENTATION & CORS ==========
    builder.Services.AddOpenApi(opt => opt.AddDocumentTransformer((doc, _, ct) =>
    {
        doc.Info.Title = "OrderService API";
        doc.Info.Version = "v1";
        doc.Info.Description = "Order lifecycle + fulfilment saga orchestration (choreography via Kafka), Postgres + transactional outbox";
        return Task.CompletedTask;
    }));
    builder.Services.AddCors(opt => opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // ========== BUILD APP & CONFIGURE PIPELINE ==========
    var app = builder.Build();

    // ========== 8. DATABASE MIGRATIONS ==========
    if (app.Environment.IsDevelopment())
        await app.Services.ApplyOrderMigrationsAsync();

    // ========== 9. MIDDLEWARE PIPELINE ==========
    // Correlation ID EARLY in pipeline
    app.UseCorrelationId();

    // Logging
    app.UseSerilogRequestLogging(o => o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms");

    // API Documentation
    app.MapOpenApi();
    app.MapScalarApiReference(opt => opt.WithTitle("OrderService API").WithTheme(ScalarTheme.Moon));

    // CORS before Auth
    app.UseCors("AllowAll");

    // Exception handling
    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

    app.UseHttpsRedirection();

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Routes & Health Checks
    app.MapControllers();
    app.MapOrderEndpoints();
    app.MapHealthChecks("/health/live");
    app.MapHealthChecks("/health");

    Log.Information("OrderService ready");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrderService terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}