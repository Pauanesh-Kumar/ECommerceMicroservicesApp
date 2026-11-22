# ============================================================
#  AnalyticsService — Multi-Stage Dockerfile (.NET 10)
#  Build context must be the ECommerceMicroservicesApp root:
#    docker build -t product-service-api \
#      -f services/AnalyticsService/AnalyticsService.API/Dockerfile .
# ============================================================

# ── Stage 1: Runtime base ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Install curl for the HEALTHCHECK — must run as root, BEFORE switching to $APP_UID
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8090
EXPOSE 8091

# ── Stage 2: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy .csproj files first — maximises Docker layer-cache hits on restore.
# Paths mirror the repo structure so "COPY . ." below aligns perfectly.
COPY ["services/AnalyticsService/AnalyticsService.API/AnalyticsService.API.csproj",                       "services/AnalyticsService/AnalyticsService.API/"]
COPY ["services/AnalyticsService/AnalyticsService.Application/AnalyticsService.Application.csproj",       "services/AnalyticsService/AnalyticsService.Application/"]
COPY ["services/AnalyticsService/AnalyticsService.Domain/AnalyticsService.Domain.csproj",                 "services/AnalyticsService/AnalyticsService.Domain/"]
COPY ["services/AnalyticsService/AnalyticsService.Infrastructure/AnalyticsService.Infrastructure.csproj", "services/AnalyticsService/AnalyticsService.Infrastructure/"]
COPY ["shared/ECommerce.Shared/ECommerce.Shared.csproj",                                            "shared/ECommerce.Shared/"]

RUN dotnet restore "services/AnalyticsService/AnalyticsService.API/AnalyticsService.API.csproj"

COPY . .

WORKDIR "/src/services/AnalyticsService/AnalyticsService.API"
RUN dotnet build "AnalyticsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Stage 3: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AnalyticsService.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Final image ──────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8090/health/live || exit 1

ENTRYPOINT ["dotnet", "AnalyticsService.API.dll"]