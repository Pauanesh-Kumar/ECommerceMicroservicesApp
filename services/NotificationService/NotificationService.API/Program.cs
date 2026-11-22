using ECommerce.Shared.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using NotificationService.Api.GraphQL;
using NotificationService.API.Endpoints;
using NotificationService.Application.Behaviors;
using NotificationService.Application.Extensions;
using NotificationService.Infrastructure.Extensions;
using NotificationService.Infrastructure.Persistence.MongoDb;
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
    Log.Information("Starting Notification Service...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "NotificationService")
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddCurrentUserAccessor();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // ── JWT Bearer Authentication — same signing key/issuer/audience as
    // UserService, since tokens are minted there and must validate here too.
    var jwtKey = builder.Configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Jwt:Key is not configured. Set Jwt__Key env var.");
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "userservice";
    var jwtAud = builder.Configuration["Jwt:Audience"] ?? "ecommerce-api";

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAud,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

    // Same two-tier policy scheme as UserService
    builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy("Admin", p => p.RequireRole("Admin"));
        opts.AddPolicy("User", p => p.RequireRole("Admin", "User"));
    });

    var mongoSettings = builder.Configuration
        .GetSection(MongoDbSettings.SectionName)
        .Get<MongoDbSettings>() ?? new MongoDbSettings();

    builder.Services
        .AddHealthChecks()
        .AddMongoDb(
            clientFactory: sp => sp.GetRequiredService<IMongoClient>(),
            name: "mongodb-notification",
            tags: ["db", "mongodb"])
        .AddRedis(
            redisConnectionString: builder.Configuration["Redis:ConnectionString"]!,
            name: "redis",
            tags: ["cache", "redis"]);

    builder.Services.AddOpenApi(opt =>
    {
        opt.AddDocumentTransformer((doc, ctx, ct) =>
        {
            doc.Info.Title = "NotificationService API";
            doc.Info.Version = "v1";
            doc.Info.Description = "Notification management — send, query, and track notifications";
            return Task.CompletedTask;
        });
    });

    builder.Services.AddCors(opt =>
        opt.AddPolicy("AllowAll", p =>
            p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // ── GraphQL — .AddAuthorization() registered so [Authorize]/[Authorize(Policy=...)]
    // actually enforce; this was missing before, meaning [Authorize] on
    // GetMyNotificationsAsync had no policy backing to check against.
    builder.Services
        .AddGraphQLServer()
        .AddAuthorization()
        .AddQueryType<Query>()
        .AddMutationType<Mutation>()
        .AddTypeExtension<NotificationQueries>()
        .AddTypeExtension<NotificationMutations>()
        .AddErrorFilter(error =>
            error.Exception is not null
                ? error.WithMessage("An unexpected error occurred.")
                : error);

    var app = builder.Build();

    app.UseSerilogRequestLogging(o =>
        o.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms");

    app.MapOpenApi();

    app.MapScalarApiReference(opt =>
        opt.WithTitle("NotificationService API")
           .WithTheme(ScalarTheme.Moon)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));

    app.UseCors("AllowAll");
    app.UseAuthentication();     // NEW — must precede UseAuthorization
    app.UseAuthorization();      // NEW

    app.MapNotificationEndpoints();
    app.MapGraphQL("/graphql");

    app.MapHealthChecks("/health/live");
    app.MapHealthChecks("/health/ready");

    Log.Information("NotificationService ready");
    Log.Information("  Scalar UI  → http://localhost:8082/scalar/v1");
    Log.Information("  OpenAPI    → http://localhost:8082/openapi/v1.json");
    Log.Information("  Health     → http://localhost:8082/health/live");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotificationService terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}