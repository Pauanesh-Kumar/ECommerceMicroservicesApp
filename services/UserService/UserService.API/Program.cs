using DotNetEnv;
using ECommerce.Shared.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Text;
using UserService.Api.GraphQL.Users;
using UserService.API.Endpoints;
using UserService.API.Extensions;
using UserService.Application.Behaviors;
using UserService.Application.Features.Auth.Commands.ExternalLogin;
using UserService.Application.Features.Users.Commands.RegisterUser;
using UserService.Infrastructure.Extensions;
using UserService.Infrastructure.Persistence;
using UserService.Infrastructure.Security;

// ------------------------------------------------------------------------------
//  BOOTSTRAP LOGGER — captures startup errors before Serilog is fully wired
// ------------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting UserService...");
    var builder = WebApplication.CreateBuilder(args);

    // CONFIGURATION priority order handled automatically by ASP.NET Core
    builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables(); // ✅ must be here, LAST

    // -- Serilog structured logging ---------------------------------------------
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "UserService")
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}"));

    // -- Infrastructure: EF Core + Redis + Kafka + Security + Email ------------
    builder.Services.AddInfrastructure(builder.Configuration);

    // -- MediatR + pipeline behaviors ------------------------------------------
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblyContaining<RegisterUserCommand>();
        cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });

    // -- FluentValidation ------------------------------------------------------
    builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserCommandValidator>();

    // -- JWT Bearer Authentication ---------------------------------------------
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
    builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy("Admin", p => p.RequireRole("Admin"));
        opts.AddPolicy("User", p => p.RequireRole("Admin", "User"));
    });


    // Cross-cutting infrastructure — must exist before anything that injects it ──
    builder.Services.AddCurrentUserAccessor();

    // GraphQL — depends on ICurrentUserAccessor being registered above ─────
    builder.Services
    .AddGraphQLServer()
    .AddAuthorization()   // <-- this is critical
    .AddQueryType<Query>()                // root Query
    .AddMutationType<Mutation>()          // root Mutation
    .AddTypeExtension<UserQueries>()      // extension with GetMeAsync
    .AddTypeExtension<UserMutations>()   // extension with mutations
    .AddErrorFilter(error =>
        error.Exception is not null
            ? error.WithMessage("An unexpected error occurred.")
            : error);

    // -- gRPC server ------------------------------------------------------------
    builder.Services.AddGrpc(opt =>
        opt.EnableDetailedErrors = builder.Environment.IsDevelopment());

    // -- Health checks ----------------------------------------------------------
    builder.Services
        .AddHealthChecks()
        .AddNpgSql(
            connectionString: builder.Configuration.GetConnectionString("UserServiceDb")!,
            name: "postgres-user",
            tags: new[] { "db", "postgres" })
        .AddRedis(
            redisConnectionString: builder.Configuration["Redis:ConnectionString"]!,
            name: "redis",
            tags: new[] { "cache", "redis" });

    // -- .NET 10 built-in OpenAPI ----------------------------------------------
    builder.Services.AddOpenApi(opt =>
    {
        opt.AddDocumentTransformer((doc, ctx, ct) =>
        {
            // Keep the transformer simple to avoid type mismatches with low-level OpenAPI models.
            doc.Info.Title = "UserService API";
            doc.Info.Version = "v1";
            doc.Info.Description = "User management — registration, authentication, profile";
            return Task.CompletedTask;
        });
    });

    // -- CORS -------------------------------------------------------------------
    builder.Services.AddCors(opt =>
        opt.AddPolicy("AllowAll", p =>
            p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    var app = builder.Build();

    // -- Auto-migrate EF Core on startup ---------------------------------------
    var dbOpts = builder.Configuration
        .GetSection(DatabaseOptions.Section)
        .Get<DatabaseOptions>() ?? new DatabaseOptions();

    if (dbOpts.MigrateOnStartup)
        await app.Services.ApplyMigrationsAsync();

    // -- Middleware pipeline ---------------------------------------------------
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging(o =>
        o.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms");

    // Always expose OpenAPI document (JSON)
    app.MapOpenApi();

    // Scalar UI: integrate with OpenAPI explorer — enabled in Development and Production
    // Scalar package must be restored (added to csproj above).
    app.MapScalarApiReference(opt =>
    {
        opt.WithTitle("UserService API")
           .WithTheme(ScalarTheme.Moon)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    app.UseCors("AllowAll");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapUserEndpoints();
    app.MapGraphQL("/graphql");
    app.MapGraphQLSchema("/graphql/sdl");


    app.MapHealthChecks("/health/live");
    app.MapHealthChecks("/health/ready");
    app.MapHealthChecks("/health");

    Log.Information("UserService ready");
    Log.Information("  Scalar UI  ? http://localhost:8080/scalar/v1");
    Log.Information("  OpenAPI    ? http://localhost:8080/openapi/v1.json");
    Log.Information("  Health     ? http://localhost:8080/health/live");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "UserService terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}