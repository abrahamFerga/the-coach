# The Coach — Product specification

## In one sentence

The Coach is a multi-tenant fitness coaching platform — serving solo users, independent trainers, coaching studios, and enterprise gyms — that unifies workout delivery, nutrition, performance analytics, and client compliance monitoring into one configurable system, with per-tenant subscription plans that fit every business size.

## Primary jobs to be done

- When I'm running a coaching business, I want to build and deliver programs to all my clients in one place, so that I don't lose clients to spreadsheets, WhatsApp, and forgotten follow-ups.
- When a client goes silent, I want to see who is at risk and reach out immediately, so that I can reduce churn before it happens.
- When I'm preparing a client's next training block, I want their nutrition, compliance, and performance history in front of me, so that my program adjustments are data-driven, not guesswork.
- When a competitive athlete wants to peak for an event, I want to plan their periodization and monitor their training load, so that they arrive at competition day recovered and sharp.
- When I run a gym or studio, I want to manage coaches, clients, and billing from one dashboard, so that I don't need five separate tools to operate my business.
- When I'm a self-coached individual, I want to follow a structured program and track my own progress, so that I stay consistent without hiring a trainer.

## Target personas

- **Trainer** — An independent or studio-based coach who builds and delivers programming to a client roster (remote or hybrid). Top 3 tasks:
  1. Build a phased training program and assign it to a client.
  2. Scan the compliance roster each morning for at-risk clients and send a follow-up message.
  3. Review a client's weekly check-in and adjust next week's load accordingly.

- **Operator** — A gym or studio owner/admin who manages coaches, client accounts, and org-wide billing. Top 3 tasks:
  1. Onboard a new coach and assign them a client roster.
  2. Review monthly revenue, active client count, and average compliance rate across the org.
  3. Configure subscription tiers and manage billing for the org's membership.

- **Client** — A coached individual following a trainer's assigned program (remote or in-person). Top 3 tasks:
  1. Log today's workout set-by-set on mobile, seeing previous weights as defaults.
  2. Submit the weekly check-in (sleep quality, stress, energy, motivation) when prompted.
  3. Log meals against coach-set macro targets and view daily nutrition progress.

- **Athlete** — A performance-focused or competitive-sport client who tracks periodization, training load, and readiness. Top 3 tasks:
  1. View today's readiness score (HRV-adjusted training load) before the session.
  2. Log sport-specific drill performance (e.g., sprint times, vertical jump) alongside strength work.
  3. Review their training load chart across the current mesocycle to spot overreach.

- **Member** — A self-coached individual with no assigned trainer who uses the program library independently. Top 3 tasks:
  1. Browse and adopt a public program template from the library.
  2. Log workouts and track personal bests over time.
  3. Set their own macro targets and track nutrition without a coach.

## Capabilities

### Must have (v1)

| Capability | One-line description | Personas |
|---|---|---|
| Workout programming & delivery | Build phased programs with exercise library (video demos); clients log set-by-set on mobile with previous-rep defaults. | Trainer, Client, Athlete, Member |
| Client compliance dashboard | Roster view sorted by at-risk status; color-coded by last-logged date; one-click to message flagged clients. | Trainer, Operator |
| Nutrition tracking | Coach-set macro/calorie targets; clients log via a 500k+ food database with barcode scanner; daily progress ring. | Trainer, Client, Member |
| Progress & body metrics | Time-series charts for weight, body fat %, measurements, and progress photos; coach and client both see the trend. | Trainer, Client, Athlete |
| Automated check-ins & assessment forms | Recurring questionnaires (weekly check-in, movement screen, onboarding intake); answers surface as trend charts on the coach dashboard. | Trainer, Client |
| In-app messaging | 1:1 and group coaching chat with voice notes; push notifications; read receipts. | Trainer, Client, Operator, Athlete |
| Tenant-scoped subscription billing | Per-tenant plan tier (Member / Trainer / Studio / Gym) with Stripe-backed recurring billing, plan upgrades, and a self-serve billing portal. | Operator, Member |

### Differentiators (v1)

| Capability | Why it matters | Personas |
|---|---|---|
| AI workout + meal plan generation | Coaches running 30+ client rosters save 40–60% programming time; AI generates a full mesocycle or meal plan from a prompt and the client's profile. | Trainer |
| Workflow automations & drip messaging | Automated onboarding sequences, scheduled content delivery, and re-engagement drips reduce manual follow-up and separate process-driven coaches from reactive ones. | Trainer, Operator |
| Sport/athlete performance analytics | Training load charts, HRV-adjusted readiness scores, sport-specific drill logging, and season periodization blocks — the only v1 feature targeted at competitive athletes. | Athlete, Trainer |

### Explicitly out of scope (v1)

- **Wearable integrations (Apple Watch, Garmin, WHOOP, OURA)** — OAuth device flows per vendor add significant complexity; athlete manual entry covers the HRV/readiness surface in v1. v2.
- **Habit tracking** — Valuable daily accountability loop, but not primary to the coaching delivery job. v2.
- **SMS + email outreach** — In-app messaging covers communication in v1; SMS/email adds TCPA/CAN-SPAM compliance and deliverability infrastructure. Zapier bridge in v2.
- **Appointment / class booking** — Market treats this as peripheral (Calendly, Google Calendar); only 2 of 5 players are deep on it. v2.
- **Native video calls / live coaching** — Clients default to Zoom; building native video adds WebRTC infrastructure with no competitive differentiation. v2.
- **Custom white-label branded mobile app** — App Store submission pipeline is enterprise-tier complexity; v1 ships a web portal and standard-branded iOS/Android apps. v2.
- **Client marketplace / public discovery** — Two-sided marketplace mechanics add acquisition complexity to a product-building exercise. v2.
- **Multi-language / locale** — US-first launch; i18n framework is v1 plumbing but no translated strings until v2.

## RBAC model (initial)

- **System Admin** — Platform-level operator; manages tenants, billing disputes, and support. Cannot read client health data.
- **Operator** — Scoped to one tenant (gym or studio); manages coaches, clients, subscription, and org-wide reporting. Cannot write to client programs unless also a Coach.
- **Head Coach** — Senior coach within a studio or gym tenant; can manage other coaches' client rosters, view cross-coach compliance reports, and create org-wide program templates.
- **Coach** — Delivers programming to assigned clients; full read/write on their clients' programs, nutrition targets, and check-ins; cannot access other coaches' clients.
- **Client** — Reads their own programs, logs workouts and nutrition, submits check-ins; cannot see other clients or coach-internal notes.
- **Athlete** — Client with access to the sport analytics surface (training load, readiness, drill logs); same data-isolation rules as Client.
- **Member** — Self-coached individual; access to the public program library and their own logs; no coach relationship; manages their own billing.

*Solo Trainer tenants collapse Operator + Coach into a single role. Solo user tenants use Member only.*

## Regulatory constraints

- **GDPR (EU Regulation 2016/679, Art. 17 + 20)** — Health metrics, photos, and workout logs are personal data. Soft-delete required everywhere; tenant data export API required. US-first does not eliminate GDPR exposure if EU clients onboard.
- **CCPA (California Civil Code § 1798.100)** — California consumers have the right to know, delete, and opt out of sale of personal information. Data export and deletion flows required; no selling health data to third parties.
- **PCI DSS SAQ A** — Stripe redirect model keeps card data off the platform's servers. Platform must never log raw card numbers; Stripe Customer Portal handles saved-payment management.
- **COPPA (15 U.S.C. § 6501)** — Collecting health data from under-13 users without verifiable parental consent is prohibited. Age gate on sign-up; no minor accounts in v1.
- **FTC Health Breach Notification Rule (16 CFR Part 318)** — Applies to personal health records handled by non-HIPAA entities; breach notification to affected users and the FTC within 60 days.

## Success metrics

- **Week-1 activation:** ≥60% of new Coach/Trainer tenants assign at least one program to one client within 7 days of signup.
- **Client logging rate:** ≥70% of clients with an active assigned program log ≥3 workouts per week (measured per tenant at P50).
- **At-risk response time:** median time from a compliance alert firing (client silent for 3+ days) to a coach message ≤24 hours.
- **90-day coach retention:** ≥80% of Coach tenants who assigned a program in month 1 are still active in month 3.
- **Time to first program delivered:** median time from tenant creation to first client program assignment ≤30 minutes.

## Open questions for plan-system

1. **Multi-tenancy isolation model** — row-level security (RLS) in a shared schema, or schema-per-tenant (PG schemas)? Affects the Foundations epic scope and migration strategy.
2. **Athlete analytics data source in v1** — since wearables are deferred, does the Athlete surface rely on manual HRV/readiness input by the athlete, coach-entered session RPE, or computed load from workout logs only?
3. **Program library visibility** — are program templates tenant-private (each coach's own library), or is there a shared public library that Members can browse in v1?
4. **Billing portal ownership** — Stripe-hosted Customer Portal (zero custom UI work) or a custom in-app billing page (full control, more build)? Affects the Foundations epic timeline.
