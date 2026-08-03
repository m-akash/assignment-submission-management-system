# Assignment & Submission Management System

A role-based **Assignment & Submission Management System** for a school/college — built for **OnnoRokom Projukti Limited**'s Assistant Software Engineer recruitment project.

> Admins manage the organisation (users, classes, subjects, teacher assignments) and can view everything. Teachers create/publish assignments for a class + subject and grade submissions. Students browse assignments for their class, submit answers (text and/or file), and track marks and feedback.

## Contents

- [Main Features](#main-features)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Getting Started — Docker Compose (recommended)](#getting-started--docker-compose-recommended)
- [Getting Started — Running Manually](#getting-started--running-manually)
- [Database](#database)
- [Running Tests](#running-tests)
- [Demo Credentials](#demo-credentials)
- [API Documentation](#api-documentation)
- [Assumptions](#assumptions)
- [Known Limitations](#known-limitations)
- [License](#license)

## Main Features

- **JWT authentication** with a rotating refresh token (httpOnly cookie) and role-based authorization (Admin / Teacher / Student), enforced entirely server-side.
- **Admin** — manage users, classes/courses, subjects, and teacher-to-class-subject assignments; view every assignment and submission in the system.
- **Teacher** — create/update/delete assignments scoped to a class + subject they are assigned to, publish or keep as draft, view submissions, grade with marks + feedback, and change submission status.
- **Student** — see assignments for their own class, submit a text answer and/or file attachments before the deadline, update a submission before the deadline (if the assignment allows resubmission), and view marks/feedback once graded.
- **File uploads** on submissions — allow-listed extensions, size cap, magic-byte signature validation (the actual file header is checked, not just the `Content-Type` the browser sends), sanitized filenames, and authorization-checked streamed download (no static file serving).
- **Business rules enforced server-side**, not just in the UI — see [Business rules](#business-rules-enforced) below.
- Pagination, sorting, filtering and free-text search on every list endpoint (users, classes, subjects, assignments, submissions).
- Structured logging (Serilog), consistent error responses (RFC 7807 ProblemDetails), and a Swagger/OpenAPI document with JWT auth wired in.

### Business rules enforced

- A student only sees assignments for their own class.
- A submission can only be updated before the deadline, and only if the assignment allows resubmission.
- A teacher can only manage (create/update/delete/publish/grade) assignments scoped to a class+subject they are assigned to.
- Marks assigned during grading can never exceed the assignment's maximum marks.
- An assignment moves Draft → Published only (one-way); students cannot see or submit to a Draft assignment.
- A teacher can grade only published assignments and can change a submission's status when necessary.
- One submission per student per assignment (enforced by a unique DB constraint); submitting after the deadline marks it `Late`.

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
│       │   └── (dashboard)/        # Role-gated pages: assignments, classes, subjects, submissions, teacher-mappings, users
│       ├── components/
│       │   ├── ui/                 # shadcn/ui primitives
│       │   ├── layout/             # Dashboard shell, sidebar nav
│       │   ├── shared/             # Cross-page pieces (pagination, search input, status badge, empty/error states...)
│       │   └── features/           # Role-specific views: admin CRUD dialogs, assignment views, submission dialogs
│       ├── hooks/                  # TanStack Query hooks per resource
│       ├── lib/                    # Axios client, query keys, formatting helpers
│       ├── schemas/                # Zod schemas mirroring backend DTOs
│       └── proxy.ts                # Route gate (Next's renamed middleware) — redirects based on cookie presence only
└── docker-compose.yml              # postgres + api + web, with healthchecks and named volumes
```

## Getting Started — Docker Compose (recommended)

Prerequisites: Docker and Docker Compose.

```bash
# from the repository root
docker compose up --build
```

This starts three services:

| Service | URL | Notes |
|---|---|---|
| PostgreSQL | `localhost:5432` | Data persisted in the `pgdata` named volume |
| API | http://localhost:5080 | Swagger at http://localhost:5080/swagger, health at http://localhost:5080/health |
| Frontend | http://localhost:3000 | |

On first boot the API container applies EF Core migrations and seeds demo data automatically (`Database__AutoMigrate` / `Database__SeedOnStartup`, both `true` by default) — **no manual table creation is needed**. Uploaded submission files persist in the `submission-files` named volume, mounted at `/data/submissions` inside the API container.

To reset to a clean database, stop the stack and remove the volumes:

```bash
docker compose down -v
```

## Getting Started — Running Manually

Prerequisites: .NET 10 SDK, Node.js 20+, and a PostgreSQL 17 instance (or run just the `postgres` service from Docker: `docker compose up -d postgres`).

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

## Database

- **Engine:** PostgreSQL. The schema is highly relational (users → classes → subjects → assignments → submissions with FK/cascade rules and several unique constraints), which is a better fit than a document model for the required relationships and integrity guarantees — see [Assumptions](#assumptions).
- **Schema creation:** handled entirely by EF Core migrations under `backend/src/AssignmentSystem.Infrastructure/Migrations/`, applied automatically on API startup (or manually via `dotnet ef database update`, see above). No SQL scripts need to be run by hand.
- **Seed data:** applied automatically and idempotently on startup (`DbSeeder`, skips if the admin account already exists). It creates the three demo accounts plus a broader sample dataset — 10 classes, 12 subjects, 12 teachers, 15 students, 15 teacher-assignments, 15 assignments (13 published, 2 draft), and 15 submissions spread across every status (Pending, Submitted, Graded, Late) — so the system looks populated immediately rather than empty.

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

1. **PostgreSQL over MongoDB** — the domain is highly relational (users ↔ classes ↔ subjects ↔ assignments ↔ submissions) with FK/cascade rules and several unique constraints that a relational schema enforces naturally.
2. **"Class/Course" is one entity, `Class`** — a student belongs to exactly one class; teachers are linked to classes via a `TeacherAssignment` (teacher + subject + class).
3. **One submission per student per assignment** — resubmission updates the existing row until the deadline, gated per-assignment by an `AllowResubmission` flag.
4. **Soft delete only for `Assignment` and `User`** — both may need to be restored and are referenced by history (submissions, grading records) that must stay intact. Other entities hard-delete with restrict/cascade rules.
5. **Refresh tokens are included** even though the brief doesn't require them — a browser client needs sessions longer than a short-lived access token safely allows; the refresh token rotates and is stored hashed, with reuse-detection revoking the whole token family.
6. **Custom identity, not ASP.NET Identity** — a single `ApplicationUser` entity discriminated by a `Role` enum, to keep the domain model clean and match "design the entities yourself."
7. **Self-registration is disabled** — this is a closed, Admin-provisioned system (a school doesn't let students register themselves).
8. **Submissions support both a text answer and file attachments** — files are stored on disk (a Docker volume in the compose setup) via an `IFileStorage` abstraction; only metadata lives in the database.
9. **Single-school deployment** — no multi-tenancy.
10. **All timestamps are stored and compared in UTC.**

## Known Limitations

- **No virus/malware scanning on uploaded files.** Uploads are validated by extension allow-list, size cap, and magic-byte signature check, but bytes are not scanned by an AV engine (e.g. ClamAV) before being persisted.
- **No email/in-app notifications** (assignment published, submission graded, deadline approaching).
- **No plagiarism detection** on submitted text answers.
- **Pagination is API-complete but not fully surfaced in the UI** — list endpoints support `page`/`pageSize`/`search`, but the frontend currently fetches up to 100 rows per list rather than exposing page-through controls (acceptable at the current seeded data volume).
- **A student belongs to exactly one class** — there's no support for a student taking subjects across multiple classes/sections.
- **No frontend automated test suite** — testing focus (per the brief) went into backend business-rule, authorization and workflow tests.
- **Local/Docker-volume file storage only** — `IFileStorage` is an interface specifically so a cloud backend (S3/Azure Blob) could be swapped in later, but that swap isn't implemented.
- **Single-region, non-HA setup** — this is a local/demo deployment (Docker Compose), not a production topology (no managed Postgres, no reverse proxy/HTTPS termination, no horizontal scaling).

## License

Recruitment project — © OnnoRokom Projukti Limited.
