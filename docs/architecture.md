# Architecture, service review and restoration

This document records the local source and validation. Intended architecture and code paths are not proof of working integration or production readiness.

## System context

```mermaid
flowchart TB
	Client[Client] --> Gateway[YARP Gateway]
	Gateway --> User[User API]
	Gateway --> Notification[Notification API]
	Client --> Cart[Cart API]
	Client --> Product[Product API]
	Cart -->|HTTP| Order[Order API]
	Order -->|outbox publisher| Bus[(Kafka)]
	Bus --> Product
	Bus --> Payment[Payment]
	Bus --> Notification
	Bus --> Analytics[Analytics]
	Payment -->|outbox publisher| Bus
	Order --> OrderDB[(PostgreSQL)]
	Payment --> PaymentDB[(PostgreSQL)]
	Product --> ProductDB[(MongoDB)]
	Cart --> Redis[(Redis)]
	Analytics --> AnalyticsDB[(MongoDB)]
	Analytics --> Reporting[(PostgreSQL reporting)]
```

The gateway reads YARP routes from configuration. Its native aggregator calls downstream services; not every API should be assumed to be reachable through the gateway. The source also contains gRPC and shared contracts, but the reviewed cart-to-order path uses HTTP, not gRPC. Verify any claimed gRPC payment flow before relying on it.

## Service-by-service review

| Component | Source evidence | Build check |
| --- | --- | --- |
| User | `UserEndpoints.cs`: registration, login and user management; API includes GraphQL and gRPC registration. | Passed, warnings. |
| Notification | Notification endpoints and user/order/payment Kafka consumers; direct send endpoint is commented out. | Passed, warnings. |
| Product | Catalog read/create endpoints, MongoDB repository, order-event consumer and product publisher. | Passed, warnings. |
| Cart | Redis cart repository, HTTP order client and checkout endpoint. Checkout generates an idempotency key when absent, so a client must send a stable key for retries. | Passed, warnings. |
| Order | PostgreSQL repository, idempotency record, outbox and saga consumer. `CreateOrderCommand` saves order, idempotency and outbox together. | Passed, warnings. |
| Payment | PostgreSQL, Kafka request consumer/outbox and Razorpay gateway/webhook. | Passed, warnings. |
| Analytics | Kafka consumer, MongoDB event storage and timed PostgreSQL reporting projection. | Passed, warnings. |
| Gateway | YARP configuration, auth, health, Scalar and user/notification aggregation. | Passed, warnings. |
| Shared | Shared messaging, models, correlation, resilience, OpenTelemetry and Redis-related code. Some services also retain service-specific implementations; do not assume all use the shared infrastructure. | Passed, warnings. |

These are build checks, not a comprehensive functional, security or production audit. Restore reported dependency advisory warnings including critical and high severity packages; review the resolved dependency graph and update/test packages before deployment.

## Example order flow in source

1. Cart checkout reads the authenticated user ID and calls its configured Order HTTP client.
2. Order creation requires `Idempotency-Key`; its handler checks for a prior key and writes the order, key and `order-created` outbox row with one save.
3. The Order outbox publisher and saga consumer are registered as hosted services. Product, Payment and other services register Kafka consumers/publishers for their respective event paths.
4. Payment has an endpoint for signature-verified Razorpay webhooks and a payment outbox publisher. Analytics registers a Kafka consumer and a periodic reporting projection.

Event schemas, routing, retries, duplicate handling, authorization and behavior under failures must be tested end to end before describing the flow as reliable. The Order idempotency implementation shown in source is database-backed; do not assume it uses the shared Redis idempotency store.

## Data and deployment

Order and Payment configure Npgsql contexts; Product and Analytics configure MongoDB; Cart configures Redis. Analytics additionally expects an `AnalyticsReportingDb` connection for its reporting projection. The Compose file defines PostgreSQL, MongoDB, Redis, Kafka, service containers, a gateway and observability tools. Order and Payment apply their EF migrations on startup in Development; other environments require a reviewed explicit migration step. Do not print resolved Compose configuration in shared logs because it can contain secrets.

## Validation and restoration

### Local validation

- Installed SDK: `11.0.100`; projects target .NET 10.
- A fresh `dotnet build` passed all seven services, Shared and Gateway, with warnings.
- Shared no longer references unused EF Core and EF instrumentation packages or redundant Microsoft.Extensions packages supplied by the ASP.NET Core framework. Its ASP.NET Core OpenTelemetry instrumentation was aligned with User and Notification, resolving their `NU1605` conflicts without suppressing errors or modifying those services. Cart endpoint injection now uses the registered repository interface. Analytics project references and its configuration-binding dependency were corrected.
- Added Order and Payment EF initial migrations and verified them against the previously empty local databases. Analytics reporting creates its reporting schema in a hosted service; no Analytics EF migration was added.
- Fresh User and Notification images were built, checked in temporary containers, and deployed. Their health checks passed, as did the gateway's `/health`; all seven service containers and the gateway reported healthy. Product catalog and authenticated cart, order, payment, analytics and notification reads returned HTTP 200 through the gateway during local validation. The authenticated reads preceded the final User and Notification replacement.
- The Kafka initialization job was run to create missing saga topics. Missing Product and Analytics MongoDB application users were created in existing local volumes using their configured credentials; no existing users were modified. Product initialization now receives the correct database setting. Mongo initialization scripts run only on a fresh volume; preexisting volumes require a manual, non-destructive user check.
- Analytics driver/BSON versions were aligned after a runtime assembly error. Product now relies on MongoDB's automatically unique `_id` inbox index. Shared Redis registration accepts the existing `Redis:ConnectionString` key and registers the expected interface.
- No tracked test project files were found in `tests`. Health and read checks do not prove checkout, Kafka saga, payment provider or recovery behavior; those flows were not exercised end to end.

### Resume after a Visual Studio restart

1. Open the repository folder and inspect `git status --short --branch`; preserve unrelated local modifications. Each service has its own `.slnx`. The historical `STEP_BY_STEP_GUIDE.md` references older structure and should not be treated as authoritative.
2. **Before public push**, inspect current files *and all reachable Git history* for credentials, archives, backups and sensitive data. The local history includes tracked `.env` files, backup env files, settings files and an archive containing an `.env`. Ignoring a file does not untrack it or remove it from earlier commits. Assume any real credentials in such files require rotation; follow the owner's approved history-sanitization plan. Do not publish this history as-is.
3. Replace committed secrets with protected local configuration and provide a non-secret example configuration only after reviewing all required settings. Ensure every service and Compose receives the correct settings without printing them in logs. The gateway explicitly requires `appsettings.json`, so removing settings requires a deliberate safe replacement.
4. Review NU1603 and security advisory warnings, restore all solutions and rebuild. Current project instructions prohibit modifying User and Notification. The package downgrade fix is in `shared\ECommerce.Shared\ECommerce.Shared.csproj`.
5. Start Docker Desktop; validate Compose with `docker compose -f .\docker\docker-compose.yml config --quiet`. For an existing installation, run `docker compose -f .\docker\docker-compose.yml run --rm --no-deps kafka-init` to create missing topics without deleting existing ones. Inspect existing databases and back up data before applying migrations; never recreate or remove volumes merely to rerun Mongo initialization.
6. Build each individual `.slnx`, then add and run meaningful unit, integration and failure/retry tests. Verify each service's health endpoint, authorization, checkout/idempotency behavior, Kafka event flows, payment webhook and analytics projection against isolated infrastructure. Liveness does not check all external dependencies.
7. Generate the portfolio assets from the repository root with `python .\docs\generate_portfolio.py` (requires Pillow and Windows Segoe UI fonts). Open the PNG, GIF, PDF and HTML and verify their content after changing project status.
8. Public `main` has a separate sanitized history; local `master` retains sensitive history and must not be pushed. Publish future fixes as commits on the sanitized history, after scanning the content and reachable history.

## Portfolio materials

The assets deliberately present architecture and implementation areas rather than claiming verified production operation or business outcomes. Update their status text whenever build and end-to-end verification results change. The reference repository provides an architecture document, HTML and generated images; this project additionally includes a PDF case study.
