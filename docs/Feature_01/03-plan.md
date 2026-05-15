# /plan prompt

Technical implementation plan for v1.

## Backend

- **.NET 10** with ASP.NET Core Web API
- **Architecture**: Layered (not CQRS) with four projects. Dependencies flow downward only.

  - `RepoManager.Domain` — entities, value objects, enums (`Role`, `ProviderType`, `ReleaseStatus`, `ChangeType`, `JiraStatusCategory`). No external dependencies.
  - `RepoManager.Application` — service interfaces, DTOs (record types), validators (FluentValidation), custom exceptions (`NotFoundException`, `ConflictException`, `ValidationException`, `ExternalServiceException`). Depends only on Domain.
  - `RepoManager.Infrastructure` — service implementations, EF Core 10 DbContext + entity configurations, Azure DevOps client, Confluence client, Jira client, JWT issuance, password hashing, encryption via `IDataProtectionProvider`. Depends on Application and Domain.
  - `RepoManager.Api` — controllers, global exception handler, JWT auth middleware, OpenAPI/Swagger, DI composition root, request-correlation middleware. Depends on all other projects.

- **No CQRS, no MediatR.** Controllers inject service interfaces directly and call methods like `await _projects.CreateAsync(dto, ct)`.

- **Service conventions**:
  - One service per aggregate (`ProjectService`, `ReleaseService`, `RepositoryService`, `GitProviderConnectionService`, `JiraService`, `ConfluencePublisher`, `AuthService`, `ReleaseReconciliationService`, `CommitSyncService`, `ConventionalCommitParser`)
  - Service methods named for the use case (`CreateAsync`, `ListAsync`, `PublishToConfluenceAsync`, `SyncFromConnectionAsync`, `ReconcileAsync`)
  - Every async method takes a `CancellationToken` as the last parameter, defaulting to `default`
  - Services accept and return DTOs (record types), never EF entities
  - Validators injected and called explicitly: `await _validator.ValidateAndThrowAsync(dto, ct)`
  - Errors thrown as typed exceptions, handled centrally by a global `IExceptionHandler` mapping to RFC 7807 ProblemDetails responses

- **No service file over 300 lines, no method over 30 lines.** Split by use-case group when limits approach.

- **Cross-cutting concerns**:
  - Logging: Serilog with `UseSerilogRequestLogging`; services inject `ILogger<T>` for domain events; all external API calls logged with duration and outcome
  - Transactions: services call `_db.Database.BeginTransactionAsync()` only when crossing aggregate boundaries (e.g., publishing a release writes to Releases, ReleaseRepositoryTags, and ReleaseReconciliations)
  - Auth: `[Authorize]` at the controller level for all endpoints; `[Authorize(Roles = "Admin")]` on write/admin endpoints
  - Validation: FluentValidation injected into services; one validator per write DTO

## Database

- **SQLite** via `Microsoft.EntityFrameworkCore.Sqlite`, file at `./data/repomanager.db` (configurable)
- WAL mode enabled
- EF migrations auto-applied on startup in development; explicit migration command in production
- Seed initial Admin user from configuration on first run
- Backup strategy: documented file-copy procedure in README

## Entity Schema (key entities, simplified)

```
Users:                 Id, Email, PasswordHash, Role, IsActive, CreatedAt, LastLoginAt

GitProviderConnections: Id, ProviderType, Name, OrganizationUrl, EncryptedPat,
                       IsActive, LastSyncedAt

Repositories:          Id, GitProviderConnectionId, ExternalId, Name, DefaultBranch,
                       WebUrl, AzureProjectName, IsTracked, LastSyncedAt

Projects:              Id, Name, Description, Color, ReleaseNoteTemplateId,
                       ConfluenceSpaceKey, ConfluenceParentPageId,
                       JiraConnectionId, JiraProjectKeys (json),
                       FixVersionPattern, AutoCreateFixVersion,
                       MatchSubtasksToParents, CreatedAt, UpdatedAt

ProjectRepositories:   ProjectId, RepositoryId, IsPrimary (composite PK)

Commits:               Id, RepositoryId, Sha (indexed), ShortSha, Message,
                       AuthorName, AuthorEmail, CommittedAt,
                       Type, Scope, Description, IsBreaking, IsConventional,
                       JiraTicketId (indexed),
                       UNIQUE (RepositoryId, Sha)

Tickets:               Id, TicketId, RepositoryId, FromTag, ToTag, Title,
                       PrimaryType, IsBreaking, CommitCount, ContributorCount,
                       FirstCommittedAt, LastCommittedAt
                       INDEX (RepositoryId, FromTag, ToTag, TicketId)

Releases:              Id, ProjectId, Version, Status, GeneratedNotesMarkdown,
                       EditedNotesMarkdown, ConfluencePageId, ConfluencePageUrl,
                       CreatedByUserId, CreatedAt, PublishedAt

ReleaseRepositoryTags: ReleaseId, RepositoryId, FromTag, ToTag, CommitCount

ReleaseNoteTemplates:  Id, Name, ContentTemplate, IsDefault

ConfluenceConnections: Id, BaseUrl, Username, EncryptedApiToken, ChecklistTemplate,
                       IsActive, LastTestedAt

JiraConnections:       Id, BaseUrl, Username, EncryptedApiToken, IsActive,
                       LastTestedAt, TestStatus

JiraReleases:          Id, JiraConnectionId, ProjectId, JiraProjectKey,
                       JiraVersionId, Name, Description, IsReleased, ReleaseDate,
                       LastSyncedAt, UNIQUE (JiraConnectionId, JiraVersionId)

JiraTickets:           Id, JiraReleaseId, Key, Summary, Status, StatusCategory,
                       IssueType, AssigneeName, AssigneeEmail, Priority,
                       ParentKey (nullable, for subtasks), LastSyncedAt,
                       UNIQUE (JiraReleaseId, Key)

ReleaseReconciliations: Id, ReleaseId, JiraReleaseId, RunAt, MatchedCount,
                       JiraOnlyCount, GitOnlyCount, MatchRatePercent,
                       Snapshot (json blob of full result)
```

## Git Provider Abstraction

`IGitProvider` interface in Application layer with methods:

- `Task<bool> TestConnectionAsync(ProviderConnection conn, CancellationToken ct)`
- `Task<IEnumerable<RepoSummary>> ListRepositoriesAsync(ProviderConnection conn, CancellationToken ct)`
- `Task<IEnumerable<TagInfo>> ListTagsAsync(ProviderConnection conn, string repoExternalId, CancellationToken ct)`
- `Task<IEnumerable<CommitInfo>> GetCommitsBetweenAsync(ProviderConnection conn, string repoExternalId, string fromRef, string toRef, CancellationToken ct)`
- `Task<IEnumerable<PullRequestInfo>> GetMergedPullRequestsAsync(ProviderConnection conn, string repoExternalId, DateTime since, CancellationToken ct)`

`IGitProviderFactory` resolves the correct implementation based on `ProviderType`. v1 ships `AzureDevOpsGitProvider` using `Microsoft.TeamFoundationServer.Client`.

## Confluence Integration

`IConfluencePublisher` interface with:

- `Task<bool> TestConnectionAsync(ConfluenceConnection conn, CancellationToken ct)`
- `Task<PublishResult> CreateOrUpdatePageAsync(...)`
- `Task<PublishResult> CreateChecklistPageAsync(...)`

Implementation uses Confluence Cloud REST API v2 with Basic auth. Markdown converted to Confluence storage format using Markdig with a custom renderer.

## Jira Integration

`IJiraService` interface with:

- `Task<bool> TestConnectionAsync(JiraConnection conn, CancellationToken ct)`
- `Task<IReadOnlyList<JiraProjectDto>> ListProjectsAsync(Guid connectionId, CancellationToken ct)`
- `Task<JiraReleaseDto> SyncFixVersionAsync(Guid connectionId, string projectKey, string versionName, bool createIfMissing, CancellationToken ct)`
- `Task AddTicketToFixVersionAsync(Guid connectionId, string ticketKey, string versionId, CancellationToken ct)`

Implementation uses plain `HttpClient` (typed via `services.AddHttpClient<IJiraService, JiraService>()`) against Jira Cloud REST API v3:

- List projects: `GET /rest/api/3/project/search`
- Get fix versions: `GET /rest/api/3/project/{key}/versions`
- Get tickets in version: `GET /rest/api/3/search?jql=fixVersion = {id}&fields=summary,status,assignee,issuetype,priority,parent`
- Add ticket to version: `PUT /rest/api/3/issue/{key}` with `fields.fixVersions` update
- Create version: `POST /rest/api/3/version`

Polly retry policy on the HttpClient: 3 retries with exponential backoff on 429 and 5xx, max 30s total per request. Basic auth header from email + API token.

## Reconciliation Service

`IReleaseReconciliationService` orchestrates:

1. Resolve project, validate Jira link exists
2. Format fix version name from pattern
3. Call `IJiraService.SyncFixVersionAsync` to refresh cache
4. Query commits in [last release tag, HEAD] across project repos via existing change-tracking query
5. Compute set diff: matched / Jira-only / Git-only
6. If subtask-to-parent matching enabled, resolve subtask parents and re-match Git-only items
7. Persist `ReleaseReconciliations` row with snapshot JSON
8. Return `ReconciliationResultDto`

## Conventional Commit Parser

`IConventionalCommitParser` in Application layer with `ParsedCommit Parse(string commitMessage)` returning a record with `Type`, `Scope`, `Description`, `IsBreaking`, `IsConventional`, `JiraTicketId`.

Implementation: pure C# regex-based.

- Header regex: `^(?<type>\w+)(\((?<scope>[^)]+)\))?(?<breaking>!)?:\s*(?<desc>.+)$`
- Body breaking regex: `^BREAKING[ -]CHANGE:` anywhere in body
- Jira pattern: `^[A-Z]{2,10}-\d+$` applied to scope

Build incrementally test-first with a comprehensive fixture covering:
- All standard types
- Scope with and without Jira ticket
- Breaking via `!`
- Breaking via body
- Multi-line bodies
- Empty bodies
- Non-conventional (`WIP`, `fix stuff`, merge commits)

Parsing happens during commit sync; results stored on `Commits` table.

## Ticket Aggregation

After commits are synced for a repo, run a projection pass:

- For each unique `(RepositoryId, JiraTicketId)` where commits exist in [last tag, HEAD]
- Insert or update a row in `Tickets`
- Idempotent: drop existing rows for the `(RepoId, FromTag, ToTag)` range, then insert fresh
- Use EF Core `ExecuteUpdate`/bulk operations for efficiency

## Auth

- ASP.NET Core Identity with custom user store backed by EF Core
- JWT bearer tokens, 8-hour expiry, refresh-token rotation
- Role claim included in token
- `BCrypt.Net-Next` for password hashing
- First-run admin bootstrap: if no users exist, expose `/api/v1/auth/setup` endpoint that accepts one admin creation and then auto-disables itself

## Frontend

- **React 18+** with **Vite** and **TypeScript**
- **UI**: shadcn/ui + Tailwind CSS
- **Routing**: React Router v6 with route-level role guards
- **State**: TanStack Query (server state) + Zustand (auth, UI state)
- **Forms**: React Hook Form + Zod
- **API client**: Generated from OpenAPI via `openapi-typescript`
- **Markdown editor**: `@uiw/react-md-editor` for release notes
- **Diff viewer**: `react-diff-viewer-continued` for commit views

## API Endpoints (high-level)

```
POST   /api/v1/auth/setup                                   one-time admin bootstrap
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh

GET    /api/v1/users                                        admin
POST   /api/v1/users                                        admin
PUT    /api/v1/users/{id}                                   admin
DELETE /api/v1/users/{id}                                   admin

POST   /api/v1/integrations/git/test                        admin
GET    /api/v1/integrations/git
POST   /api/v1/integrations/git                             admin
PUT    /api/v1/integrations/git/{id}                        admin
POST   /api/v1/integrations/git/{id}/sync                   admin

POST   /api/v1/integrations/confluence/test                 admin
GET    /api/v1/integrations/confluence
PUT    /api/v1/integrations/confluence                      admin

POST   /api/v1/integrations/jira/test                       admin
GET    /api/v1/integrations/jira
PUT    /api/v1/integrations/jira                            admin
GET    /api/v1/integrations/jira/projects

GET    /api/v1/repositories                                 list, filterable
PATCH  /api/v1/repositories/{id}                            admin; toggle IsTracked
GET    /api/v1/repositories/{id}/changes                    groupBy=ticket|commit|contributor

GET    /api/v1/projects
POST   /api/v1/projects                                     admin
GET    /api/v1/projects/{id}
PUT    /api/v1/projects/{id}                                admin
DELETE /api/v1/projects/{id}                                admin
POST   /api/v1/projects/{id}/repositories/{repoId}          admin; assign
DELETE /api/v1/projects/{id}/repositories/{repoId}          admin
PUT    /api/v1/projects/{id}/jira                           admin; configure Jira mapping
GET    /api/v1/projects/{id}/changes                        aggregated across repos

POST   /api/v1/projects/{id}/releases                       create draft
GET    /api/v1/releases/{id}
PUT    /api/v1/releases/{id}                                edit notes
POST   /api/v1/releases/{id}/publish                        push to Confluence
POST   /api/v1/releases/{id}/reconcile                      run reconciliation
GET    /api/v1/releases/{id}/reconciliation                 latest snapshot
POST   /api/v1/releases/{id}/reconciliation/jira-tickets    Git-only → add to Jira

GET    /api/v1/templates
POST   /api/v1/templates                                    admin
PUT    /api/v1/templates/{id}                               admin
DELETE /api/v1/templates/{id}                               admin

GET    /health/live
GET    /health/ready
```

## Project Structure

```
/repo-release-manager
  /backend
    /src
      RepoManager.Domain/
        Entities/
        Enums/
        ValueObjects/
      RepoManager.Application/
        Common/Exceptions/
        Auth/
        Projects/
          IProjectService.cs
          Dtos/
          Validators/
        Releases/
        Repositories/
        GitProviders/
        Confluence/
        Jira/
        Reconciliation/
        Commits/
        Templates/
      RepoManager.Infrastructure/
        Persistence/
          AppDbContext.cs
          Configurations/
          Migrations/
        Auth/
        Projects/
        Releases/
        Repositories/
        GitProviders/
          AzureDevOpsGitProvider.cs
          GitProviderFactory.cs
        Confluence/
          ConfluencePublisher.cs
        Jira/
          JiraService.cs
        Reconciliation/
          ReleaseReconciliationService.cs
        Commits/
          ConventionalCommitParser.cs
          CommitSyncService.cs
        DependencyInjection.cs
      RepoManager.Api/
        Controllers/
        Middleware/
          GlobalExceptionHandler.cs
          CorrelationIdMiddleware.cs
        Program.cs
        appsettings.json
    /tests
      RepoManager.UnitTests/
      RepoManager.IntegrationTests/
    /data/                                                  SQLite location
  /frontend
    /src
      /features
        /auth
        /projects
        /repositories
        /releases
        /reconciliation
        /settings
          /integrations
          /projects
          /repositories
          /templates
          /users
      /components/ui                                        shadcn
      /lib                                                  api client, utils
      /routes
  /docs
  docker-compose.yml
  README.md
```

## NuGet Dependencies

- `Microsoft.EntityFrameworkCore.Sqlite` (10.x)
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `FluentValidation`
- `FluentValidation.DependencyInjectionExtensions`
- `Mapster` (entity-to-DTO mapping)
- `Microsoft.TeamFoundationServer.Client`
- `Microsoft.VisualStudio.Services.Client`
- `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`
- `Swashbuckle.AspNetCore`
- `Markdig`
- `BCrypt.Net-Next`
- `Polly`, `Microsoft.Extensions.Http.Polly`
- `HandlebarsDotNet` (release note templates)
- Test stack: `xunit`, `FluentAssertions`, `Moq`, `Microsoft.AspNetCore.Mvc.Testing`

## Security

- All external secrets encrypted at rest via `IDataProtectionProvider`
- Configuration via user-secrets in dev, env vars in production
- HTTPS-only in production
- CSP headers, anti-forgery on state-changing endpoints
- Correlation ID per request, logged with every external API call

## Deployment

- Containerised via Docker
- Single deployment unit
- `/health/live` and `/health/ready` for orchestrator probes
- SQLite database mounted as volume for persistence
- Initial admin credentials supplied via environment for first-run bootstrap
