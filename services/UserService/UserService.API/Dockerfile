# ============================================================
#  UserService — Multi-Stage Dockerfile (.NET 10)
#  Build context must be the ECommerceMicroservicesApp root:
#    docker build -t userservice-api -f services/UserService/UserService.API/Dockerfile .
# ============================================================

# ── Stage 1: Runtime base ────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Install curl for the HEALTHCHECK — must run as root, BEFORE switching to $APP_UID
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# ── Stage 2: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy .csproj files first (layer-cache-friendly restore).
# IMPORTANT: destination paths mirror the ORIGINAL repo structure exactly,
# so they line up with what "COPY . ." brings in below.
COPY ["services/UserService/UserService.API/UserService.API.csproj",                      "services/UserService/UserService.API/"]
COPY ["services/UserService/UserService.Application/UserService.Application.csproj",      "services/UserService/UserService.Application/"]
COPY ["services/UserService/UserService.Domain/UserService.Domain.csproj",                "services/UserService/UserService.Domain/"]
COPY ["services/UserService/UserService.Infrastructure/UserService.Infrastructure.csproj","services/UserService/UserService.Infrastructure/"]
COPY ["services/UserService/UserService.Grpc/UserService.Grpc.csproj",                    "services/UserService/UserService.Grpc/"]

# Copy shared kernel keeping the same relative path
COPY ["shared/ECommerce.Shared/ECommerce.Shared.csproj", "shared/ECommerce.Shared/"]

RUN dotnet restore "services/UserService/UserService.API/UserService.API.csproj"

# Copy all source — paths now match what restore already populated
COPY . .

WORKDIR "/src/services/UserService/UserService.API"
RUN dotnet build "UserService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Stage 3: Publish ─────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "UserService.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Final image ──────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "UserService.API.dll"]
