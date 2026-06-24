# The Coach — Architecture Decision Records

---

## ADR-0001: Shared-schema multi-tenancy with EF Core global query filters

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (automated)

### Context

The system must isolate every tenant's data. Two viable models exist: schema-per-tenant (each tenant gets its own Postgres schema, migrations run per-tenant) and shared-schema RLS (one schema, `TenantId` column on every table, enforced via EF Core global query filters). v1 targets up to ~100 tenants/month growth; enterprise gym tenants may have up to 1,000 clients. No contractual data-residency SLA has been required by any customer yet.

### Decision

We will use shared-schema multi-tenancy enforced at the application layer via EF Core global query filters (`e => e.TenantId == _tenantContext.TenantId`). All domain entities implement `ITenantScoped`. `SystemAdmin` calls bypass the filter explicitly via `IgnoreQueryFilters()`. A single Postgres migration pipeline applies schema changes across all tenants in one step.

### Consequences

- **Positive**: one migration pipeline; no per-tenant schema overhead at onboarding; simpler DevOps; adequate for v1 scale targets.
- **Negative**: a bug in `TenantResolutionMiddleware` can expose cross-tenant data; mitigated by integration tests that assert tenant isolation on every endpoint.
- **Neutral**: query performance depends on selective indexes on `(TenantId, ...)` pairs — added for all high-frequency access patterns.

### Alternatives considered

- **Schema-per-tenant** — stronger isolation; Postgres native boundary; no filter bug risk. Rejected because: per-tenant migration execution adds ~2 hours to the Foundations epic, adds operational complexity at onboarding, and provides isolation beyond what v1 contractually requires.
- **Database-per-tenant** — maximum isolation; required for SOC 2 Type II in some enterprise contracts. Rejected because: prohibitive cost at launch ($x per tenant/month vs. shared pool); revisit if enterprise isolation SLA is sold.

---

## ADR-0002: Open Food Facts API as primary food database, USDA FoodData Central as fallback

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (automated)

### Context

The nutrition tracking feature requires a food database with text search and barcode lookup for ~100k+ common food items. Options evaluated: USDA FoodData Central (free, ~1M items, API, no barcodes), Nutritionix (licensed, barcode coverage, $0.001/call over free tier), Open Food Facts (open source CC BY-SA, ~3.4M products, barcodes, API, free). The SPEC targets US-first users; the free tier of licensed APIs limits scale.

### Decision

We will use Open Food Facts API as the primary food database (text search + barcode resolution) and fall back to USDA FoodData Central for items not found via barcode. `FoodItem` rows with `Source = Global` are seeded from Open Food Facts data on first lookup and cached in Postgres. The `FoodItem.ExternalId` column links to the upstream barcode. An `IFoodDatabaseGateway` interface in `Application.HealthTracking` keeps the provider replaceable.

### Consequences

- **Positive**: zero licensing cost; barcode support included; 3.4M items covers US market well; cacheable in Postgres reduces runtime API calls.
- **Negative**: Open Food Facts data quality is community-sourced and varies; calorie accuracy lower than Nutritionix. Mitigated by showing a "reported inaccuracy" flag on community entries.
- **Neutral**: attribution required (CC BY-SA license) — added as a tooltip in the food search UI.

### Alternatives considered

- **Nutritionix API** — professional data quality, barcode support, FDA-aligned values. Rejected because: $0.001/call adds ~$100/month at 100k daily lookups; pricing scales with usage and creates unpredictable cost; evaluate as premium upgrade in v2.
- **Build own database** — full control. Rejected because: seeding and maintaining a nutrition database is a separate product; out of scope for v1.

---

## ADR-0003: React PWA with Web Push (no React Native in v1)

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (automated)

### Context

The client-facing interface needs mobile-first UX and push notifications. Two paths: (a) React PWA (single Vite codebase, Web Push via browser service worker, no App Store) or (b) React Native (native push, native performance, requires iOS App Store review + Android Google Play submission). The SPEC targets US-first with a mix of remote and in-person clients; most client interactions are brief daily log entries.

### Decision

We will build `TheCoach.Web` as a Vite React PWA. The Vite PWA plugin adds a service worker that handles Web Push subscriptions (FCM handles the vapid key / service account). Users install the PWA to their home screen via browser prompt. React Native is explicitly deferred to v2 once the product is validated.

### Consequences

- **Positive**: single codebase; no App Store review delays; Web Push works on Chrome (Android + desktop) and Safari 16.4+ (iOS); deployed instantly with the SPA; reduces build time by ~4–6 weeks.
- **Negative**: iOS Web Push requires Safari 16.4+ (iOS 16.4+, released March 2023) — older devices cannot receive push; Apple does not list PWAs in the App Store. Acceptable for v1 target demographic (fitness professionals and coached clients likely on recent hardware).
- **Neutral**: Workout logging offline support implemented via `localStorage` + TanStack Query mutation retry; adequate for basic offline use.

### Alternatives considered

- **React Native (Expo)** — native push, native performance, App Store presence. Rejected because: App Store review adds 1–3 day delay; separate iOS + Android build pipelines; ~4 weeks additional build time; higher ongoing maintenance cost per platform.
- **Capacitor (web wrapped in native shell)** — App Store presence with single web codebase. Rejected because: Capacitor adds native project complexity without meaningful performance benefit for a data-entry app; push notification config still requires App Store registration.

---

## ADR-0004: Stripe-hosted Customer Portal for billing self-service

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (automated)

### Context

The Billing epic requires self-serve plan management (upgrade, downgrade, cancel, view invoices) and dunning. Two paths: (a) Stripe-hosted Customer Portal (Stripe renders the UI; the API creates a portal session and redirects) or (b) custom in-app billing page (full control of the billing UX, built against Stripe APIs).

### Decision

We will use the Stripe-hosted Customer Portal for v1. `POST /api/v1/billing/portal-session` returns a short-lived Stripe portal URL; the SPA redirects to it. Dunning sequences are configured in the Stripe Dashboard (retry schedule, email templates). No custom billing UI is built.

### Consequences

- **Positive**: zero UI build time for billing self-service; Stripe portal handles all edge cases (prorations, coupon application, SCA); PCI compliance is Stripe's responsibility for the portal.
- **Negative**: portal branding is limited (logo + colours only; no custom layout); portal URL redirects away from the app and back, adding a context-switch.
- **Neutral**: if custom billing UI is required (e.g., enterprise contract terms, custom invoice line items), it is a scoped v2 feature with a clear interface boundary at `Application.Billing`.

### Alternatives considered

- **Custom in-app billing page** — full branding, no redirect, custom invoice display. Rejected because: building subscription management UI is 1–2 weeks of frontend work with no v1 product differentiation; Stripe portal handles every edge case correctly.

---

## ADR-0005: Quartz.NET as the in-process background scheduler

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (automated)

### Context

The system requires six background jobs: three scheduled (ComplianceAlertScanner, StripeSubscriptionSyncJob, CheckInReminderDispatcher) and three reactive (PushNotificationDispatcher, WorkflowAutomationRunner, AIGenerationWorker). Options: Quartz.NET (in-process, persistent job store, cron support), Hangfire (in-process + dashboard, Postgres-backed), Azure Container Apps Jobs (separate compute, scaling), or raw `IHostedService` + `PeriodicTimer`.

### Decision

We will use Quartz.NET for scheduled jobs in `TheCoach.Workers`, with the ADO.NET job store backed by Postgres (same database, `quartz` schema). Reactive jobs are triggered via Redis pub/sub (`ISubscriber`); the workers subscribe and execute inline. Single process; no separate infrastructure.

### Consequences

- **Positive**: single in-process scheduler eliminates distributed lock complexity; Quartz.NET persistent job store survives worker restarts; cron syntax maps directly to `CheckInTemplate.RecurrenceCron` values; no extra infrastructure cost.
- **Negative**: all scheduled and reactive work runs in a single `TheCoach.Workers` Container App revision — if the worker crashes, all jobs pause until restart. Mitigated by Azure Container Apps automatic restart policy.
- **Neutral**: Hangfire is slightly more ergonomic but adds a web dashboard and requires a separate NuGet package; Quartz.NET is simpler for the required job surface.

### Alternatives considered

- **Hangfire** — ergonomic API, built-in dashboard. Rejected because: dashboard adds an administrative surface to secure; Quartz.NET covers the required job types without the dashboard overhead.
- **Azure Container Apps Jobs** — serverless, isolated compute, no worker process. Rejected because: adds ACA Job provisioning to Terraform + GitHub Actions, increases cold-start latency, and is unnecessary for v1 job volume.

---

## ADR-0006: Athlete analytics computed from RPE + manual HRV entry (no wearables in v1)

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (automated); confirmed in SPEC open questions.

### Context

Wearable integrations (Apple Watch, Garmin, WHOOP) are explicitly deferred to v2. The Athlete Performance epic must still deliver meaningful training load and readiness data in v1. Three sub-options: (a) manual HRV + readiness entry only, (b) computed load from WorkoutLog RPE × volume only, or (c) both. SPEC open question 2 was resolved: both.

### Decision

We will compute ATL/CTL/TSB from `WorkoutLog` RPE × volume (Banister impulse-response model) and simultaneously allow athletes to manually enter HRV (RMSSD, ms) and a readiness score (1–10) each day. `TrainingLoadEntry.ComputedFromWorkoutLog = true` for automatically computed rows; `false` for manual entries. Both types are plotted on the same chart; the client sees one unified readiness surface.

### Consequences

- **Positive**: coaches get training load data without requiring wearables; athletes who own wearables can still manually enter data; both data streams co-exist without schema changes when wearables are added in v2.
- **Negative**: manual HRV entry has lower compliance than automatic wearable sync; the readiness surface is less complete than a Whoop/Garmin feed. Accepted as a known v1 limitation.
- **Neutral**: adding wearable OAuth flows in v2 means creating new `TrainingLoadEntry` rows from wearable data — the same table; no migration needed.
