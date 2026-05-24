# Implementation Plan: Project Page Templates

**Branch**: `007-project-page-templates` | **Date**: 2026-05-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/007-project-page-templates/spec.md`

## Summary

Projects need the ability to bind multiple Handlebars page templates — each with its own title format, parent-page target, and cross-link flag — and have those templates auto-filled and published to Confluence when a release is started. The wizard gains a "Prepare pages" step that renders all bound templates with live release context (versions, repos, tickets, contributors, optional reconciliation data) and lets the tech lead edit, preview, and publish a coordinated set of release pages.

Key technical additions: `ProjectTemplateBinding` and `ProjectCustomVariable` entities, a `ReleaseRenderService` that coordinates Handlebars rendering with unknown-token capture, six new backend service interfaces, eleven new REST endpoints, a backward-compatible EF Core migration that retires `DefaultReleaseNoteTemplateId`, a Zustand wizard store with a three-state `DraftState` discriminated union, and two new frontend feature areas (Project Pages settings tab, Prepare Pages wizard step).

## Technical Context

**Language/Version**: .NET 10 (backend), React 18 + TypeScript (frontend)
**Primary Dependencies**: EF Core 10 + SQLite, ASP.NET Core Web API, HandlebarsDotNet, FluentValidation, Mapster, IConfluencePublisher (existing), TanStack Query, Zustand, shadcn/ui, React Hook Form + Zod
**Storage**: SQLite WAL — `./data/repomanager.db`
**Testing**: xunit + FluentAssertions + Moq (unit), SQLite per-test file (integration), Playwright (E2E for publish flow)
**Target Platform**: Web — browser (React SPA) + Linux server (.NET)
**Project Type**: Full-stack web application
**Performance Goals**: "Prepare pages" step renders all templates in < 2 s for a project with 3 repos, 30 commits, 15 tickets (SC-002)
**Constraints**: Page title ≤ 255 chars; wizard session single-user single-tab; external APIs never called from request handlers; server stateless for wizard session; custom variables plain text only
**Scale/Scope**: Up to ~10 bindings per project; typical release has 3 repos, 30 commits, 15 tickets

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked post-design — marks below reflect final state.*

| Principle | Check | Notes |
|-----------|-------|-------|
| I. Layered Architecture | PASS | `ProjectTemplateBinding`, `ProjectCustomVariable` entities in Domain; 3 new service interfaces + DTOs + validators in Application; `ReleaseRenderService`, `ProjectTemplateBindingService`, `ProjectCustomVariableService`, EF configs in Infrastructure; 3 new controllers + endpoint extensions in Api. No upward dependencies. |
| II. API-First Design | PASS | 11 endpoints fully specified in `contracts/api-endpoints.md`. Frontend client regenerated from OpenAPI after backend changes. No hand-written HTTP calls. |
| III. TDD | PASS | `ReleaseRenderService` (unknown-token capture, context build, version-bump fallback) and `HandlebarsHelpers` unit tests written before implementation (Red-Green-Refactor). Integration tests cover migration backfill, full prepare→publish flow, and idempotent re-publish. |
| IV. Security by Default | PASS | All new endpoints require JWT bearer. Write/Admin endpoints carry `[Authorize(Roles = "Admin")]`. Custom variable values stored as plain text with clear documentation (FR-021); no new secrets paths introduced. |
| V. Observability | PASS | `ReleaseRenderService.PrepareAsync` and `PublishAsync` log with duration and outcome. Confluence publish calls already logged by `IConfluencePublisher`. Correlation ID propagated through all service calls via existing middleware. |
| VI. Simplicity | PASS | Three focused services (render, bindings, custom vars) — none exceeds 300 lines. No CQRS, no MediatR. Wizard session state in client Zustand store, not server-side. |
| VII. Extensibility | PASS | No new `I*` interfaces beyond the three required services. `MissingTokenRecorder` is an implementation detail of `ReleaseRenderService`, not an extensibility point. |
| VIII. UX Standards | PASS | `ProjectPagesTab`, `PreparedPageTab`, `ConflictResolutionDialog` use shadcn/ui exclusively. Loading states on prepare and publish. Confirmation required before Confluence publish (destructive action). WCAG AA via shadcn defaults. |
| IX. Data Integrity | PASS | Migration backfill runs in the same transaction as schema changes. `ReorderAsync` wraps sort-order swaps in an explicit transaction. `PublishAsync` is idempotent (matched by space + parent + title). No external API calls from request handlers — `PrepareAsync` reads only from SQLite; Confluence calls happen only in `PublishAsync` at the user's explicit request. |

**Violations**: None.

## Project Structure

### Documentation (this feature)

```text
specs/007-project-page-templates/
├── plan.md              ← this file
├── research.md          ← Phase 0 complete
├── data-model.md        ← Phase 1 complete
├── quickstart.md        ← Phase 1 complete
├── contracts/
│   ├── api-endpoints.md      ← Phase 1 complete
│   └── service-interfaces.md ← Phase 1 complete
└── tasks.md             ← generated by /speckit-tasks (not yet)
```

### Source Code

```text
backend/
├── src/
│   ├── RepoManager.Domain/
│   │   ├── Entities/
│   │   │   ├── ProjectTemplateBinding.cs      ← NEW
│   │   │   └── ProjectCustomVariable.cs        ← NEW
│   │   │   Project.cs                          ← add VersionBumpStrategy, nav props; remove DefaultReleaseNoteTemplateId
│   │   └── Enums/
│   │       ├── TemplateBindingKind.cs          ← NEW
│   │       └── VersionBumpStrategy.cs          ← NEW
│   ├── RepoManager.Application/
│   │   ├── Services/
│   │   │   ├── IProjectTemplateBindingService.cs   ← NEW
│   │   │   ├── IProjectCustomVariableService.cs    ← NEW
│   │   │   └── IReleaseRenderService.cs            ← NEW
│   │   ├── DTOs/
│   │   │   ├── Bindings/                           ← NEW (4 records)
│   │   │   └── Releases/                           ← NEW (10 records)
│   │   └── Validators/
│   │       ├── CreateBindingRequestValidator.cs    ← NEW
│   │       ├── UpdateBindingRequestValidator.cs    ← NEW
│   │       ├── PublishPagesRequestValidator.cs     ← NEW
│   │       └── ProjectCustomVariableUpsertValidator.cs ← NEW
│   ├── RepoManager.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs                    ← add 2 new DbSets
│   │   │   ├── EntityConfigurations/
│   │   │   │   ├── ProjectTemplateBindingConfiguration.cs ← NEW
│   │   │   │   └── ProjectCustomVariableConfiguration.cs  ← NEW
│   │   │   └── Migrations/
│   │   │       └── [timestamp]_AddProjectTemplateBindings/ ← NEW (with data backfill)
│   │   └── Services/
│   │       ├── Handlebars/
│   │       │   ├── MissingTokenRecorder.cs        ← NEW
│   │       │   ├── HandlebarsHelpers.cs            ← NEW
│   │       │   └── HandlebarsFactory.cs            ← NEW
│   │       ├── ProjectTemplateBindingService.cs   ← NEW
│   │       ├── ProjectCustomVariableService.cs    ← NEW
│   │       └── ReleaseRenderService.cs            ← NEW
│   └── RepoManager.Api/
│       └── Controllers/
│           ├── ProjectTemplateBindingsController.cs ← NEW
│           ├── ProjectCustomVariablesController.cs  ← NEW
│           ├── ReleasesController.cs               ← add prepare-pages + publish-pages
│           └── TemplatesController.cs              ← add preview endpoint
└── tests/
    ├── RepoManager.UnitTests/
    │   ├── Services/
    │   │   ├── ReleaseRenderServiceTests.cs        ← NEW (TDD first)
    │   │   ├── ProjectTemplateBindingServiceTests.cs ← NEW (TDD first)
    │   │   └── HandlebarsHelpersTests.cs           ← NEW (TDD first)
    └── RepoManager.IntegrationTests/
        ├── Bindings/
        │   ├── TemplateBindingCrudTests.cs         ← NEW
        │   └── BindingMigrationTests.cs            ← NEW
        └── Releases/
            ├── PreparePagesTests.cs                ← NEW
            └── PublishPagesTests.cs                ← NEW

frontend/
└── src/
    └── features/
        ├── settings/
        │   └── projects/
        │       ├── ProjectPagesTab.tsx              ← NEW
        │       ├── BindingFormSheet.tsx             ← NEW
        │       └── CustomVariablesSection.tsx       ← NEW
        └── releases/
            └── wizard/
                ├── steps/
                │   └── PreparePagesStep.tsx         ← NEW
                ├── PreparedPageTab.tsx              ← NEW
                ├── ReconciliationRefreshBar.tsx     ← NEW
                ├── ConflictResolutionDialog.tsx     ← NEW
                └── store/
                    └── useWizardStore.ts            ← NEW
```

## Phase 0: Research

**Status**: Complete — see [research.md](research.md).

Seven decisions resolved:
1. Unknown token detection via `IFormatterProvider` + `[ThreadStatic]` capture bag.
2. Wizard session state in Zustand with `sessionStorage` persistence and `DraftState` union.
3. `VersionBumpStrategy` resolves project-level render context version (Feature 005 snapshot preferred).
4. `PrepareAsync` is stateless on the server; client owns wizard state.
5. Cross-linking via post-publish Confluence page update using `<ri:page>` macros.
6. Reconciliation staleness is client-side state in Zustand.
7. Migration drops `DefaultReleaseNoteTemplateId` in the same EF migration as the new tables.

## Phase 1: Design & Contracts

**Status**: Complete.

| Artifact | Status | Notes |
|----------|--------|-------|
| [data-model.md](data-model.md) | Complete | 2 new entities, Project modification, migration plan, validation rules, ephemeral DTO structures |
| [contracts/api-endpoints.md](contracts/api-endpoints.md) | Complete | 11 endpoints with full request/response shapes |
| [contracts/service-interfaces.md](contracts/service-interfaces.md) | Complete | 3 service interfaces, 14 DTOs, 4 validators, Handlebars helpers table, frontend store shape |
| [quickstart.md](quickstart.md) | Complete | 12-step implementation sequence + smoke test |

## Implementation Notes

### Backend sequence

1. **Domain entities first** — `ProjectTemplateBinding`, `ProjectCustomVariable` POCOs, enums, nav properties on `Project`. No EF config yet.
2. **TDD gate** — write failing unit tests for `ReleaseRenderService` (unknown-token capture, version resolution, error cases), `HandlebarsHelpers` (all 9 helpers), and `ProjectTemplateBindingService` (constraint violations) before any service code.
3. **Application layer** — service interfaces, DTOs (record types), FluentValidation validators.
4. **EF Core config + migration** — entity configurations, `AppDbContext` updates, migration with data backfill SQL and column drop.
5. **MissingTokenRecorder + Handlebars setup** — `IFormatterProvider` implementation, helper registration, DI wiring.
6. **IReleaseRenderService** — `BuildContextAsync`, `PrepareAsync`, `PublishAsync`, `PreviewTemplateAsync`. Unit tests go green.
7. **IProjectTemplateBindingService + IProjectCustomVariableService** — CRUD + constraints + audit log. Integration tests go green.
8. **Controllers + DI registration** — all 11 endpoints; extend `PATCH /projects/{id}` for `versionBumpStrategy`.

### Frontend sequence

9. Regenerate OpenAPI client after backend is runnable.
10. `useWizardStore` — Zustand store with `sessionStorage` persistence, `DraftState` union, all 7 actions.
11. `ProjectPagesTab` + `BindingFormSheet` + `CustomVariablesSection` — Settings → Projects → Pages.
12. `PreparePagesStep` + `PreparedPageTab` + `ReconciliationRefreshBar` + `ConflictResolutionDialog` — Release wizard.

### Critical invariants

- Server **never stores wizard session state** — `PrepareAsync` is fully stateless; client owns edits.
- `PublishAsync` **validates all titles server-side** (1–255 chars); client mirrors in Zod but server is authoritative.
- **Deleting the last `ReleaseNotes` binding** throws `ConflictException("last_release_notes_binding")` — controller maps to 409.
- **Migration backfill** is in the same transaction as schema changes — either both succeed or neither.
- **Re-publish is idempotent** — matched by `(spaceKey, parentPageId, title)`; calls `CreateOrUpdatePageAsync` for every page.
- **Cross-link update** happens after all pages are published — the primary page update is the last Confluence call.
- Unknown tokens in `{{custom.<key>}}` are reported as `"custom.<key>"` (fully qualified) — the `custom` dict wrapper records the miss with the full dotted path.

### Follow-up (out of scope for this branch)

- Jira reconciliation integration should be updated to use the reconciliation data from the wizard payload rather than a separate fetch (existing `ReconciliationService` is unchanged here).
- Dark mode styling for `@uiw/react-md-editor` in `PreparedPageTab` — controlled by Tailwind dark class; verify in a separate PR.

## Complexity Tracking

No constitution violations. This section is intentionally empty.
