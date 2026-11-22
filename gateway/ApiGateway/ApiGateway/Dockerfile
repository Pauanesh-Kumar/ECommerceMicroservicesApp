# ── Stage 1: base runtime ──────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Install curl for the HEALTHCHECK — must run as root, BEFORE switching to $APP_UID
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8000
EXPOSE 8001

# ── Stage 2: restore ────────────────────────────────────────────────────────────
# Build context root = ECommerceMicroservicesApp/ (set by "context: .." in docker-compose.yml)
# so paths here must be relative to that root, not to this Dockerfile's own folder.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY gateway/ApiGateway/ApiGateway/ApiGateway.csproj gateway/ApiGateway/ApiGateway/
RUN dotnet restore gateway/ApiGateway/ApiGateway/ApiGateway.csproj

# ── Stage 3: build ───────────────────────────────────────────────────────────────
FROM restore AS build
COPY gateway/ApiGateway/ApiGateway/ gateway/ApiGateway/ApiGateway/
RUN dotnet build gateway/ApiGateway/ApiGateway/ApiGateway.csproj -c Release --no-restore -o /app/build

# ── Stage 4: publish ──────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish gateway/ApiGateway/ApiGateway/ApiGateway.csproj -c Release --no-restore -o /app/publish

# ── Stage 5: final ─────────────────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -f http://localhost:8000/health || exit 1
ENTRYPOINT ["dotnet", "ApiGateway.dll"]