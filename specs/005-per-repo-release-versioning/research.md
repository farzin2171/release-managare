# Research: Per-Repo Release Versioning

**Phase**: 0 — Outline & Research  
**Branch**: `006-per-repo-release-versioning`  
**Date**: 2026-05-23

## Summary

All technical decisions for this feature are pre-resolved in `docs/Feature_05/02-plan-addendum.md`. This document records the decisions and their rationale for traceability.

---

## Decision 1: ReleaseRepository as snapshot join entity (not a view or re-derived field)

**Decision**: Store `PreviousVersion`, `NextVersion`, `BumpType`, `FromCommitSha`, `ToCommitSha`, `CommitCount`, and `TicketCount` as columns on the `ReleaseRepositories` join table, written once at release creation and never updated.

**Rationale**: A release is a historical artifact. Tags can be deleted, branches force-pushed, repos detached from the project. Re-deriving these values from live Git state would break the audit trail. The existing reconciliation snapshot follows the same pattern.

**Alternatives considered**:
- Re-derive from Git on every read → rejected: breaks audit trail, causes latency on every page load.
- Store only version strings, derive SHAs and counts on demand → rejected: partial snapshot still breaks if the tag is deleted.

---

## Decision 2: IVersionBumpService called per-repo, not per-project

**Decision**: `IVersionBumpService.SuggestAsync(int repositoryId, CancellationToken ct)` is called once per selected repository during `PreviewAsync` and `CreateDraftAsync`. It returns the previous version, suggested next version, bump type, and the from/to SHAs and counts for the commit range.

**Rationale**: The existing single-version wizard already had this logic; moving it behind an interface and calling it per-repo reuses the conventional-commit parsing without duplication.

**Alternatives considered**:
- Call with `(projectId, repoId)` → rejected: the project ID adds no value; the commit range is per-repo.
- Inline the bump logic in `IReleaseCompositionService` → rejected: violates single-responsibility and makes the logic untestable independently.

---

## Decision 3: IReleaseCompositionService as a new service alongside IReleaseService

**Decision**: Introduce `IReleaseCompositionService` (preview, create draft, update draft) as a sibling to the existing `IReleaseService` (publish, archive, Confluence push). The two services have non-overlapping responsibilities.

**Rationale**: Avoids bloating `IReleaseService` beyond 300 lines and keeps the multi-repo composition logic independently testable.

**Alternatives considered**:
- Add preview/create/update to `IReleaseService` → rejected: `IReleaseService` would exceed 300 lines and mix two distinct concerns.
- Use a generic `IReleaseRepository` data-access pattern → rejected: CQRS and repository patterns are explicitly prohibited by the constitution.

---

## Decision 4: DeriveReleaseVersion fallback to alphabetically-first repo

**Decision**: If the primary repo is not in the selection, the release version is taken from the repo whose name sorts first alphabetically among the selected repos.

**Rationale**: Simple, deterministic, and consistent for all users. The spec explicitly confirms this fallback (see spec FR-007).

**Alternatives considered**:
- Block submission if primary repo not included → rejected: legitimate use case (primary repo has no changes this cycle).
- Let user pick any included repo as the "release label source" → rejected: adds wizard complexity for a rare case.

---

## Decision 5: EF Core migration with backfill SQL

**Decision**: Migration `AddReleaseRepository` creates the `ReleaseRepositories` table and backfills one row per existing `Release` (primary repo, `BumpType = "manual"`, empty snapshot fields) using raw SQL in `Up()`.

**Rationale**: Legacy releases need a `ReleaseRepository` row for the detail-page table to render gracefully (with em-dash placeholders). The empty snapshot fields are clearly documented as "pre-feature" state.

**Alternatives considered**:
- No backfill, handle null `ReleaseRepositories` at the UI level → rejected: forces every code path to handle a null collection; cleaner to have one well-documented backfill row.
- Backfill with live Git data → rejected: unreliable if repos/tags have changed since the release.

---

## Decision 6: Semver validation — strictly-greater-than on the server

**Decision**: The server validates `NextVersion > PreviousVersion` using semver comparison (major → minor → patch), regardless of what `BumpType` the client sends.

**Rationale**: Prevents logical regressions (e.g., versioning a repo from `2.0.0` down to `1.9.0`). The client mirrors this rule in Zod for immediate feedback, but the server check is authoritative.

**Alternatives considered**:
- Validate only that `NextVersion` is valid semver, not that it is greater → rejected: does not catch regressions.
- Trust the client `BumpType` to derive the expected next version → rejected: client can send mismatched values; server re-derives the snapshot regardless.

---

## Decision 7: Snapshot re-captured on every Draft save, not just on creation

**Decision**: When a Draft release is updated (`PUT /releases/{id}`), the entire `ReleaseRepository` collection is replaced and all snapshot fields are re-captured server-side via `IVersionBumpService.SuggestAsync`.

**Rationale**: A Draft is mutable. The commit range may have advanced since the preview was loaded. Re-capturing ensures the snapshot is accurate at the moment the user saves, not at the moment the wizard was opened.

**Alternatives considered**:
- Cache the snapshot from preview and reuse on save → rejected: stale if commits arrived in the interim.
- Allow the client to submit snapshot fields and trust them → rejected: spec explicitly forbids client-supplied `PreviousVersion`, `FromCommitSha`, etc.

---

## NEEDS CLARIFICATION: None

All decisions are pre-resolved by the plan addendum. No open questions remain.
