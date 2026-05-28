# Research: Project Page Templates

**Phase**: 0
**Status**: Decisions recorded

This document captures resolved research questions for the feature. Each entry is structured as **Question → Options → Decision → Rationale**.

---

## R-001: Idempotent cross-linking on the primary release-notes page

**Question**: When the publisher updates the release-notes page to add a related-pages section, how do we keep republishes from duplicating or scrambling the section?

**Options considered**:

1. Regenerate the entire body from the template on every publish (loses manual edits).
2. Detect-and-replace via a marker comment block in Confluence Storage Format.
3. Store the related-pages section in a Confluence "Page Properties" macro at the bottom.

**Decision**: **Option 2**. Use HTML comment markers `<!-- related-pages:start -->` and `<!-- related-pages:end -->` to delimit the section. On every publish, the publisher fetches the current body, removes anything between the markers, appends a freshly built section, then updates. If no markers are present (first publish), append both markers + section to the end.

**Rationale**: Markers survive manual edits, are invisible in the rendered Confluence page, work with the existing `CreateOrUpdatePageAsync` without new endpoints, and are easy to validate in tests. Option 1 is too destructive; Option 3 couples us to a specific macro we can't easily test offline.

---

## R-002: Detecting unknown Handlebars tokens during render

**Question**: Handlebars.Net does not by default emit warnings for missing tokens — it renders them as empty strings silently. How do we surface them to the user in the wizard?

**Options considered**:

1. Custom `IMissingNameFormatter` (Handlebars.Net's extension point for unknown helpers/fields).
2. Pre-render AST scan extracting every `{{token}}` reference and diffing against the context's known property set via reflection.
3. Post-render textual scan for empty patterns (fragile).

**Decision**: **Option 1**. Implement `RecordingMissingMemberResolver` that captures the missing token path each time Handlebars.Net resolves an empty member, then exposes the captured set after each render via a thread-static collection (or, more cleanly, a `PageRenderer` instance field reset per render).

**Rationale**: Single-pass, no double parsing, zero impact on render output, exactly the abstraction Handlebars.Net offers. Option 2 requires a separate parser and breaks if helpers are used; Option 3 produces false positives for any genuinely empty value.

---

## R-003: Where wizard state lives between steps

**Question**: Prepared pages and edited drafts need to survive navigation between wizard steps and shouldn't be lost if the user closes the tab during a 5-minute editing session.

**Options considered**:

1. Server-side session (Redis or in-memory cache).
2. Client-side state (React state) plus `sessionStorage` snapshot for tab refresh.
3. Persist wizard drafts as rows in SQLite under `Releases.DraftState`.

**Decision**: **Option 2** for v1. Crash recovery beyond a refresh is deferred to a follow-up.

**Rationale**: The wizard is a single-user, single-tab flow. Server-side session would force a Redis dependency the platform doesn't otherwise need; SQLite drafts add a cleanup problem (when is a draft stale?). `sessionStorage` survives refreshes within the same tab and gets cleared on close, which matches the wizard's lifecycle. If users start losing work due to accidental tab closes, revisit with Option 3.

---

## R-004: Detecting reconciliation staleness

**Question**: After the user runs reconciliation and then later changes the change range (e.g. bumps to a different tag), the reconciliation result is no longer valid. How do we detect this?

**Options considered**:

1. Wall-clock TTL (e.g. "stale after 10 minutes").
2. Input-hash comparison: hash a tuple of `(repoId, previousTag, headSha)` for every repo in the project; compare current vs reconciliation-time hash.
3. Mark stale whenever the user changes anything in the wizard's "Confirm change range" step.

**Decision**: **Option 2**. Compute a SHA-256 of the sorted JSON of `(repoId, previousTag, headSha)` tuples at the moment reconciliation runs; recompute on each render; compare; mark stale if different.

**Rationale**: Deterministic, no false positives, doesn't require tracking which UI controls were touched, and handles the case where the head SHA changes because of a new push to the source branch during the wizard session. Option 1 is too coarse; Option 3 misses commits-since-last-reconcile.

---

## R-005: Behaviour when the version-primary repo has no semver tag

**Question**: The existing platform doesn't fully define behaviour when the version-primary repo has never had a tag. What's the right UX for this feature?

**Options considered**:

1. Block the wizard entirely and require the admin to push an initial tag.
2. Block by default but allow an admin override that types a version string.
3. Default the version to `0.1.0` and proceed silently.

**Decision**: **Option 2**. Surface a blocking validation in the "Prepare pages" step naming the repository. Offer an inline override input that accepts a version string conforming to `^\d+\.\d+\.\d+$`. If overridden, the next version is computed from the override using the project's bump strategy.

**Rationale**: Avoids silently producing a wrong-looking version (Option 3) while still letting teams bootstrap a brand-new repo without having to leave the wizard. The override is a one-time, single-release escape hatch — the admin can push a real tag afterwards and the override becomes unnecessary.

---

## Non-research notes worth recording

- The `Handlebars.Net` package version pinned in the existing solution supports `IMissingNameFormatter` from v2.x — no upgrade required.
- The Confluence Storage Format `<ac:link>` macro accepts `<ri:page ri:content-title="..."/>` references; this is what cross-linking will emit. No new Confluence-side configuration is needed.
- Azure DevOps tag-list API is paginated; for repos with very large tag histories, the version resolver should cap retrieval at the most recent 100 tags and fall back to a server-side semver sort. This isn't new for this feature — same constraint applies to the existing release wizard — but it's worth re-validating once `VersionResolver` is in place.
