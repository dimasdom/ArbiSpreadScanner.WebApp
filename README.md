# ArbiScannerWebApp

The main user-facing web application for the ArbiScanner platform. It displays real-time cryptocurrency arbitrage spread opportunities discovered by the ArbitrageScanner engine. Users can browse current spreads, view historical ticker data, set alerts, and manage their account — including linking a Telegram account for push notifications.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Project Responsibilities](#project-responsibilities)
- [Technologies](#technologies)
- [Prerequisites](#prerequisites)
- [Running Locally](#running-locally)
- [Environment Variables](#environment-variables)
- [Database Migrations](#database-migrations)
- [Docker](#docker)
- [Project Structure](#project-structure)

---

## Overview

ArbiScannerWebApp is a full-stack application composed of an ASP.NET Core 10 REST API with a React 19 + Vite SPA. The backend consumes spread events from a RabbitMQ fanout exchange, persists them to MongoDB, and pushes updates to connected browser clients in real time over SignalR WebSockets. User accounts, identity, refresh tokens, and Telegram link state are stored in PostgreSQL. Redis provides caching.

Key user-facing features:

- Live spread table updated in real time via SignalR
- Historical spread ticker charts (ApexCharts)
- Spread detail pages with order book visualization
- User registration, login, email confirmation, and password reset
- Telegram account linking for notifications delivered by the TelegramNotifierApp service
- Subscription management

---

## Architecture

The solution follows Clean Architecture, organized into five projects with a strict dependency direction: Domain <- Abstractions <- Infrastructure <- API. The React SPA is an independent project under the same solution.

```
ArbiScannerWeb.Client  (SPA)
        |
        | HTTP / WebSocket
        v
ArbiScannerWeb.API  ──>  ArbiScannerWeb.Infrastructure  ──>  ArbiScannerWeb.Abstractions  ──>  ArbiScannerWeb.Domain
```

**Data flows:**

1. The ArbitrageScanner engine publishes spread events to the `spread_fanout_exchange` RabbitMQ exchange.
2. `RabbitMqService` in Infrastructure consumes from the `spread_api` queue, processes messages via `MessageProcessingService`, and persists the active spreads to MongoDB (`CurrentSpreads` collection) and ticker history (`SpreadsTicker` collection).
3. After persistence, `IRealtimeNotifier` (implemented by `SignalRService` in the API project) pushes the updated spread data to all connected browser clients over the `TradeOpportunityHub` SignalR hub.
4. The React SPA receives real-time events via `@microsoft/signalr` and updates the Redux store.
5. REST endpoints serve historical data from MongoDB and user/account data from PostgreSQL.
6. Telegram linking: the user requests a link ID via the API; the TelegramNotifierApp service handles the Telegram-side confirmation and writes the result back to PostgreSQL.

---

## Project Responsibilities

### ArbiScannerWeb.Domain

Pure domain layer. Contains entity models, DTOs, and enumerations. Has no external package dependencies.

Notable types:
- `SpreadType` enum — `Futures`, `Funding`, `Spot`
- `TradeOpportunityModel` — core spread entity (stored in MongoDB)
- `TradeOpportunityTickerModel` — historical ticker entry
- `TelegramLinkRequest` — state for Telegram account linking
- All request/response DTOs (account, spread, subscription, payment)

### ArbiScannerWeb.Abstractions

Defines the contracts (interfaces) consumed by the API and implemented by Infrastructure. References only the Domain project.

Key interfaces:
- `IRabbitMqService` — RabbitMQ consumer lifecycle
- `IRealtimeNotifier` — push notifications to SignalR clients
- `IAccountService`, `IEmailService`, `ISubscriptionService`, `IAdminService`, `IUserSettingsService`
- `ITradeOpportunityRepository`, `ITradeOpportunityTickerRepository` — MongoDB repositories
- `IAccountRepository`, `IExchangeLinkRepository` — PostgreSQL repositories

Package dependency: `FluentResults` (result-type pattern used throughout services).

### ArbiScannerWeb.Infrastructure

Concrete implementations of all abstractions. Owns all external service integrations.

Responsibilities:
- **EF Core DbContext** (`AppDbContext`) with PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` 10 — user accounts, ASP.NET Core Identity, refresh tokens, Telegram link state, email confirmation codes, subscriptions
- **ASP.NET Core Identity + JWT** — authentication and authorization
- **MongoDB repositories** (`TradeOpportunityRepositoryMongo`, `TradeOpportunityTickerRepositoryMongo`) via `MongoDB.Driver` 3.x
- **Redis cache** via `StackExchange.Redis` 2.10
- **RabbitMQ consumer** (`RabbitMqService`) via `RabbitMQ.Client` 7 — binds to `spread_fanout_exchange`, queue `spread_api`
- **Email service** — SMTP via Gmail
- `MessageProcessingService` — deserializes incoming RabbitMQ messages and orchestrates MongoDB writes + SignalR push

### ArbiScannerWeb.API

ASP.NET Core 10 Web API host. Wires up Infrastructure services via `StartupSetup.cs`, exposes REST endpoints, hosts the SignalR hub, and serves API documentation.

Controllers:
- `AccountController` — registration, login, token refresh, email confirmation, password reset, Telegram link, profile management
- `TradeOpportunityController` — current spreads, historical tickers, spread detail
- `SubscriptionController` — subscription plans and payment flows
- `ClientLogController` — frontend error logging endpoint

Other:
- `TradeOpportunityHub` — SignalR hub for real-time spread push
- `SignalRService` — implements `IRealtimeNotifier`, calls hub methods
- `ExceptionHandlingMiddleware` — global error handling
- `ResultFailLoggingFilter` — logs FluentResults failures
- Swagger/Scalar API documentation at `/scalar`
- Serilog structured logging, shipped to Grafana Loki

### ArbiScannerWeb.Client

React 19 + Vite + TypeScript single-page application. Built with Vite and served in production behind an nginx reverse proxy.

Pages:
- `SpreadsPage` — real-time spread table with filtering
- `SpreadPage` — individual spread detail with live chart and order book
- `AccountPage` — profile, email change, password change, Telegram linking
- `LoginPage`, `RegisterPage`, `ConfirmEmailPage`, `ForgotPasswordPage`, `ResetPasswordPage`
- `SubscriptionPage` — plans; `PaymentInfoPage`, `PaymentCryptoPage`, `PaymentSuccessPage`
- `FaqPage` — FAQ accordion

State management: Redux Toolkit with RTK Query (services: `account`, `spread`, `subscription`). SignalR connection is managed by `signalrService.ts`. Token refresh coordination is handled by `refreshCoordinator.ts` to prevent concurrent refresh races.

---

## Technologies

### Backend

| Concern | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 |
| ORM | EF Core 10 + Npgsql 10 (PostgreSQL) |
| Identity | ASP.NET Core Identity + JWT Bearer |
| Document store | MongoDB.Driver 3.x |
| Cache | StackExchange.Redis 2.10 |
| Message broker | RabbitMQ.Client 7 |
| Real-time | ASP.NET Core SignalR |
| Logging | Serilog → Grafana Loki |
| API docs | Scalar / OpenAPI |
| Serialization | Google.Protobuf, protobuf-net |
| Result type | FluentResults 4 |
| Exchange data | ccxt 4.5 |

### Frontend

| Concern | Technology |
|---|---|
| Framework | React 19 + TypeScript |
| Build tool | Vite 6 |
| UI libraries | HeroUI, MUI 7, Tailwind CSS 4 |
| State | Redux Toolkit 2 + RTK Query |
| Charts | ApexCharts 4 / react-apexcharts |
| Real-time | @microsoft/signalr 8 |
| HTTP | axios 1 |
| Routing | react-router 7 |
| Animations | framer-motion 12 |

### Backend Observability

| Package | Purpose |
|---|---|
| `OpenTelemetry.Extensions.Hosting` | OTel SDK host integration |
| `OpenTelemetry.Instrumentation.AspNetCore` | ASP.NET Core request tracing |
| `OpenTelemetry.Instrumentation.Http` | Outbound HTTP client tracing |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | EF Core query tracing |
| `OpenTelemetry.Instrumentation.StackExchangeRedis` | Redis command tracing |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP gRPC export to Tempo |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | `/metrics` endpoint for Prometheus |
| `Serilog.Enrichers.Span` | Enriches every log event with `TraceId` and `SpanId` |

### Infrastructure / Observability

| Service | Version |
|---|---|
| PostgreSQL | 17 |
| MongoDB | latest |
| Redis | 7 |
| RabbitMQ | 3 (management plugin) |
| Grafana Loki | latest |
| Grafana Tempo | latest |
| Prometheus | latest |
| Grafana | latest |
| nginx | alpine |

---

## Prerequisites

Ensure the following are installed and running before starting local development:

- .NET 10 SDK
- Node.js 20 and npm
- PostgreSQL (default port 5432)
- MongoDB (default port 27017)
- Redis (default port 6379)
- RabbitMQ with the management plugin (AMQP port 5672, management UI port 15672)

You can start all infrastructure services using the provided `docker-compose.yml` (see [Docker](#docker)).

---

## Running Locally

### 1. Start infrastructure services

From the `ArbiScannerWebApp` directory:

```bash
docker compose up postgres rabbitmq redis loki -d
```

This starts PostgreSQL, RabbitMQ, Redis, and Loki without building the application images.

### 2. Apply database migrations

```bash
cd ArbiScannerWeb.API
dotnet ef database update
```

This applies all EF Core migrations to the PostgreSQL database configured in `appsettings.json`.

### 3. Start the API

```bash
cd ArbiScannerWeb.API
dotnet run
```

The API listens on `https://localhost:7xxx` / `http://localhost:5xxx` (see `Properties/launchSettings.json`). The SPA proxy URL is `https://localhost:12321`.

API documentation is available at `https://localhost:<port>/scalar`.

### 4. Start the React client

In a separate terminal:

```bash
cd ArbiScannerWeb.Client
npm install
npm run dev
```

The Vite dev server starts at `https://localhost:12321`. It proxies `/api/` and `/hubs/` to the running API process.

### 5. Open the application

Navigate to `https://localhost:12321` in your browser.

---

## Environment Variables

### API (`appsettings.json`)

All settings below can be overridden via environment variables using the `__` double-underscore separator (e.g., `ConnectionStrings__SqlServer`).

```json
{
  "ConnectionStrings": {
    "SqlServer": "Host=localhost;Port=5432;Database=ArbiScannerBot;Username=postgres;Password=<password>"
  },
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "ArbiScannerWebApp",
    "CurrentSpreadsCollection": "CurrentSpreads",
    "SpreadsTickerCollection": "SpreadsTicker"
  },
  "RabbitMq": {
    "Host": "localhost",
    "Queue": "spread_api",
    "Username": "guest",
    "Password": "guest",
    "Exchange": "spread_fanout_exchange",
    "RoutingKey": ""
  },
  "Redis": {
    "Endpoint": "localhost:6379"
  },
  "OpenTelemetry": {
    "Endpoint": "http://localhost:4317"
  },
  "Jwt": {
    "SigningKey": "<min-32-char-secret>",
    "Issuer": "<issuer>",
    "Audience": "<audience>",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Cors": {
    "AllowedOrigins": ["https://localhost:12321"]
  },
  "AdminApiUrl": "http://localhost:5046",
  "ClientUrl": "https://localhost:12321",
  "AdminUser": {
    "UserName": "manager",
    "Password": "REDACTED"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "<sender@example.com>",
    "SenderPassword": "<app-password>",
    "SenderName": "ArbiScanner"
  }
}
```

**Security note:** Never commit real credentials. Use `dotnet user-secrets`, environment variables, or a secrets manager in production.

### React client

| Variable | Description | Default |
|---|---|---|
| `VITE_API_URL` | Base URL for the API. Set to an empty string when running behind the nginx reverse proxy in production, or to the API origin during development if not using the Vite proxy. | `""` |

Set via a `.env` file in `ArbiScannerWeb.Client/`:

```
VITE_API_URL=
```

Or pass as a Docker build argument (see [Docker](#docker)).

---

## Database Migrations

Migrations are managed with EF Core and live in `ArbiScannerWeb.Infrastructure/Migrations/`.

**Apply existing migrations:**

```bash
cd ArbiScannerWeb.API
dotnet ef database update
```

**Create a new migration** (after modifying `AppDbContext` or entity models):

```bash
cd ArbiScannerWeb.API
dotnet ef migrations add <MigrationName> --project ../ArbiScannerWeb.Infrastructure
```

**Remove the last migration** (if not yet applied):

```bash
cd ArbiScannerWeb.API
dotnet ef migrations remove --project ../ArbiScannerWeb.Infrastructure
```

---

## Docker

### Build context

The API Dockerfile requires the **repository root** (the `Projects/ArbiScanner` parent directory) as the Docker build context because it copies sibling projects. The client Dockerfile uses `ArbiScannerWebApp/` as its build context.

### Build images manually

```bash
# From the ArbiScanner repo root (Projects/ArbiScanner/)
docker build -f ArbiScannerWebApp/Dockerfile -t arbiscanner-web .

# Client image — from the ArbiScannerWebApp/ directory
docker build -f Dockerfile.client -t arbiscanner-client .
```

Pass `VITE_API_URL` as a build argument if the client needs to call an explicit API URL:

```bash
docker build -f Dockerfile.client --build-arg VITE_API_URL=https://api.example.com -t arbiscanner-client .
```

### Run the full stack with Docker Compose

From the **ArbiScannerWebApp** directory:

```bash
docker compose up --build
```

This starts:

| Container | Port(s) | Description |
|---|---|---|
| `arbiscanner-postgres` | 5432 | PostgreSQL database |
| `arbiscanner-rabbitmq` | 5672, 15672 | RabbitMQ + management UI |
| `arbiscanner-redis` | 6379 | Redis cache |
| `arbiscanner-web` | 8080 | .NET API (+ `/metrics` for Prometheus) |
| `arbiscanner-client` | 3001 | React SPA (nginx) |
| `arbiscanner-loki` | 3100 | Log aggregation |
| `arbiscanner-tempo` | 3200, 4317, 4318 | Distributed trace storage |
| `arbiscanner-prometheus` | 9090 | Metrics storage |
| `arbiscanner-grafana` | 3000 | Grafana dashboards |

After startup, open `http://localhost:3001` in your browser. Grafana is available at `http://localhost:3000` (default credentials: `admin` / `admin`). RabbitMQ management UI is at `http://localhost:15672`.

### nginx reverse proxy

In the production Docker setup the React app is served by nginx. The `nginx.conf` configuration:

- Proxies `/api/*` to the `web` container on port 8080
- Proxies `/hubs/*` to the `web` container with WebSocket upgrade headers (`Connection: upgrade`) and a 3600-second read timeout for persistent SignalR connections
- Falls back to `index.html` for all other paths to support client-side routing

---

## Project Structure

```
ArbiScannerWebApp/
├── ArbiScannerWeb.Domain/              # Domain models, DTOs, enums (SpreadType, etc.)
│   └── Models/
│       ├── DTOs/                       # Request/response data transfer objects
│       └── *.cs                        # Entity and value models
│
├── ArbiScannerWeb.Abstractions/        # Service and repository interfaces
│   └── Interfaces/
│       ├── Repositories/               # ITradeOpportunityRepository, etc.
│       └── *.cs                        # IAccountService, IRabbitMqService, etc.
│
├── ArbiScannerWeb.Infrastructure/      # Concrete implementations
│   ├── DbContext/                      # AppDbContext (EF Core + PostgreSQL)
│   ├── EntityConfigurations/           # EF model configuration
│   ├── Migrations/                     # EF Core migration files
│   ├── Repositories/                   # MongoDB and PostgreSQL repositories
│   ├── Services/                       # RabbitMqService, AccountService, EmailService, etc.
│   ├── Settings/                       # JwtOptions, MongoDbSettings, RabbitMqConfiguration
│   └── StartupSetup.cs                 # DI registration extension method
│
├── ArbiScannerWeb.API/                 # ASP.NET Core 10 Web API host
│   ├── Controllers/                    # AccountController, TradeOpportunityController, etc.
│   ├── Hubs/                           # TradeOpportunityHub (SignalR)
│   ├── Middleware/                     # ExceptionHandlingMiddleware
│   ├── Filters/                        # ResultFailLoggingFilter
│   ├── Services/                       # SignalRService (IRealtimeNotifier)
│   ├── Extensions/                     # ResultExtensions
│   ├── Program.cs                      # Application entry point
│   └── appsettings.json                # Configuration file
│
├── ArbiScannerWeb.Client/              # React 19 + Vite + TypeScript SPA
│   ├── src/
│   │   ├── components/                 # Shared UI components (NavBar, Footer, modals)
│   │   ├── hooks/                      # Custom React hooks
│   │   ├── pages/                      # Page components organized by feature
│   │   │   ├── Account/                # Login, Register, Profile, email/password flows
│   │   │   ├── Spread/                 # SpreadsPage, SpreadPage, real-time chart
│   │   │   ├── Subscription/           # Plans, payment pages
│   │   │   └── Faq/                    # FAQ page
│   │   ├── services/                   # authTokenService, signalrService, refreshCoordinator
│   │   ├── store/                      # Redux store, RTK Query services, slices
│   │   ├── types/                      # TypeScript type definitions
│   │   └── utils/                      # Chart, spread, and validation utilities
│   ├── vite.config.ts                  # Dev Vite config (SPA proxy to API)
│   └── vite.config.prod.ts             # Production Vite config
│
├── ArbiScannerWebApp.sln               # Visual Studio solution file
├── Dockerfile                          # API multi-stage build (.NET 10 SDK + Node 20)
├── Dockerfile.client                   # React SPA build → nginx alpine
├── docker-compose.yml                  # Full stack compose (infra + app + monitoring)
├── nginx.conf                          # nginx config for SPA + reverse proxy
└── grafana/
    └── provisioning/
        └── datasources/
            ├── loki.yaml               # Grafana Loki datasource (uid: loki)
            ├── tempo.yaml              # Grafana Tempo datasource (trace-to-log, service map)
            └── prometheus.yaml         # Grafana Prometheus datasource (uid: prometheus)
```
