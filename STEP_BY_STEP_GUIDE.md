# ====================================================================
#  E-Commerce Microservices — Complete PowerShell Step-by-Step Guide
#  .NET 10 | Clean Architecture | Vertical Slice | gRPC | Kafka
#  GraphQL (HotChocolate) | PostgreSQL | MongoDB | Redis | YARP
# ====================================================================
#  ALL commands are PowerShell only.
#  Use the backtick ` for line continuation in PowerShell.
#  Run PowerShell 7+ (pwsh) or Windows PowerShell 5.1 as Administrator.
# ====================================================================

# ────────────────────────────────────────────────────────────────────
#  DIRECTORY LAYOUT  (every service is an independent solution)
# ────────────────────────────────────────────────────────────────────
#
# ECommerceMicroservicesApp\
# ├── shared\
# │   └── ECommerce.Shared\
# │       ├── ECommerce.Shared.csproj
# │       ├── Contracts\Proto\          ← *.proto for all 6 services
# │       ├── Events\                   ← Integration event classes
# │       ├── Kafka\Abstractions\       ← IEventProducer, IEventConsumer
# │       ├── Kafka\Extensions\         ← KafkaEventProducer, BaseKafkaConsumer
# │       ├── Redis\Abstractions\       ← ICacheService, CacheKeys
# │       ├── Redis\Extensions\         ← RedisCacheService
# │       └── Middleware\               ← CorrelationId, GlobalException
# │
# ├── services\
# │   ├── UserService\                  ← owns postgres-user  (port 5433)
# │   │   ├── UserService.sln
# │   │   ├── UserService.Domain\
# │   │   │   ├── Entities\
# │   │   │   ├── ValueObjects\
# │   │   │   ├── Repositories\
# │   │   │   └── Events\
# │   │   ├── UserService.Application\
# │   │   │   ├── Features\Users\Commands\RegisterUser\
# │   │   │   ├── Features\Users\Commands\UpdateUser\
# │   │   │   ├── Features\Users\Commands\DeactivateUser\
# │   │   │   ├── Features\Users\Queries\GetUser\
# │   │   │   ├── Features\Users\Queries\GetUsers\
# │   │   │   ├── Features\Auth\Commands\Login\
# │   │   │   ├── Behaviors\
# │   │   │   └── Interfaces\
# │   │   ├── UserService.Infrastructure\
# │   │   │   ├── Persistence\Configurations\
# │   │   │   ├── Persistence\Migrations\
# │   │   │   ├── Repositories\
# │   │   │   ├── Kafka\
# │   │   │   └── Cache\
# │   │   ├── UserService.Grpc\
# │   │   └── UserService.API\
# │   │       ├── Endpoints\
# │   │       ├── GraphQL\
# │   │       └── Extensions\
# │   │
# │   ├── ProductService\               ← owns mongodb-product (port 27018)
# │   │   ├── ProductService.sln
# │   │   ├── ProductService.Domain\
# │   │   │   ├── Entities\
# │   │   │   ├── Repositories\
# │   │   │   └── Events\
# │   │   ├── ProductService.Application\
# │   │   │   ├── Features\Products\Commands\CreateProduct\
# │   │   │   ├── Features\Products\Commands\UpdateStock\
# │   │   │   ├── Features\Products\Queries\GetProduct\
# │   │   │   ├── Features\Products\Queries\ListProducts\
# │   │   │   └── Behaviors\
# │   │   ├── ProductService.Infrastructure\
# │   │   │   ├── Persistence\
# │   │   │   ├── Repositories\
# │   │   │   ├── Kafka\
# │   │   │   └── Cache\
# │   │   ├── ProductService.Grpc\
# │   │   └── ProductService.API\
# │   │       ├── Endpoints\
# │   │       ├── GraphQL\
# │   │       └── Extensions\
# │   │
# │   ├── OrderService\                 ← owns postgres-order  (port 5434)
# │   │   ├── OrderService.sln
# │   │   ├── OrderService.Domain\
# │   │   │   ├── Entities\
# │   │   │   ├── ValueObjects\
# │   │   │   ├── Repositories\
# │   │   │   └── Events\
# │   │   ├── OrderService.Application\
# │   │   │   ├── Features\Orders\Commands\PlaceOrder\
# │   │   │   ├── Features\Orders\Commands\CancelOrder\
# │   │   │   ├── Features\Orders\Queries\GetOrder\
# │   │   │   ├── Features\Orders\Queries\GetUserOrders\
# │   │   │   ├── Behaviors\
# │   │   │   └── Interfaces\
# │   │   ├── OrderService.Infrastructure\
# │   │   │   ├── Persistence\Configurations\
# │   │   │   ├── Persistence\Migrations\
# │   │   │   ├── Repositories\
# │   │   │   ├── Kafka\
# │   │   │   └── Cache\
# │   │   ├── OrderService.Grpc\
# │   │   └── OrderService.API\
# │   │       ├── Endpoints\
# │   │       ├── GraphQL\
# │   │       └── Extensions\
# │   │
# │   ├── PaymentService\               ← owns postgres-payment (port 5435)
# │   │   ├── PaymentService.sln
# │   │   ├── PaymentService.Domain\
# │   │   │   ├── Entities\
# │   │   │   ├── Repositories\
# │   │   │   └── Events\
# │   │   ├── PaymentService.Application\
# │   │   │   ├── Features\Payments\Commands\AuthorizePayment\
# │   │   │   ├── Features\Payments\Commands\ProcessPayment\
# │   │   │   ├── Features\Payments\Commands\RefundPayment\
# │   │   │   ├── Features\Payments\Queries\GetPayment\
# │   │   │   └── Behaviors\
# │   │   ├── PaymentService.Infrastructure\
# │   │   │   ├── Persistence\Configurations\
# │   │   │   ├── Persistence\Migrations\
# │   │   │   ├── Repositories\
# │   │   │   └── Kafka\
# │   │   ├── PaymentService.Grpc\
# │   │   └── PaymentService.API\
# │   │       ├── Endpoints\
# │   │       └── Extensions\
# │   │
# │   ├── NotificationService\          ← owns mongodb-notification (port 27019)
# │   │   ├── NotificationService.sln
# │   │   ├── NotificationService.Domain\
# │   │   │   ├── Entities\
# │   │   │   ├── Repositories\
# │   │   │   └── Events\
# │   │   ├── NotificationService.Application\
# │   │   │   ├── Features\Notifications\Commands\SendNotification\
# │   │   │   ├── Features\Notifications\Commands\MarkAsRead\
# │   │   │   ├── Features\Notifications\Queries\GetNotifications\
# │   │   │   ├── Behaviors\
# │   │   │   └── Interfaces\
# │   │   ├── NotificationService.Infrastructure\
# │   │   │   ├── Persistence\
# │   │   │   ├── Repositories\
# │   │   │   ├── Kafka\
# │   │   │   └── Channels\
# │   │   ├── NotificationService.Grpc\
# │   │   └── NotificationService.API\
# │   │       ├── Endpoints\
# │   │       └── Extensions\
# │   │
# │   └── AnalyticsService\             ← owns mongodb-analytics (port 27020)
# │       ├── AnalyticsService.sln
# │       ├── AnalyticsService.Domain\
# │       │   ├── Entities\
# │       │   └── Repositories\
# │       ├── AnalyticsService.Application\
# │       │   ├── Features\Analytics\Commands\TrackEvent\
# │       │   ├── Features\Analytics\Queries\GetMetrics\
# │       │   ├── Features\Analytics\Queries\GetUserBehavior\
# │       │   └── Behaviors\
# │       ├── AnalyticsService.Infrastructure\
# │       │   ├── Persistence\
# │       │   ├── Repositories\
# │       │   └── Kafka\
# │       ├── AnalyticsService.Grpc\
# │       └── AnalyticsService.API\
# │           ├── Endpoints\
# │           └── Extensions\
# │
# ├── gateway\
# │   └── ApiGateway\
# │       ├── ApiGateway.sln
# │       ├── ApiGateway.csproj
# │       ├── Configuration\
# │       ├── Middleware\
# │       ├── Extensions\
# │       └── Aggregators\
# │
# ├── tests\
# │   ├── ECommerce.Tests.sln
# │   ├── ECommerce.Tests.Unit\
# │   ├── ECommerce.Tests.Integration\
# │   └── ECommerce.Tests.Architecture\
# │
# └── docker\
#     ├── docker-compose.yml
#     ├── .env
#     ├── prometheus\prometheus.yml
#     ├── grafana\provisioning\datasources\prometheus.yml
#     └── init\
#         ├── mongo\product\init.js
#         ├── mongo\notification\init.js
#         └── mongo\analytics\init.js

# ────────────────────────────────────────────────────────────────────
#  DATABASE & PORT REFERENCE
# ────────────────────────────────────────────────────────────────────
#
#  Container               Host Port   DB Name              Credentials
#  ─────────────────────   ─────────   ──────────────────   ──────────────────────
#  postgres-user           5433        userservice_db        user_svc / user_pass
#  postgres-order          5434        orderservice_db       order_svc / order_pass
#  postgres-payment        5435        paymentservice_db     payment_svc / payment_pass
#  mongodb-product         27018       productservice_db     product_svc / product_pass
#  mongodb-notification    27019       notificationservice_db notif_svc / notif_pass
#  mongodb-analytics       27020       analyticsservice_db   analytics_svc / analytics_pass
#  redis                   6379        (shared cache)        redis_pass
#  kafka                   9092        (event bus)
#  zookeeper               2181
#  kafdrop                 9000        Kafka UI
#  jaeger                  16686       Trace UI
#  prometheus              9090        Metrics
#  grafana                 3000        Dashboards  admin/admin
#
#  Service              HTTP Port   gRPC Port   GraphQL
#  ────────────────     ─────────   ─────────   ──────────────────
#  ApiGateway           5000        —           /graphql (stitched)
#  UserService          5001        5001        /graphql
#  ProductService       5002        5002        /graphql
#  OrderService         5003        5003        /graphql
#  PaymentService       5004        5004        —
#  NotificationService  5005        5005        —
#  AnalyticsService     5006        5006        —

# ====================================================================
#  SECTION 0 — PREREQUISITES
# ====================================================================

# 1. Verify .NET 10 SDK
dotnet --version
# Expected: 10.x.x

# 2. Install global tools
dotnet tool install --global dotnet-ef
dotnet tool install --global dotnet-grpc

# 3. Verify tools
dotnet ef --version
dotnet grpc --version

# 4. Set your root — adjust path to match your machine
$ROOT = "E:\Visual Studio 2022 projects and Apps\ECommerceWebApplication\ECommerceSolution\ECommerceMicroservicesApp"

# ====================================================================
#  SECTION 1 — CREATE FULL DIRECTORY STRUCTURE (PowerShell arrays)
# ====================================================================

New-Item -ItemType Directory -Force -Path $ROOT | Out-Null

# ── Shared kernel ────────────────────────────────────────────────────
@(
  "shared\ECommerce.Shared\Contracts\Proto",
  "shared\ECommerce.Shared\Events",
  "shared\ECommerce.Shared\Kafka\Abstractions",
  "shared\ECommerce.Shared\Kafka\Extensions",
  "shared\ECommerce.Shared\Redis\Abstractions",
  "shared\ECommerce.Shared\Redis\Extensions",
  "shared\ECommerce.Shared\Middleware",
  "shared\ECommerce.Shared\Extensions",
  "shared\ECommerce.Shared\Models"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── UserService ──────────────────────────────────────────────────────
@(
  "services\UserService\UserService.Domain\Entities",
  "services\UserService\UserService.Domain\ValueObjects",
  "services\UserService\UserService.Domain\Repositories",
  "services\UserService\UserService.Domain\Events",
  "services\UserService\UserService.Application\Features\Users\Commands\RegisterUser",
  "services\UserService\UserService.Application\Features\Users\Commands\UpdateUser",
  "services\UserService\UserService.Application\Features\Users\Commands\DeactivateUser",
  "services\UserService\UserService.Application\Features\Users\Queries\GetUser",
  "services\UserService\UserService.Application\Features\Users\Queries\GetUsers",
  "services\UserService\UserService.Application\Features\Auth\Commands\Login",
  "services\UserService\UserService.Application\Behaviors",
  "services\UserService\UserService.Application\Interfaces",
  "services\UserService\UserService.Infrastructure\Persistence\Configurations",
  "services\UserService\UserService.Infrastructure\Persistence\Migrations",
  "services\UserService\UserService.Infrastructure\Repositories",
  "services\UserService\UserService.Infrastructure\Kafka",
  "services\UserService\UserService.Infrastructure\Cache",
  "services\UserService\UserService.Grpc",
  "services\UserService\UserService.API\Endpoints",
  "services\UserService\UserService.API\GraphQL",
  "services\UserService\UserService.API\Extensions"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── ProductService ───────────────────────────────────────────────────
@(
  "services\ProductService\ProductService.Domain\Entities",
  "services\ProductService\ProductService.Domain\Repositories",
  "services\ProductService\ProductService.Domain\Events",
  "services\ProductService\ProductService.Application\Features\Products\Commands\CreateProduct",
  "services\ProductService\ProductService.Application\Features\Products\Commands\UpdateStock",
  "services\ProductService\ProductService.Application\Features\Products\Queries\GetProduct",
  "services\ProductService\ProductService.Application\Features\Products\Queries\ListProducts",
  "services\ProductService\ProductService.Application\Behaviors",
  "services\ProductService\ProductService.Infrastructure\Persistence",
  "services\ProductService\ProductService.Infrastructure\Repositories",
  "services\ProductService\ProductService.Infrastructure\Kafka",
  "services\ProductService\ProductService.Infrastructure\Cache",
  "services\ProductService\ProductService.Grpc",
  "services\ProductService\ProductService.API\Endpoints",
  "services\ProductService\ProductService.API\GraphQL",
  "services\ProductService\ProductService.API\Extensions"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── OrderService ─────────────────────────────────────────────────────
@(
  "services\OrderService\OrderService.Domain\Entities",
  "services\OrderService\OrderService.Domain\ValueObjects",
  "services\OrderService\OrderService.Domain\Repositories",
  "services\OrderService\OrderService.Domain\Events",
  "services\OrderService\OrderService.Application\Features\Orders\Commands\PlaceOrder",
  "services\OrderService\OrderService.Application\Features\Orders\Commands\CancelOrder",
  "services\OrderService\OrderService.Application\Features\Orders\Queries\GetOrder",
  "services\OrderService\OrderService.Application\Features\Orders\Queries\GetUserOrders",
  "services\OrderService\OrderService.Application\Behaviors",
  "services\OrderService\OrderService.Application\Interfaces",
  "services\OrderService\OrderService.Infrastructure\Persistence\Configurations",
  "services\OrderService\OrderService.Infrastructure\Persistence\Migrations",
  "services\OrderService\OrderService.Infrastructure\Repositories",
  "services\OrderService\OrderService.Infrastructure\Kafka",
  "services\OrderService\OrderService.Infrastructure\Cache",
  "services\OrderService\OrderService.Grpc",
  "services\OrderService\OrderService.API\Endpoints",
  "services\OrderService\OrderService.API\GraphQL",
  "services\OrderService\OrderService.API\Extensions"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── PaymentService ───────────────────────────────────────────────────
@(
  "services\PaymentService\PaymentService.Domain\Entities",
  "services\PaymentService\PaymentService.Domain\Repositories",
  "services\PaymentService\PaymentService.Domain\Events",
  "services\PaymentService\PaymentService.Application\Features\Payments\Commands\AuthorizePayment",
  "services\PaymentService\PaymentService.Application\Features\Payments\Commands\ProcessPayment",
  "services\PaymentService\PaymentService.Application\Features\Payments\Commands\RefundPayment",
  "services\PaymentService\PaymentService.Application\Features\Payments\Queries\GetPayment",
  "services\PaymentService\PaymentService.Application\Behaviors",
  "services\PaymentService\PaymentService.Infrastructure\Persistence\Configurations",
  "services\PaymentService\PaymentService.Infrastructure\Persistence\Migrations",
  "services\PaymentService\PaymentService.Infrastructure\Repositories",
  "services\PaymentService\PaymentService.Infrastructure\Kafka",
  "services\PaymentService\PaymentService.Grpc",
  "services\PaymentService\PaymentService.API\Endpoints",
  "services\PaymentService\PaymentService.API\Extensions"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── NotificationService ──────────────────────────────────────────────
@(
  "services\NotificationService\NotificationService.Domain\Entities",
  "services\NotificationService\NotificationService.Domain\Repositories",
  "services\NotificationService\NotificationService.Domain\Events",
  "services\NotificationService\NotificationService.Application\Features\Notifications\Commands\SendNotification",
  "services\NotificationService\NotificationService.Application\Features\Notifications\Commands\MarkAsRead",
  "services\NotificationService\NotificationService.Application\Features\Notifications\Queries\GetNotifications",
  "services\NotificationService\NotificationService.Application\Behaviors",
  "services\NotificationService\NotificationService.Application\Interfaces",
  "services\NotificationService\NotificationService.Infrastructure\Persistence",
  "services\NotificationService\NotificationService.Infrastructure\Repositories",
  "services\NotificationService\NotificationService.Infrastructure\Kafka",
  "services\NotificationService\NotificationService.Infrastructure\Channels",
  "services\NotificationService\NotificationService.Grpc",
  "services\NotificationService\NotificationService.API\Endpoints",
  "services\NotificationService\NotificationService.API\Extensions"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── AnalyticsService ─────────────────────────────────────────────────
@(
  "services\AnalyticsService\AnalyticsService.Domain\Entities",
  "services\AnalyticsService\AnalyticsService.Domain\Repositories",
  "services\AnalyticsService\AnalyticsService.Application\Features\Analytics\Commands\TrackEvent",
  "services\AnalyticsService\AnalyticsService.Application\Features\Analytics\Queries\GetMetrics",
  "services\AnalyticsService\AnalyticsService.Application\Features\Analytics\Queries\GetUserBehavior",
  "services\AnalyticsService\AnalyticsService.Application\Behaviors",
  "services\AnalyticsService\AnalyticsService.Infrastructure\Persistence",
  "services\AnalyticsService\AnalyticsService.Infrastructure\Repositories",
  "services\AnalyticsService\AnalyticsService.Infrastructure\Kafka",
  "services\AnalyticsService\AnalyticsService.Grpc",
  "services\AnalyticsService\AnalyticsService.API\Endpoints",
  "services\AnalyticsService\AnalyticsService.API\Extensions"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── API Gateway ──────────────────────────────────────────────────────
@(
  "gateway\ApiGateway\Configuration",
  "gateway\ApiGateway\Middleware",
  "gateway\ApiGateway\Extensions",
  "gateway\ApiGateway\Aggregators"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── Tests ─────────────────────────────────────────────────────────────
@(
  "tests\ECommerce.Tests.Unit\UserService",
  "tests\ECommerce.Tests.Unit\OrderService",
  "tests\ECommerce.Tests.Unit\ProductService",
  "tests\ECommerce.Tests.Integration\UserService",
  "tests\ECommerce.Tests.Integration\OrderService",
  "tests\ECommerce.Tests.Architecture"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── Docker ────────────────────────────────────────────────────────────
@(
  "docker\init\mongo\product",
  "docker\init\mongo\notification",
  "docker\init\mongo\analytics",
  "docker\prometheus",
  "docker\grafana\provisioning\datasources",
  "docker\grafana\provisioning\dashboards"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

# ── K8s ───────────────────────────────────────────────────────────────
@(
  "k8s\user-service","k8s\product-service","k8s\order-service",
  "k8s\payment-service","k8s\notification-service","k8s\analytics-service",
  "k8s\api-gateway","k8s\kafka","k8s\postgres","k8s\mongodb","k8s\redis"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path "$ROOT\$_" | Out-Null }

Write-Host "✅ All directories created" -ForegroundColor Green
(Get-ChildItem -Path $ROOT -Recurse -Directory).Count

# ====================================================================
#  SECTION 2 — SHARED KERNEL  (build this FIRST)
# ====================================================================

Set-Location "$ROOT\shared\ECommerce.Shared"

dotnet new classlib -n ECommerce.Shared -f net10.0 --force
Remove-Item "Class1.cs" -ErrorAction SilentlyContinue

dotnet add package Google.Protobuf                                       --version 3.29.1
dotnet add package Grpc.Net.Client                                       --version 2.67.0
dotnet add package Grpc.Tools                                            --version 2.67.0
dotnet add package Confluent.Kafka                                       --version 2.6.0
dotnet add package StackExchange.Redis                                   --version 2.8.16
dotnet add package Microsoft.Extensions.DependencyInjection.Abstractions --version 9.0.0
dotnet add package Microsoft.Extensions.Logging.Abstractions             --version 9.0.0
dotnet add package Microsoft.Extensions.Configuration.Abstractions       --version 9.0.0
dotnet add package Microsoft.AspNetCore.Http.Abstractions                --version 2.3.0
dotnet add package OpenTelemetry                                         --version 1.10.0
dotnet add package OpenTelemetry.Extensions.Hosting                      --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.Http                    --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.GrpcNetClient           --version 1.10.0-beta.1
dotnet add package OpenTelemetry.Exporter.Jaeger                         --version 1.5.1
dotnet add package Polly                                                 --version 8.4.1
dotnet add package Polly.Extensions                                      --version 8.4.1
dotnet add package Serilog                                               --version 4.1.0
dotnet add package Serilog.Extensions.Hosting                            --version 8.0.0
dotnet add package Serilog.Sinks.Console                                 --version 6.0.0
dotnet add package Serilog.Enrichers.CorrelationId                       --version 3.0.1
dotnet add package Serilog.Settings.Configuration                        --version 8.0.2

dotnet build
Write-Host "✅ Shared kernel ready" -ForegroundColor Green
Set-Location $ROOT

# ====================================================================
#  SECTION 3 — INFRASTRUCTURE  (Docker must be RUNNING first)
# ====================================================================

# ► Open Docker Desktop and wait for the green whale icon.
# ► Verify Docker is ready:
docker version
docker ps

Set-Location "$ROOT\docker"
docker compose up -d

# Check all containers are healthy:
docker compose ps

# ── Expected output ───────────────────────────────────────────────────
# NAME                           STATUS
# ecommerce_postgres_user        running (healthy)   port 5433
# ecommerce_postgres_order       running (healthy)   port 5434
# ecommerce_postgres_payment     running (healthy)   port 5435
# ecommerce_mongodb_product      running (healthy)   port 27018
# ecommerce_mongodb_notification running (healthy)   port 27019
# ecommerce_mongodb_analytics    running (healthy)   port 27020
# ecommerce_redis                running (healthy)   port 6379
# ecommerce_zookeeper            running (healthy)   port 2181
# ecommerce_kafka                running (healthy)   port 9092
# ecommerce_kafka_init           exited (0)          ← normal one-shot
# ecommerce_kafdrop              running             port 9000
# ecommerce_jaeger               running             port 16686
# ecommerce_prometheus           running             port 9090
# ecommerce_grafana              running             port 3000

Set-Location $ROOT

# ====================================================================
#  SECTION 4 — USER SERVICE  (PostgreSQL  port 5433)
# ====================================================================

Set-Location "$ROOT\services\UserService"

# ── 4.1  Create solution and projects ────────────────────────────────
dotnet new sln -n UserService
dotnet new classlib -n UserService.Domain         -f net10.0 -o UserService.Domain
dotnet new classlib -n UserService.Application    -f net10.0 -o UserService.Application
dotnet new classlib -n UserService.Infrastructure -f net10.0 -o UserService.Infrastructure
dotnet new classlib -n UserService.Grpc           -f net10.0 -o UserService.Grpc
dotnet new webapi   -n UserService.API            -f net10.0 -o UserService.API --use-minimal-apis

Remove-Item "UserService.Domain\Class1.cs"         -ErrorAction SilentlyContinue
Remove-Item "UserService.Application\Class1.cs"    -ErrorAction SilentlyContinue
Remove-Item "UserService.Infrastructure\Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "UserService.Grpc\Class1.cs"           -ErrorAction SilentlyContinue

dotnet sln UserService.sln add UserService.Domain\UserService.Domain.csproj
dotnet sln UserService.sln add UserService.Application\UserService.Application.csproj
dotnet sln UserService.sln add UserService.Infrastructure\UserService.Infrastructure.csproj
dotnet sln UserService.sln add UserService.Grpc\UserService.Grpc.csproj
dotnet sln UserService.sln add UserService.API\UserService.API.csproj

# ── 4.2  Project references  (Dependency Rule: inner layers never depend on outer) ──
dotnet add UserService.Application\UserService.Application.csproj `
    reference UserService.Domain\UserService.Domain.csproj

dotnet add UserService.Infrastructure\UserService.Infrastructure.csproj `
    reference UserService.Application\UserService.Application.csproj
dotnet add UserService.Infrastructure\UserService.Infrastructure.csproj `
    reference UserService.Domain\UserService.Domain.csproj

dotnet add UserService.Grpc\UserService.Grpc.csproj `
    reference UserService.Application\UserService.Application.csproj
dotnet add UserService.Grpc\UserService.Grpc.csproj `
    reference UserService.Domain\UserService.Domain.csproj

dotnet add UserService.API\UserService.API.csproj `
    reference UserService.Application\UserService.Application.csproj
dotnet add UserService.API\UserService.API.csproj `
    reference UserService.Infrastructure\UserService.Infrastructure.csproj
dotnet add UserService.API\UserService.API.csproj `
    reference UserService.Grpc\UserService.Grpc.csproj

# Shared kernel reference — all 5 projects
$shared = "..\..\shared\ECommerce.Shared\ECommerce.Shared.csproj"
dotnet add UserService.Domain\UserService.Domain.csproj                 reference $shared
dotnet add UserService.Application\UserService.Application.csproj       reference $shared
dotnet add UserService.Infrastructure\UserService.Infrastructure.csproj reference $shared
dotnet add UserService.Grpc\UserService.Grpc.csproj                     reference $shared
dotnet add UserService.API\UserService.API.csproj                       reference $shared

# ── 4.3  NuGet packages ──────────────────────────────────────────────

# Domain — zero external dependencies (pure domain model)

# Application
Set-Location UserService.Application
dotnet add package MediatR                                            --version 12.4.1
dotnet add package FluentValidation                                   --version 11.11.0
dotnet add package FluentValidation.DependencyInjectionExtensions     --version 11.11.0
dotnet add package Microsoft.Extensions.Logging.Abstractions          --version 9.0.0
dotnet add package BCrypt.Net-Next                                    --version 4.0.3
dotnet add package AutoMapper                                         --version 13.0.1
Set-Location ..

# Infrastructure
Set-Location UserService.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore                      --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design               --version 10.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL              --version 10.0.0
dotnet add package StackExchange.Redis                                --version 2.8.16
dotnet add package Confluent.Kafka                                    --version 2.6.0
dotnet add package Microsoft.Extensions.DependencyInjection           --version 9.0.0
dotnet add package Microsoft.Extensions.Configuration                 --version 9.0.0
Set-Location ..

# Grpc
Set-Location UserService.Grpc
dotnet add package Grpc.AspNetCore                                    --version 2.67.0
dotnet add package Google.Protobuf                                    --version 3.29.1
dotnet add package Grpc.Tools                                         --version 2.67.0
dotnet add package Microsoft.AspNetCore.Authorization                 --version 10.0.0
Set-Location ..

# API
Set-Location UserService.API
dotnet add package MediatR                                            --version 12.4.1
dotnet add package Serilog.AspNetCore                                 --version 8.0.3
dotnet add package Serilog.Settings.Configuration                     --version 8.0.2
dotnet add package Serilog.Sinks.Console                              --version 6.0.0
dotnet add package Serilog.Enrichers.CorrelationId                   --version 3.0.1
dotnet add package HotChocolate.AspNetCore                            --version 14.3.0
dotnet add package HotChocolate.Data                                  --version 14.3.0
dotnet add package OpenTelemetry.Extensions.Hosting                   --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore           --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore  --version 1.0.0-beta.13
dotnet add package OpenTelemetry.Exporter.Jaeger                      --version 1.5.1
dotnet add package AspNetCore.HealthChecks.NpgSql                     --version 8.0.2
dotnet add package AspNetCore.HealthChecks.Redis                      --version 8.0.2
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer      --version 10.0.0
dotnet add package Swashbuckle.AspNetCore                             --version 7.3.0
dotnet add package BCrypt.Net-Next                                    --version 4.0.3
Set-Location ..

# ── 4.4  EF Core migration (postgres-user must be running) ───────────
dotnet ef migrations add InitialCreate `
    --project UserService.Infrastructure\UserService.Infrastructure.csproj `
    --startup-project UserService.API\UserService.API.csproj `
    --output-dir Persistence\Migrations

dotnet ef database update `
    --project UserService.Infrastructure\UserService.Infrastructure.csproj `
    --startup-project UserService.API\UserService.API.csproj

# ── 4.5  Build and run ───────────────────────────────────────────────
dotnet build UserService.sln
dotnet run --project UserService.API\UserService.API.csproj
# REST    → https://localhost:5001/swagger
# GraphQL → https://localhost:5001/graphql

Set-Location $ROOT

# ====================================================================
#  SECTION 5 — PRODUCT SERVICE  (MongoDB  port 27018)
# ====================================================================

Set-Location "$ROOT\services\ProductService"

dotnet new sln -n ProductService
dotnet new classlib -n ProductService.Domain         -f net10.0 -o ProductService.Domain
dotnet new classlib -n ProductService.Application    -f net10.0 -o ProductService.Application
dotnet new classlib -n ProductService.Infrastructure -f net10.0 -o ProductService.Infrastructure
dotnet new classlib -n ProductService.Grpc           -f net10.0 -o ProductService.Grpc
dotnet new webapi   -n ProductService.API            -f net10.0 -o ProductService.API --use-minimal-apis

Remove-Item "ProductService.Domain\Class1.cs"         -ErrorAction SilentlyContinue
Remove-Item "ProductService.Application\Class1.cs"    -ErrorAction SilentlyContinue
Remove-Item "ProductService.Infrastructure\Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "ProductService.Grpc\Class1.cs"           -ErrorAction SilentlyContinue

dotnet sln ProductService.sln add ProductService.Domain\ProductService.Domain.csproj
dotnet sln ProductService.sln add ProductService.Application\ProductService.Application.csproj
dotnet sln ProductService.sln add ProductService.Infrastructure\ProductService.Infrastructure.csproj
dotnet sln ProductService.sln add ProductService.Grpc\ProductService.Grpc.csproj
dotnet sln ProductService.sln add ProductService.API\ProductService.API.csproj

dotnet add ProductService.Application\ProductService.Application.csproj `
    reference ProductService.Domain\ProductService.Domain.csproj
dotnet add ProductService.Infrastructure\ProductService.Infrastructure.csproj `
    reference ProductService.Application\ProductService.Application.csproj
dotnet add ProductService.Infrastructure\ProductService.Infrastructure.csproj `
    reference ProductService.Domain\ProductService.Domain.csproj
dotnet add ProductService.Grpc\ProductService.Grpc.csproj `
    reference ProductService.Application\ProductService.Application.csproj
dotnet add ProductService.API\ProductService.API.csproj `
    reference ProductService.Application\ProductService.Application.csproj
dotnet add ProductService.API\ProductService.API.csproj `
    reference ProductService.Infrastructure\ProductService.Infrastructure.csproj
dotnet add ProductService.API\ProductService.API.csproj `
    reference ProductService.Grpc\ProductService.Grpc.csproj

$shared = "..\..\shared\ECommerce.Shared\ECommerce.Shared.csproj"
dotnet add ProductService.Domain\ProductService.Domain.csproj                 reference $shared
dotnet add ProductService.Application\ProductService.Application.csproj       reference $shared
dotnet add ProductService.Infrastructure\ProductService.Infrastructure.csproj reference $shared
dotnet add ProductService.Grpc\ProductService.Grpc.csproj                     reference $shared
dotnet add ProductService.API\ProductService.API.csproj                       reference $shared

Set-Location ProductService.Application
dotnet add package MediatR                                            --version 12.4.1
dotnet add package FluentValidation                                   --version 11.11.0
dotnet add package FluentValidation.DependencyInjectionExtensions     --version 11.11.0
dotnet add package Microsoft.Extensions.Logging.Abstractions          --version 9.0.0
Set-Location ..

Set-Location ProductService.Infrastructure
dotnet add package MongoDB.Driver                                     --version 3.1.0
dotnet add package Confluent.Kafka                                    --version 2.6.0
dotnet add package StackExchange.Redis                                --version 2.8.16
dotnet add package Microsoft.Extensions.DependencyInjection           --version 9.0.0
dotnet add package Microsoft.Extensions.Configuration                 --version 9.0.0
Set-Location ..

Set-Location ProductService.Grpc
dotnet add package Grpc.AspNetCore                                    --version 2.67.0
dotnet add package Google.Protobuf                                    --version 3.29.1
dotnet add package Grpc.Tools                                         --version 2.67.0
Set-Location ..

Set-Location ProductService.API
dotnet add package MediatR                                            --version 12.4.1
dotnet add package Serilog.AspNetCore                                 --version 8.0.3
dotnet add package Serilog.Settings.Configuration                     --version 8.0.2
dotnet add package HotChocolate.AspNetCore                            --version 14.3.0
dotnet add package HotChocolate.Data                                  --version 14.3.0
dotnet add package OpenTelemetry.Extensions.Hosting                   --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore           --version 1.10.0
dotnet add package AspNetCore.HealthChecks.MongoDb                    --version 8.0.2
dotnet add package AspNetCore.HealthChecks.Redis                      --version 8.0.2
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer      --version 10.0.0
dotnet add package Swashbuckle.AspNetCore                             --version 7.3.0
Set-Location ..

dotnet build ProductService.sln
# dotnet run --project ProductService.API\ProductService.API.csproj
# REST    → https://localhost:5002/swagger
# GraphQL → https://localhost:5002/graphql

Set-Location $ROOT

# ====================================================================
#  SECTION 6 — ORDER SERVICE  (PostgreSQL  port 5434)
# ====================================================================

Set-Location "$ROOT\services\OrderService"

dotnet new sln -n OrderService
dotnet new classlib -n OrderService.Domain         -f net10.0 -o OrderService.Domain
dotnet new classlib -n OrderService.Application    -f net10.0 -o OrderService.Application
dotnet new classlib -n OrderService.Infrastructure -f net10.0 -o OrderService.Infrastructure
dotnet new classlib -n OrderService.Grpc           -f net10.0 -o OrderService.Grpc
dotnet new webapi   -n OrderService.API            -f net10.0 -o OrderService.API --use-minimal-apis

Remove-Item "OrderService.Domain\Class1.cs"         -ErrorAction SilentlyContinue
Remove-Item "OrderService.Application\Class1.cs"    -ErrorAction SilentlyContinue
Remove-Item "OrderService.Infrastructure\Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "OrderService.Grpc\Class1.cs"           -ErrorAction SilentlyContinue

dotnet sln OrderService.sln add OrderService.Domain\OrderService.Domain.csproj
dotnet sln OrderService.sln add OrderService.Application\OrderService.Application.csproj
dotnet sln OrderService.sln add OrderService.Infrastructure\OrderService.Infrastructure.csproj
dotnet sln OrderService.sln add OrderService.Grpc\OrderService.Grpc.csproj
dotnet sln OrderService.sln add OrderService.API\OrderService.API.csproj

dotnet add OrderService.Application\OrderService.Application.csproj `
    reference OrderService.Domain\OrderService.Domain.csproj
dotnet add OrderService.Infrastructure\OrderService.Infrastructure.csproj `
    reference OrderService.Application\OrderService.Application.csproj
dotnet add OrderService.Infrastructure\OrderService.Infrastructure.csproj `
    reference OrderService.Domain\OrderService.Domain.csproj
dotnet add OrderService.Grpc\OrderService.Grpc.csproj `
    reference OrderService.Application\OrderService.Application.csproj
dotnet add OrderService.API\OrderService.API.csproj `
    reference OrderService.Application\OrderService.Application.csproj
dotnet add OrderService.API\OrderService.API.csproj `
    reference OrderService.Infrastructure\OrderService.Infrastructure.csproj
dotnet add OrderService.API\OrderService.API.csproj `
    reference OrderService.Grpc\OrderService.Grpc.csproj

$shared = "..\..\shared\ECommerce.Shared\ECommerce.Shared.csproj"
dotnet add OrderService.Domain\OrderService.Domain.csproj                 reference $shared
dotnet add OrderService.Application\OrderService.Application.csproj       reference $shared
dotnet add OrderService.Infrastructure\OrderService.Infrastructure.csproj reference $shared
dotnet add OrderService.Grpc\OrderService.Grpc.csproj                     reference $shared
dotnet add OrderService.API\OrderService.API.csproj                       reference $shared

Set-Location OrderService.Application
dotnet add package MediatR                                            --version 12.4.1
dotnet add package FluentValidation                                   --version 11.11.0
dotnet add package FluentValidation.DependencyInjectionExtensions     --version 11.11.0
dotnet add package Grpc.Net.Client                                    --version 2.67.0
dotnet add package Google.Protobuf                                    --version 3.29.1
dotnet add package Microsoft.Extensions.Logging.Abstractions          --version 9.0.0
Set-Location ..

Set-Location OrderService.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore                      --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design               --version 10.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL              --version 10.0.0
dotnet add package Confluent.Kafka                                    --version 2.6.0
dotnet add package StackExchange.Redis                                --version 2.8.16
dotnet add package Microsoft.Extensions.DependencyInjection           --version 9.0.0
Set-Location ..

Set-Location OrderService.Grpc
dotnet add package Grpc.AspNetCore                                    --version 2.67.0
dotnet add package Google.Protobuf                                    --version 3.29.1
dotnet add package Grpc.Tools                                         --version 2.67.0
Set-Location ..

Set-Location OrderService.API
dotnet add package MediatR                                            --version 12.4.1
dotnet add package Serilog.AspNetCore                                 --version 8.0.3
dotnet add package Serilog.Settings.Configuration                     --version 8.0.2
dotnet add package HotChocolate.AspNetCore                            --version 14.3.0
dotnet add package HotChocolate.Data                                  --version 14.3.0
dotnet add package OpenTelemetry.Extensions.Hosting                   --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore           --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore  --version 1.0.0-beta.13
dotnet add package AspNetCore.HealthChecks.NpgSql                     --version 8.0.2
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer      --version 10.0.0
dotnet add package Swashbuckle.AspNetCore                             --version 7.3.0
Set-Location ..

dotnet ef migrations add InitialCreate `
    --project OrderService.Infrastructure\OrderService.Infrastructure.csproj `
    --startup-project OrderService.API\OrderService.API.csproj `
    --output-dir Persistence\Migrations

dotnet ef database update `
    --project OrderService.Infrastructure\OrderService.Infrastructure.csproj `
    --startup-project OrderService.API\OrderService.API.csproj

dotnet build OrderService.sln
Set-Location $ROOT

# ====================================================================
#  SECTION 7 — PAYMENT SERVICE  (PostgreSQL  port 5435)
# ====================================================================

Set-Location "$ROOT\services\PaymentService"

dotnet new sln -n PaymentService
dotnet new classlib -n PaymentService.Domain         -f net10.0 -o PaymentService.Domain
dotnet new classlib -n PaymentService.Application    -f net10.0 -o PaymentService.Application
dotnet new classlib -n PaymentService.Infrastructure -f net10.0 -o PaymentService.Infrastructure
dotnet new classlib -n PaymentService.Grpc           -f net10.0 -o PaymentService.Grpc
dotnet new webapi   -n PaymentService.API            -f net10.0 -o PaymentService.API --use-minimal-apis

Remove-Item "PaymentService.Domain\Class1.cs"         -ErrorAction SilentlyContinue
Remove-Item "PaymentService.Application\Class1.cs"    -ErrorAction SilentlyContinue
Remove-Item "PaymentService.Infrastructure\Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "PaymentService.Grpc\Class1.cs"           -ErrorAction SilentlyContinue

dotnet sln PaymentService.sln add PaymentService.Domain\PaymentService.Domain.csproj
dotnet sln PaymentService.sln add PaymentService.Application\PaymentService.Application.csproj
dotnet sln PaymentService.sln add PaymentService.Infrastructure\PaymentService.Infrastructure.csproj
dotnet sln PaymentService.sln add PaymentService.Grpc\PaymentService.Grpc.csproj
dotnet sln PaymentService.sln add PaymentService.API\PaymentService.API.csproj

dotnet add PaymentService.Application\PaymentService.Application.csproj `
    reference PaymentService.Domain\PaymentService.Domain.csproj
dotnet add PaymentService.Infrastructure\PaymentService.Infrastructure.csproj `
    reference PaymentService.Application\PaymentService.Application.csproj
dotnet add PaymentService.Infrastructure\PaymentService.Infrastructure.csproj `
    reference PaymentService.Domain\PaymentService.Domain.csproj
dotnet add PaymentService.Grpc\PaymentService.Grpc.csproj `
    reference PaymentService.Application\PaymentService.Application.csproj
dotnet add PaymentService.API\PaymentService.API.csproj `
    reference PaymentService.Application\PaymentService.Application.csproj
dotnet add PaymentService.API\PaymentService.API.csproj `
    reference PaymentService.Infrastructure\PaymentService.Infrastructure.csproj
dotnet add PaymentService.API\PaymentService.API.csproj `
    reference PaymentService.Grpc\PaymentService.Grpc.csproj

$shared = "..\..\shared\ECommerce.Shared\ECommerce.Shared.csproj"
dotnet add PaymentService.Domain\PaymentService.Domain.csproj                 reference $shared
dotnet add PaymentService.Application\PaymentService.Application.csproj       reference $shared
dotnet add PaymentService.Infrastructure\PaymentService.Infrastructure.csproj reference $shared
dotnet add PaymentService.Grpc\PaymentService.Grpc.csproj                     reference $shared
dotnet add PaymentService.API\PaymentService.API.csproj                       reference $shared

Set-Location PaymentService.Application
dotnet add package MediatR                                            --version 12.4.1
dotnet add package FluentValidation                                   --version 11.11.0
dotnet add package FluentValidation.DependencyInjectionExtensions     --version 11.11.0
dotnet add package Microsoft.Extensions.Logging.Abstractions          --version 9.0.0
Set-Location ..

Set-Location PaymentService.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore                      --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design               --version 10.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL              --version 10.0.0
dotnet add package Confluent.Kafka                                    --version 2.6.0
dotnet add package Microsoft.Extensions.DependencyInjection           --version 9.0.0
Set-Location ..

Set-Location PaymentService.Grpc
dotnet add package Grpc.AspNetCore                                    --version 2.67.0
dotnet add package Google.Protobuf                                    --version 3.29.1
dotnet add package Grpc.Tools                                         --version 2.67.0
Set-Location ..

Set-Location PaymentService.API
dotnet add package MediatR                                            --version 12.4.1
dotnet add package Serilog.AspNetCore                                 --version 8.0.3
dotnet add package Serilog.Settings.Configuration                     --version 8.0.2
dotnet add package OpenTelemetry.Extensions.Hosting                   --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore           --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore  --version 1.0.0-beta.13
dotnet add package AspNetCore.HealthChecks.NpgSql                     --version 8.0.2
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer      --version 10.0.0
dotnet add package Swashbuckle.AspNetCore                             --version 7.3.0
Set-Location ..

dotnet ef migrations add InitialCreate `
    --project PaymentService.Infrastructure\PaymentService.Infrastructure.csproj `
    --startup-project PaymentService.API\PaymentService.API.csproj `
    --output-dir Persistence\Migrations

dotnet ef database update `
    --project PaymentService.Infrastructure\PaymentService.Infrastructure.csproj `
    --startup-project PaymentService.API\PaymentService.API.csproj

dotnet build PaymentService.sln
Set-Location $ROOT

# ====================================================================
#  SECTION 8 — NOTIFICATION SERVICE  (MongoDB  port 27019)
# ====================================================================

Set-Location "$ROOT\services\NotificationService"

dotnet new sln -n NotificationService
dotnet new classlib -n NotificationService.Domain         -f net10.0 -o NotificationService.Domain
dotnet new classlib -n NotificationService.Application    -f net10.0 -o NotificationService.Application
dotnet new classlib -n NotificationService.Infrastructure -f net10.0 -o NotificationService.Infrastructure
dotnet new classlib -n NotificationService.Grpc           -f net10.0 -o NotificationService.Grpc
dotnet new webapi   -n NotificationService.API            -f net10.0 -o NotificationService.API --use-minimal-apis

Remove-Item "NotificationService.Domain\Class1.cs"         -ErrorAction SilentlyContinue
Remove-Item "NotificationService.Application\Class1.cs"    -ErrorAction SilentlyContinue
Remove-Item "NotificationService.Infrastructure\Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "NotificationService.Grpc\Class1.cs"           -ErrorAction SilentlyContinue

dotnet sln NotificationService.sln add NotificationService.Domain\NotificationService.Domain.csproj
dotnet sln NotificationService.sln add NotificationService.Application\NotificationService.Application.csproj
dotnet sln NotificationService.sln add NotificationService.Infrastructure\NotificationService.Infrastructure.csproj
dotnet sln NotificationService.sln add NotificationService.Grpc\NotificationService.Grpc.csproj
dotnet sln NotificationService.sln add NotificationService.API\NotificationService.API.csproj

dotnet add NotificationService.Application\NotificationService.Application.csproj `
    reference NotificationService.Domain\NotificationService.Domain.csproj
dotnet add NotificationService.Infrastructure\NotificationService.Infrastructure.csproj `
    reference NotificationService.Application\NotificationService.Application.csproj
dotnet add NotificationService.Infrastructure\NotificationService.Infrastructure.csproj `
    reference NotificationService.Domain\NotificationService.Domain.csproj
dotnet add NotificationService.Grpc\NotificationService.Grpc.csproj `
    reference NotificationService.Application\NotificationService.Application.csproj
dotnet add NotificationService.API\NotificationService.API.csproj `
    reference NotificationService.Application\NotificationService.Application.csproj
dotnet add NotificationService.API\NotificationService.API.csproj `
    reference NotificationService.Infrastructure\NotificationService.Infrastructure.csproj
dotnet add NotificationService.API\NotificationService.API.csproj `
    reference NotificationService.Grpc\NotificationService.Grpc.csproj

$shared = "..\..\shared\ECommerce.Shared\ECommerce.Shared.csproj"
dotnet add NotificationService.Domain\NotificationService.Domain.csproj                 reference $shared
dotnet add NotificationService.Application\NotificationService.Application.csproj       reference $shared
dotnet add NotificationService.Infrastructure\NotificationService.Infrastructure.csproj reference $shared
dotnet add NotificationService.Grpc\NotificationService.Grpc.csproj                     reference $shared
dotnet add NotificationService.API\NotificationService.API.csproj                       reference $shared

Set-Location NotificationService.Application
dotnet add package MediatR                                            --version 12.4.1
dotnet add package FluentValidation                                   --version 11.11.0
dotnet add package FluentValidation.DependencyInjectionExtensions     --version 11.11.0
dotnet add package Microsoft.Extensions.Logging.Abstractions          --version 9.0.0
Set-Location ..

Set-Location NotificationService.Infrastructure
dotnet add package MongoDB.Driver                                     --version 3.1.0
dotnet add package Confluent.Kafka                                    --version 2.6.0
dotnet add package MailKit                                            --version 4.8.0
dotnet add package MimeKit                                            --version 4.8.0
dotnet add package Microsoft.Extensions.DependencyInjection           --version 9.0.0
dotnet add package Microsoft.Extensions.Configuration                 --version 9.0.0
Set-Location ..

Set-Location NotificationService.Grpc
dotnet add package Grpc.AspNetCore                                    --version 2.67.0
dotnet add package Google.Protobuf                                    --version 3.29.1
dotnet add package Grpc.Tools                                         --version 2.67.0
Set-Location ..

Set-Location NotificationService.API
dotnet add package MediatR                                            --version 12.4.1
dotnet add package Serilog.AspNetCore                                 --version 8.0.3
dotnet add package Serilog.Settings.Configuration                     --version 8.0.2
dotnet add package OpenTelemetry.Extensions.Hosting                   --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore           --version 1.10.0
dotnet add package AspNetCore.HealthChecks.MongoDb                    --version 8.0.2
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer      --version 10.0.0
dotnet add package Swashbuckle.AspNetCore                             --version 7.3.0
Set-Location ..

dotnet build NotificationService.sln
Set-Location $ROOT

# ====================================================================
#  SECTION 9 — ANALYTICS SERVICE  (MongoDB  port 27020)
# ====================================================================

Set-Location "$ROOT\services\AnalyticsService"

dotnet new sln -n AnalyticsService
dotnet new classlib -n AnalyticsService.Domain         -f net10.0 -o AnalyticsService.Domain
dotnet new classlib -n AnalyticsService.Application    -f net10.0 -o AnalyticsService.Application
dotnet new classlib -n AnalyticsService.Infrastructure -f net10.0 -o AnalyticsService.Infrastructure
dotnet new classlib -n AnalyticsService.Grpc           -f net10.0 -o AnalyticsService.Grpc
dotnet new webapi   -n AnalyticsService.API            -f net10.0 -o AnalyticsService.API --use-minimal-apis

Remove-Item "AnalyticsService.Domain\Class1.cs"         -ErrorAction SilentlyContinue
Remove-Item "AnalyticsService.Application\Class1.cs"    -ErrorAction SilentlyContinue
Remove-Item "AnalyticsService.Infrastructure\Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "AnalyticsService.Grpc\Class1.cs"           -ErrorAction SilentlyContinue

dotnet sln AnalyticsService.sln add AnalyticsService.Domain\AnalyticsService.Domain.csproj
dotnet sln AnalyticsService.sln add AnalyticsService.Application\AnalyticsService.Application.csproj
dotnet sln AnalyticsService.sln add AnalyticsService.Infrastructure\AnalyticsService.Infrastructure.csproj
dotnet sln AnalyticsService.sln add AnalyticsService.Grpc\AnalyticsService.Grpc.csproj
dotnet sln AnalyticsService.sln add AnalyticsService.API\AnalyticsService.API.csproj

dotnet add AnalyticsService.Application\AnalyticsService.Application.csproj `
    reference AnalyticsService.Domain\AnalyticsService.Domain.csproj
dotnet add AnalyticsService.Infrastructure\AnalyticsService.Infrastructure.csproj `
    reference AnalyticsService.Application\AnalyticsService.Application.csproj
dotnet add AnalyticsService.Infrastructure\AnalyticsService.Infrastructure.csproj `
    reference AnalyticsService.Domain\AnalyticsService.Domain.csproj
dotnet add AnalyticsService.Grpc\AnalyticsService.Grpc.csproj `
    reference AnalyticsService.Application\AnalyticsService.Application.csproj
dotnet add AnalyticsService.API\AnalyticsService.API.csproj `
    reference AnalyticsService.Application\AnalyticsService.Application.csproj
dotnet add AnalyticsService.API\AnalyticsService.API.csproj `
    reference AnalyticsService.Infrastructure\AnalyticsService.Infrastructure.csproj
dotnet add AnalyticsService.API\AnalyticsService.API.csproj `
    reference AnalyticsService.Grpc\AnalyticsService.Grpc.csproj

$shared = "..\..\shared\ECommerce.Shared\ECommerce.Shared.csproj"
dotnet add AnalyticsService.Domain\AnalyticsService.Domain.csproj                 reference $shared
dotnet add AnalyticsService.Application\AnalyticsService.Application.csproj       reference $shared
dotnet add AnalyticsService.Infrastructure\AnalyticsService.Infrastructure.csproj reference $shared
dotnet add AnalyticsService.Grpc\AnalyticsService.Grpc.csproj                     reference $shared
dotnet add AnalyticsService.API\AnalyticsService.API.csproj                       reference $shared

Set-Location AnalyticsService.Application
dotnet add package MediatR                                            --version 12.4.1
dotnet add package FluentValidation                                   --version 11.11.0
dotnet add package FluentValidation.DependencyInjectionExtensions     --version 11.11.0
dotnet add package Microsoft.Extensions.Logging.Abstractions          --version 9.0.0
Set-Location ..

Set-Location AnalyticsService.Infrastructure
dotnet add package MongoDB.Driver                                     --version 3.1.0
dotnet add package Confluent.Kafka                                    --version 2.6.0
dotnet add package StackExchange.Redis                                --version 2.8.16
dotnet add package Microsoft.Extensions.DependencyInjection           --version 9.0.0
dotnet add package Microsoft.Extensions.Configuration                 --version 9.0.0
Set-Location ..

Set-Location AnalyticsService.Grpc
dotnet add package Grpc.AspNetCore                                    --version 2.67.0
dotnet add package Google.Protobuf                                    --version 3.29.1
dotnet add package Grpc.Tools                                         --version 2.67.0
Set-Location ..

Set-Location AnalyticsService.API
dotnet add package MediatR                                            --version 12.4.1
dotnet add package Serilog.AspNetCore                                 --version 8.0.3
dotnet add package Serilog.Settings.Configuration                     --version 8.0.2
dotnet add package OpenTelemetry.Extensions.Hosting                   --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore           --version 1.10.0
dotnet add package AspNetCore.HealthChecks.MongoDb                    --version 8.0.2
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer      --version 10.0.0
dotnet add package Swashbuckle.AspNetCore                             --version 7.3.0
Set-Location ..

dotnet build AnalyticsService.sln
Set-Location $ROOT

# ====================================================================
#  SECTION 10 — API GATEWAY  (YARP + GraphQL Schema Stitching)
# ====================================================================

Set-Location "$ROOT\gateway\ApiGateway"

dotnet new sln -n ApiGateway
dotnet new webapi -n ApiGateway -f net10.0 -o ApiGateway --use-minimal-apis
dotnet sln ApiGateway.sln add ApiGateway\ApiGateway.csproj

dotnet add ApiGateway\ApiGateway.csproj `
    reference ..\..\shared\ECommerce.Shared\ECommerce.Shared.csproj

Set-Location ApiGateway
dotnet add package Yarp.ReverseProxy                                  --version 2.2.0
dotnet add package HotChocolate.AspNetCore                            --version 14.3.0
dotnet add package HotChocolate.Stitching                             --version 14.3.0
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer      --version 10.0.0
dotnet add package Serilog.AspNetCore                                 --version 8.0.3
dotnet add package Serilog.Settings.Configuration                     --version 8.0.2
dotnet add package OpenTelemetry.Extensions.Hosting                   --version 1.10.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore           --version 1.10.0
dotnet add package Polly                                              --version 8.4.1
dotnet add package Polly.Extensions.Http                              --version 3.0.0
dotnet add package AspNetCore.HealthChecks.UI.Client                  --version 8.0.2
dotnet add package Swashbuckle.AspNetCore                             --version 7.3.0
Set-Location ..

dotnet build ApiGateway.sln
Set-Location $ROOT

# ====================================================================
#  SECTION 11 — TESTS
# ====================================================================

Set-Location "$ROOT\tests"

dotnet new sln -n ECommerce.Tests
dotnet new xunit -n ECommerce.Tests.Unit         -f net10.0 -o ECommerce.Tests.Unit
dotnet new xunit -n ECommerce.Tests.Integration  -f net10.0 -o ECommerce.Tests.Integration
dotnet new xunit -n ECommerce.Tests.Architecture -f net10.0 -o ECommerce.Tests.Architecture

dotnet sln ECommerce.Tests.sln add ECommerce.Tests.Unit\ECommerce.Tests.Unit.csproj
dotnet sln ECommerce.Tests.sln add ECommerce.Tests.Integration\ECommerce.Tests.Integration.csproj
dotnet sln ECommerce.Tests.sln add ECommerce.Tests.Architecture\ECommerce.Tests.Architecture.csproj

# Unit tests
Set-Location ECommerce.Tests.Unit
dotnet add package Moq                                                --version 4.20.72
dotnet add package FluentAssertions                                   --version 7.0.0
dotnet add package MediatR                                            --version 12.4.1
dotnet add package Microsoft.Extensions.Logging.Abstractions          --version 9.0.0
dotnet add reference ..\..\..\services\UserService\UserService.Domain\UserService.Domain.csproj
dotnet add reference ..\..\..\services\UserService\UserService.Application\UserService.Application.csproj
dotnet add reference ..\..\..\services\OrderService\OrderService.Domain\OrderService.Domain.csproj
dotnet add reference ..\..\..\services\OrderService\OrderService.Application\OrderService.Application.csproj
dotnet add reference ..\..\..\services\ProductService\ProductService.Domain\ProductService.Domain.csproj
Set-Location ..

# Integration tests (spins up real DB/Kafka via Testcontainers)
Set-Location ECommerce.Tests.Integration
dotnet add package Testcontainers                                     --version 3.10.0
dotnet add package Testcontainers.PostgreSql                          --version 3.10.0
dotnet add package Testcontainers.MongoDb                            --version 3.10.0
dotnet add package Testcontainers.Kafka                              --version 3.10.0
dotnet add package FluentAssertions                                   --version 7.0.0
dotnet add package Microsoft.AspNetCore.Mvc.Testing                   --version 10.0.0
Set-Location ..

# Architecture tests (enforces layer dependency rules)
Set-Location ECommerce.Tests.Architecture
dotnet add package NetArchTest.Rules                                  --version 1.4.0
dotnet add package FluentAssertions                                   --version 7.0.0
dotnet add reference ..\..\..\services\UserService\UserService.Domain\UserService.Domain.csproj
dotnet add reference ..\..\..\services\UserService\UserService.Application\UserService.Application.csproj
dotnet add reference ..\..\..\services\UserService\UserService.Infrastructure\UserService.Infrastructure.csproj
dotnet add reference ..\..\..\services\OrderService\OrderService.Domain\OrderService.Domain.csproj
Set-Location ..

dotnet build ECommerce.Tests.sln
Set-Location $ROOT

# ====================================================================
#  SECTION 12 — EF CORE MIGRATIONS
#  Run AFTER docker compose up -d confirms databases are healthy
# ====================================================================

# UserService → postgres-user (5433)
Set-Location "$ROOT\services\UserService"
dotnet ef migrations add InitialCreate `
    --project UserService.Infrastructure\UserService.Infrastructure.csproj `
    --startup-project UserService.API\UserService.API.csproj `
    --output-dir Persistence\Migrations
dotnet ef database update `
    --project UserService.Infrastructure\UserService.Infrastructure.csproj `
    --startup-project UserService.API\UserService.API.csproj

# OrderService → postgres-order (5434)
Set-Location "$ROOT\services\OrderService"
dotnet ef migrations add InitialCreate `
    --project OrderService.Infrastructure\OrderService.Infrastructure.csproj `
    --startup-project OrderService.API\OrderService.API.csproj `
    --output-dir Persistence\Migrations
dotnet ef database update `
    --project OrderService.Infrastructure\OrderService.Infrastructure.csproj `
    --startup-project OrderService.API\OrderService.API.csproj

# PaymentService → postgres-payment (5435)
Set-Location "$ROOT\services\PaymentService"
dotnet ef migrations add InitialCreate `
    --project PaymentService.Infrastructure\PaymentService.Infrastructure.csproj `
    --startup-project PaymentService.API\PaymentService.API.csproj `
    --output-dir Persistence\Migrations
dotnet ef database update `
    --project PaymentService.Infrastructure\PaymentService.Infrastructure.csproj `
    --startup-project PaymentService.API\PaymentService.API.csproj

Set-Location $ROOT

# ====================================================================
#  SECTION 13 — RUN ALL SERVICES
#  Open a separate PowerShell terminal for each service
# ====================================================================

# Terminal 1 — UserService
Set-Location "$ROOT\services\UserService"
dotnet run --project UserService.API\UserService.API.csproj

# Terminal 2 — ProductService
Set-Location "$ROOT\services\ProductService"
dotnet run --project ProductService.API\ProductService.API.csproj

# Terminal 3 — OrderService
Set-Location "$ROOT\services\OrderService"
dotnet run --project OrderService.API\OrderService.API.csproj

# Terminal 4 — PaymentService
Set-Location "$ROOT\services\PaymentService"
dotnet run --project PaymentService.API\PaymentService.API.csproj

# Terminal 5 — NotificationService
Set-Location "$ROOT\services\NotificationService"
dotnet run --project NotificationService.API\NotificationService.API.csproj

# Terminal 6 — AnalyticsService
Set-Location "$ROOT\services\AnalyticsService"
dotnet run --project AnalyticsService.API\AnalyticsService.API.csproj

# Terminal 7 — API Gateway
Set-Location "$ROOT\gateway\ApiGateway"
dotnet run --project ApiGateway\ApiGateway.csproj

# ====================================================================
#  SECTION 14 — VERIFICATION COMMANDS
# ====================================================================

# All containers healthy
Set-Location "$ROOT\docker"
docker compose ps

# Kafka topics exist
docker exec ecommerce_kafka `
    kafka-topics --bootstrap-server localhost:9092 --list

# Redis responding
docker exec ecommerce_redis `
    redis-cli -a redis_pass ping

# PostgreSQL databases (one per service)
docker exec ecommerce_postgres_user    psql -U user_svc    -d userservice_db    -c "\dt"
docker exec ecommerce_postgres_order   psql -U order_svc   -d orderservice_db   -c "\dt"
docker exec ecommerce_postgres_payment psql -U payment_svc -d paymentservice_db -c "\dt"

# MongoDB collections (one container per service)
docker exec ecommerce_mongodb_product      mongosh --quiet `
    "mongodb://product_svc:product_pass@localhost:27017/productservice_db?authSource=admin" `
    --eval "db.getCollectionNames()"

docker exec ecommerce_mongodb_notification mongosh --quiet `
    "mongodb://notif_svc:notif_pass@localhost:27017/notificationservice_db?authSource=admin" `
    --eval "db.getCollectionNames()"

docker exec ecommerce_mongodb_analytics    mongosh --quiet `
    "mongodb://analytics_svc:analytics_pass@localhost:27017/analyticsservice_db?authSource=admin" `
    --eval "db.getCollectionNames()"

# Run unit tests
Set-Location "$ROOT\tests"
dotnet test ECommerce.Tests.Unit\ECommerce.Tests.Unit.csproj --verbosity normal

# Run integration tests (requires Docker)
dotnet test ECommerce.Tests.Integration\ECommerce.Tests.Integration.csproj --verbosity normal

# Run architecture tests
dotnet test ECommerce.Tests.Architecture\ECommerce.Tests.Architecture.csproj --verbosity normal

# ====================================================================
#  SECTION 15 — DOCKER COMPOSE MANAGEMENT
# ====================================================================

Set-Location "$ROOT\docker"

# Start all infrastructure
docker compose up -d

# Stop without removing volumes (data preserved)
docker compose stop

# Stop and remove all volumes (full reset — loses all DB data)
docker compose down -v

# Restart a single container
docker compose restart kafka
docker compose restart postgres-user

# View logs for all containers
docker compose logs -f

# View logs for one specific service
docker compose logs postgres-user     -f
docker compose logs postgres-order    -f
docker compose logs postgres-payment  -f
docker compose logs mongodb-product   -f
docker compose logs kafka             -f

# Check disk usage of named volumes
docker volume ls
docker system df
