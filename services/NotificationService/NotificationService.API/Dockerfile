# ============================================================
#  NotificationService — Multi-Stage Dockerfile (.NET 10)
#  Build context must be the ECommerceMicroservicesApp root:
#    docker build -t notification-service-api \
#      -f services/NotificationService/NotificationService.API/Dockerfile .
# ============================================================

# ── Stage 1: Runtime base ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Install curl for the HEALTHCHECK — must run as root, BEFORE switching to $APP_UID
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8082
EXPOSE 8083

# ── Stage 2: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy .csproj files first — maximises Docker layer-cache hits on restore.
# Paths mirror the repo structure so "COPY . ." below aligns perfectly.
COPY ["services/NotificationService/NotificationService.API/NotificationService.API.csproj",                       "services/NotificationService/NotificationService.API/"]
COPY ["services/NotificationService/NotificationService.Application/NotificationService.Application.csproj",       "services/NotificationService/NotificationService.Application/"]
COPY ["services/NotificationService/NotificationService.Domain/NotificationService.Domain.csproj",                 "services/NotificationService/NotificationService.Domain/"]
COPY ["services/NotificationService/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "services/NotificationService/NotificationService.Infrastructure/"]
COPY ["shared/ECommerce.Shared/ECommerce.Shared.csproj",                                                           "shared/ECommerce.Shared/"]

RUN dotnet restore "services/NotificationService/NotificationService.API/NotificationService.API.csproj"

COPY . .

WORKDIR "/src/services/NotificationService/NotificationService.API"
RUN dotnet build "NotificationService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Stage 3: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NotificationService.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Final image ──────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8082/health/live || exit 1

ENTRYPOINT ["dotnet", "NotificationService.API.dll"]