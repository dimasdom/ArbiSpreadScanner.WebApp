# ArbiScannerWebApp

[![Quality gate](https://sonarcloud.io/api/project_badges/quality_gate?project=dimasdom_ArbiSpreadScanner.WebApp)](https://sonarcloud.io/summary/new_code?id=dimasdom_ArbiSpreadScanner.WebApp)


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
- [Error Handling](#error-handling)
- [Message Processing Correctness](#message-processing-correctness)
- [Internationalization](#internationalization)
- [Database Migrations](#database-migrations)
- [CI/CD](#cicd)
- [Testing](#testing)
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
- Localized UI in 6 languages with language-prefixed routing (`/en/...`, `/uk/...`, etc.)

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

1. The ArbitrageScanner engine publishes spread events to the durable `spread_fanout_exchange` RabbitMQ exchange with publisher confirms.
2. `RabbitMqService` in Infrastructure consumes from the durable `spread_api` queue (dead-lettering to `spread_api_dlq` on failure), de-duplicates via a Redis `SET NX` claim keyed on `(queue, Guid, ActionType)`, and only acknowledges a message to the broker *after* `MessageProcessingService` has actually finished persisting it — see [Message Processing Correctness](#message-processing-correctness).
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
- **Error handling** (`Extensions/ErrorCodes.cs`, `Extensions/TypedErrors.cs`, `Filters/ResultStatusCodeFilter.cs`, `Middleware/ExceptionHandlingMiddleware.cs`) — see [Error Handling](#error-handling)

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
- Swagger/Scalar API documentation at `/scalar`
- Serilog structured logging, shipped to Grafana Loki

Global error handling (`ExceptionHandlingMiddleware`, `ResultStatusCodeFilter`, `TypedErrors`/`ErrorCodes`) lives in `ArbiScannerWeb.Infrastructure` rather than the API project — see [Error Handling](#error-handling).

### ArbiScannerWeb.Client

React 19 + Vite + TypeScript single-page application. Built with Vite and served in production behind an nginx reverse proxy.

Pages:
- `MainPage` — landing page; includes `LiveDemoWidget`, a self-contained simulated-data preview (fake ticker feed via `useLiveDemoWidget`) of the real-time chart and order book so visitors can see the product before signing up
- `SpreadsPage` — real-time spread table with filtering
- `SpreadPage` — individual spread detail with live chart and order book
- `AccountPage` — profile, email change, password change, Telegram linking
- `LoginPage`, `RegisterPage`, `ConfirmEmailPage`, `ForgotPasswordPage`, `ResetPasswordPage`
- `SubscriptionPage` — plans; `PaymentInfoPage`, `PaymentCryptoPage`, `PaymentSuccessPage`
- `FaqPage` — FAQ accordion

State management: Redux Toolkit with RTK Query (services: `account`, `spread`, `subscription`) plus a plain `language` slice mirroring the active i18next locale. SignalR connection is managed by `signalrService.ts`. Token refresh coordination is handled by `refreshCoordinator.ts` to prevent concurrent refresh races.

Error handling: `normalizeApiError.ts` converts any RTK Query `FetchBaseQueryError`/`SerializedError` into a `{ code, message }` shape, reading the `errorCode`/`message` fields the API's `TypedErrors` envelope always returns (see [Error Handling](#error-handling)); `ErrorState.tsx` renders that shape consistently wherever a query fails.

Enums shared with the wire format (`SpreadType`, `PositionAction`, `Exchange` in `src/types/`) are defined as `as const` objects with a derived union type rather than TypeScript `enum` — the project's `tsconfig.app.json` sets `erasableSyntaxOnly`, which rejects real `enum` declarations because they emit runtime code instead of erasing away. `SpreadType.ts` also exports `SpreadTypeNames`, a reverse `value -> key` lookup, since plain objects (unlike numeric enums) don't get that mapping for free.

Internationalization: every route is prefixed with a `:lang` segment (`LangGuard` in `src/components/LangGuard.tsx` validates it and redirects to a detected/stored language when missing or invalid); translations are lazy-loaded per-namespace JSON via `i18next-http-backend` — see [Internationalization](#internationalization).

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
| i18n | i18next 25 + react-i18next 15 + i18next-http-backend 3 |

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

## Error Handling

All service interfaces return FluentResults' `Result`/`Result<T>` rather than throwing (see the monorepo-wide layering convention). This API turns that into a single, predictable JSON error envelope for both expected failures (`Result.Fail`) and unhandled exceptions:

```json
{
  "isSuccess": false,
  "isFailed": true,
  "errorCode": "NOT_FOUND",
  "message": "Trade opportunity not found.",
  "value": null,
  "reasons": []
}
```

How it's produced:

- **`TypedErrors`** (`Infrastructure/Extensions/TypedErrors.cs`) — factory methods (`NotFound`, `Validation`, `Unauthorized`, `Forbidden`, `Conflict`) that build a FluentResults `Error` tagged with an HTTP status code and an `ErrorCode` string (from `Infrastructure/Extensions/ErrorCodes.cs`) as metadata. Services call these instead of constructing `Error` objects ad hoc, so every failure carries a status code from the point it's raised.
- **`ResultExtensions.ToSerializable()`** (`Infrastructure/Extensions/ResultExtensions.cs`) — controllers call this on the `Result`/`Result<T>` they get back from a service before returning it, flattening it into a `SerializableResult`/`SerializableResult<T>` with plain `IsSuccess`/`Value`/`ErrorCode`/`Message`/`Reasons` properties that serialize predictably (FluentResults' own `Result` type doesn't serialize cleanly to JSON).
- **`ResultStatusCodeFilter`** (`Infrastructure/Filters/ResultStatusCodeFilter.cs`) — an `IAsyncActionFilter` registered globally in `Program.cs` (`AddControllers(opts => opts.Filters.Add<ResultStatusCodeFilter>())`). After the action executes, it reflects over the returned object for `IsSuccess`/`Reasons`, and on failure reads the `HttpStatusCode` metadata off the first reason to set the actual HTTP status code on the response — so a controller can simply return `Ok(result.ToSerializable())` and still get a 404/401/403/409 as appropriate. It also logs every failed result with the request method, path, and error messages.
- **`ExceptionHandlingMiddleware`** (`Infrastructure/Middleware/ExceptionHandlingMiddleware.cs`) — registered in `Program.cs` via `app.UseMiddleware<ExceptionHandlingMiddleware>()`. Catches anything that escapes the filter/controller layer, logs it, and writes the same JSON shape with `errorCode: "INTERNAL_ERROR"` and a generic message (no internal exception details leak to the client).

On the client, `normalizeApiError.ts` reads `errorCode`/`message` off this envelope and `ErrorState.tsx` renders it consistently — see the `ArbiScannerWeb.Client` entry under [Project Responsibilities](#project-responsibilities) above.

Note that both the middleware and the filter/error-code helpers live in `ArbiScannerWeb.Infrastructure`, not the API project, so the same error contract is reusable if another host project is ever added.

---

## Message Processing Correctness

`RabbitMqService` (shared with `ArbiScanner.TelegramNotifierApp` via project reference) used to acknowledge a message to the broker as soon as the `OnMessageReceived` handler was *fired*, not once it actually finished — the handler was invoked without being awaited, and the wrapper (`MessageProcessingService`) compounded this by detaching its real work via `Task.Run` and returning an already-completed `Task`. A message could be acked before the Mongo write it represented had even started.

This is now fixed end-to-end:

- The consumer awaits the full handler chain before acking. On failure it retries (Polly, exponential backoff + jitter) before dead-lettering the message (`x-dead-letter-exchange` → `spread_api_dlq`) rather than requeuing forever.
- Before processing, a Redis `SET NX` claim keyed on `(queue, Guid, ActionType)` — not `Guid` alone, since the same event legitimately recurs across the Open/Update/Close lifecycle — de-duplicates redelivered messages. The claim is deleted if processing ultimately fails, so a later DLQ replay isn't mistaken for a stale duplicate.
- `MessageProcessingService`'s `SemaphoreSlim(30)` concurrency limiter was removed: it existed to bound concurrent Mongo writes while the broker had already (incorrectly) moved on to the next message. Now that RabbitMQ's default sequential consumer dispatch only advances after the current message is actually acked, at most one message is ever in flight — the limiter had become dead weight.
- Publisher confirms and durable queues (`spread_api`, `spread_telegram`) with a `spread_dlx` dead-letter exchange were added on the `ArbitrageScanner` publishing side to match.

Proven, not just asserted: `ArbiScannerWeb.IntegrationTests/RabbitMq/RabbitMqIdempotencyTests.cs` publishes the same event twice against a real Testcontainers broker and asserts exactly one ticker write results; `RabbitMqDeadLetterTests.cs` publishes a non-deserializable message and asserts it lands in the dead-letter queue instead of looping forever. See `docs/completed-work-summary.md` (monorepo root) for the full write-up.

---

## Internationalization

The client is localized into 6 languages via `i18next` + `react-i18next`: English (default), Spanish, German, French, Russian, and Ukrainian.

**Routing.** Every route carries a leading `:lang` segment (`/en/spreads`, `/uk/faq`, ...), enforced by `LangGuard` (`src/components/LangGuard.tsx`), which wraps all routes in `App.tsx`:
- A valid `:lang` param syncs `i18next`'s active language via `i18n.changeLanguage`.
- A missing/invalid `:lang` (including bare `/`, handled by `RootRedirect`) redirects to `/<detected-lang>/<original-path>`, preserving query string and hash.
- `useLocalizedNavigate()` (`src/i18n/routing.ts`) is a drop-in replacement for react-router's `useNavigate()` that re-prepends the current language segment on every absolute in-app navigation, so links don't need to hardcode it. `stripLangPrefix()` strips that segment back off for path comparisons (e.g. the `AnimatePresence` page-transition key in `App.tsx`).

**Language detection** (`src/i18n/detect.ts`, `getPreferredLanguage()`) resolves in order: a previously-chosen language in `localStorage` (`preferredLang`) → the browser's `navigator.languages` → hard fallback to English. This only picks the redirect target for first-visit/invalid-URL cases — once a URL has a valid `:lang` segment, that segment is the source of truth.

**Translation loading.** Translation strings live under `public/locales/<lang>/<namespace>.json` (namespaces: `common`, `main`, `account`, `spreads`, `subscription`, `faq`) and are fetched on demand by `i18next-http-backend` (`loadPath: /locales/{{lng}}/{{ns}}.json`) the first time a component calls `useTranslation('<namespace>')` — only `common` is preloaded at startup (needed by `NavBar`/`Footer` on every page), so navigating to a page doesn't pull down every namespace's JSON up front.

**State sync.** `i18n.on('languageChanged', ...)` (`src/i18n/config.ts`) is the single place that keeps the Redux `language` slice (`src/store/slices/languageSlice.ts`) and `localStorage` in sync with `i18next`, regardless of whether the change originated from `LangGuard` parsing the URL or the user picking a language in `LanguageSwitcher` (`src/components/LanguageSwitcher.tsx`, `src/i18n/languages.ts` — flag + label metadata per language code).

**Adding a language:** add an entry to `SUPPORTED_LANGUAGES` in `src/i18n/languages.ts`, then add a matching `public/locales/<code>/` directory with all six namespace JSON files.

**Adding a namespace:** create `public/locales/<lang>/<namespace>.json` for every supported language, add the namespace name to the `NAMESPACES` list in `src/i18n/config.ts` (documentation only — not passed to `init()`, to keep it lazy-loaded), and call `useTranslation('<namespace>')` in the consuming component.

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

**Notable migrations:**

| Migration | What it does |
|---|---|
| `RemoveVolatility` | Drops the long-unused legacy Postgres tables `ExchangeRates`, `CurrentSpreads`, and `SpreadsTicker`. These predate the move to MongoDB for spread/ticker storage and had been dead schema ever since; the Mongo collections of the same name are the ones actually in use (see [Architecture](#architecture)). Named for the accompanying removal of the `Volatility` field from `TradeOpportunityModel`/`ExchangeRateModel` — the 30-minute OHLCV-based volatility and "risk level" scoring it fed was cut platform-wide (ArbitrageScanner, this API, and the Telegram Notifier) as it wasn't reliable enough to keep computing on every cycle. |
| `AddUserSettingsNotificationIndexes` | Adds indexes on `UserSettings` (`AccountId`, `ChatId`, and three partial indexes on `SpreadSize` filtered by `Active` + each spread-type flag) to speed up the per-user, per-strategy criteria matching that `ArbiScanner.TelegramNotifierApp`'s `SpreadService` runs on every incoming spread event. |

---

## CI/CD

`.editorconfig` and `Directory.Build.props` enable `AnalysisLevel=latest`/`AnalysisMode=Recommended` with `TreatWarningsAsErrors`. `Directory.Build.props` documents the specific pre-existing warning rule IDs grandfathered in — nullable-safety warnings are not among them and fail the build if introduced.

This repo has its own GitHub Actions, independent of the monorepo root's Actions tab (it's a separate git remote — see the monorepo root's CI/CD section for how the two relate). Three workflows live under `.github/workflows/`:

### `ci.yml` — build, test, quality gate

Runs on every push/PR to `main`:

1. A SonarCloud scan (project `dimasdom_ArbiSpreadScanner.WebApp`) wraps everything below; `sonar.qualitygate.wait=true` fails the job on a red quality gate.
2. CodeQL initializes twice — C# (`build-mode: manual`) and JavaScript/TypeScript (`build-mode: none`, since the client isn't compiled) — then analyzes both after the build/test steps.
3. `npm ci --ignore-scripts` + `npm test -- --coverage --reporter=junit` in `ArbiScannerWeb.Client`, then the vitest lcov report's `SF:` source paths are rewritten to absolute (`sed`) so SonarCloud's coverage import resolves them correctly.
4. `dotnet restore`/`build` on `ArbiScannerWebApp.sln` with analyzers (the API references the React client as an MSBuild project, so Node/npm needs to be set up first).
5. `ArbiScannerWeb.Tests` (unit), then `ArbiScannerWeb.IntegrationTests` (Testcontainers — real Postgres/Mongo/RabbitMQ/Redis containers), both with coverage collection feeding the SonarCloud scan.
6. `.trx` and JUnit results are published as check-run summaries via `dorny/test-reporter`.

Both SonarCloud and CodeQL are free for this public repo; SonarCloud additionally requires a `SONAR_TOKEN` secret.

### `deploy.yml` — manual deploy to the VPS

A `workflow_dispatch`-triggered workflow (optional `dry_run` boolean input) that calls the monorepo root's reusable `deploy-service.yml` (`dimasdom/SpreadScanner/.github/workflows/deploy-service.yml`, pinned to a specific commit SHA) with this repo's specifics: solution/test project paths, `has_client_tests: true` + `client_dir`, the SonarCloud exclusion list, and two image specs — `arbiscanner-web` (API, build context `.`) and `arbiscanner-web-client` (client, build context `.` with `needs_root_checkout: true` since the client image also needs the repo-root `nginx/` directory).

End to end: tests + client tests + quality gate → build and push `ghcr.io/dimasdom/arbiscanner-web(-client):latest` / `:sha-<commit>` to GHCR → (unless `dry_run: true`) SSH into the VPS and restart `web` then `web-client` via `scripts/deploy-remote.sh`. Requires `SONAR_TOKEN` plus `VPS_HOST`/`VPS_USER`/`VPS_SSH_KEY`/`VPS_SSH_PORT`/`VPS_DEPLOY_PATH` secrets on this repo.

The root monorepo also has `.github/workflows/docker-build.yml`, since this API's Dockerfile needs repo-root build context — it builds this service's images alongside the other three on every push/PR to `master`, as a build-breakage smoke check separate from this repo's own CI.

### `load-test.yml` — scheduled + on-demand load test

Runs `ArbiScannerWeb.LoadTests` separately from `ci.yml` (`workflow_dispatch` with `queries_per_minute`/`duration_seconds` inputs, plus a nightly `0 3 * * *` cron at the defaults), gated behind a `load-test` GitHub Environment holding the `WEB_LOADTEST_BASE_URL`/`_EMAIL`/`_PASSWORD` secrets — see [ArbiScannerWeb.LoadTests](#arbiscannerwebloadtests) below.

### Health checks

`/health` (Postgres, Mongo, Redis, RabbitMQ) is exposed on the same host/port as the REST API, backed by `Microsoft.Extensions.Diagnostics.HealthChecks` and the classes in `ArbiScannerWeb.Infrastructure/HealthChecks/` — several of which (`RedisHealthCheck`, `RabbitMqHealthCheck`, `DbContextHealthCheck<T>`, `DbContextFactoryHealthCheck<T>`) are reused by `ArbiScannerAdminPannel` and `ArbiScanner.TelegramNotifierApp` via the same project reference that shares `RabbitMqService`.

---

## Testing

Three .NET test projects and a client-side Vitest suite were added alongside the error-handling and test-coverage push described above. None of the three .NET suites are required for `dotnet build` (they're excluded from the "just build" flow only in the sense that a slow/no-Docker environment can skip `IntegrationTests`/`LoadTests`), but all three are part of `ArbiScannerWebApp.sln` and run with `dotnet test`.

### ArbiScannerWeb.Tests (unit)

Fast, no-I/O unit tests (xUnit + FluentAssertions + Moq):

| File | Coverage |
|---|---|
| `API/AccountControllerTests` | Controller behavior against mocked `IAccountService` |
| `API/ExceptionHandlingMiddlewareTests` | Middleware produces the expected JSON envelope for thrown exceptions |
| `API/ResultStatusCodeFilterTests` | Filter maps `Result` failures to the correct HTTP status code from `TypedErrors` metadata |
| `Domain/RefreshTokenModelTests`, `Domain/UserSettingsModelTests` | Domain model invariants |
| `Infrastructure/AccountServiceTests`, `SubscriptionServiceTests`, `TradeOpportunityServiceTests` | Service-layer logic against mocked repositories |

```bash
dotnet test ArbiScannerWeb.Tests/ArbiScannerWeb.Tests.csproj
```

### ArbiScannerWeb.IntegrationTests

Testcontainers-backed tests using `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory` to host the real API in-process against real Postgres, MongoDB, RabbitMQ, and Redis containers. **Docker must be running locally.** The AdminPanel dependency is faked with WireMock.Net rather than run for real.

| File | Coverage |
|---|---|
| `Api/AccountFlowTests` | End-to-end registration/login/refresh flows against a real Postgres-backed Identity store |
| `Api/TradeOpportunityControllerTests` | Spread endpoints against real Mongo data |
| `Api/ClientLogSmokeTests` | The frontend error-logging endpoint |
| `AdminPanel/AdminPanelIntegrationTests` | Subscription data sourced from a WireMock-stubbed AdminPanel API |
| `RabbitMq/RabbitMqIntegrationTests` | `RabbitMqService` consuming real messages from a `Testcontainers.RabbitMq` broker |
| `RabbitMq/RabbitMqIdempotencyTests` | Publishing the same event twice results in exactly one ticker write — proves the Redis dedupe claim actually prevents reprocessing, not just that it compiles |
| `RabbitMq/RabbitMqDeadLetterTests` | A non-deserializable ("poison") message lands in `spread_api_dlq` instead of being nacked-and-requeued forever |

```bash
dotnet test ArbiScannerWeb.IntegrationTests/ArbiScannerWeb.IntegrationTests.csproj
```

### ArbiScannerWeb.LoadTests

A separate, not-part-of-the-normal-test-run project (per the monorepo's CLAUDE.md) with a small hand-rolled throttled load runner (`Support/LoadRunner.cs` — a `SemaphoreSlim`-bounded request loop, not a dependency like NBomber) rather than pass/fail assertions:

- `LoadTests/AccountUpdateLoadTest`, `LoadTests/SpreadFetchLoadTest` — sustained-throughput smoke tests against a running instance of the API

Uses `Xunit.SkippableFact` so these can be skipped by default and only run explicitly against an environment configured for load testing, via these environment variables:

| Variable | Purpose |
|---|---|
| `WEB_LOADTEST_BASE_URL` | Target instance, e.g. `https://www.arbiscannerwebapp.site` (no trailing slash/path) |
| `WEB_LOADTEST_EMAIL` / `WEB_LOADTEST_PASSWORD` | A real, email-confirmed account on that instance |
| `WEB_LOADTEST_QUERIES_PER_MINUTE` | Target rate per endpoint (default `60`) |
| `WEB_LOADTEST_DURATION_SECONDS` | Sustained duration (default `60`) |

`LoadRunner` paces strictly off queries-per-minute — one request every `60 / QueriesPerMinute` seconds — rather than a per-second batch with a 1-req/sec floor, since the latter silently sent 6x the configured rate for any QPM under 60 and tripped the target's rate limiter. `xunit.runner.json` disables collection parallelism so the two tests run sequentially instead of racing on the same login session (both authenticate as the same account; concurrent logins were invalidating each other's cookie mid-run).

### Client-side (Vitest)

```bash
cd ArbiScannerWeb.Client
npm run test           # vitest run
npm run test:coverage  # vitest run --coverage (v8 provider)
```

Configuration in `vitest.config.ts` (jsdom environment, `src/test/setup.ts` for global setup). Coverage:

| File | Coverage |
|---|---|
| `components/ErrorBoundary.test.tsx` | React error boundary fallback rendering |
| `components/ProtectedRoute.test.tsx` | Auth-gated routing redirects |
| `hooks/useIsMobile.test.ts` | Responsive breakpoint hook across `resize` events |
| `store/slices/accountSlice.test.ts` | Redux slice reducers/selectors |
| `utils/chartUtils.test.ts`, `utils/spreadUtils.test.ts`, `utils/validationUtils.test.ts` | Chart data shaping, spread math, and form validation helpers |

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
│   ├── Migrations/                     # EF Core migration files
│   ├── Repositories/                   # MongoDB and PostgreSQL repositories
│   ├── Services/                       # RabbitMqService, AccountService, EmailService, etc.
│   ├── HealthChecks/                   # Redis/RabbitMQ/DbContext(Factory) checks — shared with AdminPanel and TelegramNotifierApp
│   ├── Settings/                       # JwtOptions, MongoDbSettings, RabbitMqConfiguration
│   ├── Middleware/                     # ExceptionHandlingMiddleware (see Error Handling)
│   ├── Filters/                        # ResultStatusCodeFilter
│   ├── Extensions/                     # ResultExtensions, ErrorCodes, TypedErrors
│   └── StartupSetup.cs                 # DI registration extension method
│
├── ArbiScannerWeb.API/                 # ASP.NET Core 10 Web API host
│   ├── Controllers/                    # AccountController, TradeOpportunityController, etc.
│   ├── Hubs/                           # TradeOpportunityHub (SignalR)
│   ├── Services/                       # SignalRService (IRealtimeNotifier)
│   ├── Program.cs                      # Application entry point
│   ├── appsettings.json                # Configuration file
│   └── CHANGELOG.md                    # Visual Studio project-scaffolding log
│
├── ArbiScannerWeb.Tests/                # Unit tests (xUnit + FluentAssertions + Moq) — see Testing
│   ├── API/                             # Controller, middleware, filter tests
│   ├── Domain/                          # Domain model tests
│   └── Infrastructure/                  # Service-layer tests against mocked repositories
│
├── ArbiScannerWeb.IntegrationTests/     # Testcontainers-backed API tests — see Testing
│   ├── Api/                             # Account/spread/client-log flow tests
│   ├── AdminPanel/                      # WireMock-stubbed AdminPanel dependency tests
│   ├── RabbitMq/                        # Real-broker consumer tests
│   ├── Fixtures/                        # WebApiTestFixture, AdminPanelTestFixture, etc.
│   └── Support/                         # CustomWebApplicationFactory and helpers
│
├── ArbiScannerWeb.LoadTests/             # Throttled load tests (not part of the normal test run)
│   ├── LoadTests/                       # Account update / spread fetch load scenarios
│   ├── Settings/
│   └── Support/                         # Hand-rolled LoadRunner
│
├── ArbiScannerWeb.Client/              # React 19 + Vite + TypeScript SPA
│   ├── public/
│   │   └── locales/                    # Per-language translation JSON (en, es, de, fr, ru, uk) — see Internationalization
│   │       └── <lang>/                 # common, main, account, spreads, subscription, faq namespaces
│   ├── src/
│   │   ├── components/                 # Shared UI components (NavBar, Footer, ErrorState, modals, LangGuard, LanguageSwitcher)
│   │   ├── hooks/                      # Custom React hooks
│   │   ├── i18n/                       # i18next config, language metadata, detection, localized routing helpers
│   │   ├── pages/                      # Page components organized by feature
│   │   │   ├── Main/                   # MainPage, LiveDemoWidget (simulated-data preview)
│   │   │   ├── Account/                # Login, Register, Profile, email/password flows
│   │   │   ├── Spread/                 # SpreadsPage, SpreadPage, real-time chart
│   │   │   ├── Subscription/           # Plans, payment pages
│   │   │   └── Faq/                    # FAQ page
│   │   ├── services/                   # authTokenService, signalrService, refreshCoordinator
│   │   ├── store/                      # Redux store, RTK Query services, slices
│   │   ├── test/                       # Vitest global setup
│   │   ├── types/                      # TypeScript type definitions (ApiError, SpreadType, etc.)
│   │   └── utils/                      # Chart, spread, validation utilities, normalizeApiError
│   ├── vite.config.ts                  # Dev Vite config (SPA proxy to API)
│   ├── vite.config.prod.ts             # Production Vite config
│   ├── vitest.config.ts                # Vitest config (jsdom, coverage)
│   └── CHANGELOG.md / ARCHITECTURE_REVIEW.md  # Scaffolding log / prior architecture review notes
│
├── ArbiScannerWebApp.sln               # Visual Studio solution file
├── Dockerfile                          # API multi-stage build (.NET 10 SDK + Node 20)
├── Dockerfile.client                   # React SPA build → nginx alpine
├── docker-compose.yml                  # Full stack compose (infra + app + monitoring)
├── nginx.conf                          # nginx config for SPA + reverse proxy
└── grafana/
    └── provisioning/
        ├── datasources/
        │   ├── loki.yaml               # Grafana Loki datasource (uid: loki)
        │   ├── tempo.yaml              # Grafana Tempo datasource (trace-to-log, service map)
        │   └── prometheus.yaml         # Grafana Prometheus datasource (uid: prometheus)
        └── dashboards/                 # Dashboard provisioning directory (empty; add JSON here)
```
