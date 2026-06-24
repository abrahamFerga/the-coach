# The Coach — Architecture

## Context (C4 L1)

**Actors:** Trainer/Head Coach, Client/Athlete, Operator (gym/studio admin), Member (self-coached individual), Stripe, Firebase Cloud Messaging, Azure Entra ID, Azure OpenAI Service, Open Food Facts API, Azure Blob Storage.

**The system** is a multi-tenant SaaS platform that unifies workout delivery, nutrition tracking, compliance monitoring, messaging, billing, AI-assisted programming, and athlete performance analytics.

Diagram: `docs/diagrams/c1-context.puml`

## Containers (C4 L2)

| Container | Technology | Role |
|---|---|---|
| `TheCoach.Web` | Vite + React 19 + TypeScript + shadcn/ui + Tailwind CSS | Trainer console (desktop-first) + client PWA (mobile). Feature folders per bounded context. Chatbot slide-over panel. |
| `TheCoach.Api` | .NET 10 minimal APIs + ASP.NET Core | REST API versioned at `/api/v1/`. Grouped by bounded context. Problem Details errors. OIDC auth + RBAC policies. |
| `TheCoach.Workers` | .NET 10 `IHostedService` + Quartz.NET | Scheduled jobs + reactive workers via Redis pub/sub. All external side effects go through outbox. |
| PostgreSQL 16 | Azure Database for PostgreSQL Flexible Server | Primary operational store. Shared schema; EF Core global query filters enforce `TenantId`. Separate `audit` schema for audit log. |
| Redis | Azure Cache for Redis (Basic C1) | Session state, idempotency replay, rate-limit windows, pub/sub message bus, outbox lock. |
| Azure Blob Storage | Azure Storage Account | Progress photos, voice notes. Encrypted at rest. SAS tokens for scoped read access. |

Diagram: `docs/diagrams/c2-containers.puml`

## Components (C4 L3) — key containers only

### TheCoach.Api internals

The API container is a single ASP.NET Core minimal-API host. Endpoint groups are registered by bounded context:

| Endpoint group | Routes | Application layer |
|---|---|---|
| Coaching | `/api/v1/programs`, `/api/v1/exercises`, `/api/v1/workout-logs`, `/api/v1/compliance` | `Application.Coaching` |
| HealthTracking | `/api/v1/nutrition`, `/api/v1/metrics`, `/api/v1/check-ins` | `Application.HealthTracking` |
| Messaging | `/api/v1/conversations`, `/api/v1/messages`, `/api/v1/push` | `Application.Messaging` |
| Billing | `/api/v1/billing`, `/api/v1/webhooks/stripe` | `Application.Billing` |
| Intelligence | `/api/v1/intelligence`, `/api/v1/chat` | `Application.Intelligence` + MAF agent |
| Performance | `/api/v1/performance` | `Application.Performance` |

Diagram: `docs/diagrams/c3-components-api.puml`

## Solution layout

```
src/
  TheCoach.AppHost/                    ← Aspire AppHost; declares all resources + projects
  TheCoach.ServiceDefaults/            ← OTel, health checks, Polly resilience defaults
  TheCoach.Api/                        ← Minimal-API host; endpoint groups + DI wiring
  TheCoach.Domain/                     ← Entities, value objects, domain events, enums
  TheCoach.Application/                ← Shared interfaces (ITenantContext, IAuditLogger, IOutbox)
  TheCoach.Application.Coaching/       ← Epic 2: programs, exercises, compliance
  TheCoach.Application.HealthTracking/ ← Epic 3: nutrition, metrics, check-ins
  TheCoach.Application.Messaging/      ← Epic 4: conversations, messages, push registration
  TheCoach.Application.Billing/        ← Epic 5: subscriptions, Stripe, dunning
  TheCoach.Application.Intelligence/   ← Epic 6: AI generation, automations, CoachingAssistant agent
  TheCoach.Application.Performance/    ← Epic 7: training load, readiness, drills, periodization
  TheCoach.Infrastructure/             ← EF Core DbContext, outbox, audit, PII handling, migrations
  TheCoach.Infrastructure.Azure/       ← Azure-specific: Key Vault, Blob Storage, OpenAI, FCM client
  TheCoach.Web/                        ← Vite SPA (React 19 + TypeScript)

tests/
  TheCoach.Api.Tests/                  ← Integration tests (Aspire TestingBuilder + Testcontainers)
  TheCoach.Application.Coaching.Tests/
  TheCoach.Application.HealthTracking.Tests/
  TheCoach.Application.Messaging.Tests/
  TheCoach.Application.Billing.Tests/
  TheCoach.Application.Intelligence.Tests/
  TheCoach.Application.Performance.Tests/

infra/                                 ← Terraform (Azure provider)
.github/workflows/                     ← CI (build + test) + CD (Terraform apply + Container Apps deploy)
```

## Cross-cutting wiring

- **AuthN**: Azure Entra ID via OIDC. Single multi-tenant app registration (`common` authority). `TenantId` resolved from a custom `coach_tenant_id` claim injected during token issuance (App Role assignment). `TenantResolutionMiddleware` populates `ITenantContext` from the claim before any handler runs.
- **RBAC**: ASP.NET Core `AuthorizationOptions`; policy names from PLAN (`Programs.Create`, `Compliance.ViewOwn`, etc.) registered in `Application.Foundations`; role-to-policy mapping in `appsettings.{env}.json` (never hardcoded); policy handlers check both the claim role and the `TenantId` boundary.
- **Multi-tenancy**: Shared Postgres schema. `TenantId` (UUID) column on every domain entity. EF Core `IModelCustomizer` applies a global query filter `e => e.TenantId == _tenantContext.TenantId` on all `ITenantScoped` entities. `SystemAdmin` bypasses the filter via `IgnoreQueryFilters()`.
- **Observability**: OTel SDK via Aspire `ServiceDefaults.AddServiceDefaults()`; traces + metrics exported to Azure Monitor (OTLP exporter); structured logs to Application Insights; `/health` (deep) and `/health/live` (liveness) on every container. Append-only audit log: `AuditEntry { TenantId, UserId, Action, EntityType, EntityId, Before, After, Timestamp }` written to `audit.audit_entries` table outside the main EF Core `SaveChanges` pipeline via `IAuditLogger`.
- **Resilience**: Polly `ResiliencePipeline` on all outbound HTTP clients (Stripe, FCM, Azure OpenAI, Open Food Facts): 3 retries with jitter, circuit breaker (50% failure in 30 s window, 30 s open). Configured in `ServiceDefaults` via `AddResilienceHandler`.
- **Caching**: Redis via `IDistributedCache` + `StackExchange.Redis` directly. Cached: session tokens (15-min sliding TTL), idempotency keys (24h absolute TTL), rate-limit counters (per-tenant+per-endpoint, 1-min window), exercise library (5-min TTL, tag `exercises:{tenantId}` for invalidation). Exercise library is the only query result cached; all other reads go to Postgres.
- **Background work**: Quartz.NET (single in-process scheduler in `TheCoach.Workers`) for scheduled jobs. Redis pub/sub (`ISubscriber`) for reactive worker trigger (producers publish a channel key; workers subscribe). All workers run inside the same process for v1 — no separate queue infrastructure.
- **Outbox**: `OutboxMessage { Id, TenantId, Type, Payload, CreatedAt, ProcessedAt, RetryCount }` table in the `main` schema. `IAuditLogger` and domain event handlers write `OutboxMessage` rows inside the same `SaveChanges` transaction. `OutboxProcessor` (`IHostedService`) polls every 5 s; claims rows with `FOR UPDATE SKIP LOCKED`; dispatches; marks `ProcessedAt`. Retry max 10; dead-letter to `outbox_dead_letters` after max.
- **Idempotency**: `Idempotency-Key` header required on all POST/PUT writes. Value stored in Redis for 24 h with the response status + body. Middleware checks key before handler; 409 Conflict returned on duplicate within TTL.
- **Secrets**: All secrets in Azure Key Vault; accessed at startup via `Azure.Extensions.AspNetCore.Configuration.Secrets` + Managed Identity. `IOptions<T>` validated on startup with `ValidateOnStart()`. No secrets in `appsettings.json` or environment variables in production.

## Cloud topology

- **Provider**: Azure
- **Compute**: Azure Container Apps (Consumption plan). `TheCoach.Api` and `TheCoach.Workers` as separate Container App revisions; `TheCoach.Web` served as a Static Web App.
- **Data**: Azure Database for PostgreSQL — Flexible Server, General Purpose 4 vCores, 32 GiB storage (v1). Private endpoint in the VNet.
- **Cache**: Azure Cache for Redis, Basic C1 (1 GB, 1 shard, v1). Private endpoint in the VNet.
- **Secrets**: Azure Key Vault (Standard). Managed Identity (system-assigned on each Container App) grants `Key Vault Secrets User` role.
- **Identity**: Azure Entra ID. Single multi-tenant app registration; `consumer` tenants added via invitation flow.
- **Storage**: Azure Storage Account (Standard LRS). One container per tenant (`photos-{tenantId}`). Blob encryption: Microsoft-managed key at rest; TLS 1.3 in transit.
- **AI**: Azure OpenAI Service, `East US 2`. Deployed models: `gpt-4o` (coaching assistant + complex generation) and `gpt-4o-mini` (quick content, automation messages).
- **Push**: Firebase Cloud Messaging (FCM HTTP v1 API). Not an Azure service; FCM is the universal push gateway (Web Push + Android + iOS). Service account key stored in Key Vault.
- **CDN / Edge**: Azure Static Web Apps CDN for `TheCoach.Web` (included). No additional CDN for the API v1.
- **Networking**: VNet with two subnets — `apps` (Container Apps delegated) and `data` (private endpoints for Postgres + Redis). No public endpoints on data layer. Ingress to Container Apps via Azure Application Gateway (WAF_v2) for the API; SWA handles its own ingress.
- **IaC**: Terraform (Azure provider `~> 3.x`); state in Azure Storage backend (separate `tfstate` storage account). GitHub Actions drives `terraform plan` on PR, `terraform apply` on merge to `main`.

## Data model (concrete)

All entities implement `ITenantScoped { Guid TenantId }` unless noted. All `Id` fields are `Guid` (UUIDs v7 for insert-order friendliness). Soft-delete via `DeletedAt` (nullable `DateTimeOffset`). PII fields tagged `[Pii]` for audit export.

### Foundations context

```csharp
Tenant         { Id, Name, Type (enum: Gym/Studio/Trainer/Member), PlanTier (enum), 
                 Region, StripeCustomerId, StripeSubscriptionId, CreatedAt, DeletedAt }
               // NOT tenant-scoped — it IS the tenant root

User           { Id, TenantId, ExternalAuthId, [Pii] Email, [Pii] DisplayName, 
                 Role (enum), CreatedAt, DeletedAt }

CoachClientRel { Id, TenantId, CoachId→User, ClientId→User, AssignedAt, RevokedAt }

AuditEntry     { Id, TenantId (nullable), UserId, Action, EntityType, EntityId, 
                 Before (jsonb), After (jsonb), Timestamp }  // in audit schema

OutboxMessage  { Id, TenantId, Type, Payload (jsonb), CreatedAt, ProcessedAt, RetryCount }
```

### Coaching context

```csharp
Program        { Id, TenantId, CreatedByCoachId, Name, IsTemplate, IsGlobal (null-tenant starter), 
                 CreatedAt, DeletedAt }

Block          { Id, TenantId, ProgramId, WeekNumber, Name }

Workout        { Id, TenantId, BlockId, DayOfWeek, Name }

WorkoutExercise { Id, TenantId, WorkoutId, ExerciseId, SetCount, RepTarget, 
                  WeightTargetKg (decimal?), RpeTarget (byte?), OrderIndex }

Exercise       { Id, TenantId (null=global), Name, MuscleGroups (text[]), 
                 DemoVideoUrl, Tags (text[]), CreatedAt, DeletedAt }

ProgramAssignment { Id, TenantId, ProgramId, ClientId, StartDate, 
                    Status (enum: Active/Paused/Completed), AssignedByCoachId }

WorkoutLog     { Id, TenantId, ClientId, ProgramAssignmentId?, WorkoutId?,
                 LoggedAt, Sets (jsonb: [{exerciseId, setNum, reps, weightKg, rpe}]),
                 CreatedAt }  // append-only

ComplianceAlert { Id, TenantId, CoachId, ClientId, TriggeredAt, 
                  AcknowledgedAt?, AcknowledgedByCoachId? }
```

### HealthTracking context

```csharp
NutritionTarget { Id, TenantId, ClientId, CalorieTarget, ProteinG, CarbG, FatG, 
                  SetByCoachId?, EffectiveDate, SupersededAt? }

NutritionLog   { Id, TenantId, ClientId, [Pii] LoggedAt, 
                 FoodItems (jsonb: [{foodItemId, quantityG, unitLabel}]),
                 CaloriesTotal, ProteinG, CarbG, FatG }

FoodItem       { Id, TenantId (null=global), Name, CaloriesPer100g, ProteinPer100g,
                 CarbPer100g, FatPer100g, Barcode?, Source (enum: Global/Custom),
                 ExternalId? }  // ExternalId links to Open Food Facts barcode

BodyMetric     { Id, TenantId, ClientId, [Pii] RecordedAt, [Pii] WeightKg, 
                 [Pii] BodyFatPct?, [Pii] Measurements (jsonb: {waistCm, chestCm, ...}),
                 PhotoBlobUrl? }

CheckInTemplate { Id, TenantId, CreatedByCoachId, Name, 
                  Questions (jsonb: [{id, text, type, options[]}]),
                  RecurrenceCron?, IsActive }

CheckInResponse { Id, TenantId, TemplateId, ClientId, [Pii] SubmittedAt, 
                  [Pii] Answers (jsonb: [{questionId, value}]) }  // append-only
```

### Messaging context

```csharp
Conversation   { Id, TenantId, Type (enum: Direct/Group), 
                 ParticipantIds (uuid[]), LastMessageAt }

Message        { Id, TenantId, ConversationId, SenderId, [Pii] Body (text?), 
                 AudioBlobUrl?, SentAt, ReadAt?, DeletedAt }  // soft-delete nulls Body

PushRegistration { Id, TenantId, UserId, [Pii] DeviceToken, Platform (enum), 
                   RegisteredAt, LastSeenAt }

CoachingSession { Id, TenantId, UserId, Messages (jsonb: [{role, content, timestamp}]),
                  CreatedAt, LastMessageAt }  // MAF agent conversation history
```

### Billing context

```csharp
Subscription   { Id, TenantId, StripeSubscriptionId, PlanTier (enum), 
                 Status (enum: Trialing/Active/PastDue/Canceled),
                 TrialEndsAt?, CurrentPeriodStart, CurrentPeriodEnd }
```

### Intelligence context

```csharp
GenerationJob  { Id, TenantId, RequestedByUserId, Type (enum: Program/MealPlan),
                 Prompt, Status (enum: Pending/Running/Done/Failed),
                 ResultPayload (jsonb)?, CreatedAt, CompletedAt? }

WorkflowAutomation { Id, TenantId, CreatedByUserId, Name, IsActive,
                     TriggerEvent (enum), 
                     Actions (jsonb: [{type, delayDays, templateRef}]) }

AutomationRun  { Id, TenantId, WorkflowAutomationId, TriggeredForClientId, 
                 TriggeredAt, CurrentStep, CompletedAt?, Status (enum) }
```

### Performance context

```csharp
TrainingLoadEntry { Id, TenantId, ClientId, SessionDate, LoadScore (decimal),
                    [Pii] HRVmsRMSSD (decimal?), ReadinessScore (byte?),
                    Sport (enum: General/Cycling/Running/Lifting/Swimming/Other),
                    ComputedFromWorkoutLog (bool) }

DrillLog       { Id, TenantId, ClientId, Sport (enum), DrillType, LoggedAt,
                 Metrics (jsonb: [{label, value, unit}]) }

PeriodizationPlan { Id, TenantId, CoachId, ClientId, Name,
                    Mesocycles (jsonb: [{name, startWeek, endWeek, targetLoadMin, targetLoadMax, phase}]) }
```

### Indexes (key)

- `(TenantId, ClientId)` on WorkoutLog, NutritionLog, BodyMetric, CheckInResponse, TrainingLoadEntry
- `(TenantId, CoachId)` on ComplianceAlert (unacknowledged scan)
- `(TenantId, ConversationId)` + `SentAt DESC` on Message (conversation paging)
- `Barcode` on FoodItem (lookup)
- `(TenantId, Status)` on ProgramAssignment (active programs query)
- `ProcessedAt IS NULL` partial index on OutboxMessage (processor scan)

## API surface (concrete)

All routes prefixed `/api/v1/`. All errors: RFC 7807 `ProblemDetails`. All POST/PUT: `Idempotency-Key` header required; 409 on replay within 24 h. Rate limits: 1000 req/min per tenant global; per-endpoint overrides below.

### Coaching

| Method | Route | Auth policy | Notes |
|---|---|---|---|
| GET | `/programs` | `Programs.View` | List tenant programs + global starters |
| POST | `/programs` | `Programs.Create` | Returns 201 + Location |
| GET | `/programs/{id}` | `Programs.View` | |
| PUT | `/programs/{id}` | `Programs.Create` | Full replace |
| DELETE | `/programs/{id}` | `Programs.Create` | Soft-delete |
| POST | `/programs/{id}/assign` | `Programs.Assign` | Body: `{clientId, startDate}` |
| GET | `/exercises` | authenticated | Paginated; `?search=&muscle=` |
| POST | `/exercises` | `Programs.Create` | Tenant-custom exercise |
| POST | `/workout-logs` | `WorkoutLogs.Create` | Append-only; 201 + log id |
| GET | `/workout-logs?clientId=&from=&to=` | `Compliance.ViewOwn` | Coach: own clients; Client: self |
| GET | `/compliance/roster` | `Compliance.ViewOwn` | Sorted by last-log date |
| POST | `/compliance/alerts/{id}/acknowledge` | `Compliance.ViewOwn` | Rate: 100/min |

### HealthTracking

| Method | Route | Auth policy | Notes |
|---|---|---|---|
| GET/PUT | `/nutrition/targets/{clientId}` | `Nutrition.SetTargets` | Coach sets for client; Member sets for self |
| POST | `/nutrition/logs` | `NutritionLogs.Create` | Append-only |
| GET | `/nutrition/logs?clientId=&date=` | `HealthTracking.ViewOwn` | |
| GET | `/nutrition/foods?q=&barcode=` | authenticated | Queries cache → Postgres → Open Food Facts |
| POST | `/metrics/body` | `BodyMetrics.Log` | Returns 201 |
| GET | `/metrics/body/{clientId}` | `HealthTracking.ViewOwn` | Time-series, paginated |
| GET | `/check-ins/templates` | `CheckIns.Create` | Coach-owned |
| POST | `/check-ins/templates` | `CheckIns.Create` | |
| POST | `/check-ins/responses` | `CheckIns.Submit` | Append-only |
| GET | `/check-ins/responses?templateId=&clientId=` | `HealthTracking.ViewOwn` | |

### Messaging

| Method | Route | Notes |
|---|---|---|
| GET | `/conversations` | Authenticated; own conversations only |
| POST | `/conversations` | Create direct or group |
| GET | `/conversations/{id}/messages` | Paginated (cursor), newest-first |
| POST | `/conversations/{id}/messages` | Rate: 300/min/user |
| POST | `/push/register` | Device token registration |
| DELETE | `/push/register/{deviceToken}` | Unregister |

### Billing

| Method | Route | Auth policy | Notes |
|---|---|---|---|
| GET | `/billing/plans` | public | Available plan tiers |
| GET | `/billing/subscription` | `Billing.Manage` | Own tenant subscription |
| POST | `/billing/portal-session` | `Billing.Manage` | Returns Stripe portal URL |
| POST | `/webhooks/stripe` | — | HMAC verified; no auth token |

### Intelligence

| Method | Route | Auth policy | Notes |
|---|---|---|---|
| POST | `/intelligence/generate` | `AI.Generate` | Enqueues GenerationJob; 202 + job id |
| GET | `/intelligence/generate/{jobId}` | `AI.Generate` | Poll for result |
| GET | `/intelligence/automations` | `Automations.Create` | List tenant automations |
| POST | `/intelligence/automations` | `Automations.Create` | |
| PUT | `/intelligence/automations/{id}` | `Automations.Create` | |
| POST | `/chat` | authenticated | MAF agent; streaming SSE response |

### Performance

| Method | Route | Auth policy | Notes |
|---|---|---|---|
| POST | `/performance/load` | `Performance.LogOwn` | TrainingLoadEntry; 201 |
| GET | `/performance/load/{clientId}` | `Performance.ViewOwn` | Time-series |
| POST | `/performance/drills` | `Performance.LogOwn` | DrillLog; 201 |
| GET | `/performance/drills/{clientId}` | `Performance.ViewOwn` | |
| GET | `/performance/periodization/{clientId}` | `Performance.ViewOwn` | |
| PUT | `/performance/periodization/{clientId}` | `Programs.Create` | Coach sets plan |

## MAF agents

### CoachingAssistant

**Purpose:** Fitness coaching chatbot accessible from the slide-over panel in every view. Answers questions about client progress, generates workout drafts, explains nutrition data, and surfaces compliance alerts in natural language.

**Host:** `Application.Intelligence` — registered via MAF `AddAgent<CoachingAssistant>()` in the DI container; exposed through the `/api/v1/chat` SSE endpoint.

**Model:** `gpt-4o` via Azure OpenAI (`IChatClient` from MEAI).

**Tools registered:**
- `GetClientProfile(clientId)` — fetches User, active ProgramAssignment, last 7 WorkoutLogs, latest NutritionTarget
- `GetComplianceRoster()` — returns at-risk clients (last log > 2 days ago) for the calling coach's tenant
- `GetCheckInSummary(clientId, days)` — aggregates recent CheckInResponse answers into trends
- `GetTrainingLoad(clientId, days)` — ATL/CTL/TSB for the requested period
- `GenerateWorkoutDraft(goal, durationWeeks, equipment, restrictions)` — enqueues a `GenerationJob` and returns the job id for the front end to poll
- `SendMessageToClient(clientId, body)` — posts a Message in the existing direct Conversation

**System prompt outline:** "You are an expert personal trainer and sports nutritionist. You have access to real-time client data. When asked to generate programs, always enqueue a draft and do not invent exercise names outside the platform library. Respond concisely — coaches are busy."

**Memory:** Conversation history persisted in `CoachingSession.Messages` (JSON). Session loaded at request time from `sessionId` cookie; new session created if absent. Max 40-turn context window; older turns summarised into `systemSummary` prepended to the prompt.

**Conversation persistence:** `CoachingSession` table in the main schema; one session per user per device (sessionId = UUID stored in HttpOnly cookie).

## SPA architecture

- **Framework:** React 19 + TypeScript + Vite 6
- **Routing:** React Router v7 (file-based routes under `src/routes/`)
- **State:** TanStack Query v5 for server state (queries + mutations); Zustand for local UI state (sidebar collapse, chatbot open/closed, active tenant)
- **Components:** shadcn/ui primitives (copied, not imported); shared `DataTable` wrapper over TanStack Table v8; shared `Form` = shadcn `Form` + `react-hook-form` + `zod`; `ChatPanel` = slide-over using shadcn `Sheet` wired to `/api/v1/chat` SSE
- **Feature folders:**
  - `src/features/coaching/` — program builder, exercise library, workout logging, compliance roster
  - `src/features/health/` — nutrition log, body metrics, check-in forms and trends
  - `src/features/messaging/` — conversation list, message thread
  - `src/features/billing/` — plan page, portal redirect
  - `src/features/performance/` — load charts, drill log, periodization view
  - `src/features/admin/` — tenant management (Operator/SystemAdmin only)
- **PWA:** Vite PWA plugin (`vite-plugin-pwa`); service worker handles Web Push subscription. Push notifications displayed as system notifications (Notification API); in-app badge on conversation list.
- **Offline:** Workout logging view stores pending sets in `localStorage`; syncs on reconnect via TanStack Query mutation retry.
- **Dashboard chrome:** Fixed left sidebar (collapsible on mobile) with nav links gated by RBAC policy. Top bar: tenant selector (for SystemAdmin), user menu. Chatbot `ChatPanel` always rendered; toggled via floating button.

## Diagrams checked into the repo

- `docs/diagrams/c1-context.puml`
- `docs/diagrams/c2-containers.puml`
- `docs/diagrams/c3-components-api.puml`
