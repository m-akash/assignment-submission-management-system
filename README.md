# Assignment & Submission Management System

A role-based assignment and submission platform for a school and college — **ASP.NET Core 10 Web API + PostgreSQL 17 + Next.js 16**, deployed on **AWS EC2** behind Caddy with automatic HTTPS.

**Admins** provision users, academic years, classes, courses, course offerings, enrollments and teaching mappings. **Teachers** author assignments for offerings they teach, publish them, and grade submissions. **Students** see only their enrolled classes' published work, hand in files, and read their marks. Six domain events queue email automatically; a new account is mailed a single-use link to choose a password — never a password.

---

## Live demo (AWS EC2)

| | |
|---|---|
| **App** | **https://35.154.209.160.sslip.io** |
| **API** | `https://35.154.209.160.sslip.io/api/v1` · health [`/health`](https://35.154.209.160.sslip.io/health) |

| Role | Email | Password |
|---|---|---|
| Admin | `admin@assignment.test` | `Password123!` |
| Teacher | `teacher@assignment.test` | `Password123!` |
| Student | `student@assignment.test` | `Password123!` |

The database is seeded with a plausible school — 14 classes, 36 courses, 78 users, 24 assignments (published + drafts) with real attachments, and 40 graded submissions — so every screen is populated on first login.

## Run locally

Docker is the only prerequisite — no .NET SDK, no Node, no Postgres, no mail account.

```bash
git clone https://github.com/m-akash/assignment-submission-management-system.git
cd assignment-submission-management-system
cp .env.example .env      # every value has a working default
docker compose up --build
```

| | |
|---|---|
| Frontend | http://localhost:3000 |
| API / Swagger | http://localhost:5080/swagger |
| Mailpit (catch-all inbox) | http://localhost:8025 |

Migrations and seed data apply on boot — **no SQL runs by hand**. `docker compose down -v` resets. Backend tests: `cd backend && dotnet test` (integration project needs Docker).

### Without Docker

Needs **.NET 10 SDK**, **Node 20+** and **PostgreSQL 17** running on `localhost:5432` with database `assignment_system` and user/password `assignments`/`assignments` — or start just the backing services with `docker compose up -d postgres mailpit` and install nothing else.

```bash
cd backend   && dotnet run --project src/AssignmentSystem.Api   # → http://localhost:5269
cd frontend  && npm install && npm run dev                      # → http://localhost:3000
```

Create `frontend/.env.local` with the API's **base origin** — no `/api/v1` suffix, the client appends that itself:

```
NEXT_PUBLIC_API_URL=http://localhost:5269
```

The API still migrates and seeds itself on startup. Three differences from Compose:

- **Port** is `5269` (`launchSettings.json`), not `5080`. Swagger is served in `Development`, which `dotnet run` sets.
- **Mail** — `Email__Host` is blank by default, so notifications are queued and their full contents written to the log instead of being sent. To read them in Mailpit instead, set `Email__Host=localhost`, `Email__Port=1025`, `Email__UseSsl=false` (`mailpit` is a container hostname and will not resolve from the host).
- **Uploads** land in a local `_uploads/` folder rather than the container's `/data/submissions`.

To manage the schema explicitly instead of auto-migrating, from `backend/`:

```bash
dotnet ef database update --project src/AssignmentSystem.Infrastructure --startup-project src/AssignmentSystem.Api
```

## Architecture

```
backend/src/
  Domain/          Entities, value objects, enums — zero external dependencies
  Application/     Commands/queries + handlers, validators, DTOs, specifications,
                   authorization attributes, decorator pipeline
  Infrastructure/  EF Core DbContext + 17 migrations, repositories, JWT, file storage,
                   SMTP outbox dispatcher, seeder
  Api/             Controllers, middleware, Swagger, rate limiting
  Shared/          Result<T>/Error, pagination primitives
frontend/src/      app (App Router, role-gated) · components · hooks · lib · schemas
```

Clean Architecture with a hand-rolled **CQRS dispatcher**: every request flows `Controller → Dispatcher → [Authorization decorator → Validation decorator] → Handler → Repository`. Handlers return `Result<T>` — no exceptions for business failures.

**Authorization is a pipeline, not a convention.** Every command and query declares who may send it (`[RequiresRole]` / `[RequiresAuthentication]` / `[AllowAnonymous]`), and `AddApplication()` **refuses to build the DI container** if any message lacks a declaration — a new endpoint physically cannot ship unguarded. Row-level rules ("is this *your* assignment?") live behind `IAssignmentAccess` / `ISubmissionAccess`.

## Data model

14 tables, UUID keys, Fluent API only, audit columns and Postgres `xmin` optimistic concurrency on every entity, soft delete via global query filters (`users`, `assignments`).

`users` · `academic_years` · `classes` · `courses` · `class_courses` · `student_enrollments` · `teacher_assignments` · `assignments` · `assignment_files` · `submissions` · `submission_files` · `refresh_tokens` · `password_setup_tokens` · `notifications`

A **class** is a grade + section held apart (never a composed string). A **course offering** (`class_courses`) is the join a class studies once, taught by at most one teacher. An **enrollment** names its academic year, so repeating a grade is expressible. Delete behaviour is set per relationship — Cascade for pure children, Restrict where deleting destroys meaning, Set null for historical attribution.

## Engineering decisions worth a look

- **Password setup by single-use link.** User + SHA-256-hashed token + notification are written in **one transaction**, so no account can exist without a way to reach it. Redeeming it revokes every refresh token the account held. Every rejection (unknown / expired / spent / deactivated) returns the same error, so nothing can be used as an account oracle.
- **Two independent brute-force defences.** Per-IP rate limit on credential endpoints (stops one wordlist) plus a per-account DB lockout (stops a distributed spray). A lockout returns the same `401` as a wrong password.
- **Transactional outbox for email.** The notification row commits with the change that caused it; a background dispatcher sends afterwards with exponential backoff and `FOR UPDATE SKIP LOCKED` claims, so a dead SMTP server can never fail a publish/submit/grade, and multiple API instances never double-send. Admins see pending/sent/failed and can retry.
- **File uploads are validated by bytes, not headers** — extension allow-list, size cap, per-owner count cap, **magic-byte signature check**, sanitized names, and authorization-checked streaming download (no static file serving).
- **Deterministic paging.** Sort keys are a per-endpoint allow-list, each with a unique tiebreaker, so paging can never repeat or skip a row; page size capped server-side at 100.
- **Dashboards aggregate server-side.** Nine charts are grouped reads behind three role-scoped endpoints — no screen ships a table of submissions to the browser to count it there.
- **JWT + rotating refresh token** in an httpOnly cookie; reuse of a rotated token revokes the whole family.
- **Operability** — Serilog, correlation id on every request/error, RFC 7807 ProblemDetails everywhere, `/health` covering the database.

## Notifications

Real email, sent automatically — nothing is triggered by hand. Six state changes queue a mail in the same transaction that makes the change:

| Event | Recipient |
|---|---|
| Account created | its owner — with the single-use link to set a password |
| Teacher assigned to an offering | that teacher |
| Student enrolled in a class | that student, listing the class's courses |
| Assignment published | every student enrolled in that class |
| Submission received | the teacher who owns the assignment |
| Submission graded | the student who owns it |

Moving a submission back to `Pending` sends nothing — that is bookkeeping, and mailing it would announce marks that were just withdrawn.

The **EC2 deployment sends through a real SMTP provider**, so account-setup links and assignment notices land in the recipient's actual inbox. Locally, Compose points the API at **Mailpit** — a genuine SMTP handshake, every message readable at http://localhost:8025, nothing leaving the machine. With `Email__Host` empty, notifications are still queued and their full contents written to the log; nothing silently does nothing. **Admin → Notifications** exposes the outbox itself: pending/sent/failed counts, each body, the error behind any failure, and a retry.

## Tests

**300+ (xUnit).** `Application.Tests` covers domain invariants, handlers and the authorization pipeline with no external dependencies; `Api.Tests` runs end-to-end through `WebApplicationFactory` against a **real Postgres container** (Testcontainers) — per-endpoint authorization, the submit → grade workflow, DB constraints, throttling, outbox concurrency and dashboard scoping.

## Deployment (AWS EC2)

Single EC2 instance, four containers on one Docker network. Clone on the host, fill in `.env` (`SITE_HOST`, `Jwt__Key`, DB password, SMTP credentials), and ship:

```bash
git clone https://github.com/m-akash/assignment-submission-management-system.git
cd assignment-submission-management-system && cp .env.example .env && nano .env
docker compose -f docker-compose.prod.yml up -d --build
```

| Container | Role |
|---|---|
| `caddy` | Only public listener (80/443). Terminates TLS with an **auto-provisioned Let's Encrypt certificate**, redirects HTTP → HTTPS, routes `/api/*`, `/health`, `/swagger/*` → API and everything else → Next.js |
| `web` | Next.js standalone build (multi-stage image, non-root, `NEXT_PUBLIC_API_URL` baked at build) |
| `api` | ASP.NET Core, non-root, container `HEALTHCHECK` on `/health`, auto-migrates and seeds on boot |
| `postgres` | Bound to `127.0.0.1` only — never reachable from the internet |

- **HTTPS without a domain**: `SITE_HOST` is an `sslip.io` hostname derived from the instance's public IP (`35.154.209.160` → `35-154-209-160.sslip.io`), which resolves back to that IP and lets Caddy pass the ACME challenge — a real trusted certificate with no DNS registration.
- **State survives redeploys** — Postgres data and uploaded files are host bind mounts (`/data/pgdata`, `/data/submissions`), not container layers.
- **Sized for a small instance** — per-container memory limits keep API + web + Postgres + Caddy inside ~1 GB.
- Secrets (`Jwt__Key`, SMTP credentials, DB password) come from the host `.env`; nothing real is committed.

## Assumptions & known limits

Self-registration is disabled (admin-provisioned, closed system). Custom identity over ASP.NET Core Identity, one `ApplicationUser` discriminated by a `Role` enum. One submission per student per assignment, updated in place. One teacher per offering. A submission **is** its attachments — students hand in files, never prose. All timestamps UTC, single tenant.

Not built: virus scanning, deadline reminders, forgot-password, plagiarism detection, frontend test suite, CI pipeline. File storage is local disk behind `IFileStorage` (S3 is a drop-in swap) — that, plus in-memory per-instance rate limiting, is what still pins the API to one machine.
