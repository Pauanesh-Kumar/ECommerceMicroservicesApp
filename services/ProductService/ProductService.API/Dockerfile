# ============================================================
#  ProductService — Multi-Stage Dockerfile (.NET 10)
#  Build context must be the ECommerceMicroservicesApp root:
#    docker build -t product-service-api \
#      -f services/ProductService/ProductService.API/Dockerfile .
# ============================================================

# ── Stage 1: Runtime base ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Install curl for the HEALTHCHECK — must run as root, BEFORE switching to $APP_UID
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8084
EXPOSE 8085

# ── Stage 2: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy .csproj files first — maximises Docker layer-cache hits on restore.
# Paths mirror the repo structure so "COPY . ." below aligns perfectly.
COPY ["services/ProductService/ProductService.API/ProductService.API.csproj",                       "services/ProductService/ProductService.API/"]
COPY ["services/ProductService/ProductService.Application/ProductService.Application.csproj",       "services/ProductService/ProductService.Application/"]
COPY ["services/ProductService/ProductService.Domain/ProductService.Domain.csproj",                 "services/ProductService/ProductService.Domain/"]
COPY ["services/ProductService/ProductService.Infrastructure/ProductService.Infrastructure.csproj", "services/ProductService/ProductService.Infrastructure/"]
COPY ["shared/ECommerce.Shared/ECommerce.Shared.csproj",                                            "shared/ECommerce.Shared/"]

RUN dotnet restore "services/ProductService/ProductService.API/ProductService.API.csproj"

COPY . .

WORKDIR "/src/services/ProductService/ProductService.API"
RUN dotnet build "ProductService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Stage 3: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ProductService.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Final image ──────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8084/health/live || exit 1

ENTRYPOINT ["dotnet", "ProductService.API.dll"]