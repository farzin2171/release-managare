# Research: Latest Tag Selection for Repositories

**Phase 0 output** — All decisions resolved. No NEEDS CLARIFICATION items remain.

---

## Decision 1: Azure DevOps Tags API — Refs Endpoint with `peelTags=true`

**Decision**: Use `GET /_apis/git/repositories/{externalId}/refs?filter=tags&peelTags=true&api-version=7.1` to retrieve the tag list. For annotated tags, use `peeledObjectId` as the commit SHA. For lightweight tags (where `peeledObjectId` is absent or empty), fall back to `objectId`.

**Rationale**: The `/refs` endpoint is already in use by the platform for the `AzureDevOpsGitProvider`. Adding `filter=tags&peelTags=true` to the existing query surface requires no new authentication paths or client registration. The `peelTags=true` parameter instructs the API to dereference annotated tag objects to their underlying commit SHA — without it, `objectId` for an annotated tag points to the tag object itself, not the commit, making commit-level lookups impossible. Lightweight tags always point directly to a commit, so their `objectId` is already the commit SHA.

**Key API patterns**:
```csharp
// Step 1: Fetch refs filtered to tags
// GET /_apis/git/repositories/{externalId}/refs?filter=tags&peelTags=true&api-version=7.1
var refs = await gitClient.GetRefsAsync(
    repositoryId: externalId,
    filter: "tags",
    peelTags: true,
    top: 200,
    cancellationToken: ct);

// Step 2: Resolve commit SHA per ref
foreach (var r in refs)
{
    var commitSha = !string.IsNullOrEmpty(r.PeeledObjectId)
        ? r.PeeledObjectId   // annotated tag — points to commit
        : r.ObjectId;        // lightweight tag — already points to commit

    var tagName = r.Name.Replace("refs/tags/", string.Empty);
}
```

**Commit metadata (date + author)**: The refs response does not include commit date or author. A second call to `GET /_apis/git/repositories/{externalId}/commits/{sha}` is required per tag. These are batched in parallel (up to the 200-tag cap) using `Task.WhenAll` to minimise latency. See Decision 3 for the cap rationale.

**Alternatives considered**: Using `GitHttpClient.GetTagsAsync()` — this method returns `GitAnnotatedTag` objects but only for annotated tags; it silently omits lightweight tags, violating the requirement to display both types (FR-003, spec Assumption 3). The `/refs` endpoint with `peelTags=true` handles both types uniformly.

---

## Decision 2: On-Demand Tag Fetch — No Session Caching

**Decision**: Every "Fetch tags" action triggers a live call to the Azure DevOps refs API. No tag list is cached between requests in SQLite, in-memory, or distributed cache.

**Rationale**: Story 3 states that freshness is a correctness requirement. A cached tag list could contain tags that have been deleted on the remote since the last fetch, allowing an Admin to pin a tag that no longer exists — which write-time validation (Decision 5) would then reject with a confusing error. Eliminating caching removes this class of inconsistency entirely. Tag fetches are user-initiated (not background polling), so the performance cost is bounded to deliberate Admin actions, not ambient load.

**Consequence for the API layer**: `GET /api/v1/repositories/{id}/tags` always calls `IGitProvider.GetTagsAsync(...)` and returns the result directly. No EF Core read for tags; the endpoint is a pure proxy to the provider with error mapping.

**Alternatives considered**: Short-lived in-memory cache (TTL 60 s) — reduces provider calls on repeated clicks but risks the "pinned tag was just deleted" scenario that write-time validation exists to catch. The marginal latency saving does not outweigh the correctness risk at this feature's scope. Persisting tags in SQLite between fetches — adds a sync-staleness problem without the benefit of a background sync job to keep it fresh.

---

## Decision 3: Pagination Cap at 200 Tags

**Decision**: Cap tag retrieval at 200 tags per fetch using `top=200` on the refs API call. The full list (up to 200) is returned to the frontend in a single response. The frontend table virtualises rows client-side when the list exceeds 100 entries.

**Rationale**: The Azure DevOps refs API default page size is 200. Exceeding 200 requires cursor-based pagination (`continuationToken`), which would require the backend to accumulate multiple pages and the frontend to handle streaming or multi-request assembly — adding complexity for a case that is extremely rare in practice (repositories with more than 200 tags represent < 1% of real-world ADO repos). The cap is documented in the API response and surfaced to the Admin as an informational note when the full 200 are returned.

**Frontend table virtualisation**: When the tag list length exceeds 100, the table switches to a virtualised scroll container (TanStack Virtual or equivalent) to maintain 60 fps scroll performance. The threshold of 100 is chosen because standard DOM rendering degrades noticeably beyond ~100 rows on low-end hardware.

**Alternatives considered**: Unlimited pagination (fetch all pages) — adds provider round-trips, increases response latency for large repos, and couples the on-demand fetch duration to an unbounded tag count. Not worth the complexity given the < 200 cap satisfies > 99% of real repos. Client-side pagination of the response — adds a second fetch step from the UI; returning the full list up-front with virtualisation is simpler and has no perceptible overhead below 200 rows.

---

## Decision 4: Tag Type Filtering — Show Both Annotated and Lightweight Tags

**Decision**: Display both annotated tags (those with a non-empty `peeledObjectId`) and lightweight tags (those without) in the same list. No visual distinction between tag types is shown in v1. Both resolve to a commit SHA using the fallback logic in Decision 1.

**Rationale**: Azure DevOps repositories commonly use both tag types depending on tooling and convention. Hiding lightweight tags would silently omit valid release tags, confusing Admins who cannot see a tag they know exists. The commit SHA fallback (`objectId` when `peeledObjectId` is absent) ensures lightweight tags are equally functional for the pinning workflow. Adding a type icon was evaluated but deferred: it requires communicating the annotated/lightweight distinction to users who may not know what it means, adding documentation burden with minimal functional payoff.

**Alternatives considered**: Show annotated tags only — cleaner semantics (annotated tags carry a message, tagger, and date directly on the tag object) but silently omits valid lightweight tags, violating the spec assumption. Add a type badge/icon — deferred to v2; not in FR-003 and adds UI surface area without a concrete user need identified.

---

## Decision 5: Write-Time Tag Existence Validation via Provider Re-Fetch

**Decision**: When an Admin submits a tag to pin (`PUT /api/v1/repositories/{id}/latest-tag`), the server calls `IGitProvider.GetTagsAsync(externalId, ct)` to re-fetch the live tag list and confirms the submitted tag name is present before persisting. If the tag is not found, the endpoint returns `422 Unprocessable Entity` with a `ProblemDetails` body indicating the tag no longer exists.

**Rationale**: The gap between "Admin sees the tag list" and "Admin clicks Confirm" may be seconds to minutes. In that window, a tag can be deleted on the remote. Pinning a name that no longer maps to a commit SHA on the provider would silently corrupt the baseline used by downstream release features. Re-fetching at write time is cheap (one API call) and eliminates this class of corruption. The commit SHA captured at this re-fetch point is authoritative and stored as `LatestTagCommitSha` on the `Repository` entity.

**Implementation pattern**:
```csharp
// In RepositoryService.PinLatestTagAsync
public async Task PinLatestTagAsync(Guid repositoryId, string tagName, Guid userId, CancellationToken ct = default)
{
    var repository = await _db.Repositories.FindAsync([repositoryId], ct)
        ?? throw new NotFoundException(nameof(Repository), repositoryId);

    var liveTags = await _gitProvider.GetTagsAsync(repository.ExternalId, ct);
    var match = liveTags.FirstOrDefault(t => t.Name == tagName)
        ?? throw new ValidationException($"Tag '{tagName}' no longer exists in the remote repository.");

    repository.PinLatestTag(tagName, match.CommitSha, userId, DateTime.UtcNow);
    await _db.SaveChangesAsync(ct);
    await _auditLogger.LogAsync(AuditAction.LatestTagSet, repository.Id, userId,
        oldValue: repository.LatestTagName, newValue: tagName, ct);
}
```

**Alternatives considered**: Trust the client-submitted tag name and SHA without re-fetching — eliminates the extra provider call but risks persisting a stale or spoofed SHA. Reject on mismatch only if SHA differs — requires the client to submit a SHA, which must then be validated; the re-fetch is still required, so this is strictly more complex with no benefit.

---

## Decision 6: Concurrent Write Semantics — Last-Write-Wins, No Optimistic Locking

**Decision**: Concurrent pin/clear operations on the same repository use last-write-wins semantics. No EF Core concurrency token (`[ConcurrencyCheck]` or `RowVersion`) is added. `LatestTagSetAt` is always set to `DateTime.UtcNow` on the server; client-supplied timestamps are ignored.

**Rationale**: The spec explicitly calls out last-write-wins (Edge Cases, spec § Edge Cases; Assumption 5). Simultaneous Admin writes to the same repository's latest tag are expected to be rare and low-stakes — both Admins are selecting a valid tag, and the second write corrects rather than corrupts. Adding optimistic locking would surface a `409 Conflict` to one Admin with no recovery path other than retrying, which is worse UX than silent last-write-wins for this particular domain. The audit log (Decision 8) preserves the full history of changes, so the "losing" write is recoverable via audit review if needed.

**Server-generated timestamp**: `LatestTagSetAt = DateTime.UtcNow` is assigned inside `Repository.PinLatestTag(...)` using the injected `utcNow` parameter, keeping the domain method deterministic and testable without `DateTime.UtcNow` leaking into the entity.

**Alternatives considered**: EF Core `[ConcurrencyCheck]` on `LatestTagSetAt` — would surface a `DbUpdateConcurrencyException` that maps to a `409`, but the spec explicitly rejects this. Pessimistic locking via SQLite `BEGIN EXCLUSIVE` — unnecessary overhead for a low-contention field; SQLite WAL handles the single-writer constraint at the storage level.

---

## Decision 7: Domain Method Placement on `Repository` Entity

**Decision**: The `Repository` entity exposes two domain methods: `PinLatestTag(string tagName, string commitSha, Guid userId, DateTime utcNow)` and `ClearLatestTag(Guid userId, DateTime utcNow)`. The `IsTracked == true` guard is enforced inside these methods, not in the service layer.

**Rationale**: Placing the guard in the domain method ensures it is enforced regardless of which service or test calls the method, preventing the tracked-only invariant from being accidentally bypassed if a second caller is added in a future milestone. This follows the existing platform convention where entity invariants are encapsulated in the entity itself (Principle I: domain model owns its own invariants). The `userId` and `utcNow` parameters are passed in rather than resolved inside the method to keep the entity free of infrastructure dependencies (`DateTime.UtcNow`, `IHttpContextAccessor`) and to keep the methods unit-testable with deterministic inputs.

**Implementation pattern**:
```csharp
// In RepoManager.Domain / Repository.cs
public void PinLatestTag(string tagName, string commitSha, Guid userId, DateTime utcNow)
{
    if (!IsTracked)
        throw new InvalidOperationException(
            "Cannot pin a latest tag on a repository that is not tracked.");

    LatestTagName      = tagName;
    LatestTagCommitSha = commitSha;
    LatestTagSetAt     = utcNow;
    LatestTagSetByUserId = userId;
}

public void ClearLatestTag(Guid userId, DateTime utcNow)
{
    if (!IsTracked)
        throw new InvalidOperationException(
            "Cannot clear the latest tag on a repository that is not tracked.");

    LatestTagName        = null;
    LatestTagCommitSha   = null;
    LatestTagSetAt       = null;
    LatestTagSetByUserId = null;
}
```

**Alternatives considered**: Guard only in `RepositoryService` — simpler but leaks the invariant out of the domain, making it bypassable. Separate `LatestTag` value object — adds indirection without benefit; the four nullable fields are a simple extension of the `Repository` aggregate root with no independent lifecycle.

---

## Decision 8: Audit Logging — Reuse Existing `IAuditLogger` Pattern

**Decision**: Pin and clear operations each emit one audit entry via the existing `IAuditLogger` abstraction (the same pattern used by all other write operations in the platform). Each entry captures: `action` (enum value `LatestTagSet` or `LatestTagCleared`), `entityId` (repository ID), `userId` (acting Admin), `oldValue` (previous tag name or `null`), `newValue` (new tag name or `null`), and `timestamp` (UTC, server-generated). No new logging primitive or table is introduced.

**Rationale**: The platform already has an `IAuditLogger` wired to the DI container and used consistently across write operations. Reusing it keeps audit query tooling uniform — a single table and a single query shape covers all auditable actions. Introducing a separate `RepositoryTagAuditLog` table would fragment audit queries and add a migration, a new EF entity, and a new repository interface with no functional benefit over the existing pattern.

**Audit call sites**:
```csharp
// Pin
await _auditLogger.LogAsync(
    action: AuditAction.LatestTagSet,
    entityId: repository.Id,
    userId: userId,
    oldValue: previousTagName,   // null if first pin
    newValue: tagName,
    ct: ct);

// Clear
await _auditLogger.LogAsync(
    action: AuditAction.LatestTagCleared,
    entityId: repository.Id,
    userId: userId,
    oldValue: previousTagName,
    newValue: null,
    ct: ct);
```

**Alternatives considered**: Application-level event / domain event pattern — adds an event bus and handler indirection that is explicitly excluded from this platform (no MediatR, no CQRS per CLAUDE.md). Structured log entry via Serilog only (no DB row) — not queryable without a log aggregation tool; the existing `IAuditLogger` writes to SQLite, which is directly queryable from the admin audit UI.
