using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentService.API.Endpoints;
using PaymentService.Application.Commands.ConfirmPayment;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Gateways;

namespace PaymentService.Tests;

public class RazorpayWebhookEndpointTests
{
    private const string Secret = "isolated-webhook-test-secret";

    [Theory]
    [InlineData("payment.captured", PaymentStatus.Succeeded)]
    [InlineData("payment.failed", PaymentStatus.Failed)]
    public async Task ValidSignedWebhookUsesHeaderEventId(string eventType, PaymentStatus expectedStatus)
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 25m, "INR", "order_test");
        var uow = new FakeUnitOfWork(payment);
        await using var app = await StartApp(uow);
        using var client = CreateClient(app);
        var body = Payload(eventType);
        using var request = Request(body, true, true);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal("evt_test", uow.InboxId);
        Assert.Equal("payment-status", uow.Topic);
        Assert.Equal(1, uow.Saves);
    }

    [Theory]
    [InlineData(false, true, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, HttpStatusCode.BadRequest)]
    public async Task RejectsMissingSignatureOrEventId(bool sign, bool eventId, HttpStatusCode expected)
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 25m, "INR", "order_test");
        var uow = new FakeUnitOfWork(payment);
        await using var app = await StartApp(uow);
        using var client = CreateClient(app);
        using var request = Request(Payload("payment.captured"), sign, eventId);

        using var response = await client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task IgnoresOtherSignedEventsWithoutFailingPayment()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 25m, "INR", "order_test");
        var uow = new FakeUnitOfWork(payment);
        await using var app = await StartApp(uow);
        using var client = CreateClient(app);
        using var request = Request(Payload("payment.authorized"), true, true);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(0, uow.Saves);
    }

    private static async Task<WebApplication> StartApp(FakeUnitOfWork uow)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("User", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("Admin", policy => policy.RequireAuthenticatedUser());
        });
        builder.Services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<ConfirmPaymentCommand>());
        builder.Services.AddSingleton<IPaymentUnitOfWork>(uow);
        builder.Services.AddScoped<PaymentService.Domain.Repositories.IPaymentRepository>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IPaymentGateway>(new RazorpayPaymentGateway(new HttpClient(), Options.Create(new RazorpaySettings { KeyId = "test", KeySecret = "test", WebhookSecret = Secret }), NullLogger<RazorpayPaymentGateway>.Instance));
        var app = builder.Build();
        app.UseAuthorization();
        app.MapPaymentEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static string Payload(string eventType) => System.Text.Json.JsonSerializer.Serialize(new
    {
        @event = eventType,
        payload = new { payment = new { entity = new { id = "pay_test", order_id = "order_test" } } }
    });

    private static HttpRequestMessage Request(string body, bool sign, bool eventId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/razorpay") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (sign)
            request.Headers.Add("X-Razorpay-Signature", Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body))));
        if (eventId) request.Headers.Add("X-Razorpay-Event-Id", "evt_test");
        return request;
    }

    private sealed class FakeUnitOfWork(Payment payment) : IPaymentUnitOfWork
    {
        public string? InboxId { get; private set; }
        public string? Topic { get; private set; }
        public int Saves { get; private set; }
        public Task<Payment?> FindByProviderOrderIdAsync(string id, CancellationToken ct = default) => Task.FromResult<Payment?>(id == payment.ProviderOrderId ? payment : null);
        public Task<Payment?> FindByOrderIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public void Add(Payment value) => throw new NotSupportedException();
        public void EnqueueOutboxMessage(string topic, string key, string payload) => Topic = topic;
        public void RecordInboxEvent(string eventId) => InboxId = eventId;
        public Task<bool> TrySaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.FromResult(true); }
    }
}
