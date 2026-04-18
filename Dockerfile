# syntax=docker/dockerfile:1.7

# OpenGate container image.
#
# This image is published from CI on every release tag (v*). The runtime
# auto-bootstraps on first start: MongoIndexInitializer ensures the required
# indexes exist and SeedData seeds roles, settings, themes, tax rates, and the
# bootstrap admin user. Set OPENGATE_ADMIN_EMAIL and OPENGATE_ADMIN_PASSWORD
# (or the equivalent Bootstrap:* config keys) to control the admin account; if
# you don't, a strong random password is generated and written to the log on
# first start.

ARG DOTNET_VERSION=10.0
ARG BUILD_CONFIGURATION=Release

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG BUILD_CONFIGURATION
ARG BUILD_VERSION=0.0.0
WORKDIR /src

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    NUGET_XMLDOC_MODE=skip

COPY OpenGate.sln Directory.Build.props ./
COPY src/ ./src/
COPY extensions/ ./extensions/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/OpenGate.Web/OpenGate.Web.csproj \
        --runtime linux-x64

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/OpenGate.Web/OpenGate.Web.csproj \
        --configuration ${BUILD_CONFIGURATION} \
        --runtime linux-x64 \
        --self-contained false \
        --no-restore \
        -p:Version=${BUILD_VERSION} \
        -p:InformationalVersion=${BUILD_VERSION} \
        -p:UseAppHost=false \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        --output /app/publish

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG BUILD_VERSION=0.0.0

LABEL org.opencontainers.image.title="OpenGate" \
      org.opencontainers.image.description="Open-source hosting billing platform (.NET 10 / Blazor Server / MongoDB)" \
      org.opencontainers.image.source="https://github.com/Mirin/OpenGate" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version="${BUILD_VERSION}"

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

WORKDIR /app

# tini for proper PID 1 signal handling, curl for the healthcheck,
# tzdata + ICU so date/locale handling matches the host expectations.
RUN apt-get update \
 && apt-get install -y --no-install-recommends tini curl ca-certificates tzdata libicu-dev \
 && rm -rf /var/lib/apt/lists/*

# Run as a dedicated non-root user. The published payload and the writable
# data directory are both owned by it.
RUN groupadd --system --gid 1000 opengate \
 && useradd  --system --uid 1000 --gid opengate --home /app --shell /usr/sbin/nologin opengate \
 && mkdir -p /app/wwwroot/uploads /var/lib/opengate \
 && chown -R opengate:opengate /app /var/lib/opengate

COPY --from=build --chown=opengate:opengate /app/publish/ ./

USER opengate

EXPOSE 8080

# ASP.NET reports 200 on /healthz once Kestrel is accepting traffic. If you
# add a richer health endpoint later, swap this URL.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl --fail --silent --show-error http://127.0.0.1:8080/ > /dev/null || exit 1

ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "OpenGate.Web.dll"]
