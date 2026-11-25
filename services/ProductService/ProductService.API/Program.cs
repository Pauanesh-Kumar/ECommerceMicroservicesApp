using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProductService.API.Endpoints;
using ProductService.Application.Commands.CreateProduct;
using ProductService.Infrastructure.Extensions;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Text;

// ------------------------------------------------------------------------------
//  BOOTSTRAP LOGGER — same pattern as UserService.API/Program.cs
// ------------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ProductService...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "ProductService")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}"));

    // -- Infrastructure: Mongo + Kafka --------------------------------------
    builder.Services.AddProductInfrastructure(builder.Configuration);

    // -- MediatR + FluentValidation -------------------------------------------
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateProductCommand>());
    builder.Services.AddValidatorsFromAssemblyContaining<CreateProductCommandValidator>();

    // -- JWT Bearer Authentication (same signing key/issuer/audience as UserService) --
    var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured. Set Jwt__Key env var.");
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

    builder.Services.AddOpenApi(opt =>
    {
        opt.AddDocumentTransformer((doc, _, ct) =>
        {
            doc.Info.Title = "ProductService API";
            doc.Info.Version = "v1";
            doc.Info.Description = "Product catalog — MongoDB, cursor-paginated listing, saga-driven stock reservation";
            return Task.CompletedTask;
        });
    });


    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging(o => o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms");

    app.MapOpenApi();
    app.MapScalarApiReference(opt => opt.WithTitle("ProductService API").WithTheme(ScalarTheme.Moon).WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapProductEndpoints();
    app.MapHealthChecks("/health/live");
    app.MapHealthChecks("/health");

    Log.Information("ProductService ready");
    Log.Information("  Scalar UI  ? http://localhost:8084/scalar/v1");
    Log.Information("  OpenAPI    ? http://localhost:8084/openapi/v1.json");
    Log.Information("  Health     ? http://localhost:8084/health/live");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ProductService terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
