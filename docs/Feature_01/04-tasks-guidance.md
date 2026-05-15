# /tasks prompt — Suggested Build Order

When `/tasks` runs, ensure tasks are organised into these vertical-slice milestones in this order. Each milestone is independently shippable.

## Milestone 1 — Foundation
- Solution structure and four-project layout
- EF Core DbContext with all entity configurations and initial migration
- Serilog, global exception handler, OpenAPI/Swagger, health endpoints
- Auth: User entity, JWT issuance, password hashing, login endpoint, refresh tokens
- First-run admin bootstrap endpoint (`/auth/setup`)
- Role-based authorization middleware
- Frontend: Vite + React + TS + Tailwind + shadcn scaffolding; login page; protected routes; auth store
- Smoke test: admin can log in and reach a placeholder dashboard

## Milestone 2 — Git provider integration
- `IGitProvider` abstraction and `IGitProviderFactory`
- `AzureDevOpsGitProvider` implementation
- `GitProviderConnection` CRUD: service, controller, DTOs, validators
- Settings → Integrations UI (Azure DevOps connection form, test-connection action)
- PAT encryption via `IDataProtectionProvider`
- Smoke test: admin can connect to Azure DevOps and see "test connection succeeded"

## Milestone 3 — Repository sync
- Repository entity sync from Git provider (sync action)
- Mark tracked/untracked endpoint
- Settings → Repositories UI with search, filter by Azure DevOps project, bulk tracked toggle
- Smoke test: admin can sync and see all org repos, mark some as tracked

## Milestone 4 — Logical projects
- Project entity, ProjectRepositories join, full CRUD
- Repository assignment endpoints (assign, unassign, set primary)
- Settings → Projects UI: project list sidebar, project detail panel, assigned repos table, Confluence target fields
- Smoke test: admin can create project "Apply", assign 3 repos, mark one as primary

## Milestone 5 — Conventional commit parsing and ticket aggregation
- `IConventionalCommitParser` with comprehensive unit tests
- `CommitSyncService` that fetches commits and parses them on the way in
- Ticket aggregation projection
- API endpoint: `GET /repositories/{id}/changes?fromTag=&toTag=&groupBy=ticket|commit|contributor`
- Frontend: per-repository detail screen with summary cards, three view tabs, ticket grouping, unscoped commit warning bucket
- Smoke test: viewer can see all tickets and unscoped commits for a repo since last tag

## Milestone 6 — Project change visibility
- API endpoint: `GET /projects/{id}/changes` aggregating across project repos
- Frontend: project dashboard showing per-repo summary cards and aggregate metrics
- Smoke test: viewer sees "23 commits, 7 tickets, 1 breaking" rolled up across project

## Milestone 7 — Release creation and notes generation
- Release entity, ReleaseRepositoryTags
- ReleaseNoteTemplate CRUD
- Release generation service that produces markdown grouped by ticket using Handlebars templates
- Section ordering: Breaking → Features → Fixes → Other; ticket-promotion logic
- Frontend: release wizard (confirm range → choose template → edit notes → preview)
- Smoke test: tech lead generates release notes for a project; notes are correctly grouped

## Milestone 8 — Confluence publishing
- `IConfluencePublisher` abstraction and Cloud REST v2 implementation
- Confluence connection CRUD and test
- Markdown-to-Confluence-storage-format converter (Markdig + custom renderer)
- Publish endpoint on Release
- Optional checklist page creation
- Frontend: Confluence settings UI; publish button in release wizard with confirmation
- Smoke test: release notes appear in Confluence at the configured space

## Milestone 9 — Jira integration foundation
- `IJiraService` interface and Jira Cloud REST v3 implementation
- HttpClient typed registration with Polly retry policy
- JiraConnection CRUD and test
- Per-project Jira config: select project keys, fix version pattern, auto-create flag, subtask matching flag
- Frontend: Jira settings UI; per-project Jira configuration block
- Smoke test: admin can connect to Jira and configure a project's Jira mapping

## Milestone 10 — Reconciliation
- `IReleaseReconciliationService` orchestrating Jira sync + commit query + set diff
- Subtask-to-parent matching when enabled
- ReleaseReconciliations entity with snapshot persistence
- "Add to Jira" endpoint
- Frontend: reconciliation screen with three buckets, summary cards, Jira links, status badges, "Add to Jira" actions
- Smoke test: tech lead runs reconciliation, sees matched/Jira-only/Git-only correctly, adds a Git-only ticket to Jira

## Milestone 11 — Hardening
- Users CRUD UI
- Templates UI with live preview pane
- Audit logging review (ensure every state-changing operation is logged with user + correlation ID)
- Background sync option (manual-trigger only for v1; document the cron-job pattern for v2)
- Production deployment: Dockerfile, docker-compose.yml, README deployment section, SQLite volume

Each milestone closes with the smoke test passing end-to-end before moving to the next.
