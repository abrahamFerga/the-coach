# The Coach — Plan

## Epics (in build order)

1. **Foundations** — Auth (OIDC), multi-tenancy (tenant-scoped data isolation), RBAC scaffold (policy-named authorization), OpenTelemetry observability, health checks, audit logging, dashboard shell (trainer console + client mobile view), connector registry, Stripe subscription entitlement gate. Always epic 1; pulled from enterprise guardrails. Capabilities (from SPEC): *(cross-cutting — enables all capabilities below)*.

2. **Coaching Core** — The primary job to be done: build programs, deliver them, and see who is falling behind. Capabilities (from SPEC): *Workout programming & delivery*, *Client compliance dashboard*. Depends on: Foundations.

3. **Health Tracking** — Client self-reporting surfaces: nutrition logs against coach-set targets, body metrics over time, progress photos, and the recurring check-in questionnaire that coaches use to adjust load. Capabilities (from SPEC): *Nutrition tracking*, *Progress & body metrics*, *Automated check-ins & assessment forms*. Depends on: Foundations, Coaching Core (client context).

4. **Messaging** — In-app 1:1 and group coaching chat; push notification dispatch for workouts, check-in reminders, and compliance alerts. Capabilities (from SPEC): *In-app messaging*. Depends on: Foundations.

5. **Billing** — Per-tenant subscription lifecycle: plan selection (Member / Trainer / Studio / Gym), Stripe recurring billing, feature gating by plan tier, self-serve billing portal, dunning. Capabilities (from SPEC): *Tenant-scoped subscription billing*. Depends on: Foundations.

6. **AI & Automation** — AI-generated workout programs and meal plans from a prompt + client profile; workflow automations (trigger → action chains) and scheduled drip messaging sequences. Capabilities (from SPEC): *AI workout + meal plan generation*, *Workflow automations & drip messaging*. Depends on: Coaching Core, Health Tracking, Messaging.

7. **Athlete Performance** — Training load charts (ATL/CTL/TSB), manually-entered HRV + readiness scores, sport-specific drill logging, and mesocycle periodization planning. Capabilities (from SPEC): *Sport/athlete performance analytics*. Depends on: Coaching Core, Health Tracking.

## Module list

| Module (.NET project name) | Bounded context | Capabilities served | Skills used to build it |
|---|---|---|---|
| `TheCoach.ServiceDefaults` | cross-cutting | OTel, health checks, shared middleware, resilience defaults | dotnet-aspire-base |
| `TheCoach.AppHost` | orchestration | Aspire AppHost wiring all services + resources | aspire |
| `TheCoach.Application.Foundations` | foundations | Auth, multi-tenancy, RBAC, audit logging, dashboard shell, connector registry, subscription entitlement gate | dotnet-aspnet, rbac, multi-tenant |
| `TheCoach.Application.Coaching` | coaching | Workout programming, exercise library, program delivery, set-by-set logging, compliance dashboard, compliance alert engine | dotnet-aspnet, dotnet-data, dotnet-ai (AI epic) |
| `TheCoach.Application.HealthTracking` | health-tracking | Nutrition logging, macro targets, food database, body metrics, progress photos, check-ins, assessment forms | dotnet-aspnet, dotnet-data |
| `TheCoach.Application.Messaging` | messaging | 1:1 and group chat, message history, push notification dispatch | dotnet-aspnet, dotnet-data |
| `TheCoach.Application.Billing` | billing | Tenant subscription plans, Stripe webhooks, plan tier entitlements, billing portal | dotnet-aspnet, dotnet-data |
| `TheCoach.Application.Intelligence` | intelligence | AI program generation, AI meal plan generation, workflow automations, drip message sequences | dotnet-aspnet, dotnet-ai |
| `TheCoach.Application.Performance` | athlete-performance | Training load (ATL/CTL/TSB), HRV + readiness, drill logs, periodization blocks | dotnet-aspnet, dotnet-data |
| `TheCoach.Web` | SPA | Trainer console (desktop-first), client mobile view (React PWA) | (frontend; not a .NET project) |
| `TheCoach.Workers` | background | All background jobs (compliance scanner, push dispatcher, automation runner, subscription sync) | dotnet-aspnet |

## Data model sketch

- **Tenant** — Id, Name, Type (Gym/Studio/Trainer/Member), PlanTier, Region, StripeCustomerId. Root of all data isolation; `TenantId` on every domain table enforced via EF Core global query filter.
- **User** — Id, TenantId, ExternalAuthId (OIDC sub), Email (PII), DisplayName (PII), Role. Soft-deleted; export endpoint surfaces all PII fields tagged `[Pii]`.
- **CoachClientRelationship** — CoachId → UserId, ClientId → UserId, TenantId. Many coaches can share a client (studio context).
- **Program** — Id, TenantId, CreatedByCoachId, Name, PhaseCount, IsTemplate (true for public library entries). Composed of Blocks.
- **Block** — Id, ProgramId, TenantId, WeekNumber, Name. Composed of Workouts.
- **Workout** — Id, BlockId, TenantId, DayOfWeek, Name. Composed of WorkoutExercises.
- **WorkoutExercise** — Id, WorkoutId, ExerciseId, SetCount, RepTarget, WeightTarget, RPETarget, Order.
- **Exercise** — Id, TenantId (null = global library), Name, MuscleGroups, DemoVideoUrl, Tags. Global entries never deleted; tenant-custom entries soft-deleted.
- **ProgramAssignment** — ProgramId, ClientId, TenantId, StartDate, Status (Active/Paused/Completed).
- **WorkoutLog** — Id, ClientId, TenantId, WorkoutId (nullable — can log ad-hoc), LoggedAt, Sets (JSON: [{ExerciseId, setNum, reps, weight, rpe}]). Audit-only after submission; no edits.
- **ComplianceAlert** — Id, TenantId, CoachId, ClientId, TriggeredAt, AcknowledgedAt, AcknowledgedByCoachId.
- **NutritionTarget** — ClientId, TenantId, CalorieTarget, ProteinG, CarbG, FatG, SetByCoachId, EffectiveDate.
- **NutritionLog** — Id, ClientId, TenantId, LoggedAt (PII), FoodItems (JSON: [{FoodItemId, quantity, unit}]), MacroTotals. PII-tagged.
- **FoodItem** — Id, TenantId (null = global), Name, CaloriesPer100g, Protein, Carb, Fat, BarCode, Source (USDA/Custom).
- **BodyMetric** — Id, ClientId, TenantId, RecordedAt, WeightKg, BodyFatPct, Measurements (JSON), PhotoBlobUrl. PII-tagged; photo blob encrypted at rest.
- **CheckInTemplate** — Id, TenantId, CreatedByCoachId, Name, Questions (JSON: [{id, text, type, options}]), Schedule (RecurrenceCron).
- **CheckInResponse** — Id, TemplateId, ClientId, TenantId, SubmittedAt, Answers (JSON). PII-tagged; append-only.
- **Message** — Id, ConversationId, TenantId, SenderId, Body (PII), SentAt, ReadAt. Soft-deleted (body nulled).
- **Conversation** — Id, TenantId, Type (Direct/Group), ParticipantIds (JSON), LastMessageAt.
- **PushRegistration** — UserId, TenantId, DeviceToken (PII), Platform (iOS/Android/Web).
- **Subscription** — Id, TenantId, StripeSubscriptionId, PlanTier, Status (Active/PastDue/Canceled), CurrentPeriodEnd.
- **WorkflowAutomation** — Id, TenantId, CreatedByUserId, TriggerEvent (enum), Actions (JSON: [{type, delayDays, templateRef}]), IsActive.
- **TrainingLoadEntry** — Id, ClientId, TenantId, SessionDate, LoadScore, HRVmsRMSSD (PII, optional), ReadinessScore, Sport (enum). PII-tagged.
- **DrillLog** — Id, ClientId, TenantId, DrillType, Sport, LoggedAt, Metrics (JSON: [{label, value, unit}]).

## RBAC model (refined)

| Role | Policies | Notes |
|---|---|---|
| **SystemAdmin** | `Tenants.Manage`, `Users.ViewAll`, `Billing.ViewAll` | Platform-level only; cannot read client health data (`HealthTracking.*`). |
| **Operator** | `Users.Invite`, `Users.Deactivate`, `Coaching.ViewAll`, `HealthTracking.ViewSummary`, `Billing.Manage`, `Automations.Create`, `Reports.ViewAll` | Scoped to own tenant. Cannot write programs or log health data. |
| **HeadCoach** | `Users.Invite`, `Programs.Create`, `Programs.Assign`, `Programs.ViewAll`, `Compliance.ViewAll`, `HealthTracking.ViewAll`, `CheckIns.Create`, `Messaging.SendAll`, `Performance.ViewAll`, `AI.Generate`, `Automations.Create` | Scoped to own tenant. Can read all coaches' clients. |
| **Coach** | `Programs.Create`, `Programs.Assign`, `Compliance.ViewOwn`, `HealthTracking.ViewOwn`, `CheckIns.Create`, `Messaging.Send`, `Performance.ViewOwn`, `AI.Generate` | `Own` = assigned clients only. Cannot see other coaches' clients. |
| **Client** | `WorkoutLogs.Create`, `NutritionLogs.Create`, `BodyMetrics.Log`, `CheckIns.Submit`, `Messaging.Send`, `Performance.LogOwn` | Own data only. Cannot see other clients or coach-internal notes. |
| **Athlete** | Same as Client + `Performance.ViewOwn` (full athlete surface) | Client subtype; activated when coach tags client as Athlete. |
| **Member** | `Programs.BrowseLibrary`, `Programs.Adopt`, `WorkoutLogs.Create`, `NutritionLogs.Create`, `BodyMetrics.Log`, `Billing.ManageOwn` | Self-coached; no Coach relationship; manages own billing. |

*Solo Trainer tenants: owner account is Operator + Coach (combined at create time). Solo Member tenants: single Member role.*

Policy names are registered in ASP.NET Core `AuthorizationOptions`; handlers check `TenantId` claim for data isolation. Roles in `appsettings.{env}.json`; never hardcoded.

## Integration surface

No connectors are declared in `workflow.json` at this stage. Implicit integrations that require architecture decisions:

| Integration | Direction | Purpose | Routes / config |
|---|---|---|---|
| Stripe | Inbound (webhook) + outbound | Subscription lifecycle, payment events | `/api/v1/webhooks/stripe` (HMAC verified); `StripeSecretKey`, `StripeWebhookSecret` in secret store |
| OIDC Provider (Entra ID) | Inbound | AuthN for all user identities | `Authority`, `ClientId`, `ClientSecret` per tenant (or shared cloud app registration) |
| Push Notification Gateway (APNs / FCM) | Outbound | Workout reminders, compliance alerts, check-in prompts | `ApnsKey`, `FcmKey` in secret store; dispatched via outbox |
| Food Database (USDA FoodData Central or licensed) | Outbound (read-only) | Food item lookup and barcode resolution | `FoodDbApiKey`; cached aggressively; architecture decision on provider |
| Azure OpenAI (or OpenAI) | Outbound | AI program + meal plan generation, automation content | `AzureOpenAIEndpoint`, `AzureOpenAIKey` in secret store; async job queue |
| Azure Blob Storage | Outbound | Progress photo storage; encrypted at rest | `BlobStorageConnectionString` via Aspire resource |

## Background work

| Job | Trigger | Cadence | Outbox required? |
|---|---|---|---|
| `ComplianceAlertScanner` | Scheduled | Daily at 06:00 tenant local time | No — writes ComplianceAlert rows; no external side effect |
| `PushNotificationDispatcher` | Reactive (event: workout assigned, check-in due, compliance alert fired) | On event | Yes — APNs/FCM calls are external; failure must not lose the send |
| `WorkflowAutomationRunner` | Reactive (trigger event) + scheduled (drip delay elapsed) | On event + periodic sweep | Yes — dispatches messages and external actions |
| `AIGenerationWorker` | Reactive (AI generate request enqueued) | On enqueue | No — returns result to caller via polling; LLM call is idempotent per job ID |
| `StripeSubscriptionSyncJob` | Scheduled | Daily at 02:00 UTC | Yes — reconciles missed Stripe webhooks; updates Subscription rows |
| `CheckInReminderDispatcher` | Scheduled | Matches CheckInTemplate.Schedule (cron) | Yes — push + in-app notifications are external side effects |

## Open questions for design-architecture

1. **Multi-tenancy isolation model** — row-level security with EF Core global query filters (simpler, shared schema) vs. schema-per-tenant (stronger isolation, higher migration cost)? Schema-per-tenant adds ~2 hours to the Foundations epic; RLS is the default unless isolation is a hard sales requirement.
2. **Athlete analytics data source in v1** — since wearables are deferred, does the athlete surface rely on (a) manually entered HRV + readiness by the athlete, (b) computed session load from WorkoutLog RPE values only, or (c) both? Affects TrainingLoadEntry schema and the UI.
3. **Program library visibility** — are program templates tenant-private (each coach's own library only), or is there a shared read-only public library in v1 that Members can browse? Public library requires a content-moderation policy and different authz.
4. **Food database provider** — USDA FoodData Central (free, ~900k items, no barcodes) vs. Nutritionix (licensed, barcode scan, macro quality)? Affects external spend and integration complexity.
5. **Mobile delivery model** — React PWA (one codebase, web push only) vs. React Native (native push, better UX, two platform submissions, longer build time)? Affects push notification integration and the Web project scope.
6. **Billing portal** — Stripe-hosted Customer Portal (zero custom UI, limited branding) vs. custom in-app billing page (full control, more build)? Affects Billing epic scope.
7. **v1 scale targets** — tenants/day, max clients/tenant, and requests/second estimates needed to right-size the Aspire resource configuration (Redis cache, DB instance tier, background worker concurrency).
