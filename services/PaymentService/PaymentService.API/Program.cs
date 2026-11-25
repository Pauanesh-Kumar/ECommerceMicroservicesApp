using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PaymentService.API.Endpoints;
using PaymentService.Infrastructure.Extensions;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting PaymentService...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "PaymentService")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddPaymentInfrastructure(builder.Configuration);
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.Load("PaymentService.Application")));
    builder.Services.AddValidatorsFromAssembly(Assembly.Load("PaymentService.Application"));

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
        doc.Info.Title = "PaymentService API";
        doc.Info.Version = "v1";
        doc.Info.Description = "Razorpay-backed payment ledger — idempotent charge creation, HMAC-verified webhook confirmation";
        return Task.CompletedTask;
    }));

    builder.Services
        .AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("PaymentServiceDb")!, name: "postgres-payment", tags: ["db", "postgres"]);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
        await app.Services.ApplyPaymentMigrationsAsync();

    app.UseSerilogRequestLogging(o => o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms");
    app.MapOpenApi();
    app.MapScalarApiReference(opt => opt.WithTitle("PaymentService API").WithTheme(ScalarTheme.Moon));
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapPaymentEndpoints();
    app.MapHealthChecks("/health/live");
    app.MapHealthChecks("/health");

    Log.Information("PaymentService ready");
    await app.RunAsync();
}
catch (Exception ex) { Log.Fatal(ex, "PaymentService terminated unexpectedly"); throw; }
finally { Log.CloseAndFlush(); }
