<!-- SYNC_IMPACT_REPORT
Version change: UNVERSIONED → 1.0.0
Bump type: MAJOR (initial ratification — all placeholders filled for the first time)
Modified principles: n/a (first fill, no prior principles to compare)
Added sections:
  - Core Principles: 9 principles fully populated from docs/01-constitution.md
  - Technology Constraints: approved tech stack and key libraries
  - Development Standards: milestone delivery, code quality gates, resilience rules
  - Governance: amendment procedure, versioning policy, compliance review
Removed sections: n/a (template comments stripped; no semantic removal)
Templates reviewed:
  - .specify/templates/plan-template.md ✅
    Constitution Check section retains "[Gates determined based on constitution file]";
    this is intentional — the /speckit-plan command fills it per-feature. No change required.
  - .specify/templates/spec-template.md ✅ No constitution-specific references. No update needed.
  - .specify/templates/tasks-template.md ✅ No constitution-specific references. No update needed.
  - CLAUDE.md ✅ Already aligned; references all 9 principles and correct tech stack.
Deferred items: None. All placeholders resolved.
-->

# Repository Release Management Platform Constitution

## Core Principles

### I. Layered Architecture, Not CQRS

The backend MUST be organised as exactly four .NET projects with strictly downward dependencies:
`RepoManager.Domain` → `RepoManager.Application` → `RepoManager.Infrastructure` → `RepoManager.Api`.
No project MAY depend on a project above it in this chain.

Controllers MUST inject service interfaces directly and call them by use-case name
(e.g., `await _projects.CreateAsync(dto, ct)`). MediatR, command/query handler patterns,
and any equivalent dispatch indirection are prohibited.

One service per aggregate; service methods named for the use case they serve
(e.g., `CreateAsync`, `PublishToConfluenceAsync`, `ReconcileAsync`).
No service file MAY exceed 300 lines; no service method MAY exceed 30 lines —
split by use-case group or extract private helpers when limits approach.

### II. API-First Design

All platform functionality MUST be exposed via a versioned REST API at `/api/v1`.
The OpenAPI/Swagger specification is the authoritative contract between backend and frontend.
The frontend API client MUST be generated from the OpenAPI spec via `openapi-typescript`;
hand-written HTTP calls to the backend are prohibited.
The frontend is a pure consumer of the API and MUST NOT bypass it.

### III. Test-Driven Development

Unit tests are REQUIRED for all domain logic and parsers. `IConventionalCommitParser` is
the primary enforced example: tests MUST be written and confirmed failing before any
implementation begins (Red-Green-Refactor cycle is mandatory for parsers and domain logic).

Integration tests MUST run against a real SQLite database (per-test temp file or Testcontainers).
Mocking the database in integration tests is prohibited.

E2E tests are REQUIRED for the critical release-creation and Confluence-publish flow.

### IV. Security by Default

All API endpoints MUST require JWT bearer authentication, with the sole exceptions of
`/health/live`, `/health/ready`, and `/api/v1/auth/login`.
Write and administrative endpoints MUST carry `[Authorize(Roles = "Admin")]`.

All external secrets (Azure DevOps PATs, Confluence API tokens, Jira API tokens) MUST be
encrypted at rest via `IDataProtectionProvider`. Plaintext secrets in the database are prohibited.

Configuration MUST come from environment variables or `dotnet user-secrets` in development.
Secrets in source code or committed configuration files are prohibited.

JWT tokens carry role claims (`Admin`, `Viewer`), expire after 8 hours, and use
refresh-token rotation. The `/api/v1/auth/setup` bootstrap endpoint MUST auto-disable
after the first Admin account is created.

### V. Observability

Structured logging via Serilog is REQUIRED; production deployments MUST use the JSON sink.
Every inbound HTTP request MUST be logged via `UseSerilogRequestLogging`.
Every call to an external API (Azure DevOps, Confluence, Jira) MUST be logged with its
duration and outcome (success / failure / HTTP status code).

A correlation ID MUST be attached to every request via `CorrelationIdMiddleware` and
propagated through all log entries for that request.

Health check endpoints MUST be live at `/health/live` and `/health/ready` for orchestrator
probes. Services MUST inject `ILogger<T>` for all domain-significant events.

### VI. Simplicity Over Cleverness

Boring, well-documented patterns MUST be preferred over novel or clever ones.
No service file MAY exceed 300 lines; no service method MAY exceed 30 lines.

Features MUST be organised as vertical slices — each feature owns its DTOs, service methods,
and controller endpoints — rather than horizontal abstraction layers.

Premature optimisation is prohibited. Complexity MUST be justified by a measured need;
anticipated future requirements are not sufficient justification.

### VII. Extensibility Where It Matters

Exactly three integration seams are intentionally extensible:

- `IGitProvider` / `IGitProviderFactory` — v1 ships `AzureDevOpsGitProvider`. Adding
  GitHub, GitLab, or Bitbucket MUST require only a new `IGitProvider` implementation.
- `IConfluencePublisher` — Confluence Cloud REST v2 in v1. Swapping to another wiki
  MUST require only a new implementation of this interface.
- `IJiraService` — Jira Cloud REST v3 in v1.

Nothing else MAY be abstracted preemptively. Adding an interface without a concrete,
imminent need to swap implementations is prohibited.

### VIII. User Experience Standards

The frontend MUST use shadcn/ui components consistently. Custom-built primitives that
duplicate functionality already provided by shadcn/ui are prohibited.

Every async action (data fetch, form submit, external sync) MUST display a loading state
and, on failure, a clear human-readable error message.

Destructive or irreversible actions (delete project, publish to Confluence, add ticket to Jira)
MUST require explicit user confirmation before execution.

The UI MUST be keyboard-accessible and MUST target WCAG AA compliance. Dark mode MUST be supported.

### IX. Data Integrity

SQLite is the source of truth for all platform data. WAL mode MUST be enabled.
All writes that cross aggregate boundaries MUST be wrapped in an explicit database transaction.

External data (Azure DevOps repositories, Jira tickets) MUST be cached in the local database
before being referenced. External APIs MUST NOT be called from within request handlers.

All sync jobs MUST be idempotent — re-running the same job MUST produce the same database state.

## Technology Constraints

**Backend runtime**: .NET 10, ASP.NET Core Web API
**Database**: SQLite via `Microsoft.EntityFrameworkCore.Sqlite` (10.x); file at `./data/repomanager.db`
**Approved backend libraries**: FluentValidation, Mapster, Serilog.AspNetCore, Serilog.Sinks.Console,
Serilog.Sinks.File, Swashbuckle.AspNetCore, Markdig, BCrypt.Net-Next, Polly,
Microsoft.Extensions.Http.Polly, HandlebarsDotNet, Microsoft.TeamFoundationServer.Client,
Microsoft.VisualStudio.Services.Client
**Test stack**: xunit, FluentAssertions, Moq, Microsoft.AspNetCore.Mvc.Testing

**Frontend runtime**: React 18+, TypeScript, Vite
**Approved frontend libraries**: Tailwind CSS, shadcn/ui, TanStack Query, Zustand,
React Hook Form + Zod, React Router v6, openapi-typescript, @uiw/react-md-editor,
react-diff-viewer-continued

**Deployment**: Single Docker container; SQLite database mounted as a persistent volume.
Initial admin credentials supplied via environment variables for first-run bootstrap.

## Development Standards

**Milestone delivery**: Implementation proceeds through 11 vertical-slice milestones
(see `docs/04-tasks-guidance.md`). Each milestone MUST end with its designated smoke test
passing before work on the next milestone begins.

**Code quality gates** (enforced per PR):
- All new service logic accompanied by unit or integration tests
- OpenAPI spec regenerated and frontend client updated whenever backend endpoints change
- Correlation ID present in all structured log entries for the changed code path
- No service file over 300 lines and no method over 30 lines introduced

**External API resilience**: All `HttpClient`-based integrations (Jira, Confluence, Azure DevOps)
MUST be registered with a Polly retry policy: 3 retries, exponential backoff, max 30 s total,
triggered on HTTP 429 and 5xx responses.

**Entity mapping**: EF Core entities MUST NOT leak out of `RepoManager.Infrastructure`.
Services accept and return DTO record types; Mapster handles the projection.

## Governance

This constitution supersedes all other architectural guidance in this repository.
When this document conflicts with a README, plan, or task file, this document wins.

**Amendment procedure**:
1. Propose the change with rationale in a PR description.
2. Update this file, increment the version per the policy below, and set `Last Amended`
   to the date of the change (ISO 8601: YYYY-MM-DD).
3. Propagate any impacts to dependent templates (`.specify/templates/`) in the same PR.
4. Update `CLAUDE.md` if the change affects AI-assisted development guidance.

**Versioning policy**:
- MAJOR — backward-incompatible governance change: principle removed, renamed, or
  fundamentally redefined.
- MINOR — new principle or section added, or materially expanded guidance.
- PATCH — clarifications, wording corrections, or non-semantic refinements.

**Compliance review**: Every PR touching `backend/` or `frontend/` source MUST be reviewed
against the nine Core Principles. The "Constitution Check" gate in the plan template
enforces this at the feature-planning stage.

**Runtime guidance**: See `CLAUDE.md` at the repository root for AI-assisted development
guidance derived from this constitution.

**Version**: 1.0.0 | **Ratified**: 2026-05-12 | **Last Amended**: 2026-05-12
