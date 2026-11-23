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
	ports = {name: unused_port() for name in ("KAFKA", "POSTGRES", "MONGO", "REDIS", "PRODUCT", "CART", "ORDER", "PAYMENT")}
	env = os.environ.copy()
	for key in list(env):
		if key.startswith(("ConnectionStrings__", "Kafka__", "Redis__", "Jwt__", "Razorpay__", "Services__", "OpenTelemetry__")):
			del env[key]
	for name in ("KAFKA", "POSTGRES", "MONGO", "REDIS"):
		env[f"FLOW_{name}_PORT"] = str(ports[name])
	project = "ecommerce-saga-test-" + uuid.uuid4().hex[:12]
	compose = ["docker", "compose", "-p", project, "-f", str(COMPOSE)]
	processes = []
	logs = []
	provider = None
	started = False
	test_directory = tempfile.TemporaryDirectory(prefix="ecommerce-saga-test-")
	try:
		for service in ("Product", "Cart", "Order", "Payment"):
			project_file = ROOT / "services" / f"{service}Service" / f"{service}Service.API" / f"{service}Service.API.csproj"
			subprocess.run(["dotnet", "build", str(project_file), "--nologo", "-v", "quiet"], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
		started = True
		subprocess.run(compose + ["up", "-d", "--wait", "--wait-timeout", "180"], env=env, check=True)
		for database in ("orderdb", "paymentdb"):
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
		}
		with contextlib.nullcontext(test_directory.name) as temp:
			for service in ("Product", "Order", "Payment", "Cart"):
				service_env = env.copy()
				service_env.update(common)
				service_env.update(settings[service])
				service_env["ASPNETCORE_URLS"] = f"http://127.0.0.1:{ports[service.upper()]}"
				dll = ROOT / "services" / f"{service}Service" / f"{service}Service.API" / "bin" / "Debug" / "net10.0" / f"{service}Service.API.dll"
				log = open(Path(temp) / f"{service}.log", "w+", encoding="utf-8")
				logs.append((service, log))
				processes.append(subprocess.Popen(["dotnet", str(dll)], cwd=temp, env=service_env, stdout=log, stderr=subprocess.STDOUT))
				eventually(f"{service} health", lambda: request(f"http://127.0.0.1:{ports[service.upper()]}/health/live")[0] == 200)

			user_id = str(uuid.uuid4())
			auth = {"Authorization": "Bearer " + token(user_id)}
			status, product = request(f"http://127.0.0.1:{ports['PRODUCT']}/api/products/", "POST", {"sku": "integration-" + uuid.uuid4().hex, "name": "Test product", "description": "test", "category": "test", "price": 25, "currency": "INR", "initialStock": 5}, auth)
			assert status == 201, f"Product creation failed: {status} {product}"
			product_id = product["productId"]
			status, cart = request(f"http://127.0.0.1:{ports['CART']}/api/cart/items", "POST", {"productId": product_id, "name": "Test product", "unitPrice": 25, "quantity": 2}, auth)
			assert status == 200 and cart["total"] == 50, f"Cart update failed: {status} {cart}"
			status, checkout = request(f"http://127.0.0.1:{ports['CART']}/api/cart/checkout", "POST", {}, {**auth, "Idempotency-Key": "saga-" + uuid.uuid4().hex})
			assert status == 200, f"Checkout failed: {status} {checkout}"
			order_id = checkout["orderId"]
			status, cart = request(f"http://127.0.0.1:{ports['CART']}/api/cart/", headers=auth)
			assert status == 200 and not cart["items"], f"Cart not cleared: {status} {cart}"

			def pending_payment():
				status, payment = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/order/{order_id}", headers=auth)
				return payment if status == 200 and payment.get("providerOrderId") else None
			payment = eventually("pending payment after Kafka stock reservation", pending_payment)
			assert payment["amount"] == 50, payment
			assert Provider.calls and Provider.calls[0][0] == "/v1/orders" and Provider.calls[0][1]["amount"] == 5000, Provider.calls
			status, product = request(f"http://127.0.0.1:{ports['PRODUCT']}/api/products/{product_id}")
			assert status == 200 and product["stock"] == 3 and product["reservedStock"] == 2, product

			webhook = {"event": "payment.captured", "payload": {"payment": {"entity": {"id": "pay_test", "order_id": payment["providerOrderId"]}}}}
			raw = json.dumps(webhook, separators=(",", ":")).encode()
			signature = hmac.new(WEBHOOK_SECRET.encode(), raw, hashlib.sha256).hexdigest()
			status, result = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/webhooks/razorpay", "POST", webhook, {"X-Razorpay-Signature": signature, "X-Razorpay-Event-Id": "evt_test"})
			assert status == 200, f"Webhook failed: {status} {result}"

			def confirmed():
				status, orders = request(f"http://127.0.0.1:{ports['ORDER']}/api/orders/user/{user_id}", headers=auth)
				if status == 200:
					return next((o for o in orders["data"] if o["orderId"] == order_id and o["status"] == "Confirmed"), None)
				return None
			eventually("order confirmation after payment-status Kafka event", confirmed)
			status, payment = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/order/{order_id}", headers=auth)
			assert status == 200 and payment["status"] == 1, f"Payment not succeeded: {payment}"
			status, result = request(f"http://127.0.0.1:{ports['PAYMENT']}/api/payments/webhooks/razorpay", "POST", webhook, {"X-Razorpay-Signature": signature, "X-Razorpay-Event-Id": "evt_test"})
			assert status == 200, f"Webhook replay failed: {status} {result}"
			print("PASS: checkout -> Kafka stock reservation -> payment -> signed webhook -> confirmed order")

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
			subprocess.run(compose + ["down", "--remove-orphans"], env=env, check=True)


if __name__ == "__main__":
	main()
