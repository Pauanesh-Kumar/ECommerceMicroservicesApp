# ============================================================
#  CartService — Multi-Stage Dockerfile (.NET 10)
#  Build context must be the ECommerceMicroservicesApp root:
#    docker build -t cart-service-api \
#      -f services/CartService/CartService.API/Dockerfile .
# ============================================================

# ── Stage 1: Runtime base ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Install curl for the HEALTHCHECK — must run as root, BEFORE switching to $APP_UID
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8088
EXPOSE 8089

# ── Stage 2: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy .csproj files first — maximises Docker layer-cache hits on restore.
# Paths mirror the repo structure so "COPY . ." below aligns perfectly.
COPY ["services/CartService/CartService.API/CartService.API.csproj",                       "services/CartService/CartService.API/"]
COPY ["services/CartService/CartService.Application/CartService.Application.csproj",       "services/CartService/CartService.Application/"]
COPY ["services/CartService/CartService.Domain/CartService.Domain.csproj",                 "services/CartService/CartService.Domain/"]
COPY ["services/CartService/CartService.Infrastructure/CartService.Infrastructure.csproj", "services/CartService/CartService.Infrastructure/"]
COPY ["shared/ECommerce.Shared/ECommerce.Shared.csproj",                                            "shared/ECommerce.Shared/"]

RUN dotnet restore "services/CartService/CartService.API/CartService.API.csproj"

COPY . .

WORKDIR "/src/services/CartService/CartService.API"
RUN dotnet build "CartService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Stage 3: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "CartService.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Final image ──────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8088/health/live || exit 1

ENTRYPOINT ["dotnet", "CartService.API.dll"]