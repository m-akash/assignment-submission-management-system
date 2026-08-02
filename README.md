# Assignment & Submission Management System

A production-grade, role-based **Assignment & Submission Management System** for a school/college — built as part of **OnnoRokom Projukti Limited**'s Assistant Software Engineer recruitment project.

> Admins manage the organisation (users, classes, subjects, teacher assignments). Teachers create/publish assignments and grade submissions. Students browse assignments for their class, submit answers (text and/or file), and track marks and feedback.

---

## 📌 Project Status

🚧 **Under active development.** See the [Software Architecture Document](.claude/plans/merry-wondering-marble.md) for the full design.

## 🧱 Tech Stack

**Backend** — ASP.NET Core 10 · C# · Clean Architecture · EF Core · PostgreSQL · JWT (access + refresh) · FluentValidation · Serilog · Swashbuckle · Mapperly · xUnit + FluentAssertions + Moq

**Frontend** — Next.js 15 (App Router) · TypeScript · Tailwind CSS · shadcn/ui · React Hook Form · Zod · TanStack Query · Axios

**Database** — PostgreSQL (UUID keys, Fluent API, soft delete where it pays, optimistic concurrency)

## 📂 Repository Structure

```
assignment-submission-management-system/
├── backend/        .NET solution (Domain · Application · Infrastructure · Api · Shared + tests)
├── frontend/       Next.js 15 application
└── docker-compose.yml
```

## ▶️ Quick Start

```bash
# Copy environment template (no real secrets are committed)
cp .env.example .env

# Run the full backend stack
docker compose up --build

# API + Swagger:  http://localhost:5080/swagger
```

_Setup, demo credentials, migrations, and test instructions will be documented here in the final README (Phase 6)._

## 🔐 Demo Accounts

_Provided in the final README once seeding is in place._

| Role    | Email | Password |
|---------|-------|----------|
| Admin   | —     | —        |
| Teacher | —     | —        |
| Student | —     | —        |

## 📄 License

Recruitment project — © OnnoRokom Projukti Limited.
