# Assignment & Submission Management System

A role-based **Assignment & Submission Management System** for a school/college — built for **OnnoRokom Projukti Limited**'s Assistant Software Engineer recruitment project.

> Admins manage the organisation (users, classes, courses, course offerings, enrollments, teacher assignments) and can view everything. Teachers create/publish assignments for a course offering they teach and grade submissions. Students browse assignments for the classes they are enrolled in, submit answers (text and/or file), and track marks and feedback. Account creation, being assigned a course, being enrolled, publishing, submitting and grading each queue an email notification automatically — and a new account's mail carries a single-use link to choose a password, never a password.

## Contents

- [Main Features](#main-features)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Getting Started — Docker Compose (recommended)](#getting-started--docker-compose-recommended)
- [Getting Started — Running Manually](#getting-started--running-manually)
- [Data Model](#data-model)
- [Database](#database)
- [Account Setup](#account-setup)
- [Email Notifications](#email-notifications)
- [Running Tests](#running-tests)
- [Demo Credentials](#demo-credentials)
- [API Documentation](#api-documentation)
- [Assumptions](#assumptions)
- [Known Limitations](#known-limitations)
- [License](#license)

## Main Features

- **JWT authentication** with a rotating refresh token (httpOnly cookie) and role-based authorization (Admin / Teacher / Student), enforced entirely server-side.
- **Admin** — manage users, classes, courses, **course offerings** (which courses each class studies), **student enrollments**, and teacher-to-offering assignments; view every assignment and submission, and inspect the email outbox.
- **Teacher** — create/update/delete assignments scoped to an offering they are assigned to teach, publish or keep as draft, view submissions, grade with marks + feedback, and change submission status.
- **Student** — see assignments for every class they are enrolled in, submit a text answer and/or file attachments before the deadline, update a submission before the deadline (if the assignment allows resubmission), and view marks/feedback once graded.
- **Email notifications** — six events mail automatically: an account being created, a teacher gaining a course, a student being enrolled, an assignment being published (to every student in the class), a submission arriving (to the owning teacher), and grading (to the student). Written as a transactional outbox and sent by a background sweep, so a slow or unconfigured mail server never fails the request that caused it — see [Email Notifications](#email-notifications).
- **Password setup by single-use link** — a new account's welcome email carries an expiring, single-use link to choose a password rather than the password itself, and redeeming it drops any session opened with the admin-set one. See [Account Setup](#account-setup).
- **File uploads** on submissions — allow-listed extensions, size cap, magic-byte signature validation (the actual file header is checked, not just the `Content-Type` the browser sends), sanitized filenames, and authorization-checked streamed download (no static file serving).
- **Business rules enforced server-side**, not just in the UI — see [Business rules](#business-rules-enforced) below.
- Pagination, sorting, filtering and free-text search on every list endpoint (users, classes, courses, offerings, enrollments, assignments, submissions, notifications).
- Structured logging (Serilog), consistent error responses (RFC 7807 ProblemDetails), and a Swagger/OpenAPI document with JWT auth wired in.

### Business rules enforced

- A student only sees assignments for classes they are **enrolled in**. Enrollment is read per request rather than carried in the access token, so an admin moving a student between classes takes effect on their next request instead of when their token expires.
- A submission can only be updated before the deadline, and only if the assignment allows resubmission.
- A teacher can only manage (create/update/delete/publish/grade) assignments scoped to an offering they are assigned to teach. An admin creating work on a teacher's behalf must name a teacher who is already assigned to that offering — otherwise the assignment's own author could not publish or grade it.
- A class can only be paired with a course once, and a teacher can only be assigned to an offering once.
- A student cannot be removed from their only class — moving them means enrolling in the new class first.
- Marks assigned during grading can never exceed the assignment's maximum marks.
- An assignment moves Draft → Published only (one-way); students cannot see or submit to a Draft assignment.
- A teacher can grade only published assignments and can change a submission's status when necessary.
- One submission per student per assignment (enforced by a unique DB constraint); submitting after the deadline marks it `Late`.
- An offering cannot be removed while a teacher is assigned to it or any assignment exists for it — dropping it must not silently take student work with it.

## Technology Stack

**Backend** — ASP.NET Core 10 (C#) · Clean Architecture (Domain / Application / Infrastructure / Api / Shared) · EF Core 10 + Npgsql · PostgreSQL · JWT (access + rotating refresh) · FluentValidation · Serilog · Swashbuckle (Swagger/OpenAPI) · Mapperly (source-generated mapping) · xUnit + FluentAssertions + Moq + Testcontainers.PostgreSql

**Frontend** — Next.js (App Router) · React · TypeScript · Tailwind CSS · shadcn/ui (Radix primitives) · TanStack Query · React Hook Form + Zod · Axios

**Database** — PostgreSQL, UUID primary keys, Fluent API configuration (no data annotations), soft delete via global query filters, optimistic concurrency on `Assignment`/`Submission`.

## Project Structure

```
assignment-submission-management-system/
├── backend/
│   ├── AssignmentSystem.slnx
│   └── src/
│       ├── AssignmentSystem.Domain/          # Entities, value objects, enums — no external dependencies
│       ├── AssignmentSystem.Application/     # Use-cases (commands/queries + handlers), validators, DTOs, specifications
│       ├── AssignmentSystem.Infrastructure/  # EF Core DbContext, migrations, repositories, JWT/auth, file storage, DB seeder
│       ├── AssignmentSystem.Api/             # Controllers, middleware, Swagger, JWT wiring, Program.cs (composition root)
│       └── AssignmentSystem.Shared/          # Result<T>/Error, pagination, cross-cutting types shared by Application + Api
│   └── tests/
│       ├── AssignmentSystem.Application.Tests/  # Domain + handler + validator unit tests (Moq)
│       └── AssignmentSystem.Api.Tests/          # Integration tests against a real Postgres container (Testcontainers)
├── frontend/
│   └── src/
│       ├── app/
│       │   ├── (auth)/login/       # Public login page
│       │   └── (dashboard)/        # Role-gated pages: assignments, classes, courses, class-courses, notifications, submissions, teacher-mappings, users
│       ├── components/
│       │   ├── ui/                 # shadcn/ui primitives
│       │   ├── layout/             # Dashboard shell, sidebar nav
│       │   ├── shared/             # Cross-page pieces (pagination, search input, status badge, empty/error states...)
│       │   └── features/           # Role-specific views: admin CRUD dialogs, assignment views, submission dialogs
│       ├── hooks/                  # TanStack Query hooks per resource
│       ├── lib/                    # Axios client, query keys, formatting helpers
│       ├── schemas/                # Zod schemas mirroring backend DTOs
│       └── proxy.ts                # Route gate (Next's renamed middleware) — redirects based on cookie presence only
└── docker-compose.yml              # postgres + mailpit + api + web, with healthchecks and named volumes
```

## Getting Started — Docker Compose (recommended)

Prerequisites: Docker and Docker Compose.

```bash
# from the repository root
docker compose up --build
```

This starts four services:

| Service | URL | Notes |
|---|---|---|
| PostgreSQL | `localhost:5432` | Data persisted in the `pgdata` named volume |
| Mailpit | http://localhost:8025 | Every notification email lands here — a real SMTP server that delivers nothing onward |
| API | http://localhost:5080 | Swagger at http://localhost:5080/swagger, health at http://localhost:5080/health |
| Frontend | http://localhost:3000 | |

On first boot the API container applies EF Core migrations and seeds demo data automatically (`Database__AutoMigrate` / `Database__SeedOnStartup`, both `true` by default) — **no manual table creation is needed**. Uploaded submission files persist in the `submission-files` named volume, mounted at `/data/submissions` inside the API container.

To reset to a clean database, stop the stack and remove the volumes:

```bash
docker compose down -v
```

## Getting Started — Running Manually

Prerequisites: .NET 10 SDK, Node.js 20+, and a PostgreSQL 17 instance (or run just the backing services from Docker: `docker compose up -d postgres mailpit`).

Running the API outside compose means the `Email__Host=mailpit` hostname does not resolve — use `Email__Host=localhost` (with `Email__Port=1025`, `Email__UseSsl=false`) to reach the Mailpit container, or leave `Email__Host` empty to have notifications logged instead of sent.

### 1. Database

Make sure a Postgres instance is reachable with a database matching the connection string below (the defaults in `appsettings.json` already assume `assignment_system` / `assignments` / `assignments` on `localhost:5432` — matching the Docker Compose Postgres service, so running that one service is the easiest path).

### 2. Backend API

```bash
cd backend
dotnet restore
dotnet run --project src/AssignmentSystem.Api
```

The API applies migrations and seeds demo data on startup, same as under Docker. It listens on the URL printed in the console (typically `http://localhost:5080`); Swagger is at `/swagger`.

To manage schema changes explicitly instead of relying on auto-migrate, install the EF Core CLI tool once (`dotnet tool install --global dotnet-ef`) and run from `backend/`:

```bash
dotnet ef database update --project src/AssignmentSystem.Infrastructure --startup-project src/AssignmentSystem.Api
```

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
```

The repository's single `.env.example` (at the repo root) covers both apps — for the frontend, only `NEXT_PUBLIC_API_URL` is relevant. Create `frontend/.env.local` with:

```
NEXT_PUBLIC_API_URL=http://localhost:5080
```

(base origin only — **no** `/api/v1` suffix, the client appends that itself). The app runs at http://localhost:3000.

## Data Model

The full ERD lives in [`docs/erd.mermaid`](docs/erd.mermaid). Two junction tables carry most
of the design weight, and they are the parts worth explaining:

**`class_courses` — the course offering.** "This class studies this course," as a real row
rather than something inferred from whichever teacher happens to be mapped. Everything
downstream points at the offering: a `teacher_assignment` says who teaches one, and an
`assignment` is scoped to one. That is what stops an admin mapping a teacher to a
(class, course) pair the class does not actually study, and it means an assignment's class
and course can never disagree with each other — there is one column, not two.

**`student_enrollments` — class membership.** A row per (student, class) instead of a
`users.class_id` column, so a student can sit in more than one class (a repeated grade, an
elective cohort, a mid-year transfer where both memberships must stay visible) and the date
they joined is recorded. This is the gate for the rule that a student only sees work for
their own classes.

A few other decisions the diagram states but are worth calling out:

- **`uuid` primary keys, not `bigint`.** Ids travel in URLs and JWTs, so non-guessable and
  non-enumerable matters more here than eight bytes a row.
- **`assignments` carries its author (`teacher_id`) directly** and holds no reference to the
  teaching mapping. "May this teacher publish or grade this?" is answered by looking the
  mapping up, so an admin removing a mapping cannot orphan the authorship of work already set.
- **Attachments are two tables**, not one polymorphic one: the authorization rules differ
  completely (a teacher owns assignment material, a student owns their own attachments), and
  a shared table would need filtering by a discriminator on every check.
- **`notifications` has no foreign key to the assignment or submission it refers to.** The
  outbox is a record of mail sent and must outlive what it was about; an FK would either
  block the delete or take the history with it.

## Database

- **Engine:** PostgreSQL. The schema is highly relational (users ↔ classes ↔ courses ↔ offerings ↔ assignments ↔ submissions, with FK/cascade rules and several composite unique constraints), which is a better fit than a document model for the required relationships and integrity guarantees — see [Assumptions](#assumptions).
- **Schema creation:** handled entirely by EF Core migrations under `backend/src/AssignmentSystem.Infrastructure/Migrations/`, applied automatically on API startup (or manually via `dotnet ef database update`, see above). No SQL scripts need to be run by hand.
- **Seed data:** applied automatically and idempotently on startup (`DbSeeder`, skips if the admin account already exists). It creates the three demo accounts plus a broader sample dataset — 10 classes, 12 courses, 15 course offerings, 12 teachers, 15 students with their enrollments, 15 teaching assignments, 15 assignments (13 published, 2 draft), and 15 submissions spread across every status (Pending, Submitted, Graded, Late) — so the system looks populated immediately rather than empty.
- **No notifications are seeded**, deliberately: they are a consequence of publishing, submitting or grading, and a manufactured backlog would mean a fresh checkout tries to email fifteen fictional addresses the moment it starts. Publish an assignment from the UI to watch the outbox fill.
- **The offering/enrollment migration backfills existing data.** `AddClassCoursesEnrollmentsAndNotifications` derives offerings from the (class, course) pairs already in use, repoints assignments and teaching mappings at them, and copies `users.class_id` into `student_enrollments` before dropping the column — so an existing database keeps its assignments and class rosters rather than getting a valid schema full of zero GUIDs. Its `Down` reverses the same way, with one loss it documents: a student in several classes collapses back to their earliest, because the old column could hold only one.

## Account Setup

An admin creates accounts, so the person who will use one is not present when it is made.
Getting the account to them is the gap that has to be closed, and **emailing a password is
not how to close it**: mail is plaintext through relays nobody here operates, it sits in a
mailbox indefinitely, and it lands in logs and backups on the way. A password that has been
emailed should be treated as public.

So the account-created mail carries a **single-use link** instead:

1. `CreateUserHandler` creates the user, issues a `PasswordSetupToken`, and queues the
   welcome notification carrying the plaintext token — all in one transaction, so an account
   can never exist with no way for its owner to reach it.
2. Only the token's SHA-256 hash is stored, exactly as for refresh tokens. The plaintext
   exists in the email and nowhere else, so a database dump cannot be replayed against the
   endpoint and a lost link means issuing a new one rather than looking the old one up.
3. The recipient opens `/set-password?token=…`. The page checks the link with
   `GET /api/v1/auth/set-password` **before** showing the form, so a dead link says so
   instead of being discovered after a password has been typed twice.
4. `POST /api/v1/auth/set-password` verifies the token, sets the password, marks the token
   consumed, and revokes every refresh token the account held. Setting a password is where
   the account changes hands, so sessions opened with the admin's password do not survive it.

Both endpoints are anonymous by necessity — the caller has no password yet, which is the
point — with possession of the token as the authorisation. Every rejection returns the same
error whatever the real reason (unknown, expired, spent, deactivated): distinguishing them
would let someone trying tokens tell "not a token" from "a real token, just used".

The token is single-use and short-lived (`Auth__PasswordSetupTokenHours`, default 48). The
link is built from `Email__AppBaseUrl`, so that must point at the frontend; with it unset the
mail says so plainly rather than shipping a broken URL.

**The admin still types an initial password** — the API requires one and it remains a working
fallback — but nothing ever transmits it, so in practice the link is how an account reaches
its owner. Two deliberate gaps: there is no *forgot password* flow (the same token machinery
would serve one, but nothing issues a token outside account creation), and an admin cannot
re-send a setup link from the UI yet, so an expired link currently means recreating the
account or an admin resetting the password directly.

## Email Notifications

Six events send mail. Nobody triggers any of them by hand — each is queued by the command
handler that performs the change, in the same transaction:

| Event | Recipient | Queued by |
| --- | --- | --- |
| An account is **created** | its owner, with a link to set a password | `CreateUserHandler` |
| A teacher is assigned to a course offering | that teacher | `CreateTeacherAssignmentHandler` |
| A student is enrolled in a class | that student | `CreateEnrollmentHandler`, and `CreateUserHandler` for their first class |
| An assignment is **published** | every student enrolled in its class | `PublishAssignmentHandler` |
| A submission is **received** | the teacher who owns the assignment | `SubmitAssignmentHandler` |
| A submission is **graded** | the student who owns it | `ReviewSubmissionHandler` |

Enrollment is a class membership rather than a per-course one, so the enrollment mail is one
message about the class, listing the courses that class studies — not one message per course.
Creating a student with a class is two events, so it sends two mails: one that the account
exists, one that they are in a class.

**No password is ever emailed** — see [Account Setup](#account-setup) for what is sent instead.

**`docker compose up` sends real mail with no credentials.** The stack includes
[Mailpit](https://mailpit.axllent.org/) (MailHog's maintained successor) and the API's
`Email__*` defaults point at it, so every notification goes over a genuine SMTP handshake and
is readable at **http://localhost:8025**. Nothing leaves the machine.

To send to real addresses, set the `Email__*` variables in `.env` (see `.env.example`) —
change `Host`, `Port`, `UseSsl` and `FromAddress` together, since the defaults are Mailpit's
plaintext ones. For Gmail use an App Password, not the account password.

**It also works with no mail server at all.** With `Email__Host` empty, the notification is
still written to the `notifications` table and its full contents written to the API log
instead of being sent; the rows are marked `Sent` because there is nothing to retry against.
Nothing silently does nothing — but check the log, not an inbox.

**Why an outbox rather than sending inline.** The notification row is written in the *same
transaction* as the change that caused it, and a background service (`NotificationDispatcher`,
swept every `Email__DispatchIntervalSeconds`) sends it afterwards. That buys four things a
direct `SendEmailAsync` in the handler cannot:

- A slow, down, or misconfigured SMTP server cannot fail the publish, submit or grade that
  triggered it.
- Nothing is silently lost when one is — the row is still there.
- Retries are bounded (`Email__MaxDeliveryAttempts`, default 3) and the failure reason is
  recorded on the row, so "why did this not arrive?" has an answer.
- Tests assert on rows instead of intercepting mail.

Retrying can duplicate an email that was accepted just before a crash. That is the right
trade here: a student receiving one notice twice is a nuisance, never receiving it is a
missed deadline.

**Admin → Notifications** shows the outbox: pending/sent/failed counts, the exact subject and
body of each message, the error behind any failure, a **Send queued now** button that runs a
sweep immediately, and a retry action on rows that used up their attempts. Teachers and
students can read the same endpoint but are scoped server-side to mail addressed to them —
including when they pass someone else's `recipientId`.

## Running Tests

```bash
cd backend
dotnet test
```

This runs both test projects:

- **`AssignmentSystem.Application.Tests`** — pure unit tests (domain invariants, command/query handlers with mocked repositories, file-upload policy) — no external dependencies.
- **`AssignmentSystem.Api.Tests`** — integration tests using `WebApplicationFactory` against a **real PostgreSQL container** spun up via Testcontainers, so foreign keys, unique constraints, and optimistic-concurrency conflicts are actually exercised. **Requires Docker to be running** — Testcontainers starts and tears down the Postgres container itself.

No frontend automated tests are included (see [Known Limitations](#known-limitations)).

## Demo Credentials

All demo accounts share the password below.

| Role | Email | Password |
|---|---|---|
| Admin | `admin@assignment.test` | `Password123!` |
| Teacher | `teacher@assignment.test` | `Password123!` |
| Student | `student@assignment.test` | `Password123!` |

The seeded data also includes additional teacher (`teacher2@assignment.test` … `teacher12@assignment.test`) and student (`student2@assignment.test` … `student15@assignment.test`) accounts with the same password, useful for exploring multi-class/multi-teacher scenarios (e.g. verifying a teacher only sees their own assignments, or that two students in different classes see different assignment lists).

## API Documentation

With the API running, Swagger UI is available at `/swagger` (e.g. http://localhost:5080/swagger), including the JWT bearer scheme so requests can be authorized directly from the UI. A `/health` endpoint reports database connectivity for container orchestration.

## Assumptions

Documented per the assignment brief's request to record assumptions where requirements weren't explicit:

1. **PostgreSQL over MongoDB** — the domain is highly relational (users ↔ classes ↔ courses ↔ offerings ↔ assignments ↔ submissions) with FK/cascade rules and several composite unique constraints that a relational schema enforces naturally.
2. **`Class` and `Course` are separate, joined by a `ClassCourse` offering** — the brief's "class/course" is really two things: the cohort of students, and the subject being taught. The offering is the pair, and it is what teachers are assigned to and assignments are scoped to.
3. **A student may be enrolled in more than one class** — membership is a `StudentEnrollment` row rather than a column. In practice the seeded students each have one, but the model does not forbid a second, and an admin can add one from the class roster.
4. **One submission per student per assignment** — resubmission updates the existing row until the deadline, gated per-assignment by an `AllowResubmission` flag.
5. **An admin creating an assignment must name its teacher** — there is no "current teacher" to fall back on, and an assignment whose author cannot publish or grade it would be stuck. A teacher's own request ignores any teacher id in the body and uses their token identity, so authorship cannot be spoofed.
6. **Notifications are email-only, and delivery state is visible to admins** — the brief lists notifications as optional; this implements them as a transactional outbox rather than fire-and-forget, and exposes the queue rather than hiding failures in logs. See [Email Notifications](#email-notifications).
7. **Soft delete only for `Assignment` and `User`** — both may need to be restored and are referenced by history (submissions, grading records) that must stay intact. Other entities hard-delete with restrict/cascade rules.
8. **Refresh tokens are included** even though the brief doesn't require them — a browser client needs sessions longer than a short-lived access token safely allows; the refresh token rotates and is stored hashed, with reuse-detection revoking the whole token family.
9. **Custom identity, not ASP.NET Identity** — a single `ApplicationUser` entity discriminated by a `Role` enum, to keep the domain model clean and match "design the entities yourself."
10. **Self-registration is disabled** — this is a closed, Admin-provisioned system (a school doesn't let students register themselves).
11. **Submissions support both a text answer and file attachments** — files are stored on disk (a Docker volume in the compose setup) via an `IFileStorage` abstraction; only metadata lives in the database.
12. **Single-school deployment** — no multi-tenancy.
13. **All timestamps are stored and compared in UTC.**

## Known Limitations

- **No virus/malware scanning on uploaded files.** Uploads are validated by extension allow-list, size cap, and magic-byte signature check, but bytes are not scanned by an AV engine (e.g. ClamAV) before being persisted.
- **Notifications cover six events, and every one of them is a reaction to a state change.** A *deadline approaching* reminder is not implemented, because it needs a scheduled job scanning for upcoming deadlines rather than something to react to, and there is no in-app notification centre for end users (only the admin outbox view). Un-enrolling a student and removing a teacher's course mapping are also silent — the outbox announces access being granted, not withdrawn.
- **No forgot-password flow, and no way to re-send a setup link.** The `PasswordSetupToken` machinery would serve both, but nothing issues a token outside account creation, so an expired link means an admin sets the password directly instead. See [Account Setup](#account-setup).
- **The setup token rides in a URL query string,** so it can reach a proxy or browser-history entry. Mitigated by being single-use and short-lived rather than eliminated; the password itself only ever goes in a POST body.
- **No email templating or localisation.** Bodies are plain text built in code — deliberately, since HTML mail needs escaping, a text fallback, and inline-CSS work to survive real clients — but that means no branding and no per-recipient language.
- **No plagiarism detection** on submitted text answers.
- **Pagination is API-complete but not fully surfaced in the UI** — list endpoints support `page`/`pageSize`/`search`, but the frontend currently fetches up to 100 rows per list rather than exposing page-through controls (acceptable at the current seeded data volume).
- **Multi-class enrollment is supported by the model but thinly surfaced.** The schema, API and rule checks all handle a student in several classes; the UI shows them joined in the header and the user list, but there is no dedicated "my classes" screen.
- **The outbox is never pruned.** Sent notifications accumulate; a real deployment would archive or delete rows past a retention window.
- **No frontend automated test suite** — testing focus (per the brief) went into backend business-rule, authorization and workflow tests.
- **Local/Docker-volume file storage only** — `IFileStorage` is an interface specifically so a cloud backend (S3/Azure Blob) could be swapped in later, but that swap isn't implemented.
- **Single-region, non-HA setup** — this is a local/demo deployment (Docker Compose), not a production topology (no managed Postgres, no reverse proxy/HTTPS termination, no horizontal scaling).

## License

Recruitment project — © OnnoRokom Projukti Limited.
