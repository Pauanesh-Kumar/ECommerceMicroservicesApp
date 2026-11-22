using CartService.API.Endpoints;
using CartService.Application.Commands.Checkout;
using CartService.Infrastructure.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
    Log.Information("Starting CartService...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "CartService")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddCartInfrastructure(builder.Configuration);
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CheckoutCommand>());

    var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured. Set Jwt__Key env var.");
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt => opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
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

    builder.Services.AddOpenApi(opt => opt.AddDocumentTransformer((doc, _, ct) =>
    {
        doc.Info.Title = "CartService API";
        doc.Info.Version = "v1";
        doc.Info.Description = "Redis-backed shopping cart with synchronous checkout hand-off to OrderService";
        return Task.CompletedTask;
    }));
    builder.Services.AddCors(opt => opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging(o => o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms");
    app.MapOpenApi();
    app.MapScalarApiReference(opt => opt.WithTitle("CartService API").WithTheme(ScalarTheme.Moon));
    app.UseCors("AllowAll");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapCartEndpoints();
    app.MapHealthChecks("/health/live");
    app.MapHealthChecks("/health");

    Log.Information("CartService ready");
    await app.RunAsync();
}
catch (Exception ex) { Log.Fatal(ex, "CartService terminated unexpectedly"); throw; }
finally { Log.CloseAndFlush(); }
