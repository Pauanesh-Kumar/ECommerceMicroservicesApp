using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentService.Infrastructure.Gateways;
using PaymentService.Infrastructure.Resilience;

namespace PaymentService.Tests;

public class RazorpayGatewayTests
{
    [Theory]
    [InlineData("https://api.razorpay.com/v1")]
    [InlineData("https://api.razorpay.com/v1/")]
    public async Task CreateOrderUsesVersionedPathAndSubunitAmount(string baseUrl)
    {
        var handler = new RecordingHandler();
        var settings = Options.Create(new RazorpaySettings { BaseUrl = baseUrl, KeyId = "test-id", KeySecret = "test-secret", WebhookSecret = "webhook-secret" });
        var gateway = new RazorpayPaymentGateway(new HttpClient(handler), settings, NullLogger<RazorpayPaymentGateway>.Instance);
        var orderId = Guid.NewGuid();

        var result = await gateway.CreateOrderAsync(orderId, 12.34m, "INR");

        Assert.True(result.Success);
        Assert.Equal("order_test", result.ProviderOrderId);
        Assert.Equal("https://api.razorpay.com/v1/orders", handler.Url);
        Assert.Equal("Basic", handler.AuthorizationScheme);
        using var body = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        Assert.Equal(1234, body.RootElement.GetProperty("amount").GetInt64());
        Assert.Equal(orderId.ToString(), body.RootElement.GetProperty("receipt").GetString());
    }

    [Fact]
    public async Task ResiliencePipelineDoesNotRetryOrderCreationPost()
    {
        var handler = new UnavailableHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("provider")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddStandardResilienceHandler(RazorpayResilienceOptions.Configure);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("provider");

        using var response = await client.PostAsync("https://example.test/v1/orders", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public void WebhookSignatureRequiresMatchingHmac()
    {
        const string payload = "{\"event\":\"payment.captured\"}";
        const string secret = "webhook-secret";
        var settings = Options.Create(new RazorpaySettings { KeyId = "test-id", KeySecret = "test-secret", WebhookSecret = secret });
        var gateway = new RazorpayPaymentGateway(new HttpClient(), settings, NullLogger<RazorpayPaymentGateway>.Instance);
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
        Assert.True(gateway.VerifySignature(payload, signature));
        Assert.False(gateway.VerifySignature(payload + " ", signature));
        Assert.False(gateway.VerifySignature(payload, ""));
    }

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri?.ToString();
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { id = "order_test" }) };
        }
    }
}
