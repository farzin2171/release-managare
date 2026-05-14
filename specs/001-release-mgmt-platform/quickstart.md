# Quickstart: Repository Release Management Platform

Developer setup and first-run guide.

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | 20 LTS+ | `node --version` |
| Docker Desktop | latest | for production-mode testing |
| Git | any | for repo access |

---

## Local Development Setup

### 1. Clone and configure secrets

```powershell
git clone <repo-url>
cd repo-release-manager

# Backend secrets (never committed)
cd backend/src/RepoManager.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "<random-256-bit-base64>"
dotnet user-secrets set "DataProtection:Key" "<random-256-bit-base64>"
```

### 2. Create the data directory

```powershell
# From repo root
New-Item -ItemType Directory -Force backend/data
```

### 3. Apply EF Core migrations

```powershell
cd backend

# First time only — creates the SQLite database
dotnet ef database update `
  --project src/RepoManager.Infrastructure `
  --startup-project src/RepoManager.Api
```

### 4. Run the backend

```powershell
cd backend/src/RepoManager.Api
dotnet run
# API available at: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

### 5. Generate the frontend API client

```powershell
cd frontend
npm install

# Requires the backend to be running on :5000
npm run codegen   # runs: npx openapi-typescript http://localhost:5000/swagger/v1/swagger.json -o src/lib/api.d.ts
```

### 6. Run the frontend

```powershell
cd frontend
npm run dev
# SPA available at: http://localhost:5173
```

---

## First-Run Bootstrap

On first startup with an empty database:

1. Navigate to `http://localhost:5173`
2. The app redirects to the one-time setup page `/setup`
3. Enter the initial Admin username (email), password, and confirm password
4. POST to `/api/v1/auth/setup` — this endpoint auto-disables after creating the first Admin
5. You are redirected to the login page; log in with the credentials you just created

After first use, `/api/v1/auth/setup` returns `410 Gone` for any subsequent requests.

---

## Common Development Commands

```powershell
# Build entire backend solution
dotnet build backend/src

# Run all tests
dotnet test backend/tests

# Run only unit tests
dotnet test backend/tests/RepoManager.UnitTests

# Run only integration tests
dotnet test backend/tests/RepoManager.IntegrationTests

# Run a specific test class
dotnet test --filter "FullyQualifiedName~ConventionalCommitParser"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api

# Frontend build
cd frontend && npm run build

# Frontend lint
cd frontend && npm run lint
```

---

## Environment Variables (Production)

| Variable | Required | Description |
|----------|----------|-------------|
| `ConnectionStrings__DefaultConnection` | yes | SQLite file path, e.g. `Data Source=/data/repomanager.db` |
| `Jwt__Secret` | yes | 256-bit base64 secret for JWT signing |
| `Jwt__Issuer` | yes | Token issuer claim (e.g. `https://repomanager.internal`) |
| `Jwt__Audience` | yes | Token audience claim |
| `DataProtection__Key` | yes | Key material for `IDataProtectionProvider` |
| `Bootstrap__AdminEmail` | optional | Pre-seed admin email for first-run setup |
| `Bootstrap__AdminPassword` | optional | Pre-seed admin password (min 8 chars) |
| `Serilog__MinimumLevel__Default` | no | Default `Information` |

---

## Docker Compose (local full-stack)

```powershell
# From repo root
docker compose up --build

# API: http://localhost:5000
# SPA: http://localhost:5173 (or served from same origin in production build)
```

The `docker-compose.yml` mounts `./backend/data` as a volume so the SQLite database persists between container restarts.

---

## Project Structure Quick Reference

```
backend/src/RepoManager.Domain/         — entities, enums, value objects
backend/src/RepoManager.Application/   — service interfaces, DTOs, validators, exceptions
backend/src/RepoManager.Infrastructure/ — EF Core, service implementations, external clients
backend/src/RepoManager.Api/           — controllers, middleware, DI, Program.cs
backend/tests/RepoManager.UnitTests/   — parser + domain logic tests (TDD-first)
backend/tests/RepoManager.IntegrationTests/ — API integration tests with real SQLite
frontend/src/features/                 — one folder per domain feature
frontend/src/lib/api.d.ts              — generated OpenAPI TypeScript client (DO NOT EDIT)
specs/001-release-mgmt-platform/       — this feature's design artifacts
docs/                                  — constitution, spec, plan, task guidance
```

---

## Health Checks

```
GET /health/live   → 200 OK (process is alive)
GET /health/ready  → 200 OK (DB connection is available)
```

Both endpoints are unauthenticated. Use them for Docker/k8s liveness and readiness probes.
