using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ECommerce.Tests.Integration;

internal sealed class ProviderStub : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, JsonObject> orders = new();
    private readonly ConcurrentQueue<JsonObject> calls = new();
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int pauseNext;
    private WebApplication? app;
    public string Url { get; private set; } = "";
    public int CallCount => calls.Count;
    public int CallsFor(string receipt) => calls.Count(c => c["receipt"]?.GetValue<string>() == receipt);
    public long AmountFor(string receipt) => orders[receipt]["amount"]!.GetValue<long>();
    public string ProviderOrderId(string receipt) => orders[receipt]["id"]!.GetValue<string>();
    public void PauseNext() => Interlocked.Exchange(ref pauseNext, 1);
    public Task WaitEnteredAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(90));
    public void Release() => released.TrySetResult();

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        app = builder.Build();
        app.MapGet("/v1/orders", (HttpRequest request) =>
        {
            var receipt = request.Query["receipt"].ToString();
            return Results.Json(new { items = orders.Values.Where(o => o["receipt"]!.GetValue<string>().Contains(receipt)).Select(o => o.DeepClone()).ToArray() });
        });
        app.MapGet("/v1/orders/{id}/payments", (string id) =>
        {
            var order = orders.Values.FirstOrDefault(o => o["id"]!.GetValue<string>() == id);
            return Results.Json(new { items = order?["payments"]?.DeepClone() ?? new JsonArray() });
        });
        app.MapPost("/v1/orders", async (HttpRequest request) =>
        {
            var body = await JsonNode.ParseAsync(request.Body) as JsonObject ?? throw new InvalidDataException("Invalid provider request.");
            calls.Enqueue((JsonObject)body.DeepClone());
            var receipt = body["receipt"]!.GetValue<string>();
            var order = new JsonObject { ["id"] = "order_test_" + receipt.Replace("-", ""), ["receipt"] = receipt, ["amount"] = body["amount"]!.DeepClone(), ["currency"] = body["currency"]!.DeepClone(), ["status"] = "created" };
            if (!orders.TryAdd(receipt, order)) throw new InvalidOperationException("Duplicate provider order request.");
            if (Interlocked.Exchange(ref pauseNext, 0) == 1)
            {
                entered.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            return Results.Json(new { id = order["id"]!.GetValue<string>() });
        });
        await app.StartAsync();
        Url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public void Capture(string receipt, string paymentId)
    {
        var original = orders[receipt];
        var updated = (JsonObject)original.DeepClone();
        updated["payments"] = new JsonArray(new JsonObject { ["id"] = paymentId, ["order_id"] = updated["id"]!.DeepClone(), ["amount"] = updated["amount"]!.DeepClone(), ["currency"] = updated["currency"]!.DeepClone(), ["status"] = "captured" });
        updated["status"] = "paid";
        orders[receipt] = updated;
    }

    public async ValueTask DisposeAsync()
    {
        Release();
        if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
    }
}
