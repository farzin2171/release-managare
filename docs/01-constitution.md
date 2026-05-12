# /constitution prompt

Create a constitution for a Repository Release Management Platform with these principles:

1. **Layered Architecture, Not CQRS**: Backend is organised as four .NET projects with strictly downward dependencies: Domain → Application → Infrastructure → API. Controllers call services directly. No MediatR, no command/query handler explosion. One service per aggregate, methods named for use cases.

2. **API-First Design**: All functionality exposed via versioned REST API at `/api/v1`. Frontend is a pure consumer. OpenAPI/Swagger spec is the contract — generate the frontend client from it.

3. **Test-Driven Development**: Unit tests for domain logic and parsers (conventional commits especially). Integration tests using a real SQLite database via Testcontainers or a per-test temp file. E2E tests for the critical release flow.

4. **Security by Default**:
   - JWT-based auth with role claims (Admin, Viewer)
   - All external secrets (Azure DevOps PATs, Confluence API tokens, Jira API tokens) encrypted at rest via `IDataProtectionProvider`
   - Configuration via environment variables or user-secrets in dev — never in code
   - All API endpoints require authentication except `/health` and `/auth/login`
   - Admin-only endpoints marked with `[Authorize(Roles = "Admin")]`

5. **Observability**: Structured logging via Serilog with JSON sink in production, correlation IDs across requests, health check endpoints at `/health/live` and `/health/ready`, log every external API call (Azure DevOps, Confluence, Jira) with duration and outcome.

6. **Simplicity Over Cleverness**:
   - Prefer boring, well-documented patterns
   - No service file over 300 lines — split by use-case group when it grows
   - No service method over 30 lines — extract private helpers
   - Vertical slice features (a feature owns its DTOs, service, controller endpoints) over horizontal abstraction layers
   - No premature optimisation

7. **Extensibility Where It Matters**:
   - Git provider abstracted behind `IGitProvider` from day one — v1 ships Azure DevOps; adding GitHub/GitLab/Bitbucket later requires only a new implementation
   - Confluence publishing behind `IConfluencePublisher` — could be swapped for Notion or another wiki
   - Jira integration behind `IJiraService` — Jira Cloud only in v1
   - Do not abstract anything else preemptively

8. **User Experience Standards**:
   - Frontend uses shadcn/ui components consistently — no custom-built primitives that duplicate what shadcn provides
   - All async actions show loading and error states with clear messages
   - Destructive actions (delete project, publish to Confluence, add ticket to Jira) require explicit confirmation
   - Keyboard-accessible, WCAG AA target
   - Dark mode supported

9. **Data Integrity**:
   - SQLite as the source of truth, WAL mode enabled
   - All cross-aggregate writes happen inside an explicit transaction
   - Cache external data (Azure DevOps repos, Jira tickets) in our database; never call external APIs from request handlers
   - Sync jobs are idempotent — re-running them should produce the same result
