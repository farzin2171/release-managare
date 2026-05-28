# Implementation Plan: Project Page Templates

**Branch**: `012-project-page-templates` | **Date**: 2026-05-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/012-project-page-templates/spec.md`

## Summary

Evolve the release wizard's single-template, content-only output into a **per-project, multi-page, auto-filled** Confluence publishing system. Administrators bind one or more Handlebars templates to a project, each with a kind (release notes, checklist, custom), page-title template, parent-page override, and `LinkFromReleaseNotes` flag. When the wizard opens, the system auto-resolves version, previous version, per-repo tags, Jira fix-version names, ticket buckets, contributors, and optional reconciliation summary into a `ReleaseRenderContext`, renders every bound template, and presents tabbed editable previews. On publish, pages are created or updated in sort order, with cross-linking handled automatically. Technical approach: extend the existing layered .NET 10 architecture with one new entity (`ProjectTemplateBinding`), one new value-bag (`ReleaseRenderContext`), one orchestrator (`IReleasePagePreparationService`), and one helper (`IVersionResolver`); reuse the existing `Handlebars.Net`, `IConfluencePublisher`, `IGitProvider`, and `IJiraService`.

## Technical Context

**Language/Version**: C# 13 on .NET 10 (matches existing platform).
**Primary Dependencies**: ASP.NET Core, EF Core 10, Handlebars.Net, Markdig, FluentValidation, Polly (all already in solution). Frontend: React 18, Vite, shadcn/ui, plus new `@dnd-kit/core` and `@dnd-kit/sortable` for binding reorder.
**Storage**: SQLite via EF Core (existing); one new table `ProjectTemplateBindings`, one JSON-serialised owned collection `ProjectCustomVariables` on `Projects`, one new enum column `VersionBumpStrategy` on `Projects`.
**Testing**: xUnit for backend, Vitest + React Testing Library for frontend, snapshot tests for Handlebars rendering against fixtures. Integration test runs the end-to-end preview → publish flow against an in-memory SQLite database and a mock Confluence publisher.
**Target Platform**: Self-hosted web service (Linux container) + browser SPA, identical to existing platform deployment target.
**Project Type**: Web application — backend in `RepoManager.*` projects, frontend in `web/`.
**Performance Goals**: `/releases/preview` returns in under 2 seconds for a project with 3 repos, 30 commits, 15 tickets (cold cache). Wizard tab switch under 100ms (state held client-side).
**Constraints**: No new external integrations; no breaking changes to `IConfluencePublisher`, `IGitProvider`, `IJiraService` contracts; backward-compatible migration mandatory.
**Scale/Scope**: ~10–50 projects per deployment, ~5–10 bindings per project, ~30 commits per release median. No multi-tenancy.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The project's constitution mandates layered architecture (no CQRS), strict layer dependencies, 300-line service ceiling, 30-line method ceiling, structured logging with correlation IDs, secrets via environment variables, and JWT auth with Admin/Viewer roles.

- **Layered architecture**: Pass. New types fall cleanly into existing layers: `ProjectTemplateBinding` and `VersionBumpStrategy` in `Domain`; `ReleaseRenderContext`, `IReleasePagePreparationService`, `IVersionResolver` in `Application`; EF configuration and migrations in `Infrastructure`; controllers in `Api`.
- **No CQRS / no MediatR**: Pass. New services expose direct methods (`PrepareAsync`, `ResolveAsync`) without command/handler indirection.
- **Service size**: Risk noted. `ReleasePagePreparationService` will approach the 300-line limit. Mitigation: extract `RenderContextBuilder` and `PageRenderer` as collaborators (see Phase 1).
- **Method size**: Pass. Orchestration split into private methods `BuildContext`, `RenderTitle`, `RenderBody`, `BuildPreparedPage`, each well under 30 lines.
- **Observability**: Pass. Every binding mutation logs an audit event with correlation ID; preview and publish operations log timings and counts.
- **Secrets management**: Pass. Custom variables are explicitly non-secret plain text; secret-bearing integrations continue to use the existing encrypted token store. UI and documentation must say so explicitly (covered in FR-021).
- **Access control**: Pass. New endpoints reuse the existing JWT/role infrastructure: Admin for writes, Viewer for reads.

No violations require Complexity Tracking entries.

## Project Structure

### Documentation (this feature)

```
specs/012-project-page-templates/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output (open clarifications, decisions)
├── data-model.md        # Phase 1 output (entities, EF mappings, migration plan)
├── quickstart.md        # Phase 1 output (manual validation script)
├── contracts/           # Phase 1 output (OpenAPI fragments for new endpoints)
└── tasks.md             # Phase 2 output (generated by /tasks)
```

### Source Code (repository root)

```
# Option 2: Web application

backend/
├── src/
│   ├── RepoManager.Domain/
│   │   ├── Projects/
│   │   │   ├── Project.cs                          # extended: VersionBumpStrategy, CustomVariables
│   │   │   ├── VersionBumpStrategy.cs              # NEW (enum)
│   │   │   └── ProjectCustomVariable.cs            # NEW (value object)
│   │   └── Templates/
│   │       ├── ProjectTemplateBinding.cs           # NEW (entity)
│   │       └── TemplateKind.cs                     # NEW (enum)
│   │
│   ├── RepoManager.Application/
│   │   ├── Releases/
│   │   │   ├── Rendering/
│   │   │   │   ├── ReleaseRenderContext.cs         # NEW (record + nested records)
│   │   │   │   ├── RenderContextBuilder.cs         # NEW (collaborator)
│   │   │   │   └── PageRenderer.cs                 # NEW (collaborator)
│   │   │   ├── IReleasePagePreparationService.cs   # NEW (interface)
│   │   │   ├── ReleasePagePreparationService.cs    # NEW (orchestrator)
│   │   │   ├── IVersionResolver.cs                 # NEW (interface)
│   │   │   ├── VersionResolver.cs                  # NEW (implementation)
│   │   │   └── PreparedRelease.cs                  # NEW (DTO)
│   │   ├── Templates/
│   │   │   ├── HandlebarsHelpers.cs                # extended: gt, minus, lower, upper, truncate, jiraLink
│   │   │   └── ITemplateRenderingService.cs        # extended: RenderInline()
│   │   └── ProjectTemplateBindings/
│   │       ├── ProjectTemplateBindingService.cs    # NEW (CRUD)
│   │       └── Dtos/                               # NEW (request/response shapes)
│   │
│   ├── RepoManager.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── EfConfigurations/
│   │   │   │   ├── ProjectTemplateBindingConfiguration.cs   # NEW
│   │   │   │   └── ProjectConfiguration.cs                  # extended (owned CustomVariables, enum)
│   │   │   └── Migrations/
│   │   │       ├── 20260524_AddProjectTemplateBindings.cs   # NEW
│   │   │       ├── 20260524_AddProjectVersionBumpStrategy.cs # NEW
│   │   │       ├── 20260524_AddProjectCustomVariables.cs    # NEW
│   │   │       └── 20260524_SeedDefaultReleaseNotesBindings.cs # NEW (data migration)
│   │   └── Confluence/
│   │       └── ConfluencePublisher.cs              # extended: AppendRelatedPagesSection()
│   │
│   └── RepoManager.Api/
│       └── Controllers/
│           ├── ProjectTemplateBindingsController.cs    # NEW
│           └── ReleasesController.cs                   # extended: POST /preview, multi-page POST body
│
└── tests/
    ├── unit/
    │   ├── VersionResolverTests.cs                 # NEW
    │   ├── ReleasePagePreparationServiceTests.cs   # NEW
    │   ├── HandlebarsHelpersTests.cs               # NEW
    │   └── ProjectTemplateBindingValidationTests.cs # NEW
    ├── integration/
    │   ├── PreviewAndPublishFlowTests.cs           # NEW (end-to-end)
    │   └── BindingMigrationTests.cs                # NEW (seed migration)
    └── snapshot/
        └── HandlebarsRenderingSnapshots/           # NEW (fixture-based)

frontend/
├── src/
│   ├── pages/settings/projects/[id]/pages/
│   │   ├── PagesTab.tsx                            # NEW
│   │   ├── BindingsTable.tsx                       # NEW
│   │   ├── BindingDialog.tsx                       # NEW
│   │   └── CustomVariablesCard.tsx                 # NEW
│   ├── pages/releases/wizard/
│   │   ├── PreparePagesStep.tsx                    # NEW (replaces ChooseTemplateStep)
│   │   ├── EditPagesStep.tsx                       # extended to multi-tab
│   │   ├── PreviewStep.tsx                         # extended to multi-tab
│   │   └── wizardState.ts                          # extended: pages[] array
│   └── api/
│       ├── templateBindings.ts                     # NEW
│       └── releases.ts                             # extended: previewRelease()
└── tests/
    └── e2e/
        └── projectPagesFlow.spec.ts                # NEW (Playwright)
```

**Structure Decision**: Web application (Option 2). The existing repository already uses this split (`backend/` and `frontend/`); this feature does not introduce any new top-level project, only new folders inside the existing `RepoManager.Domain`, `RepoManager.Application`, `RepoManager.Infrastructure`, and `RepoManager.Api` projects, plus new screen folders in `frontend/src/pages/`.

## Phase 0 — Research Topics

(Phase 0 produces `research.md`. Topics queued for that phase:)

1. **Idempotent cross-linking strategy**: when the release-notes page is updated to append a related-pages section, decide between regenerating the section from scratch on every publish vs detecting and replacing a marker comment block. Recommend: marker block. Document the marker syntax.
2. **Handlebars unknown-token detection**: Handlebars.Net does not emit warnings for missing tokens by default. Decide between a custom `MissingMemberResolver` that records misses, or a pre-render AST scan. Recommend: custom resolver — runs once per render, no double-parse cost.
3. **Wizard session storage**: prepared pages and edited drafts need to survive step navigation. Decide between server-side session (Redis), client-side state (React state + sessionStorage), or per-release draft rows in SQLite. Recommend: client-side state for the wizard; persist only on publish. Crash recovery is a P3 follow-up.
4. **Reconciliation staleness detection**: decide what constitutes "stale". Recommend: a hash of the resolved per-repo (previousTag, headSha) tuple captured at reconciliation time, compared against the current value.
5. **Version-primary repo with no tags**: confirm the existing platform's behaviour (today, releases for tagless repos are not supported). Confirm the proposed UX: validation error with an explicit-version override field.

## Phase 1 — Design Artefacts

(Phase 1 produces `data-model.md`, `quickstart.md`, `contracts/`. Outline of what each will contain:)

### `data-model.md` — entities and migrations

`ProjectTemplateBinding` table:

| Column | Type | Constraints |
|---|---|---|
| `Id` | `TEXT` (Guid) | PK |
| `ProjectId` | `TEXT` (Guid) | FK → `Projects(Id)`, ON DELETE CASCADE |
| `TemplateId` | `TEXT` (Guid) | FK → `ReleaseNoteTemplates(Id)`, ON DELETE RESTRICT |
| `Kind` | `INTEGER` | NOT NULL; 0=ReleaseNotes, 1=Checklist, 2=Custom |
| `PageTitleTemplate` | `TEXT` | NOT NULL; max 500 chars |
| `ParentPageIdOverride` | `TEXT` | nullable |
| `LinkFromReleaseNotes` | `INTEGER` (bool) | NOT NULL default 0 |
| `SortOrder` | `INTEGER` | NOT NULL default 0 |
| `CreatedAtUtc` | `TEXT` (ISO 8601) | NOT NULL |
| `UpdatedAtUtc` | `TEXT` (ISO 8601) | NOT NULL |

Indexes: unique `(ProjectId, TemplateId, Kind)`; index `(ProjectId, SortOrder)`.

`Projects` table additions:

| Column | Type | Constraints |
|---|---|---|
| `VersionBumpStrategy` | `INTEGER` | NOT NULL default 1 (Minor) |
| `CustomVariablesJson` | `TEXT` | nullable; serialised `Dictionary<string,string>` |

`DefaultReleaseNoteTemplateId` is **kept and marked `[Obsolete]`** for one release. The seed migration reads it to create the initial binding; a follow-up migration drops it.

### `quickstart.md` — manual validation script

A 10-step click-through covering the smoke test scenario from `spec.md`: bind two templates, add custom variables, run wizard, edit one page, run reconciliation, refresh pages, publish, verify in Confluence.

### `contracts/` — new and changed endpoints

- `GET /api/v1/projects/{projectId}/template-bindings` → 200 `BindingDto[]`
- `POST /api/v1/projects/{projectId}/template-bindings` → 201 `BindingDto`
- `PATCH /api/v1/projects/{projectId}/template-bindings/{bindingId}` → 200 `BindingDto`
- `DELETE /api/v1/projects/{projectId}/template-bindings/{bindingId}` → 204
- `PUT /api/v1/projects/{projectId}/template-bindings/order` → 200 `{ orderedIds: Guid[] }`
- `POST /api/v1/projects/{projectId}/releases/preview` → 200 `PreparedReleaseDto`
- `POST /api/v1/projects/{projectId}/releases` (extended) — body adds `pages: PageInputDto[]`; legacy `notes` field preserved with compatibility shim

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations to track. The 300-line risk on `ReleasePagePreparationService` is mitigated by extracting two collaborators (`RenderContextBuilder` and `PageRenderer`) — both pull their weight independently and the orchestrator stays under the ceiling.
