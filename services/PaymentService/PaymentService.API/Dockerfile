# ============================================================
#  PaymentService — Multi-Stage Dockerfile (.NET 10)
#  Build context must be the ECommerceMicroservicesApp root:
#    docker build -t paymentservice-api -f services/PaymentService/PaymentService.API/Dockerfile .
# ============================================================

# ── Stage 1: Runtime base ────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Install curl for the HEALTHCHECK — must run as root, BEFORE switching to $APP_UID
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8088
EXPOSE 8089

# ── Stage 2: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy .csproj files first (layer-cache-friendly restore).
# IMPORTANT: destination paths mirror the ORIGINAL repo structure exactly,
# so they line up with what "COPY . ." brings in below.
COPY ["services/PaymentService/PaymentService.API/PaymentService.API.csproj",                      "services/PaymentService/PaymentService.API/"]
COPY ["services/PaymentService/PaymentService.Application/PaymentService.Application.csproj",      "services/PaymentService/PaymentService.Application/"]
COPY ["services/PaymentService/PaymentService.Domain/PaymentService.Domain.csproj",                "services/PaymentService/PaymentService.Domain/"]
COPY ["services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj","services/PaymentService/PaymentService.Infrastructure/"]
COPY ["services/PaymentService/PaymentService.Contracts/PaymentService.Contracts.csproj",          "services/PaymentService/PaymentService.Contracts/"]

# Copy shared kernel keeping the same relative path
COPY ["shared/ECommerce.Shared/ECommerce.Shared.csproj", "shared/ECommerce.Shared/"]

RUN dotnet restore "services/PaymentService/PaymentService.API/PaymentService.API.csproj"

# Copy all source — paths now match what restore already populated
COPY . .

WORKDIR "/src/services/PaymentService/PaymentService.API"
RUN dotnet build "PaymentService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Stage 3: Publish ─────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PaymentService.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Final image ──────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8088/health/live || exit 1

ENTRYPOINT ["dotnet", "PaymentService.API.dll"]
