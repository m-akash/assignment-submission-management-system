# Assignment & Submission Management System

A production-grade, role-based **Assignment & Submission Management System** for a school/college — built as part of **OnnoRokom Projukti Limited**'s Assistant Software Engineer recruitment project.

> Admins manage the organisation (users, classes, subjects, teacher assignments). Teachers create/publish assignments and grade submissions. Students browse assignments for their class, submit answers (text and/or file), and track marks and feedback.

---

## 📌 Project Status

✅ **Completed.** All API controllers, business requirements (B1-B7), unit/integration tests, Next.js frontend app, and Docker Compose configurations are fully implemented.

## 🧱 Tech Stack

**Backend** — ASP.NET Core 10 · C# · Clean Architecture · EF Core · PostgreSQL · JWT (access + refresh) · FluentValidation · Serilog · Swashbuckle · Mapperly · xUnit + FluentAssertions + Moq

**Frontend** — Next.js 15 (App Router) · TypeScript · Tailwind CSS · Lucide React · Axios

**Database** — PostgreSQL (UUID keys, Fluent API, soft delete, optimistic concurrency)

## 📂 Repository Structure

```
assignment-submission-management-system/
├── backend/        .NET solution (Domain · Application · Infrastructure · Api · Shared + tests)
├── frontend/       Next.js 15 application
└── docker-compose.yml
```

## ▶️ Quick Start

### Running the application with Docker Compose:
```bash
# Run the entire stack (PostgreSQL database, ASP.NET Core API, Next.js frontend)
docker compose up --build
```
- **Frontend App**: [http://localhost:3000](http://localhost:3000)
- **API Swagger Document**: [http://localhost:5080/swagger](http://localhost:5080/swagger)

### Running tests:
```bash
cd backend
dotnet test
```

## 🔐 Demo Accounts

The database is automatically migrated and seeded with the following demo accounts on startup:

| Role    | Email | Password | Details |
|---------|-------|----------|---------|
| Admin   | `admin@assignment.test` | `Password123!` | Can manage all Users, Classes, Subjects, and mappings |
| Teacher | `teacher@assignment.test` | `Password123!` | Assigned to Class 10-A, teaching Mathematics |
| Student | `student@assignment.test` | `Password123!` | Enrolled in Class 10-A |

## 📄 License

Recruitment project — © OnnoRokom Projukti Limited.
