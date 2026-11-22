# ============================================================
#  OrderService — Multi-Stage Dockerfile (.NET 10)
#  Build context must be the ECommerceMicroservicesApp root:
#    docker build -t orderservice-api -f services/OrderService/OrderService.API/Dockerfile .
# ============================================================

# ── Stage 1: Runtime base ────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Install curl for the HEALTHCHECK — must run as root, BEFORE switching to $APP_UID
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8086
EXPOSE 8087

# ── Stage 2: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy .csproj files first (layer-cache-friendly restore).
# IMPORTANT: destination paths mirror the ORIGINAL repo structure exactly,
# so they line up with what "COPY . ." brings in below.
COPY ["services/OrderService/OrderService.API/OrderService.API.csproj",                      "services/OrderService/OrderService.API/"]
COPY ["services/OrderService/OrderService.Application/OrderService.Application.csproj",      "services/OrderService/OrderService.Application/"]
COPY ["services/OrderService/OrderService.Domain/OrderService.Domain.csproj",                "services/OrderService/OrderService.Domain/"]
COPY ["services/OrderService/OrderService.Infrastructure/OrderService.Infrastructure.csproj","services/OrderService/OrderService.Infrastructure/"]
COPY ["services/OrderService/OrderService.Contracts/OrderService.Contracts.csproj",          "services/OrderService/OrderService.Contracts/"]

# Copy shared kernel keeping the same relative path
COPY ["shared/ECommerce.Shared/ECommerce.Shared.csproj", "shared/ECommerce.Shared/"]

RUN dotnet restore "services/OrderService/OrderService.API/OrderService.API.csproj"

# Copy all source — paths now match what restore already populated
COPY . .

WORKDIR "/src/services/OrderService/OrderService.API"
RUN dotnet build "OrderService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Stage 3: Publish ─────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "OrderService.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Final image ──────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8086/health/live || exit 1

ENTRYPOINT ["dotnet", "OrderService.API.dll"]
