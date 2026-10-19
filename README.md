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
- [Live Spread Updates (SSE)](#live-spread-updates-sse)
- [Internationalization](#internationalization)
- [Database Migrations](#database-migrations)
- [CI/CD](#cicd)
- [Testing](#testing)
- [Docker](#docker)
- [Project Structure](#project-structure)

---

## Overview

ArbiScannerWebApp is a full-stack application composed of an ASP.NET Core 10 REST API with a React 19 + Vite SPA. The backend consumes spread events from Kafka, persists them to MongoDB, and streams updates to connected browser clients in real time over Server-Sent Events (SSE). User accounts, identity, refresh tokens, and Telegram link state are stored in PostgreSQL. Redis provides caching.

Key user-facing features:

- Live spread detail page updated in real time via SSE
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
        | HTTP (REST + SSE)
        v
ArbiScannerWeb.API  ──>  ArbiScannerWeb.Infrastructure  ──>  ArbiScannerWeb.Abstractions  ──>  ArbiScannerWeb.Domain
```

**Data flows:**

1. The ArbitrageScanner engine publishes every `Open`, `Update` and `Close` of a spread to the Kafka `spreads` topic, keyed by the spread's stable identity so a spread's events stay in order on one partition.
2. `KafkaConsumerService` (project `ArbiScannerWeb.Messaging.Kafka`) consumes the topic in group `webapp-spreads` and hands each message to `TradeOpportunityKafkaHandler`, which de-duplicates by `message-id` in Redis and applies the event through `ITradeOpportunityService`. The offset is committed only *after* the handler has finished; a failure is retried and then dead-lettered to `spreads.dlq` — see [Message Processing Correctness](#message-processing-correctness).
3. After persistence, `IRealtimeNotifier` (implemented by `SseNotifier`) publishes the new ticker, or the close, to the in-process `ISpreadStreamBroker`, which fans it out to every open stream of that spread — see [Live Spread Updates](#live-spread-updates-sse).
4. The spread page's `spreadStreamService` receives the events from `GET /api/TradeOpportunity/Stream/{id}` and appends them to the chart.
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
- `IIdempotencyStore` (`Messaging/`) — two-state duplicate suppression shared by every spread consumer
- `IRealtimeNotifier` — transport-agnostic push of a spread update to its live clients
- `ISpreadStreamBroker`, `ISpreadStreamService` — per-spread fan-out and the SSE stream itself
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
- **Kafka consumer** — lives in the separate `ArbiScannerWeb.Messaging.Kafka` project (so `ArbiScannerAdminPannel`, which references Infrastructure, never pulls in the Kafka client); Infrastructure holds the pieces every consumer shares: `RedisIdempotencyStore`, the `SpreadKey` / `SpreadEventKeys` key helpers and the `TradeOpportunitySerializer` protobuf contract
- **Email service** — SMTP via Gmail
- `TradeOpportunityKafkaHandler` (`ArbiScannerWeb.Messaging.Kafka`) — decodes incoming Kafka messages and applies them through `ITradeOpportunityService` (MongoDB writes + push to the open SSE streams)
- **Error handling** (`Extensions/ErrorCodes.cs`, `Extensions/TypedErrors.cs`, `Filters/ResultStatusCodeFilter.cs`, `Middleware/ExceptionHandlingMiddleware.cs`) — see [Error Handling](#error-handling)

### ArbiScannerWeb.API

ASP.NET Core 10 Web API host. Wires up Infrastructure services via `StartupSetup.cs`, exposes REST endpoints and the SSE spread stream, and serves API documentation.

Controllers:
- `AccountController` — registration, login, token refresh, email confirmation, password reset, Telegram link, profile management
- `TradeOpportunityController` — current spreads, historical tickers, spread detail
- `SubscriptionController` — subscription plans and payment flows
- `ClientLogController` — frontend error logging endpoint

Other:
- `SpreadStreamController` — `GET /api/TradeOpportunity/Stream/{id}`, the SSE stream of one spread's live tickers
- `Streaming/SseFormatter` — `text/event-stream` wire format
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

State management: Redux Toolkit with RTK Query (services: `account`, `spread`, `subscription`) plus a plain `language` slice mirroring the active i18next locale. The spread page's live stream is managed by `spreadStreamService.ts`. Token refresh coordination is handled by `refreshCoordinator.ts` to prevent concurrent refresh races.

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
| Message broker | Apache Kafka via Confluent.Kafka 2.16 |
| Real-time | Server-Sent Events (in-process `Channel<T>` fan-out) |
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
| Real-time | @microsoft/fetch-event-source 2 (spread stream); @microsoft/signalr 8 (AI chat widget only) |
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
| Apache Kafka | 3.9 (KRaft, single node) |
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
- Apache Kafka with the `spreads` topic (compose network only; `docker-compose.yml` here starts a broker and a `kafka-init` job)

You can start all infrastructure services using the provided `docker-compose.yml` (see [Docker](#docker)).

---

## Running Locally

### 1. Start infrastructure services

From the `ArbiScannerWebApp` directory:

```bash
docker compose up postgres kafka kafka-init redis loki -d
```

This starts PostgreSQL, Kafka (and creates its topics), Redis, and Loki without building the application images.

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

The Vite dev server starts at `https://localhost:12321`. It proxies `/api/` (REST and the SSE stream) to the running API process, and `/ai-hub/` to the AI assistant.

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
  "Messaging": {
    "ServiceName": "webapp"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "ClientId": "arbiscanner-web",
    "Consumer": {
      "Topic": "spreads",
      "AutoOffsetReset": "Latest"
    }
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

The consumer is built around one rule: **an event is only considered done after its effects are durable**. (An earlier RabbitMQ version broke it — it acknowledged a message as soon as the handler was *fired*, so a message could be acked before the Mongo write it represented had started. The broker has since been replaced; see ADR-0007 in the monorepo, `docs/arc42/adr/`.)

- **Commit after processing.** `KafkaConsumerService` disables auto-commit, processes messages sequentially (so per-partition order is preserved) and commits offsets in batches (every 100 messages or 5 s) and on partition revoke. A crash re-delivers the uncommitted tail.
- **Failures are not swallowed.** `ITradeOpportunityService` returns a `Result` and never throws; the old consumer ignored it and acknowledged the message, silently losing it. `TradeOpportunityKafkaHandler` throws on a failed `Result`, so the event is retried three times with backoff and then published to `spreads.dlq` with the error, attempt count and source offset in headers. An undecodable payload, a missing `message-id` or an unknown action skips the retries. A failing message never blocks its partition.
- **Duplicates are suppressed by `message-id`.** `spread-dedupe:{service}:{message-id}` is a two-state Redis claim: `processing` for 60 s while the handler runs, then `done` for 15 min. A crash mid-handler leaves only the short `processing` claim, so the redelivery is retried instead of being mistaken for a duplicate and lost. (The residual window — a crash between the ticker insert and the `done` marker — can add one extra chart point; it cannot lose an event.)
- **Order.** The Kafka key is the spread's stable identity, never the long/short direction, so Open → Update… → Close of a spread are applied in publish order.
- **One replica.** The web API must run as a single instance: the SSE broker is in-process, so a second instance would receive only its share of partitions and push only to its own clients.
- **Health.** `/health` reports `kafka` (a metadata request) alongside Postgres, Mongo and Redis. Setting `Kafka__Consumer__Enabled=false` keeps the consumer from starting, for hosts without a broker.

Proven, not just asserted (Testcontainers): `Kafka/KafkaMessagingTests` (same-key ordering, resume from the committed offset after a restart, no duplicate side effect, poison message to the DLQ while later events flow, a two-instance rebalance), `Kafka/KafkaCutoverTests` (the real API against real Postgres, Redis, Mongo and Kafka: lifecycle applied in order, a duplicate delivery writes exactly one ticker, a poison message is dead-lettered) and `Kafka/RedisIdempotencyStoreTests` (a crash before completion is never mistaken for "done"). See the monorepo's `docs/architecture/kafka-operations.md` for topics, partition sizing and replay.

---

## Live Spread Updates (SSE)

The spread detail page shows a live chart. Its updates arrive over **Server-Sent Events** from `GET /api/TradeOpportunity/Stream/{id}` (this replaced a SignalR hub; the AI chat widget still uses SignalR, against its own service).

```
Kafka handler -> TradeOpportunityService -> IRealtimeNotifier (SseNotifier) -> ISpreadStreamBroker
                                                                                  |  one bounded drop-oldest Channel per open stream
                                                                       SpreadStreamService -> SpreadStreamController -> text/event-stream -> spreadStreamService.ts
```

**Protocol**

| | |
|---|---|
| Request | `GET /api/TradeOpportunity/Stream/{id}` with `Authorization: Bearer ...` (header only; a token in the query string is rejected). Optional `Last-Event-ID: <unix ms>` |
| `401` / `403` / `404` | not authenticated / no active subscription / unknown spread |
| First bytes | `: connected` and `retry: 3000`, flushed immediately so the client knows it is connected |
| `event: ticker` | `id:` is the ticker's Unix-millisecond time; `data:` is the camelCase ticker JSON including order books |
| `event: close` | the scanner closed the spread. No `id`. The server then ends the stream and the client does not reconnect. Connecting to an already-closed spread yields just this event |
| `: keep-alive` | comment every `KeepAliveSeconds` while idle, so proxies do not time the connection out |

**Resuming.** The page remembers the newest ticker of its REST snapshot and sends it as `Last-Event-ID` when it connects, and `fetch-event-source` resends the newest id it has seen on every reconnect. The server subscribes to the broker *first*, then replays tickers recorded after the cursor from MongoDB (oldest first, **without order books**, at most `ReplayLimit`, keeping the newest if there are more), then goes live, de-duplicating by id. Nothing recorded between the snapshot and the connect, or while the client was away, is lost.

**Lifetime and tokens.** A JWT is validated only when the connection opens, so the server ends every stream after `MaxLifetimeMinutes` (10); the client reconnects with a freshly renewed token and resumes from its cursor. A client that stops reading never slows the Kafka consumer: its channel drops the oldest events (counted in `arbiscanner.realtime.spread_stream.events_dropped`).

**Access control.** The spread id is in the route (there is no client-chosen group to join), and the active-subscription check runs on every connect, the same rule as `GetSpreadInfo`.

**Configuration** (`SpreadStream` section; env vars `SpreadStream__KeepAliveSeconds`, ...):

| Key | Default | |
|---|---|---|
| `SubscriberBufferSize` | 256 | events buffered per open stream before the oldest is dropped |
| `KeepAliveSeconds` | 15 | idle interval before a keep-alive comment |
| `MaxLifetimeMinutes` | 10 | server-side stream cap (forces a token refresh) |
| `ReplayLimit` | 500 | most missed tickers replayed on reconnect |

**Operations.** Metrics: `arbiscanner.realtime.spread_stream.active` (open streams) and `.events_dropped`. The route is exempt from the 60/min per-IP rate limit (one held-open request; reconnects would otherwise eat the budget) and from tracing. Behind nginx the stream needs `proxy_buffering off` and a long read timeout, and HTTP/2 so several tabs do not exhaust the browser's six-connections-per-origin limit on HTTP/1.1. The broker is in-process, so the API stays a single replica.

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
5. `ArbiScannerWeb.Tests` (unit), then `ArbiScannerWeb.IntegrationTests` (Testcontainers — real Postgres/Mongo/Kafka/Redis containers), both with coverage collection feeding the SonarCloud scan.
6. `.trx` and JUnit results are published as check-run summaries via `dorny/test-reporter`.

Both SonarCloud and CodeQL are free for this public repo; SonarCloud additionally requires a `SONAR_TOKEN` secret.

### `deploy.yml` — manual deploy to the VPS

A `workflow_dispatch`-triggered workflow (optional `dry_run` boolean input) that calls the monorepo root's reusable `deploy-service.yml` (`dimasdom/SpreadScanner/.github/workflows/deploy-service.yml`, pinned to a specific commit SHA) with this repo's specifics: solution/test project paths, `has_client_tests: true` + `client_dir`, the SonarCloud exclusion list, and two image specs — `arbiscanner-web` (API, build context `.`) and `arbiscanner-web-client` (client, build context `.` with `needs_root_checkout: true` since the client image also needs the repo-root `nginx/` directory).

End to end: tests + client tests + quality gate → build and push `ghcr.io/dimasdom/arbiscanner-web(-client):latest` / `:sha-<commit>` to GHCR → (unless `dry_run: true`) SSH into the VPS and restart `web` then `web-client` via `scripts/deploy-remote.sh`. Requires `SONAR_TOKEN` plus `VPS_HOST`/`VPS_USER`/`VPS_SSH_KEY`/`VPS_SSH_PORT`/`VPS_DEPLOY_PATH` secrets on this repo.

The root monorepo also has `.github/workflows/docker-build.yml`, since this API's Dockerfile needs repo-root build context — it builds this service's images alongside the other three on every push/PR to `master`, as a build-breakage smoke check separate from this repo's own CI.

### `load-test.yml` — scheduled + on-demand load test

Runs `ArbiScannerWeb.LoadTests` separately from `ci.yml` (`workflow_dispatch` with `queries_per_minute`/`duration_seconds` inputs, plus a nightly `0 3 * * *` cron at the defaults), gated behind a `load-test` GitHub Environment holding the `WEB_LOADTEST_BASE_URL`/`_EMAIL`/`_PASSWORD` secrets — see [ArbiScannerWeb.LoadTests](#arbiscannerwebloadtests) below.

### Health checks

`/health` (Postgres, Mongo, Redis, Kafka) is exposed on the same host/port as the REST API, backed by `Microsoft.Extensions.Diagnostics.HealthChecks` and the classes in `ArbiScannerWeb.Infrastructure/HealthChecks/` — several of which (`RedisHealthCheck`, `DbContextHealthCheck<T>`, `DbContextFactoryHealthCheck<T>`) are reused by `ArbiScannerAdminPannel` and `ArbiScanner.TelegramNotifierApp` via the same project reference; the Telegram notifier also reuses `ArbiScannerWeb.Messaging.Kafka`, which holds `KafkaHealthCheck` and the consumer loop.

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
| `Infrastructure/Realtime/*` | SSE broker (fan-out, drop-oldest, concurrency), stream service (replay, de-duplication, keep-alive, lifetime, cleanup), notifier, DI |
| `API/SseFormatterTests`, `API/SpreadStreamControllerTests` | `text/event-stream` wire format; status mapping, headers, `Last-Event-ID` parsing |
| `API/SpreadStreamEndpointTests` | The real HTTP pipeline over an in-process `TestServer` (no Docker): auth, preamble, live events, close, replay, keep-alive, lifetime cap, 20 concurrent clients, a client that never reads, resume without gaps |
| `Domain/RefreshTokenModelTests`, `Domain/UserSettingsModelTests` | Domain model invariants |
| `Infrastructure/AccountServiceTests`, `SubscriptionServiceTests`, `TradeOpportunityServiceTests` | Service-layer logic against mocked repositories |

```bash
dotnet test ArbiScannerWeb.Tests/ArbiScannerWeb.Tests.csproj
```

### ArbiScannerWeb.IntegrationTests

Testcontainers-backed tests using `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory` to host the real API in-process against real Postgres, MongoDB, Kafka, and Redis containers. **Docker must be running locally.** The AdminPanel dependency is faked with WireMock.Net rather than run for real.

| File | Coverage |
|---|---|
| `Api/AccountFlowTests` | End-to-end registration/login/refresh flows against a real Postgres-backed Identity store |
| `Api/TradeOpportunityControllerTests` | Spread endpoints against real Mongo data |
| `Api/ClientLogSmokeTests` | The frontend error-logging endpoint |
| `Api/SpreadStreamEndpointTests` | The SSE stream on the full app: 401 / 404, a query-string token is rejected, live ticker then close ends the stream, replay of stored tickers without order books |
| `Repositories/TickerReplayQueryTests` | The replay query against real MongoDB: strict cursor, chronological order, newest-kept limit, per-spread isolation, order books omitted |
| `AdminPanel/AdminPanelIntegrationTests` | Subscription data sourced from a WireMock-stubbed AdminPanel API |
| `Kafka/KafkaMessagingTests` | The consumer loop against a real broker: same-key order, restart resumes from the committed offset without loss, a duplicate `message-id` has no duplicate side effect, a poison message lands in `spreads.dlq` and later events still flow, a non-retryable message is dead-lettered after one attempt, a two-instance rebalance neither loses nor repeats work |
| `Kafka/KafkaCutoverTests` | The real API with Kafka as its only spread source: Open / Update / Close applied in order to Mongo, a duplicate delivery writes exactly one ticker, a poison message is dead-lettered, live consumer group and `kafka` health check |
| `Kafka/RedisIdempotencyStoreTests` | The two-state claim: first caller wins, a crash before completion is never reported as done, completed work is done with the long TTL, an abandoned claim can be taken again |

```bash
dotnet test ArbiScannerWeb.IntegrationTests/ArbiScannerWeb.IntegrationTests.csproj
```

### ArbiScannerWeb.LoadTests

A separate, not-part-of-the-normal-test-run project (per the monorepo's CLAUDE.md) with a small hand-rolled throttled load runner (`Support/LoadRunner.cs` — a `SemaphoreSlim`-bounded request loop, not a dependency like NBomber) rather than pass/fail assertions:

- `LoadTests/AccountUpdateLoadTest`, `LoadTests/SpreadFetchLoadTest` — sustained-throughput smoke tests against a running instance of the API
- `LoadTests/SpreadStreamLoadTest` — opens spread SSE streams at the configured rate (auth, subscription check, broker subscribe, preamble) and disconnects; needs at least one open spread

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
| `arbiscanner-kafka` | — (compose network only) | Kafka broker (KRaft) |
| `arbiscanner-kafka-init` | — | One-shot topic creation (`spreads`, `spreads.dlq`) |
| `arbiscanner-redis` | 6379 | Redis cache |
| `arbiscanner-web` | 8080 | .NET API (+ `/metrics` for Prometheus) |
| `arbiscanner-client` | 3001 | React SPA (nginx) |
| `arbiscanner-loki` | 3100 | Log aggregation |
| `arbiscanner-tempo` | 3200, 4317, 4318 | Distributed trace storage |
| `arbiscanner-prometheus` | 9090 | Metrics storage |
| `arbiscanner-grafana` | 3000 | Grafana dashboards |

After startup, open `http://localhost:3001` in your browser. Grafana is available at `http://localhost:3000` (default credentials: `admin` / `admin`).

### nginx reverse proxy

In the production Docker setup the React app is served by nginx. The `nginx.conf` configuration:

- Proxies `/api/*` to the `web` container on port 8080
- Proxies `/api/TradeOpportunity/Stream/*` to the `web` container with response buffering off and a 3600-second read timeout, so the SSE stream is delivered as it is written
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
│       └── *.cs                        # IAccountService, ITradeOpportunityService, etc.
│
├── ArbiScannerWeb.Infrastructure/      # Concrete implementations
│   ├── DbContext/                      # AppDbContext (EF Core + PostgreSQL)
│   ├── Migrations/                     # EF Core migration files
│   ├── Repositories/                   # MongoDB and PostgreSQL repositories
│   ├── Services/                       # AccountService, EmailService, TradeOpportunityService, etc.
│   ├── Messaging/                      # SpreadKey, RedisIdempotencyStore, TradeOpportunitySerializer (shared by every consumer)
│   ├── HealthChecks/                   # Redis/DbContext(Factory) checks — shared with AdminPanel and TelegramNotifierApp
│   ├── Settings/                       # JwtOptions, MongoDbSettings, MessagingOptions
│   ├── Middleware/                     # ExceptionHandlingMiddleware (see Error Handling)
│   ├── Filters/                        # ResultStatusCodeFilter
│   ├── Extensions/                     # ResultExtensions, ErrorCodes, TypedErrors
│   └── StartupSetup.cs                 # DI registration extension method
│
├── ArbiScannerWeb.Messaging.Kafka/     # Kafka producer/consumer building blocks (also used by TelegramNotifierApp)
│
├── ArbiScannerWeb.API/                 # ASP.NET Core 10 Web API host
│   ├── Controllers/                    # AccountController, TradeOpportunityController, etc.
│   ├── Streaming/                      # SseFormatter (text/event-stream)
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
│   ├── Kafka/                           # Real-broker consumer and full-stack cutover tests
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
│   │   ├── services/                   # authTokenService, spreadStreamService, refreshCoordinator
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
