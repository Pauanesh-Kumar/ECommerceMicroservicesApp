using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PaymentService.Infrastructure.Gateways;

namespace PaymentService.Tests;

public class RazorpayReconciliationTests
{
    private static RazorpayReconciliationClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new Handler(respond)), Options.Create(new RazorpaySettings { BaseUrl = "https://example.test/v1", KeyId = "key", KeySecret = "secret" }));

    [Fact]
    public async Task FindsExactPaidOrderAndCapturedPayment()
    {
        var id = Guid.NewGuid();
        var client = Client(request => request.RequestUri!.AbsolutePath.EndsWith("/payments")
            ? Json(new { items = new[] { new { id = "pay_1", status = "captured", order_id = "order_1", amount = 1000, currency = "INR" } } })
            : Json(new { items = new[] { new { id = "order_1", receipt = id.ToString(), amount = 1000, currency = "INR", status = "paid" } } }));
        var result = await client.FindAsync(id, 10m, "INR", default);
        Assert.Equal("order_1", result!.Id);
        Assert.Equal("pay_1", result.CapturedPaymentId);
    }

    [Fact]
    public async Task RequiresExactReceiptAndNeverCreatesAnOrder()
    {
        var id = Guid.NewGuid();
        var client = Client(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Json(new { items = new[] { new { id = "order_1", receipt = "other-" + id, amount = 1000, currency = "INR", status = "created" } } });
        });
        Assert.Null(await client.FindAsync(id, 10m, "INR", default));
    }

    [Fact]
    public async Task RefusesMismatchedAmount()
    {
        var id = Guid.NewGuid();
        var client = Client(_ => Json(new { items = new[] { new { id = "order_1", receipt = id.ToString(), amount = 999, currency = "INR", status = "created" } } }));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.FindAsync(id, 10m, "INR", default));
    }

    [Fact]
    public async Task RefusesMultipleMatchingOrders()
    {
        var id = Guid.NewGuid();
        var item = new { id = "order_1", receipt = id.ToString(), amount = 1000, currency = "INR", status = "created" };
        var client = Client(_ => Json(new { items = new[] { item, item } }));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.FindAsync(id, 10m, "INR", default));
    }

    [Fact]
    public async Task PaidOrderWithoutCapturedPaymentIsNotConfirmed()
    {
        var id = Guid.NewGuid();
        var client = Client(request => request.RequestUri!.AbsolutePath.EndsWith("/payments")
            ? Json(new { items = Array.Empty<object>() })
            : Json(new { items = new[] { new { id = "order_1", receipt = id.ToString(), amount = 1000, currency = "INR", status = "paid" } } }));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.FindAsync(id, 10m, "INR", default));
    }

    [Fact]
    public async Task SearchesAllPagesAndCanReuseClient()
    {
        var id = Guid.NewGuid();
        var client = Client(request => request.RequestUri!.Query.Contains("skip=0")
            ? Json(new { items = Enumerable.Range(0, 100).Select(i => new { id = "other_" + i, receipt = "other-" + id, amount = 1000, currency = "INR", status = "created" }).ToArray() })
            : Json(new { items = new[] { new { id = "order_1", receipt = id.ToString(), amount = 1000, currency = "INR", status = "created" } } }));
        Assert.Equal("order_1", (await client.FindAsync(id, 10m, "INR", default))!.Id);
        Assert.Equal("order_1", (await client.FindAsync(id, 10m, "INR", default))!.Id);
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
