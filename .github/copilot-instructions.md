# Copilot Instructions

## Project Guidelines
- Do not modify UserService and NotificationService; focus refactor on ProductService, CartService, OrderService, PaymentService, AnalyticsService.

## Project Details
- Location: E:\Visual Studio 2022 projects and Apps\ECommerceWebApplication\ECommerceSolution\ECommerceMicroservicesApp\
- Tech Stack: .NET 10.0, PostgreSQL (Order/Payment), MongoDB (Product/Analytics), Redis (cart/idempotency), Kafka (event bus), gRPC (payment authorization), Docker Compose orchestration.
- Architecture: hybrid gRPC+Kafka, outbox pattern, idempotency (Redis), Polly resilience, eventual consistency, correlation-id middleware, OpenTelemetry tracing.

## Restoration Guide
- Provide differential files and a step-by-step implementation guide for restoration.
- Ensure the guide is comprehensive and can be re-read on each VS restart/re-login, as conversation history disappears.

## Microservices Refactor - Shared Project Integration
### Workspace Context
- Root: E:\Visual Studio 2022 projects and Apps\ECommerceWebApplication\ECommerceSolution\ECommerceMicroservicesApp\
- Tech Stack: .NET 10.0, PostgreSQL (Order/Payment), MongoDB (Product/Analytics), Redis (cart/idempotency), Kafka (event bus)
- Architecture: Hybrid gRPC+Kafka, outbox pattern, idempotency (Redis), Polly resilience, eventual consistency, correlation-id middleware, OpenTelemetry tracing

### Refactor Completed
**Phase 1: Shared Project Foundation** (10 files added to shared/ECommerce.Shared/)
- Domain: OutboxMessage.cs, InboxMessage.cs (enhanced version with Status, Error, RowVersion fields), IdempotencyRecord.cs
- Infrastructure: OutboxPublisher.cs, RedisIdempotencyStore.cs, KafkaProducerWrapper.cs, KafkaConsumerWrapper.cs
- Middleware: CorrelationIdMiddleware.cs
- Resilience: ResiliencyPolicies.cs
- Constants: KafkaTopics.cs
- Extensions: SharedInfrastructureExtensions.cs (with AddKafkaProducer, AddKafkaConsumer, AddRedisCache, AddOutboxPublisher, AddOpenTelemetryTracing, AddStructuredLogging, UseCorrelationId)

**Phase 2-6: Service Integration** (26 total files across all services)
- OrderService: ProjectRef + OrderDbContext (OutboxMessage/InboxMessage/IdempotencyRecord entities) + OrderInfrastructureExtensions + OutboxPublisherBackgroundService + Program.cs update
- PaymentService: ProjectRef + PaymentDbContext (same entities) + PaymentInfrastructureExtensions + RazorpayPaymentProcessor (IPaymentProcessor) + OutboxPublisherBackgroundService + Program.cs update
- ProductService: ProjectRef + ProductInfrastructureExtensions + Program.cs update
- CartService: ProjectRef + CartInfrastructureExtensions + Program.cs update
- AnalyticsService: ProjectRef + AnalyticsInfrastructureExtensions + KafkaConsumerHostedService + Program.cs update

### Critical Patterns Implemented
1. **Outbox Pattern**: OrderService & PaymentService write domain events + OutboxMessage in single transaction; OutboxPublisherBackgroundService polls every 10s, publishes to Kafka, marks PublishedAt
2. **Inbox Pattern**: AnalyticsService consumes from Kafka using KafkaConsumerHostedService, stores InboxMessage to prevent duplicates. **Use the enhanced InboxMessage entity with the following fields**: ExternalEventId, TopicName, Payload, EventType, CorrelationId, ReceivedAt, ProcessedAt, Status (Pending/Processed/Failed), Error, RowVersion (optimistic locking). Track processing state using the Status field, not just ProcessedAt.
3. **Idempotency**: Redis-backed IIdempotencyStore for request deduplication (key=idempotencyKey+userId, TTL=24h)
4. **Correlation ID**: Middleware extracts X-Correlation-ID header or generates UUID, logs all requests with it for distributed tracing
5. **Resilience**: Polly policies (retry exponential backoff 3x, circuit breaker 3 failures/30s, timeout 5s) available via ResiliencyPolicies static factory
6. **Kafka Topics**: Centralized constants (orders.events, payments.events, products.events, etc.) + DLQ variants

### Security & Infrastructure in Shared
- Security: CorrelationIdMiddleware for tracing, JWT ready in Program.cs, Serilog correlation enrichment
- Infrastructure: Kafka producer/consumer wrappers, Redis connection management, EF Core retry policies with exponential backoff, health checks, OpenTelemetry tracing to Jaeger
- All centralized in shared/ECommerce.Shared, referenced by each service's .Infrastructure.csproj via ProjectReference

### EF Migrations Applied
- OrderService: AddOutboxInboxIdempotency migration (OutboxMessages, InboxMessages, IdempotencyRecords tables with indexes)
- PaymentService: AddOutboxInboxIdempotency migration (same)
- AnalyticsService: InitialCreate for AnalyticsReportingDbContext (reporting layer for Power BI)

### Docker & Kafka
- Services: OrderService (:5000), PaymentService (:5001), ProductService (:5002), CartService (:5003), AnalyticsService (:5004)
- Kafka bootstrap: kafka:29092 (from docker/.env)
- Redis: redis:6379 with password (from docker/.env)
- All health checks verified via curl /health

### Next Steps After Restoration
1. If dotnet build fails: check ProjectReference paths in .csproj files (relative paths: ..\..\..\..\shared\ECommerce.Shared\ECommerce.Shared.csproj)
2. If EF migrations fail: verify ConnectionStrings in appsettings.json match docker/.env (e.g., Host=postgres-order;Port=5432;Database=orderdb)
3. If Kafka topics not found: ensure docker-compose.yml creates topics OR manually create via: docker-compose exec kafka kafka-topics --create --topic orders.events --partitions 1 --replication-factor 1 --bootstrap-server localhost:9092
4. To run services: dotnet run from each .API project OR docker-compose up -d
5. To test outbox: POST /orders (creates Order + OutboxMessage); wait 10s; OutboxPublisherBackgroundService polls and publishes to Kafka
6. To test analytics: consumed events appear in MongoDB analytics_bronze database (dbs.events collections)

### Files Not Modified (As Per Constraint)
- UserService: Untouched (user requirement: no changes)
- NotificationService: Untouched (user requirement: no changes)

### Audit Remaining Files
- Review remaining files for differential changes and categorize as MODIFY vs NEW status. 
- Ensure all core packages are included in ECommerce.Shared.csproj.
- Validate docker-compose.yml (761 lines) and .env (147 lines) for accuracy and completeness.