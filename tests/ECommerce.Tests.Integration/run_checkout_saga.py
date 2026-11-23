r"""Run an isolated checkout saga against disposable Docker infrastructure.

From the repository root: python tests\ECommerce.Tests.Integration\run_checkout_saga.py
Requires Docker Compose, .NET 10 SDK and Python 3. No real payment provider is contacted.
"""
import base64
import contextlib
import hashlib
import hmac
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = Path(__file__).resolve().parents[2]
COMPOSE = ROOT / "tests" / "ECommerce.Tests.Integration" / "docker-compose.saga-test.yml"
TEST_JWT_KEY = "isolated-integration-test-signing-key-not-for-production"
WEBHOOK_SECRET = "isolated-webhook-secret"


def unused_port():
	with socket.socket() as sock:
		sock.bind(("127.0.0.1", 0))
		return sock.getsockname()[1]


def token(user_id):
	def encode(data):
		return base64.urlsafe_b64encode(data).rstrip(b"=")
	header = encode(json.dumps({"alg": "HS256", "typ": "JWT"}, separators=(",", ":")).encode())
	body = encode(json.dumps({"sub": user_id, "role": "Admin", "iss": "userservice", "aud": "ecommerce-api", "exp": int(time.time()) + 600}, separators=(",", ":")).encode())
	signature = encode(hmac.new(TEST_JWT_KEY.encode(), header + b"." + body, hashlib.sha256).digest())
	return b".".join((header, body, signature)).decode()


def request(url, method="GET", body=None, headers=None):
	data = None if body is None else json.dumps(body, separators=(",", ":")).encode()
	req = urllib.request.Request(url, data=data, method=method, headers=headers or {})
	if data is not None:
		req.add_header("Content-Type", "application/json")
	try:
		with urllib.request.urlopen(req, timeout=10) as response:
			raw = response.read()
			if not raw:
				return response.status, None
			if "application/json" in response.headers.get("Content-Type", ""):
				return response.status, json.loads(raw)
			return response.status, raw.decode(errors="replace")
	except urllib.error.HTTPError as error:
		return error.code, error.read().decode(errors="replace")


def eventually(description, action, timeout=90):
	deadline = time.monotonic() + timeout
	last = None
	while time.monotonic() < deadline:
		try:
			last = action()
			if last:
				return last
		except (OSError, ValueError, KeyError) as error:
			last = str(error)
		time.sleep(1)
	raise AssertionError(f"Timed out waiting for {description}; last result: {last}")


class Provider(BaseHTTPRequestHandler):
	calls = []

	def do_POST(self):
		if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
			chunks = []
			while True:
				size = int(self.rfile.readline().split(b";", 1)[0].strip(), 16)
				if size == 0:
					while self.rfile.readline() not in (b"\r\n", b"\n", b""):
						pass
					break
				chunks.append(self.rfile.read(size))
				self.rfile.read(2)
			raw = b"".join(chunks)
		else:
			raw = self.rfile.read(int(self.headers.get("Content-Length", "0")))
		body = json.loads(raw)
		self.calls.append((self.path, body))
		if self.path != "/v1/orders":
			self.send_error(404)
			return
		payload = json.dumps({"id": "order_test_" + body["receipt"].replace("-", "")}).encode()
		self.send_response(200)
		self.send_header("Content-Type", "application/json")
		self.send_header("Content-Length", str(len(payload)))
		self.end_headers()
		self.wfile.write(payload)

	def log_message(self, format, *args):
		pass


def main():
	ports = {name: unused_port() for name in ("KAFKA", "POSTGRES", "MONGO", "REDIS", "PRODUCT", "CART", "ORDER", "PAYMENT", "ANALYTICS")}
	env = os.environ.copy()
	for key in list(env):
		if key.startswith(("ConnectionStrings__", "Kafka__", "Redis__", "Jwt__", "Razorpay__", "Services__", "OpenTelemetry__")):
			del env[key]
	for name in ("KAFKA", "POSTGRES", "MONGO", "REDIS"):
		env[f"FLOW_{name}_PORT"] = str(ports[name])
	project = "ecommerce-saga-test-" + uuid.uuid4().hex[:12]
	compose = ["docker", "compose", "-p", project, "-f", str(COMPOSE)]
	processes = []
	service_processes = {}
	service_environments = {}
	logs = []
	provider = None
	started = False
	broker_paused = False
	test_directory = tempfile.TemporaryDirectory(prefix="ecommerce-saga-test-")
	try:
		for service in ("Product", "Cart", "Order", "Payment", "Analytics"):
			project_file = ROOT / "services" / f"{service}Service" / f"{service}Service.API" / f"{service}Service.API.csproj"
			subprocess.run(["dotnet", "build", str(project_file), "--nologo", "-v", "quiet"], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
		started = True
		subprocess.run(compose + ["up", "-d", "--wait", "--wait-timeout", "180"], env=env, check=True)
		for database in ("orderdb", "paymentdb", "analyticsdb"):
			subprocess.run(compose + ["exec", "-T", "postgres", "createdb", "-U", "flowtest", database], env=env, check=True)
		for topic in ("order-created", "stock-release", "product-stock", "payment-requested", "payment-status", "order-confirmed", "order-cancelled"):
			subprocess.run(compose + ["exec", "-T", "kafka", "kafka-topics", "--bootstrap-server", "kafka:29092", "--create", "--if-not-exists", "--topic", topic, "--partitions", "1", "--replication-factor", "1"], env=env, check=True, stdout=subprocess.DEVNULL)

		provider = ThreadingHTTPServer(("127.0.0.1", 0), Provider)
		provider_thread = threading.Thread(target=provider.serve_forever, daemon=True)
		provider_thread.start()
		common = {
			"ASPNETCORE_ENVIRONMENT": "Development", "Jwt__Key": TEST_JWT_KEY,
			"Jwt__Issuer": "userservice", "Jwt__Audience": "ecommerce-api",
			"Kafka__BootstrapServers": f"127.0.0.1:{ports['KAFKA']}",
			"Redis__ConnectionString": f"127.0.0.1:{ports['REDIS']}",
		}
		settings = {
			"Product": {"ConnectionStrings__ProductServiceDb": f"mongodb://127.0.0.1:{ports['MONGO']}"},
			"Cart": {"Services__OrderServiceBaseUrl": f"http://127.0.0.1:{ports['ORDER']}"},
			"Order": {"ConnectionStrings__OrderServiceDb": f"Host=127.0.0.1;Port={ports['POSTGRES']};Database=orderdb;Username=flowtest;Password=local-test-only"},
			"Payment": {"ConnectionStrings__PaymentServiceDb": f"Host=127.0.0.1;Port={ports['POSTGRES']};Database=paymentdb;Username=flowtest;Password=local-test-only", "Razorpay__BaseUrl": f"http://127.0.0.1:{provider.server_port}/v1", "Razorpay__KeyId": "test-key", "Razorpay__KeySecret": "test-key-secret", "Razorpay__WebhookSecret": WEBHOOK_SECRET},
			"Analytics": {"ConnectionStrings__AnalyticsServiceDb": f"mongodb://127.0.0.1:{ports['MONGO']}", "ConnectionStrings__AnalyticsReportingDb": f"Host=127.0.0.1;Port={ports['POSTGRES']};Database=analyticsdb;Username=flowtest;Password=local-test-only"},
		}
		with contextlib.nullcontext(test_directory.name) as temp:
			for service in ("Product", "Order", "Payment", "Cart", "Analytics"):
				service_env = env.copy()
				service_env.update(common)
				service_env.update(settings[service])
				service_env["ASPNETCORE_URLS"] = f"http://127.0.0.1:{ports[service.upper()]}"
				dll = ROOT / "services" / f"{service}Service" / f"{service}Service.API" / "bin" / "Debug" / "net10.0" / f"{service}Service.API.dll"
				log = open(Path(temp) / f"{service}.log", "w+", encoding="utf-8")
				logs.append((service, log))
				process = subprocess.Popen(["dotnet", str(dll)], cwd=temp, env=service_env, stdout=log, stderr=subprocess.STDOUT)
				processes.append(process)
				service_processes[service] = process
				service_environments[service] = service_env
				eventually(f"{service} health", lambda: request(f"http://127.0.0.1:{ports[service.upper()]}/health/live")[0] == 200)

			def order_sql(sql):
				return subprocess.run(compose + ["exec", "-T", "postgres", "psql", "-U", "flowtest", "-d", "orderdb", "-v", "ON_ERROR_STOP=1", "-At", "-c", sql], env=env, check=True, capture_output=True, text=True).stdout.strip()

			order_sql('CREATE FUNCTION flow_fail_payment_outbox() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW."Topic" = \'payment-requested\' THEN RAISE EXCEPTION \'injected outbox failure\'; END IF; RETURN NEW; END $$; CREATE TRIGGER flow_fail_payment_outbox BEFORE INSERT ON outbox_messages FOR EACH ROW EXECUTE FUNCTION flow_fail_payment_outbox();')
			user_id = str(uuid.uuid4())
			auth = {"Authorization": "Bearer " + token(user_id)}
			status, product = request(f"http://127.0.0.1:{ports['PRODUCT']}/api/products/", "POST", {"sku": "integration-" + uuid.uuid4().hex, "name": "Test product", "description": "test", "category": "test", "price": 25, "currency": "INR", "initialStock": 5}, auth)
			assert status == 201, f"Product creation failed: {status} {product}"
			product_id = product["productId"]
			status, cart = request(f"http://127.0.0.1:{ports['CART']}/api/cart/items", "POST", {"productId": product_id, "name": "Test product", "unitPrice": 25, "quantity": 2}, auth)
			assert status == 200 and cart["total"] == 50, f"Cart update failed: {status} {cart}"
			subprocess.run(compose + ["pause", "kafka"], env=env, check=True)
			broker_paused = True
			status, checkout = request(f"http://127.0.0.1:{ports['CART']}/api/cart/checkout", "POST", {}, {**auth, "Idempotency-Key": "saga-" + uuid.uuid4().hex})
			assert status == 200, f"Checkout failed: {status} {checkout}"
			order_id = checkout["orderId"]
			assert order_sql(f'SELECT count(*) FROM outbox_messages WHERE "Key" = \'{order_id}\' AND "Topic" = \'order-created\' AND "ProcessedAtUtc" IS NULL') == "1", "Order event was not retained during the Kafka outage"
			subprocess.run(compose + ["unpause", "kafka"], env=env, check=True)
			broker_paused = False
			print("PASS: order outbox retained during Kafka suspension; broker resumed")
			status, cart = request(f"http://127.0.0.1:{ports['CART']}/api/cart/", headers=auth)
			assert status == 200 and not cart["items"], f"Cart not cleared: {status} {cart}"

			eventually("order consumer retry after injected PostgreSQL failure", lambda: "Failed to process" in (Path(temp) / "Order.log").read_text(encoding="utf-8"))
			assert order_sql('SELECT count(*) FROM inbox_messages') == "0", "Inbox persisted despite failed transition"
			assert order_sql(f'SELECT "Status" FROM orders WHERE "Id" = \'{order_id}\'') == "Pending", "Order transition did not roll back"
			assert order_sql('SELECT count(*) FROM outbox_messages WHERE "Topic" = \'payment-requested\'') == "0", "Outbox persisted despite failed transition"
			order_sql('DROP TRIGGER flow_fail_payment_outbox ON outbox_messages; DROP FUNCTION flow_fail_payment_outbox();')
			print("PASS: failed order transition rolls back inbox/outbox and Kafka retries")

			def pending_payment():
				status, payment = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/order/{order_id}", headers=auth)
				return payment if status == 200 and payment.get("providerOrderId") else None
			payment = eventually("pending payment after Kafka stock reservation", pending_payment)
			assert payment["amount"] == 50, payment
			assert Provider.calls and Provider.calls[0][0] == "/v1/orders" and Provider.calls[0][1]["amount"] == 5000, Provider.calls
			status, product = request(f"http://127.0.0.1:{ports['PRODUCT']}/api/products/{product_id}")
			assert status == 200 and product["stock"] == 3 and product["reservedStock"] == 2, product

			service_processes["Order"].kill()
			service_processes["Order"].wait(timeout=30)
			webhook = {"event": "payment.captured", "payload": {"payment": {"entity": {"id": "pay_test", "order_id": payment["providerOrderId"]}}}}
			raw = json.dumps(webhook, separators=(",", ":")).encode()
			signature = hmac.new(WEBHOOK_SECRET.encode(), raw, hashlib.sha256).hexdigest()
			status, result = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/webhooks/razorpay", "POST", webhook, {"X-Razorpay-Signature": signature, "X-Razorpay-Event-Id": "evt_test"})
			assert status == 200, f"Webhook failed: {status} {result}"
			order_dll = ROOT / "services" / "OrderService" / "OrderService.API" / "bin" / "Debug" / "net10.0" / "OrderService.API.dll"
			restart_log = open(Path(temp) / "Order-restart.log", "w+", encoding="utf-8")
			logs.append(("Order restart", restart_log))
			processes.append(subprocess.Popen(["dotnet", str(order_dll)], cwd=temp, env=service_environments["Order"], stdout=restart_log, stderr=subprocess.STDOUT))
			eventually("restarted Order health", lambda: request(f"http://127.0.0.1:{ports['ORDER']}/health/live")[0] == 200)

			def confirmed():
				status, orders = request(f"http://127.0.0.1:{ports['ORDER']}/api/orders/user/{user_id}", headers=auth)
				if status == 200:
					return next((o for o in orders["data"] if o["orderId"] == order_id and o["status"] == "Confirmed"), None)
				return None
			eventually("order confirmation after payment-status Kafka event", confirmed)
			print("PASS: order saga resumes after abrupt Order process termination")
			status, payment = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/order/{order_id}", headers=auth)
			assert status == 200 and payment["status"] == 1, f"Payment not succeeded: {payment}"
			status, result = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/webhooks/razorpay", "POST", webhook, {"X-Razorpay-Signature": signature, "X-Razorpay-Event-Id": "evt_test"})
			assert status == 200, f"Webhook replay failed: {status} {result}"
			print("PASS: checkout -> Kafka stock reservation -> payment -> signed webhook -> confirmed order")

			event_id = order_sql(f'SELECT "Id" FROM outbox_messages WHERE "Key" = \'{order_id}\' AND "Topic" = \'order-created\'')
			source_id = "order-created:event:" + uuid.UUID(event_id).hex
			def analytics_count():
				js = f'print(db.getSiblingDB("analyticsservice_db").events.countDocuments({{SourceEventId: "{source_id}"}}))'
				return int(subprocess.run(compose + ["exec", "-T", "mongo", "mongosh", "--quiet", "--eval", js], env=env, check=True, capture_output=True, text=True).stdout.strip())
			eventually("Analytics order-created event", lambda: analytics_count() == 1)
			order_sql(f'UPDATE outbox_messages SET "ProcessedAtUtc" = NULL WHERE "Id" = \'{event_id}\'')
			subprocess.run(compose + ["exec", "-T", "kafka", "kafka-console-consumer", "--bootstrap-server", "kafka:29092", "--topic", "order-created", "--from-beginning", "--max-messages", "2", "--timeout-ms", "60000"], env=env, check=True, capture_output=True, text=True, timeout=75)
			def analytics_caught_up():
				output = subprocess.run(compose + ["exec", "-T", "kafka", "kafka-consumer-groups", "--bootstrap-server", "kafka:29092", "--describe", "--group", "analytics-service-group"], env=env, check=True, capture_output=True, text=True).stdout
				return any(len(row) >= 6 and row[1] == "order-created" and row[3] == row[4] and int(row[4]) >= 2 for row in (line.split() for line in output.splitlines()))
			eventually("Analytics duplicate event offset committed", analytics_caught_up)
			assert analytics_count() == 1, "Analytics duplicated the order-created event"
			print("PASS: Analytics deduplicates replayed outbox event and commits its offset")

			def mongo_eval(js):
				return subprocess.run(compose + ["exec", "-T", "mongo", "mongosh", "--quiet", "--eval", js], env=env, check=True, capture_output=True, text=True).stdout.strip()
			mongo_eval('db.getSiblingDB("analyticsservice_db").runCommand({collMod: "events", validator: {$nor: [{SourceTopic: "order-cancelled"}]}})')
			status, scarce = request(f"http://127.0.0.1:{ports['PRODUCT']}/api/products/", "POST", {"sku": "scarce-" + uuid.uuid4().hex, "name": "Scarce", "description": "test", "category": "test", "price": 10, "currency": "INR", "initialStock": 1}, auth)
			assert status == 201, f"Scarce product creation failed: {status} {scarce}"
			scarce_id = scarce["productId"]
			status, cart = request(f"http://127.0.0.1:{ports['CART']}/api/cart/items", "POST", {"productId": scarce_id, "name": "Scarce", "unitPrice": 10, "quantity": 2}, auth)
			assert status == 200, f"Cart update failed: {status} {cart}"
			status, checkout = request(f"http://127.0.0.1:{ports['CART']}/api/cart/checkout", "POST", {}, {**auth, "Idempotency-Key": "saga-" + uuid.uuid4().hex})
			assert status == 200, f"Scarce checkout failed: {status} {checkout}"
			order_id = checkout["orderId"]

			def cancelled():
				status, orders = request(f"http://127.0.0.1:{ports['ORDER']}/api/orders/user/{user_id}", headers=auth)
				if status == 200:
					return next((o for o in orders["data"] if o["orderId"] == order_id and o["status"] == "Cancelled"), None)
				return None
			eventually("insufficient-stock cancellation", cancelled)
			status, scarce = request(f"http://127.0.0.1:{ports['PRODUCT']}/api/products/{scarce_id}")
			assert status == 200 and scarce["stock"] == 1 and scarce["reservedStock"] == 0, scarce
			status, missing_payment = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/order/{order_id}", headers=auth)
			assert status == 404, f"Payment created for unavailable stock: {status} {missing_payment}"
			assert len(Provider.calls) == 1, Provider.calls
			print("PASS: insufficient stock cancels order without charging")
			eventually("Analytics rejected write", lambda: "Ingestion failed at" in (Path(temp) / "Analytics.log").read_text(encoding="utf-8"))
			assert mongo_eval('print(db.getSiblingDB("analyticsservice_db").events.countDocuments({SourceTopic: "order-cancelled"}))') == "0"
			mongo_eval('db.getSiblingDB("analyticsservice_db").runCommand({collMod: "events", validator: {}})')
			eventually("Analytics ingestion after database failure", lambda: mongo_eval('print(db.getSiblingDB("analyticsservice_db").events.countDocuments({SourceTopic: "order-cancelled"}))') == "1")
			print("PASS: Analytics retries failed MongoDB write without losing the Kafka event")

			status, product = request(f"http://127.0.0.1:{ports['PRODUCT']}/api/products/", "POST", {"sku": "failed-" + uuid.uuid4().hex, "name": "Failure test", "description": "test", "category": "test", "price": 10, "currency": "INR", "initialStock": 3}, auth)
			assert status == 201, f"Product creation failed: {status} {product}"
			failure_product_id = product["productId"]
			status, cart = request(f"http://127.0.0.1:{ports['CART']}/api/cart/items", "POST", {"productId": failure_product_id, "name": "Failure test", "unitPrice": 10, "quantity": 1}, auth)
			assert status == 200, f"Cart update failed: {status} {cart}"
			status, checkout = request(f"http://127.0.0.1:{ports['CART']}/api/cart/checkout", "POST", {}, {**auth, "Idempotency-Key": "saga-" + uuid.uuid4().hex})
			assert status == 200, f"Failure checkout failed: {status} {checkout}"
			order_id = checkout["orderId"]
			payment = eventually("pending payment for failure scenario", pending_payment)
			failure_webhook = {"event": "payment.failed", "payload": {"payment": {"entity": {"id": "pay_failed", "order_id": payment["providerOrderId"], "error_description": "declined"}}}}
			raw = json.dumps(failure_webhook, separators=(",", ":")).encode()
			signature = hmac.new(WEBHOOK_SECRET.encode(), raw, hashlib.sha256).hexdigest()
			status, result = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/webhooks/razorpay", "POST", failure_webhook, {"X-Razorpay-Signature": signature, "X-Razorpay-Event-Id": "evt_failed"})
			assert status == 200, f"Failure webhook failed: {status} {result}"
			eventually("payment-failure cancellation", cancelled)

			def released():
				status, product = request(f"http://127.0.0.1:{ports['PRODUCT']}/api/products/{failure_product_id}")
				return status == 200 and product["stock"] == 3 and product["reservedStock"] == 0
			eventually("stock compensation after payment failure", released)
			status, payment = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/order/{order_id}", headers=auth)
			assert status == 200 and payment["status"] == 2, f"Payment not failed: {payment}"
			assert len(Provider.calls) == 2, Provider.calls
			print("PASS: failed payment cancels order and releases reserved stock")
	except Exception:
		if started:
			subprocess.run(compose + ["logs", "--tail", "30", "kafka"], env=env, check=False)
		for service, log in logs:
			log.flush()
			log.seek(0)
			print(f"--- {service} log tail ---")
			print("".join(log.readlines()[-35:]))
		raise
	finally:
		for process in processes:
			if process.poll() is None:
				process.terminate()
		for process in processes:
			try:
				process.wait(timeout=10)
			except subprocess.TimeoutExpired:
				process.kill()
				process.wait()
		for _, log in logs:
			log.close()
		test_directory.cleanup()
		if provider is not None:
			provider.shutdown()
			provider.server_close()
		if started:
			if broker_paused:
				subprocess.run(compose + ["unpause", "kafka"], env=env, check=True)
			subprocess.run(compose + ["down", "--remove-orphans"], env=env, check=True)


if __name__ == "__main__":
	main()
