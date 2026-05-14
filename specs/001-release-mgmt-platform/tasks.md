# Tasks: Repository Release Management Platform

**Input**: Design documents from `/specs/001-release-mgmt-platform/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/api-endpoints.md, contracts/service-interfaces.md, quickstart.md

**Tests**: Unit tests are included for `IConventionalCommitParser` (TDD-first, mandatory per Constitution Principle III). Integration tests are included in the Polish phase.

**Organization**: Tasks follow the 11-milestone vertical-slice build order from `docs/04-tasks-guidance.md`, organized by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Include exact file paths in descriptions

## Path Conventions

- Backend: `backend/src/RepoManager.{Domain,Application,Infrastructure,Api}/`
- Frontend: `frontend/src/features/`, `frontend/src/lib/`, `frontend/src/routes/`
- Tests: `backend/tests/RepoManager.{Unit,Integration}Tests/`

---

## Phase 1: Setup (Solution Initialization)

**Purpose**: Create the project skeleton — backend .NET 10 solution, frontend Vite+React+TS scaffold, and Docker skeleton. No domain logic yet.

- [X] T001 Create .NET 10 solution `RepoManager.sln` with four projects (`RepoManager.Domain`, `RepoManager.Application`, `RepoManager.Infrastructure`, `RepoManager.Api`) under `backend/src/` using `dotnet new sln` and `dotnet new classlib`/`webapi`; establish project-to-project references enforcing Domain ← Application ← Infrastructure ← Api
- [X] T002 [P] Add all required NuGet packages to the correct projects: EF Core 10 + SQLite and Mapster to Infrastructure; FluentValidation.AspNetCore, Serilog.AspNetCore, Swashbuckle.AspNetCore, HandlebarsDotNet, Markdig, BCrypt.Net-Next, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.AspNetCore.DataProtection, Polly, Microsoft.TeamFoundationServer.Client to Infrastructure/Api as appropriate in `backend/src/`
- [X] T003 [P] Create frontend project with `npm create vite@latest frontend -- --template react-ts`; install Tailwind CSS, shadcn/ui, TanStack Query, Zustand, React Router v6, React Hook Form, Zod, and openapi-typescript in `frontend/`
- [X] T004 [P] Create `backend/tests/RepoManager.UnitTests/` and `backend/tests/RepoManager.IntegrationTests/` test projects with xunit, FluentAssertions, Moq, and Microsoft.AspNetCore.Mvc.Testing packages; add to solution
- [X] T005 [P] Create skeleton `docker-compose.yml` at repo root with backend service stub and `./backend/data` SQLite volume mount; create `backend/data/.gitkeep`

---

## Phase 2: Foundational (Milestone 1 — Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before any user story can be implemented: Domain entities, EF Core context and migration, JWT auth, all middleware, Serilog, OpenAPI, health endpoints, and the frontend auth shell.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Domain Layer

- [X] T006 Create all 14 Domain entity classes (`User`, `GitProviderConnection`, `Repository`, `Project`, `ProjectRepository`, `Commit`, `Ticket`, `Release`, `ReleaseRepositoryTag`, `ReleaseNoteTemplate`, `ConfluenceConnection`, `JiraConnection`, `JiraRelease`, `JiraTicket`, `ReleaseReconciliation`) with all columns from data-model.md in `backend/src/RepoManager.Domain/Entities/`
- [X] T007 [P] Create all Domain enums (`Role`, `ProviderType`, `ReleaseStatus`, `ChangeType`, `JiraStatusCategory`) in `backend/src/RepoManager.Domain/Enums/`

### Application Layer

- [X] T008 [P] Create custom exception classes (`NotFoundException`, `ConflictException`, `ValidationException`, `ExternalServiceException`) with constructors matching the service-interfaces contract in `backend/src/RepoManager.Application/Common/Exceptions/`
- [X] T009 [P] Create `IAuthService` interface and all auth DTOs (`LoginDto`, `SetupDto`, `TokenResponseDto`, `CreateUserDto`, `UpdateUserDto`, `UserDto`) in `backend/src/RepoManager.Application/Auth/`

### Infrastructure — Persistence

- [X] T010 Implement `AppDbContext` with all 14 `DbSet<>` properties, EF Core Fluent API configurations for PK/FK/unique indexes/JSON columns (`JiraProjectKeys`, `Snapshot` via `HasConversion`)/cascade deletes as specified in data-model.md in `backend/src/RepoManager.Infrastructure/Persistence/AppDbContext.cs`
- [X] T011 Add the initial EF Core migration (`dotnet ef migrations add InitialCreate`); add WAL and foreign-key pragmas (`PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;`) executed on `DbContext` after first open in `backend/src/RepoManager.Infrastructure/Persistence/`

### Infrastructure — Auth

- [X] T012 Implement `AuthService`: BCrypt password hashing (work factor 12), JWT access token issuance (8-hour HS256 with `sub` and `role` claims), refresh token generation (256-bit random, SHA-256 hashed before storage, 30-day expiry), and token rotation on each `/auth/refresh` call in `backend/src/RepoManager.Infrastructure/Auth/AuthService.cs`

### API Layer — Middleware and Infrastructure

- [X] T013 [P] Implement `CorrelationIdMiddleware` that reads `X-Correlation-Id` request header (or generates a new GUID) and adds it to response headers and Serilog's `LogContext` in `backend/src/RepoManager.Api/Middleware/CorrelationIdMiddleware.cs`
- [X] T014 [P] Implement `GlobalExceptionHandler` (`IExceptionHandler`) mapping `NotFoundException→404`, `ConflictException→409`, `ValidationException→400`, `ExternalServiceException→502` to RFC 7807 ProblemDetails with `traceId` field in `backend/src/RepoManager.Api/Middleware/GlobalExceptionHandler.cs`
- [X] T015 [P] Configure Serilog structured request logging with correlation ID and user ID enrichers; wire into `Program.cs` before all middleware in `backend/src/RepoManager.Api/Program.cs`
- [X] T016 [P] Configure OpenAPI/Swagger with JWT Bearer security definition (`securitySchemes` + `security` at operation level) in `backend/src/RepoManager.Api/Program.cs`
- [X] T017 [P] Implement health endpoints `GET /health/live` (process alive, 200 OK) and `GET /health/ready` (DB ping via `CanConnectAsync`, 200/503) with no JWT requirement in `backend/src/RepoManager.Api/Program.cs` (using `MapHealthChecks`)
- [X] T018 Implement `AuthController` with `POST /api/v1/auth/setup` (checks `Users.AnyAsync(u => u.Role == Admin)` and returns 410 Gone if true before processing), `POST /api/v1/auth/login`, and `POST /api/v1/auth/refresh` endpoints in `backend/src/RepoManager.Api/Controllers/AuthController.cs`
- [X] T019 [P] Configure JWT Bearer authentication middleware with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`; register `[Authorize]` as default policy on all controllers; register `[Authorize(Roles = "Admin")]` named policy in `backend/src/RepoManager.Api/Program.cs`
- [X] T020 [P] Register all services, FluentValidation validators, Mapster profiles, EF Core (`UseSqlite`), `IDataProtectionProvider`, and Polly-wrapped `HttpClient`s in `backend/src/RepoManager.Infrastructure/DependencyInjection.cs`

### Frontend — Auth Shell

- [X] T021 Implement login page with email + password form using React Hook Form + Zod (`z.string().email()`, min 8 password), calling `POST /api/v1/auth/login`, storing tokens in Zustand auth store on success in `frontend/src/features/auth/LoginPage.tsx`
- [X] T022 [P] Configure Zustand auth store storing JWT access token, refresh token, decoded role claim, and `isAuthenticated` flag; implement `useAuthStore` hook in `frontend/src/lib/authStore.ts`
- [X] T023 [P] Configure React Router v6 route tree with `<ProtectedRoute>` (redirects to `/login` if unauthenticated) and `<AdminRoute>` (shows 403 if Viewer role) wrapper components in `frontend/src/routes/`
- [X] T024 [P] Implement first-run setup page at `/setup` with Admin email + password + confirm-password form calling `POST /api/v1/auth/setup`; redirect to `/login` on 201; show informational page if 410 Gone in `frontend/src/features/auth/SetupPage.tsx`

**Checkpoint**: Foundation ready — admin can log in and reach a placeholder dashboard. User story implementation can begin.

---

## Phase 3: User Story 1 — Admin Configures Integrations and Creates a Project (Priority: P1) 🎯 MVP

**Goal**: Admin connects the platform to Azure DevOps (with PAT encryption), triggers a repo sync, marks repos as tracked, configures Jira and Confluence connections, creates a logical project, and assigns repositories — completing the full onboarding flow.

**Independent Test**: Starting from a fresh installation, an Admin can: (1) enter Azure DevOps org URL + PAT and click "Test Connection" with a success response; (2) click "Sync now" and see all org repos listed with default branch and web URL; (3) mark selected repos as tracked; (4) create a logical project "Apply" with a badge colour; (5) assign repos and mark one as primary; (6) configure Jira + Confluence connections with successful test responses.

### Milestone 2: Git Provider Integration

- [X] T025 [P] [US1] Define `IGitProvider` interface (5 methods: `TestConnectionAsync`, `ListRepositoriesAsync`, `ListTagsAsync`, `GetCommitsBetweenAsync`, `GetMergedPullRequestsAsync`) and provider DTOs (`ProviderConnection`, `RepoSummary`, `TagInfo`, `CommitInfo`, `PullRequestInfo`) in `backend/src/RepoManager.Application/GitProviders/IGitProvider.cs`
- [X] T026 [P] [US1] Define `IGitProviderFactory` interface (`GetProvider(ProviderType)`) in `backend/src/RepoManager.Application/GitProviders/IGitProviderFactory.cs`
- [X] T027 [US1] Implement `AzureDevOpsGitProvider` using `VssBasicCredential` + `VssConnection` + `GitHttpClient` for all 5 `IGitProvider` methods; handle pagination for large repo/commit lists in `backend/src/RepoManager.Infrastructure/GitProviders/AzureDevOpsGitProvider.cs`
- [X] T028 [P] [US1] Implement `GitProviderFactory` returning `AzureDevOpsGitProvider` for `ProviderType.AzureDevOps` (throw `NotSupportedException` for unknown types) in `backend/src/RepoManager.Infrastructure/GitProviders/GitProviderFactory.cs`
- [X] T029 [US1] Implement `IGitProviderConnectionService` interface and `GitProviderConnectionService` with `CreateAsync` (encrypt PAT via `IDataProtectionProvider`), `ListAsync`, `UpdateAsync`, and `TestAsync` (decrypt PAT, call `IGitProvider.TestConnectionAsync`, persist `LastTestStatus`) in `backend/src/RepoManager.Infrastructure/GitProviders/GitProviderConnectionService.cs`
- [X] T030 [P] [US1] Create `GitProviderConnectionsController` with `POST /api/v1/integrations/git/test`, `GET /api/v1/integrations/git`, `POST /api/v1/integrations/git`, and `PUT /api/v1/integrations/git/{id}` endpoints in `backend/src/RepoManager.Api/Controllers/GitProviderConnectionsController.cs`

### Milestone 3: Repository Sync

- [X] T031 [US1] Implement `GitProviderConnectionService.SyncAsync`: decrypt PAT, call `IGitProvider.ListRepositoriesAsync`, upsert each repo to `Repositories` table on `(GitProviderConnectionId, ExternalId)` unique key, update `LastSyncedAt`; method is idempotent — re-running updates existing rows in `backend/src/RepoManager.Infrastructure/GitProviders/GitProviderConnectionService.cs`
- [X] T032 [P] [US1] Add `POST /api/v1/integrations/git/{id}/sync` endpoint returning `202 Accepted` to `GitProviderConnectionsController` in `backend/src/RepoManager.Api/Controllers/GitProviderConnectionsController.cs`
- [X] T033 [US1] Implement `IRepositoryService` interface and `RepositoryService` with `ListAsync` (query params: `connectionId`, `isTracked`, `search` against Name) and `SetTrackedAsync` in `backend/src/RepoManager.Infrastructure/Repositories/RepositoryService.cs`
- [X] T034 [P] [US1] Create `RepositoriesController` with `GET /api/v1/repositories` (query params) and `PATCH /api/v1/repositories/{id}` (`{ "isTracked": true }`) endpoints in `backend/src/RepoManager.Api/Controllers/RepositoriesController.cs`

### Milestone 4: Logical Projects

- [X] T035 [US1] Implement `IProjectService` interface and `ProjectService` with `CreateAsync`, `ListAsync`, `GetAsync`, `UpdateAsync`, `DeleteAsync`, `AssignRepositoryAsync` (enforce at-most-one `IsPrimary` per project in a transaction), `RemoveRepositoryAsync`, and `ConfigureJiraAsync` in `backend/src/RepoManager.Infrastructure/Projects/ProjectService.cs`
- [X] T036 [P] [US1] Create `ProjectsController` with `GET /projects`, `POST /projects`, `GET /projects/{id}`, `PUT /projects/{id}`, `DELETE /projects/{id}`, `POST /projects/{id}/repositories/{repoId}`, `DELETE /projects/{id}/repositories/{repoId}`, and `PUT /projects/{id}/jira` endpoints (all under `/api/v1`) in `backend/src/RepoManager.Api/Controllers/ProjectsController.cs`

### Milestone 9: Jira Integration Foundation

- [X] T037 [P] [US1] Define `IJiraService` interface (4 methods: `TestConnectionAsync`, `ListProjectsAsync`, `SyncFixVersionAsync`, `AddTicketToFixVersionAsync`) and Jira DTOs (`JiraConnectionDto`, `JiraProjectDto`, `JiraReleaseDto`, `JiraTicketDto`) in `backend/src/RepoManager.Application/Jira/IJiraService.cs`
- [X] T038 [US1] Implement `JiraService` with typed `HttpClient` calling Jira Cloud REST v3; attach Polly retry policy (3 retries, exponential backoff `2^attempt` seconds, triggered on HTTP 429 and 5xx) via `AddPolicyHandler` in `backend/src/RepoManager.Infrastructure/Jira/JiraService.cs`
- [X] T039 [P] [US1] Implement `IJiraConnectionService` interface and `JiraConnectionService` (get single active connection, upsert with API token encryption via `IDataProtectionProvider`, test by calling `IJiraService.TestConnectionAsync`, list projects) in `backend/src/RepoManager.Infrastructure/Jira/JiraConnectionService.cs`
- [X] T040 [P] [US1] Create `JiraController` with `POST /api/v1/integrations/jira/test`, `GET /api/v1/integrations/jira`, `PUT /api/v1/integrations/jira`, and `GET /api/v1/integrations/jira/projects` endpoints in `backend/src/RepoManager.Api/Controllers/JiraController.cs`

### Confluence Connection Setup

- [X] T041 [P] [US1] Implement `IConfluenceConnectionService` interface and `ConfluenceConnectionService` (get single active connection, upsert with API token encryption, test via `IConfluencePublisher.TestConnectionAsync`) in `backend/src/RepoManager.Infrastructure/Confluence/ConfluenceConnectionService.cs`
- [X] T042 [P] [US1] Create `ConfluenceController` with `POST /api/v1/integrations/confluence/test`, `GET /api/v1/integrations/confluence`, and `PUT /api/v1/integrations/confluence` endpoints in `backend/src/RepoManager.Api/Controllers/ConfluenceController.cs`

### US1 Frontend Settings UI

- [X] T043 [US1] Add `"codegen": "npx openapi-typescript http://localhost:5000/swagger/v1/swagger.json -o src/lib/api.d.ts"` script to `frontend/package.json`; run `npm run codegen` with backend running to generate initial `frontend/src/lib/api.d.ts`
- [X] T044 [US1] Implement Settings → Integrations page with Azure DevOps connection form (name, org URL, PAT fields), "Test Connection" button showing success/failure badge, and "Sync now" button in `frontend/src/features/settings/integrations/GitSettings.tsx`
- [X] T045 [P] [US1] Implement Settings → Repositories page: paginated repository list table (name, default branch, web URL, tracked toggle), search input, and Azure DevOps project filter dropdown in `frontend/src/features/settings/repositories/RepositoriesPage.tsx`
- [X] T046 [P] [US1] Implement Settings → Jira integration form (base URL, email, API token) with "Test Connection" action in `frontend/src/features/settings/integrations/JiraSettings.tsx`
- [X] T047 [P] [US1] Implement Settings → Confluence integration form (base URL, email, API token) with "Test Connection" action in `frontend/src/features/settings/integrations/ConfluenceSettings.tsx`
- [X] T048 [US1] Implement Settings → Projects page: project list sidebar, project detail panel (name/description/color editor, Jira project keys + fix-version pattern + auto-create toggle, Confluence space key + parent page ID), and assigned repositories table with assign/unassign/set-primary actions in `frontend/src/features/settings/projects/`

**Checkpoint**: US1 smoke test — admin connects to Azure DevOps, sees "test connection succeeded", syncs repos, marks some as tracked, creates project "Apply", assigns 3 repos (one as primary), and configures Jira + Confluence connections.

---

## Phase 4: User Story 2 — Tech Lead Views Changes Since Last Release (Priority: P2)

**Goal**: A Viewer opens a project and immediately sees all commits and tickets — across all project repositories — that have not yet been released, grouped by Jira ticket, filterable by change type and contributor, with three view modes.

**Independent Test**: A Viewer opens a project whose repositories have commits since their last semver version tag and verifies: (1) summary cards show correct counts; (2) Tickets view groups commits by Jira ticket ID with expandable commit list; (3) non-conventional commits appear in a distinct "Unscoped" section; (4) Commits and Contributors view modes work; (5) type filter and ticket-ID search filter the list.

### Milestone 5: Conventional Commit Parsing (TDD-First — Mandatory)

- [X] T049 [US2] Write failing unit tests for `IConventionalCommitParser` covering: all 12 standard types (feat/fix/docs/style/refactor/perf/test/build/ci/chore/revert/unknown), scope matching Jira pattern `^[A-Z]{2,10}-\d+$`, scope not matching Jira pattern, breaking via `!` in header, breaking via `BREAKING CHANGE:` in body, multi-line bodies, empty bodies, and non-conventional messages (`WIP: blah`, `fix stuff`, `Merge pull request #123`) — all tests MUST be red before T050 begins in `backend/tests/RepoManager.UnitTests/ConventionalCommitParserTests.cs`
- [X] T050 [US2] Implement `ConventionalCommitParser` using regex `^(?<type>\w+)(\((?<scope>[^)]+)\))?(?<breaking>!)?:\s*(?<desc>.+)$` for header and body scanning for `BREAKING[ -]CHANGE:` (case-sensitive); Jira scope validation pattern `^[A-Z]{2,10}-\d+$`; all T049 tests must pass in `backend/src/RepoManager.Infrastructure/Commits/ConventionalCommitParser.cs`

### Milestone 5: Commit Sync and Ticket Aggregation

- [X] T051 [US2] Implement `CommitSyncService.SyncAsync`: call `IGitProvider.ListTagsAsync` to find latest semver tag (treat entire history as unreleased if no tags), call `IGitProvider.GetCommitsBetweenAsync`, parse each commit via `IConventionalCommitParser`, upsert to `Commits` table on `(RepositoryId, Sha)` (idempotent), update `Repository.LastSyncedAt` in `backend/src/RepoManager.Infrastructure/Commits/CommitSyncService.cs`
- [X] T052 [P] [US2] Implement ticket aggregation projection in `CommitSyncService`: for a given `(RepositoryId, fromTag, toTag)` range, drop existing `Tickets` rows and reinsert with computed `PrimaryType` (precedence: breaking→feat→fix→first non-chore type→chore), `CommitCount`, `ContributorCount`, `FirstCommittedAt`, `LastCommittedAt` in `backend/src/RepoManager.Infrastructure/Commits/CommitSyncService.cs`
- [X] T053 [US2] Implement `IRepositoryService.GetChangesAsync`: query commits for `fromTag`→HEAD, support `groupBy` (ticket groups from `Tickets` table / flat `Commits` / grouped by `AuthorEmail`), type filter, contributor filter, ticket-ID search; return shape matching the API contract in `backend/src/RepoManager.Infrastructure/Repositories/RepositoryService.cs`
- [X] T054 [P] [US2] Add `GET /api/v1/repositories/{id}/changes` endpoint (query params: `groupBy`, `type`, `contributor`, `search`) returning the full change response (summary, groups, unscoped) to `RepositoriesController` in `backend/src/RepoManager.Api/Controllers/RepositoriesController.cs`

### Milestone 6: Project Change Visibility

- [X] T055 [US2] Implement `IProjectService.GetChangesAsync`: fan out `GetChangesAsync` across all project repositories, aggregate summary counts, deduplicate Jira ticket IDs (ticket in multiple repos appears once with contributing repos listed) in `backend/src/RepoManager.Infrastructure/Projects/ProjectService.cs`
- [X] T056 [P] [US2] Add `GET /api/v1/projects/{id}/changes` endpoint to `ProjectsController` in `backend/src/RepoManager.Api/Controllers/ProjectsController.cs`

### US2 Frontend Change Views

- [X] T057 [US2] Implement per-repository detail screen with summary cards (commit count, ticket count, breaking changes, contributor count) and view-mode tab bar (Tickets / Commits / Contributors) in `frontend/src/features/repositories/RepositoryDetail.tsx`
- [X] T058 [P] [US2] Implement Tickets view tab: grouped-ticket list rows (ticket ID linked to Jira, representative title, type badge, commit count, contributor count) with expandable individual commit rows (shortSha, message, author, date) in `frontend/src/features/repositories/TicketGroupList.tsx`
- [X] T059 [P] [US2] Implement "Unscoped" section below the ticket list showing non-conventional commits with a visual warning indicator (distinct background/icon from grouped entries) in `frontend/src/features/repositories/UnscopedBucket.tsx`
- [X] T060 [P] [US2] Implement Commits view tab (flat chronological list) and Contributors view tab (commits grouped by author with per-author commit count) in `frontend/src/features/repositories/`
- [X] T061 [P] [US2] Implement change-type filter dropdown and ticket-ID search input that filter the active view; clear-filters action in `frontend/src/features/repositories/ChangeFilters.tsx`
- [X] T062 [US2] Implement project dashboard page with per-repository summary cards and aggregate project-level metrics row (total commits, unique tickets, breaking changes, unique contributors) in `frontend/src/features/projects/ProjectDashboard.tsx`

**Checkpoint**: US2 smoke test — Viewer sees "23 commits, 7 tickets, 1 breaking" rolled up across project repos; ticket grouping, unscoped bucket, and all view modes work correctly.

---

## Phase 5: User Story 3 — Tech Lead Creates a Release and Publishes to Confluence (Priority: P3)

**Goal**: A tech lead runs the release wizard: confirms version and change range, selects a template, edits auto-generated notes (Breaking → Features → Fixes → Other), optionally runs Jira reconciliation, previews the Confluence output, and publishes — creating a locked Confluence page.

**Independent Test**: Create a release for a project with at least one repo containing commits since a version tag, complete all wizard steps, and verify a Confluence page is created with the release notes and the page URL is stored on the release record and visible to all authenticated users.

### Milestone 7: Release Note Templates Backend

- [ ] T063 [P] [US3] Define `IReleaseNoteTemplateService` interface and DTOs (`CreateTemplateDto`, `UpdateTemplateDto`, `TemplateDto`) in `backend/src/RepoManager.Application/Templates/`
- [ ] T064 [P] [US3] Implement `ReleaseNoteTemplateService` (CRUD; validate Handlebars compiles without error via `Handlebars.Compile(template)` on create/update; enforce name uniqueness) in `backend/src/RepoManager.Infrastructure/Templates/ReleaseNoteTemplateService.cs`
- [ ] T065 [P] [US3] Create `TemplatesController` with `GET /api/v1/templates`, `POST /api/v1/templates`, `PUT /api/v1/templates/{id}`, and `DELETE /api/v1/templates/{id}` endpoints in `backend/src/RepoManager.Api/Controllers/TemplatesController.cs`

### Milestone 7: Release Creation and Notes Generation

- [ ] T066 [US3] Implement `IReleaseService` interface and `ReleaseService.CreateAsync`: compute semver suggestion (any breaking commit → major bump, else any feat → minor bump, else patch bump), gather ticket groups from `Tickets` table, render Handlebars template passing `{ project, sections: { breaking, features, fixes, other }, contributors, repositories }` model with section priority enforced (ticket with any `IsBreaking = true` commit goes to Breaking regardless of other types); exclude unscoped commits from notes in `backend/src/RepoManager.Infrastructure/Releases/ReleaseService.cs`
- [ ] T067 [P] [US3] Implement `ReleaseService.UpdateNotesAsync`: update `EditedNotesMarkdown` only when `Status == Draft`; throw `ConflictException("Release is published and locked")` if `Status == Published` in `backend/src/RepoManager.Infrastructure/Releases/ReleaseService.cs`
- [ ] T068 [P] [US3] Create `ReleasesController` with `POST /api/v1/projects/{id}/releases`, `GET /api/v1/releases/{id}`, and `PUT /api/v1/releases/{id}` (update edited notes, Draft only) endpoints in `backend/src/RepoManager.Api/Controllers/ReleasesController.cs`

### Milestone 8: Confluence Publishing

- [ ] T069 [P] [US3] Define `IConfluencePublisher` interface (`TestConnectionAsync`, `CreateOrUpdatePageAsync`, `CreateChecklistPageAsync`) and `PublishResult` / `ConfluenceConnectionDto` records in `backend/src/RepoManager.Application/Confluence/IConfluencePublisher.cs`
- [ ] T070 [US3] Implement Markdown → Confluence Storage Format converter as a Markdig `HtmlRenderer` subclass overriding rendering for: headings (`<h1>`–`<h4>`), bold (`<strong>`), italic (`<em>`), inline code (`<code>`), fenced code blocks (`<ac:structured-macro ac:name="code">`), links (`<a href>`), and bullet/ordered lists (`<ul>/<ol>/<li>`) in `backend/src/RepoManager.Infrastructure/Confluence/MarkdownToConfluenceRenderer.cs`
- [ ] T071 [US3] Implement `ConfluencePublisher.CreateOrUpdatePageAsync` using Confluence Cloud REST v2: POST to create page if `existingPageId` is null; PUT to update content if `existingPageId` is provided (idempotent retry); return `PublishResult` with `PageId` and `PageUrl` in `backend/src/RepoManager.Infrastructure/Confluence/ConfluencePublisher.cs`
- [ ] T072 [US3] Implement `ReleaseService.PublishAsync`: convert `EditedNotesMarkdown` (falling back to `GeneratedNotesMarkdown`) via Markdown renderer, call `ConfluencePublisher.CreateOrUpdatePageAsync` with stored `ConfluencePageId` for idempotent retry, store `ConfluencePageId` + `ConfluencePageUrl`, set `Status = Published` and `PublishedAt` in a single transaction in `backend/src/RepoManager.Infrastructure/Releases/ReleaseService.cs`
- [ ] T073 [P] [US3] Add `POST /api/v1/releases/{id}/publish` endpoint to `ReleasesController` in `backend/src/RepoManager.Api/Controllers/ReleasesController.cs`

### Milestone 10: Jira Reconciliation

- [ ] T074 [US3] Implement `IReleaseReconciliationService` interface and `ReleaseReconciliationService.ReconcileAsync`: call `IJiraService.SyncFixVersionAsync` (auto-create fix version if `Project.AutoCreateFixVersion = true`), load Git ticket IDs from `Commits` for the release range, compute matched/Jira-only/Git-only set diff; when `Project.MatchSubtasksToParents = true` resolve subtask parent keys via `JiraTickets.ParentKey`; calculate `MatchRatePercent = matched / (matched + jiraOnly + gitOnly) * 100` in `backend/src/RepoManager.Infrastructure/Reconciliation/ReleaseReconciliationService.cs`
- [ ] T075 [P] [US3] Persist reconciliation result to `ReleaseReconciliations` table as an upsert (one row per release on `ReleaseId` unique key; re-running replaces the snapshot JSON) in `backend/src/RepoManager.Infrastructure/Reconciliation/ReleaseReconciliationService.cs`
- [ ] T076 [P] [US3] Add `POST /api/v1/releases/{id}/reconcile`, `GET /api/v1/releases/{id}/reconciliation`, and `POST /api/v1/releases/{id}/reconciliation/jira-tickets` (Admin only — `[Authorize(Roles = "Admin")]`) endpoints to `ReleasesController` in `backend/src/RepoManager.Api/Controllers/ReleasesController.cs`

### US3 Frontend Release Wizard

- [ ] T077 [US3] Implement release wizard step 1: version number input pre-populated with semver suggestion, editable, plus per-repository change range summary table (fromTag, toTag, commit count) in `frontend/src/features/releases/wizard/StepConfirmRange.tsx`
- [ ] T078 [P] [US3] Implement release wizard step 2: template selector showing all templates with the project's default template pre-selected in `frontend/src/features/releases/wizard/StepSelectTemplate.tsx`
- [ ] T079 [P] [US3] Implement release wizard step 3: markdown editor (left pane) with live Confluence-format preview (right pane) rendered client-side in `frontend/src/features/releases/wizard/StepEditNotes.tsx`
- [ ] T080 [P] [US3] Implement release wizard step 4: optional Jira reconciliation panel with matched/Jira-only/Git-only bucket lists, match-rate percentage badge, and "Add to Jira fix version" button visible only to Admin role in `frontend/src/features/reconciliation/ReconciliationPanel.tsx`
- [ ] T081 [P] [US3] Implement release wizard step 5: publish confirmation dialog; on success display Confluence page URL as a clickable link and transition release to read-only state in `frontend/src/features/releases/wizard/StepPublish.tsx`
- [ ] T082 [P] [US3] Enforce read-only mode for published releases: disable markdown editor, hide "Publish"/"Edit" buttons, display Confluence page URL link prominently in `frontend/src/features/releases/ReleaseDetail.tsx`

**Checkpoint**: US3 smoke test — tech lead generates release notes for a project; notes are correctly grouped (Breaking → Features → Fixes → Other); Confluence page is created at the configured space and URL is stored on the release record.

---

## Phase 6: User Story 4 — Admin Manages Release Note Templates (Priority: P4)

**Goal**: An Admin creates and edits reusable Handlebars release note templates with a live preview pane rendering sample data. One template is marked as default per project.

**Independent Test**: Admin creates a template using `{{project.name}}`, `{{#each sections.breaking}}`, and `{{contributors}}` placeholders; live preview updates in real time; template is available and pre-selected during release creation for the configured project.

- [ ] T083 [US4] Implement Templates settings page: template list table (name, default badge, edit/delete actions), "New Template" button opening a slide-over form with name field and `ContentTemplate` textarea, available variable reference table (project.name, version, sections.*, contributors, repositories) in `frontend/src/features/settings/templates/TemplatesPage.tsx`
- [ ] T084 [P] [US4] Implement live preview pane in the template editor that renders the Handlebars template against a static sample data object on every keystroke using `handlebars` npm package client-side in `frontend/src/features/settings/templates/TemplateEditor.tsx`
- [ ] T085 [P] [US4] Add default-template selector to the project settings detail panel (dropdown of all templates); saving calls `PUT /api/v1/projects/{id}` with `releaseNoteTemplateId`; selected template is pre-filled in release wizard step 2 in `frontend/src/features/settings/projects/ProjectDetail.tsx`

**Checkpoint**: US4 smoke test — Admin creates a template, sees live preview update, marks it as project default, and confirms it is pre-selected in the release wizard.

---

## Phase 7: User Story 5 — Admin Manages Users (Priority: P5)

**Goal**: An Admin creates user accounts (email, password, Admin or Viewer role). Viewer accounts can log in and read all data but every write/config action is unavailable.

**Independent Test**: Admin creates a Viewer account; Viewer logs in and can view project dashboards and change views; all create/edit/delete/publish/config/add-to-jira actions are hidden or disabled with an appropriate message; no write endpoint accepts requests from the Viewer JWT.

- [ ] T086 [US5] Create `UsersController` with `GET /api/v1/users`, `POST /api/v1/users`, `PUT /api/v1/users/{id}` (role, isActive, password), and `DELETE /api/v1/users/{id}` endpoints — all require `[Authorize(Roles = "Admin")]` in `backend/src/RepoManager.Api/Controllers/UsersController.cs`
- [ ] T087 [US5] Implement Users settings page: user list table (email, role chip, isActive, last login), "New User" form (email + password + role select), "Deactivate" confirmation action in `frontend/src/features/settings/users/UsersPage.tsx`
- [ ] T088 [P] [US5] Enforce UI role-gating throughout all feature pages: any button, action, or form that calls a write endpoint must check `authStore.role === "Admin"` and render as hidden or disabled with tooltip for Viewers in `frontend/src/routes/` and all feature components

**Checkpoint**: US5 smoke test — Admin creates a Viewer; Viewer logs in and sees all read data; zero write actions appear or succeed anywhere in the UI.

---

## Phase 8: Polish & Cross-Cutting Concerns (Milestone 11 Hardening)

**Purpose**: Production readiness — observability completeness, integration test coverage, and Docker packaging.

- [ ] T089 [P] Add structured Serilog log statements at `Information` level (including `UserId` from JWT `sub` claim and `CorrelationId` from `LogContext`) to every state-changing method in all `backend/src/RepoManager.Infrastructure/` service implementations
- [ ] T090 [P] Write integration tests for the auth flow using `WebApplicationFactory<Program>` against a real in-memory SQLite DB: setup endpoint creates Admin and returns 410 on second call; login with correct credentials returns tokens; login with wrong password returns 401; refresh token rotation issues new pair and invalidates old token in `backend/tests/RepoManager.IntegrationTests/AuthTests.cs`
- [ ] T091 [P] Implement multi-stage `Dockerfile`: stage 1 `dotnet publish` backend; stage 2 `npm run build` frontend; stage 3 ASP.NET Core runtime image copying both artifacts; backend serves compiled frontend static files from `wwwroot/` in `Dockerfile`
- [ ] T092 [P] Finalize `docker-compose.yml`: backend service with `Dockerfile` build context, `./backend/data:/app/data` volume, all required env vars (`Jwt__Secret`, `DataProtection__Key`, `ConnectionStrings__DefaultConnection`, `Jwt__Issuer`, `Jwt__Audience`), and `/health/ready` health check in `docker-compose.yml`
- [ ] T093 Validate the full `quickstart.md` developer flow end-to-end: `dotnet ef database update` applies migration, `dotnet run` starts backend on `:5000`, Swagger UI loads at `/swagger`, `npm run codegen` generates `api.d.ts`, `npm run dev` starts frontend on `:5173`, and the first-run bootstrap creates an Admin account at `/setup`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1)**: Depends on Phase 2
- **Phase 4 (US2)**: Depends on Phase 2 and US1 (repos must exist and be synced before commits can be fetched)
- **Phase 5 (US3)**: Depends on Phase 2, US1 (Jira/Confluence connections, projects), and US2 (commit data); template backend T063–T065 must complete before the release wizard is functional
- **Phase 6 (US4)**: Template backend (T063–T065) is a prerequisite for US3 release wizard; template UI can run concurrently with US3
- **Phase 7 (US5)**: Depends only on Phase 2 (auth infrastructure) — can proceed in parallel with US1–US4
- **Phase 8 (Polish)**: Depends on all stories being complete

### Critical Sequential Chains

- T006 (entities) → T010 (DbContext) → T011 (migration) → any local smoke testing
- T049 (failing tests) → T050 (parser implementation) — tests MUST be red first
- T043 (`npm run codegen`) requires backend running on `:5000`
- T029 (GitProviderConnectionService) → T031 (SyncAsync) → T033 (RepositoryService.ListAsync)
- T066 (ReleaseService.CreateAsync) → T072 (PublishAsync) → T073 (publish endpoint)

### Parallel Opportunities Within Each Phase

**Phase 2**: After T006 entities, T007/T008/T009/T013/T014/T015/T016/T017/T022/T023/T024 can all proceed in parallel.

**Phase 3 (US1)**: T025/T026 (interfaces) in parallel; T028/T029 after T027; T037/T041 (Jira/Confluence interfaces) in parallel with T025–T036; T044/T045/T046/T047 (frontend forms) in parallel after T043.

**Phase 4 (US2)**: T049→T050 strictly sequential; T052/T053 in parallel after T051; T058/T059/T060/T061 (frontend tabs) in parallel after T057.

**Phase 5 (US3)**: T063/T064/T065/T069 in parallel; T070/T071 in parallel (both are Confluence infra); T077/T078/T079/T080/T081 (wizard steps) in parallel after T066 endpoint is complete.

---

## Parallel Example: User Story 2

```
# Strictly sequential (TDD mandate):
T049: Write failing ConventionalCommitParser tests  ← red
T050: Implement ConventionalCommitParser            ← green

# After T050, these two can run in parallel:
T051: CommitSyncService.SyncAsync (depends on parser)
T052: Ticket aggregation projection (same file, different method — coordinate with T051 developer)

# After T051+T052:
T053: RepositoryService.GetChangesAsync
T054: GET /repositories/{id}/changes endpoint
```

---

## Implementation Strategy

### MVP (User Story 1 Only)

1. Complete Phase 1 (Setup)
2. Complete Phase 2 (Foundational — critical blocker)
3. Complete Phase 3 (US1 — integrations + project setup)
4. **STOP and VALIDATE**: Admin smoke test passes end-to-end
5. Deploy or demo

### Incremental Delivery

1. Setup + Foundational → infrastructure ready
2. US1 → Admin can configure all integrations and create a project **(MVP)**
3. US2 → Viewers can see changes since last release
4. US3 → Tech leads can create and publish releases to Confluence
5. US4 + US5 → Template management and user management
6. Polish → Production-ready Docker deployment

---

## Notes

- `[P]` tasks touch different files with no pending dependencies — safe to parallelise
- `[Story]` labels map tasks to user stories for traceability and independent delivery
- T049 → T050 is strictly sequential: tests MUST fail before implementation starts (Constitution Principle III enforcement for `IConventionalCommitParser`)
- No CQRS, no MediatR — controllers call service interfaces directly (`await _service.MethodAsync(dto, ct)`)
- No custom UI component primitives — use shadcn/ui components exclusively
- External APIs (Azure DevOps, Jira, Confluence) are never called from HTTP request handlers — all data is cached in SQLite first; sync operations are always triggered explicitly
- All sync operations must be idempotent — re-running any sync produces the same DB state
- Service file hard limit: 300 lines; method hard limit: 30 lines — split by use-case group if exceeded
