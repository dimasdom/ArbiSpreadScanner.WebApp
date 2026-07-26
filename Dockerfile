# Multi-stage production Dockerfile for ArbiScannerWebApp
#
# IMPORTANT: This Dockerfile must be built with the Projects parent directory as context.
#
# Build command (run from /Users/.../Projects/):
#   docker build -f ArbiScannerWebApp/Dockerfile -t arbiscanner-web .
#
# Or from the workspace root:
#   docker build -f Dockerfile -t arbiscanner-web ../

# ─── Stage 1: .NET SDK + Node.js (for SPA build) ─────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# ─── Stage 2: Restore (project files only for layer caching) ─────────────────
COPY ["ArbiScannerWebApp/ArbiScannerWeb.API/ArbiScannerWeb.API.csproj", "ArbiScannerWebApp/ArbiScannerWeb.API/"]
COPY ["ArbiScannerWebApp/ArbiScannerWeb.Abstractions/ArbiScannerWeb.Abstractions.csproj", "ArbiScannerWebApp/ArbiScannerWeb.Abstractions/"]
COPY ["ArbiScannerWebApp/ArbiScannerWeb.Domain/ArbiScannerWeb.Domain.csproj", "ArbiScannerWebApp/ArbiScannerWeb.Domain/"]
COPY ["ArbiScannerWebApp/ArbiScannerWeb.Infrastructure/ArbiScannerWeb.Infrastructure.csproj", "ArbiScannerWebApp/ArbiScannerWeb.Infrastructure/"]
COPY ["ArbiScannerWebApp/ArbiScannerWeb.Client/ArbiScannerWeb.Client.esproj", "ArbiScannerWebApp/ArbiScannerWeb.Client/"]
COPY ["ArbiScannerWebApp/ArbiScannerWeb.Client/package.json", "ArbiScannerWebApp/ArbiScannerWeb.Client/"]
COPY ["ArbiScannerWebApp/ArbiScannerWeb.Client/package-lock.json", "ArbiScannerWebApp/ArbiScannerWeb.Client/"]

RUN dotnet restore "ArbiScannerWebApp/ArbiScannerWeb.API/ArbiScannerWeb.API.csproj"

# ─── Stage 3: Copy all sources and publish ───────────────────────────────────
COPY ["ArbiScannerWebApp/", "ArbiScannerWebApp/"]

# Remove the esproj reference so the npm build is not triggered during publish.
# The React app is built in its own container (see ArbiScannerWeb.Client/Dockerfile).
RUN sed -i '/<ProjectReference.*\.esproj/,/<\/ProjectReference>/d' \
        "ArbiScannerWebApp/ArbiScannerWeb.API/ArbiScannerWeb.API.csproj"

RUN dotnet publish "ArbiScannerWebApp/ArbiScannerWeb.API/ArbiScannerWeb.API.csproj" \
        -c Release \
        -o /app/publish \
        /p:UseAppHost=false

# ─── Stage 4: Runtime image ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
EXPOSE 8080

RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl && rm -rf /var/lib/apt/lists/*

COPY --from=build-env /app/publish .

USER app

ENTRYPOINT ["dotnet", "ArbiScannerWeb.API.dll"]
