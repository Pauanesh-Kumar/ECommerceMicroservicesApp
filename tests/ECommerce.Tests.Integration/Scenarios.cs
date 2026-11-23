using System.Text.Json;
using System.Text.Json.Nodes;

namespace ECommerce.Tests.Integration;

internal sealed partial class SagaTest
{
    private async Task ScenariosAsync()
    {
        var user = Guid.NewGuid().ToString();
        var auth = new Dictionary<string, string> { ["Authorization"] = "Bearer " + Jwt(user) };
        await SqlAsync("orderdb", "CREATE FUNCTION flow_fail_payment_outbox() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Topic\" = 'payment-requested' THEN RAISE EXCEPTION 'injected outbox failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER flow_fail_payment_outbox BEFORE INSERT ON outbox_messages FOR EACH ROW EXECUTE FUNCTION flow_fail_payment_outbox();");
        var productId = await ProductAsync(auth, "Test product", 25, 5);
        await CartAsync(auth, productId, "Test product", 25, 2);
        Check(await MongoAsync("print(db.getSiblingDB('productservice_db').runCommand({collMod:'inventory_outbox',validator:{$expr:{$eq:[1,0]}}}).ok)") == "1", "Could not inject inventory failure.");
        await PauseKafkaAsync();
        var orderId = await CheckoutAsync(auth);
        Check(await SqlAsync("orderdb", $"SELECT count(*) FROM outbox_messages WHERE \"Key\" = '{orderId}' AND \"Topic\" = 'order-created' AND \"ProcessedAtUtc\" IS NULL") == "1", "Order event was not retained while Kafka was paused.");
        await UnpauseKafkaAsync();
        Console.WriteLine("PASS: order outbox retained during Kafka suspension");
        var cart = await RequestAsync("Cart", "/api/cart/", headers: auth);
        Check(cart.Status == 200 && cart.Body!["items"]!.AsArray().Count == 0, "Cart not cleared.");
        await EventuallyAsync("Product transaction failure", () => LogContainsAsync("Product", "Failed to process"));
        await CheckStockAsync(productId, 5, 0);
        Check(await MongoAsync("print(db.getSiblingDB('productservice_db').inventory_inbox.countDocuments({}))") == "0", "Inbox persisted despite inventory failure.");
        Check(await MongoAsync("print(db.getSiblingDB('productservice_db').inventory_reservations.countDocuments({}))") == "0", "Reservation persisted despite inventory failure.");
        await StopServiceAsync("Product");
        Check(await MongoAsync("print(db.getSiblingDB('productservice_db').runCommand({collMod:'inventory_outbox',validator:{}}).ok)") == "1", "Could not restore inventory validator.");
        await StartServiceAsync("Product");
        Console.WriteLine("PASS: Product rolls back and retries after restart");
        await EventuallyAsync("Order transaction failure", () => LogContainsAsync("Order", "Failed to process"));
        Check(await SqlAsync("orderdb", "SELECT count(*) FROM inbox_messages") == "0", "Order inbox persisted despite failure.");
        Check(await SqlAsync("orderdb", $"SELECT \"Status\" FROM orders WHERE \"Id\" = '{orderId}'") == "Pending", "Order transition did not roll back.");
        Check(await SqlAsync("orderdb", "SELECT count(*) FROM outbox_messages WHERE \"Topic\" = 'payment-requested'") == "0", "Order outbox persisted despite failure.");
        await SqlAsync("orderdb", "DROP TRIGGER flow_fail_payment_outbox ON outbox_messages; DROP FUNCTION flow_fail_payment_outbox();");
        Console.WriteLine("PASS: failed order transition rolls back inbox/outbox and retries");
        var payment = await PendingPaymentAsync(auth, orderId);
        Check(payment["amount"]!.GetValue<decimal>() == 50 && provider.CallsFor(orderId) == 1 && provider.AmountFor(orderId) == 5000, "Unexpected payment or provider order amount.");
        await CheckStockAsync(productId, 3, 2);
        await StopServiceAsync("Order");
        Check(await WebhookAsync("payment.captured", "evt_test", Str(payment, "providerOrderId"), "pay_test") == 200, "Capture webhook failed.");
        await StartServiceAsync("Order");
        await ConfirmedAsync(auth, user, orderId);
        var confirmedPayment = await RequestAsync("Payment", "/api/payments/order/" + orderId, headers: auth);
        Check(confirmedPayment.Status == 200 && confirmedPayment.Body!["status"]!.GetValue<int>() == 1, "Captured payment not marked succeeded.");
        Check(await WebhookAsync("payment.captured", "evt_test", Str(payment, "providerOrderId"), "pay_test") == 200, "Webhook replay failed.");
        Console.WriteLine("PASS: checkout and Order restart; signed webhook replay");
        var eventId = await SqlAsync("orderdb", $"SELECT \"Id\" FROM outbox_messages WHERE \"Key\" = '{orderId}' AND \"Topic\" = 'order-created'");
        var sourceId = "order-created:event:" + Guid.Parse(eventId).ToString("N");
        async Task<string> AnalyticsCount() => await MongoAsync($"print(db.getSiblingDB('analyticsservice_db').events.countDocuments({{SourceEventId:'{sourceId}'}}))");
        await EventuallyAsync("Analytics event", async () => await AnalyticsCount() == "1");
        await SqlAsync("orderdb", $"UPDATE outbox_messages SET \"ProcessedAtUtc\" = NULL WHERE \"Id\" = '{eventId}'");
        await ComposeAsync("exec", "-T", "kafka", "kafka-console-consumer", "--bootstrap-server", "kafka:29092", "--topic", "order-created", "--from-beginning", "--max-messages", "2", "--timeout-ms", "60000");
        await EventuallyAsync("Analytics duplicate offset", () => CaughtUpAsync("analytics-service-group", "order-created", 2));
        await EventuallyAsync("Product duplicate offset", () => CaughtUpAsync("product-service-group", "order-created", 2));
        Check(await AnalyticsCount() == "1", "Analytics duplicated the event.");
        await CheckStockAsync(productId, 3, 2);
        Check(await MongoAsync($"print(db.getSiblingDB('productservice_db').inventory_outbox.countDocuments({{key:'{orderId}'}}))") == "1", "Product published a duplicate event.");
        Console.WriteLine("PASS: Product and Analytics deduplicate replayed event");
        await MongoAsync("db.getSiblingDB('analyticsservice_db').runCommand({collMod:'events',validator:{$nor:[{SourceTopic:'order-cancelled'}]}})");
        var scarce = await ProductAsync(auth, "Scarce", 10, 1);
        await CartAsync(auth, scarce, "Scarce", 10, 2);
        var scarceOrder = await CheckoutAsync(auth);
        await OrderStatusAsync(auth, user, scarceOrder, "Cancelled");
        await CheckStockAsync(scarce, 1, 0);
        Check((await RequestAsync("Payment", "/api/payments/order/" + scarceOrder, headers: auth)).Status == 404 && provider.CallCount == 1, "Payment created for unavailable stock.");
        await EventuallyAsync("Analytics rejected write", () => LogContainsAsync("Analytics", "Ingestion failed at"));
        Check(await MongoAsync("print(db.getSiblingDB('analyticsservice_db').events.countDocuments({SourceTopic:'order-cancelled'}))") == "0", "Rejected Analytics write persisted.");
        await MongoAsync("db.getSiblingDB('analyticsservice_db').runCommand({collMod:'events',validator:{}})");
        await EventuallyAsync("Analytics recovery", async () => await MongoAsync("print(db.getSiblingDB('analyticsservice_db').events.countDocuments({SourceTopic:'order-cancelled'}))") == "1");
        Console.WriteLine("PASS: insufficient stock and Analytics write retry");
        var failureProduct = await ProductAsync(auth, "Failure test", 10, 3);
        await CartAsync(auth, failureProduct, "Failure test", 10, 1);
        var failureOrder = await CheckoutAsync(auth);
        payment = await PendingPaymentAsync(auth, failureOrder);
        Check(await WebhookAsync("payment.failed", "evt_failed", Str(payment, "providerOrderId"), "pay_failed") == 200, "Failed attempt webhook failed.");
        payment = (await RequestAsync("Payment", "/api/payments/order/" + failureOrder, headers: auth)).Body!;
        Check(payment["status"]!.GetValue<int>() == 0, "Failed attempt resolved payment.");
        await CheckStockAsync(failureProduct, 2, 1);
        Check(await SqlAsync("paymentdb", $"SELECT count(*) FROM outbox_messages WHERE \"Key\" = '{failureOrder}' AND \"Topic\" = 'payment-status'") == "0", "Failed attempt emitted terminal outcome.");
        Check(await WebhookAsync("payment.captured", "evt_retry", Str(payment, "providerOrderId"), "pay_retry") == 200, "Retry capture failed.");
        await ConfirmedAsync(auth, user, failureOrder);
        Check(provider.CallCount == 2, "Unexpected provider calls.");
        Console.WriteLine("PASS: failed attempt followed by successful capture");
        var raceProduct = await ProductAsync(auth, "Race test", 10, 3);
        await CartAsync(auth, raceProduct, "Race test", 10, 1);
        var raceOrder = await CheckoutAsync(auth);
        payment = await PendingPaymentAsync(auth, raceOrder);
        await SqlAsync("paymentdb", "CREATE FUNCTION flow_delay_payment_update() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_sleep(3); RETURN NEW; END $$; CREATE TRIGGER flow_delay_payment_update BEFORE UPDATE ON payments FOR EACH ROW EXECUTE FUNCTION flow_delay_payment_update();");
        var race = await Task.WhenAll(WebhookAsync("payment.captured", "race-captured", Str(payment, "providerOrderId"), "pay_race"), WebhookAsync("payment.failed", "race-failed", Str(payment, "providerOrderId"), "pay_race"));
        Check(race.All(code => code == 200), "Concurrent webhooks failed.");
        Check(await SqlAsync("paymentdb", $"SELECT count(*) FROM outbox_messages WHERE \"Key\" = '{raceOrder}' AND \"Topic\" = 'payment-status'") == "1", "Concurrent webhooks produced duplicate outcomes.");
        Check(await SqlAsync("paymentdb", "SELECT count(*) FROM inbox_messages WHERE \"EventId\" IN ('race-captured','race-failed')") == "2", "Concurrent webhooks not both recorded.");
        await SqlAsync("paymentdb", "DROP TRIGGER flow_delay_payment_update ON payments; DROP FUNCTION flow_delay_payment_update();");
        Console.WriteLine("PASS: concurrent capture and failed attempt");
        await SqlAsync("paymentdb", "CREATE FUNCTION flow_fail_payment_intent() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected intent failure'; END $$; CREATE TRIGGER flow_fail_payment_intent BEFORE INSERT ON payments FOR EACH ROW EXECUTE FUNCTION flow_fail_payment_intent();");
        provider.PauseNext();
        var crashProduct = await ProductAsync(auth, "Payment crash", 10, 3);
        await CartAsync(auth, crashProduct, "Payment crash", 10, 1);
        var crashOrder = await CheckoutAsync(auth);
        await EventuallyAsync("Payment intent failure", () => LogContainsAsync("Payment", "Failed to process"));
        eventId = await SqlAsync("orderdb", $"SELECT \"Id\" FROM outbox_messages WHERE \"Key\" = '{crashOrder}' AND \"Topic\" = 'payment-requested'");
        Check(!string.IsNullOrEmpty(eventId) && await SqlAsync("paymentdb", $"SELECT count(*) FROM inbox_messages WHERE \"EventId\" = '{eventId}'") == "0", "Inbox persisted without payment intent.");
        Check(await SqlAsync("paymentdb", $"SELECT count(*) FROM payments WHERE \"OrderId\" = '{crashOrder}'") == "0" && provider.CallsFor(crashOrder) == 0, "Provider called before durable intent.");
        await SqlAsync("paymentdb", "DROP TRIGGER flow_fail_payment_intent ON payments; DROP FUNCTION flow_fail_payment_intent();");
        await provider.WaitEnteredAsync();
        Check(await SqlAsync("paymentdb", $"SELECT \"Status\" FROM payments WHERE \"OrderId\" = '{crashOrder}'") == "Initiating", "Payment intent not recorded.");
        Check(await SqlAsync("paymentdb", $"SELECT count(*) FROM inbox_messages WHERE \"EventId\" = '{eventId}'") == "1", "Payment inbox not recorded.");
        await StopServiceAsync("Payment");
        provider.Release();
        await SqlAsync("paymentdb", $"UPDATE payments SET \"CreatedAtUtc\" = now() - interval '5 minutes' WHERE \"OrderId\" = '{crashOrder}'");
        await StartServiceAsync("Payment");
        payment = await PendingPaymentAsync(auth, crashOrder);
        Check(Str(payment, "providerOrderId") == provider.ProviderOrderId(crashOrder) && provider.CallsFor(crashOrder) == 1, "Provider order not safely reconciled.");
        Check(await SqlAsync("paymentdb", $"SELECT count(*) FROM outbox_messages WHERE \"Key\" = '{crashOrder}' AND \"Topic\" = 'payment-status'") == "0", "Unknown payment outcome was published.");
        provider.Capture(crashOrder, "pay_reconciled");
        await ConfirmedAsync(auth, user, crashOrder);
        Check(await SqlAsync("paymentdb", $"SELECT \"Status\" FROM payments WHERE \"OrderId\" = '{crashOrder}'") == "Succeeded", "Capture was not reconciled.");
        Check(await SqlAsync("paymentdb", $"SELECT count(*) FROM outbox_messages WHERE \"Key\" = '{crashOrder}' AND \"Topic\" = 'payment-status'") == "1", "Unexpected payment outcome count.");
        Console.WriteLine("PASS: Payment restart and missed-webhook reconciliation");
        var payload = JsonSerializer.Serialize(new { orderId = crashOrder, items = new[] { new { productId = crashProduct, quantity = 1 } }, occurredOnUtc = "2026-01-01T00:00:00Z" });
        var ids = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        foreach (var id in ids) await SqlAsync("orderdb", $"INSERT INTO outbox_messages (\"Id\",\"Topic\",\"Key\",\"Payload\",\"CreatedAtUtc\",\"Attempts\") VALUES ('{id}','stock-release','{crashOrder}','{payload}',now(),0)");
        await EventuallyAsync("duplicate release processing", async () => await MongoAsync($"print(db.getSiblingDB('productservice_db').inventory_inbox.countDocuments({{_id:{{$in:['{ids[0]}','{ids[1]}']}}}}))") == "2");
        await CheckStockAsync(crashProduct, 3, 0);
        Console.WriteLine("PASS: duplicate stock releases compensate once");
    }

    private static string Str(JsonNode node, string key) => node[key]!.GetValue<string>();
    private async Task<bool> LogContainsAsync(string service, string value)
    {
        await using var stream = new FileStream(Path.Combine(directory, service + ".log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return (await reader.ReadToEndAsync()).Contains(value);
    }
    private async Task<string> ProductAsync(Dictionary<string, string> auth, string name, int price, int stock)
    {
        var (status, body) = await RequestAsync("Product", "/api/products/", new { sku = "test-" + Guid.NewGuid().ToString("N"), name, description = "test", category = "test", price, currency = "INR", initialStock = stock }, auth);
        Check(status == 201 && body is not null, "Product creation failed: " + status);
        return Str(body!, "productId");
    }
    private async Task CartAsync(Dictionary<string, string> auth, string id, string name, int price, int quantity)
    {
        var (status, body) = await RequestAsync("Cart", "/api/cart/items", new { productId = id, name, unitPrice = price, quantity }, auth);
        Check(status == 200 && body is not null && body["total"]!.GetValue<decimal>() == price * quantity, "Cart update or total failed: " + status);
    }
    private async Task<string> CheckoutAsync(Dictionary<string, string> auth)
    {
        var headers = new Dictionary<string, string>(auth) { ["Idempotency-Key"] = "saga-" + Guid.NewGuid().ToString("N") };
        var (status, body) = await RequestAsync("Cart", "/api/cart/checkout", new { }, headers);
        Check(status == 200 && body is not null, "Checkout failed: " + status);
        return Str(body!, "orderId");
    }
    private async Task<JsonNode> PendingPaymentAsync(Dictionary<string, string> auth, string order)
    {
        JsonNode? found = null;
        await EventuallyAsync("pending payment " + order, async () => { var result = await RequestAsync("Payment", "/api/payments/order/" + order, headers: auth); if (result.Status == 200 && result.Body?["providerOrderId"] is not null) found = result.Body; return found is not null; });
        return found!;
    }
    private Task ConfirmedAsync(Dictionary<string, string> auth, string user, string order) => OrderStatusAsync(auth, user, order, "Confirmed");
    private Task OrderStatusAsync(Dictionary<string, string> auth, string user, string order, string expected) => EventuallyAsync("order " + expected, async () =>
    {
        var result = await RequestAsync("Order", "/api/orders/user/" + user, headers: auth);
        return result.Status == 200 && result.Body!["data"]!.AsArray().Any(item => item?["orderId"]?.GetValue<string>() == order && item?["status"]?.GetValue<string>() == expected);
    });
    private async Task CheckStockAsync(string id, int stock, int reserved)
    {
        var result = await RequestAsync("Product", "/api/products/" + id);
        Check(result.Status == 200 && result.Body!["stock"]!.GetValue<int>() == stock && result.Body["reservedStock"]!.GetValue<int>() == reserved, "Unexpected stock for " + id);
    }
    private async Task<bool> CaughtUpAsync(string group, string topic, int minimum)
    {
        var output = await ComposeAsync("exec", "-T", "kafka", "kafka-consumer-groups", "--bootstrap-server", "kafka:29092", "--describe", "--group", group);
        return output.Split('\n').Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Any(row => row.Length >= 6 && row[1] == topic && row[3] == row[4] && long.TryParse(row[4], out var offset) && offset >= minimum);
    }
}
