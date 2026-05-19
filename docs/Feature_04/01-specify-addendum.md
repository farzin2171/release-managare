# Feature Addendum — Per-Repo Jira Visibility on Project & Service Pages

This is an addition to the existing `02-specify.md`. It introduces an always-on Jira comparison view on the project page (per-repo cards) and on the service/repository page (full breakdown), distinct from the release-time reconciliation wizard.

---

## Capability: Per-Repo Jira Ticket Visibility

The system MUST surface Jira ticket coverage for each tracked repository at all times — not only during release creation — so admins and viewers can spot drift between code (commits since last tag) and intent (tickets tagged in Jira for the next release) the moment it appears.

### Naming Convention

Each tracked repository has an implicit **next-release fix version** in Jira computed as:

```
<RepositoryName>_<NextVersion>
```

Where `NextVersion` is derived from the repository's current latest semver tag by incrementing the **middle (minor) segment** and resetting the patch segment to zero.

Examples:

| Current tag       | Next fix version in Jira     |
|-------------------|------------------------------|
| `1.30.0`          | `Services.UX_1.31.0`         |
| `2.5.7`           | `Apply.Api_2.6.0`            |
| `0.9.0`           | `Notification.Worker_0.10.0` |

The repository name in the fix version string is the **exact repository name as it appears in the Git provider** (e.g., `Services.UX`), preserving casing and punctuation.

Edge cases the system MUST handle:

- **No tag yet.** If the repository has no semver tag, the next version is `0.1.0` and the system displays an "Untagged" indicator next to the fix version.
- **Non-semver tag.** If the latest tag does not match `MAJOR.MINOR.PATCH`, the system displays a warning and disables the Jira comparison for that repo until the convention is fixed.
- **Fix version missing in Jira.** If the computed fix version does not exist in any Jira project mapped to this repo's logical project, the comparison shows zero Jira tickets with an informational note ("Jira release `Services.UX_1.31.0` not found — it will be created on first publish, or you can create it manually").

### Project Page — Per-Repo Cards

On the project detail page, each assigned repository renders as a card showing the Jira comparison at a glance:

- Repository name and current tag
- Computed next fix version string (clickable to filter Jira release page)
- **Three counters side-by-side**:
  - Commits since last tag
  - Unique Jira tickets referenced by those commits
  - Tickets in the Jira fix version
- **Match rate** percentage (tickets in both buckets ÷ union of both buckets) with a health pill: green ≥ 90%, amber 60–89%, red < 60%
- A small three-bucket strip (in both / Jira only / Git only) showing counts only, with the same colour treatment used elsewhere
- "View details" link that navigates to the service page (deep-links to the Jira comparison tab)
- "Re-sync" icon button per card, with last-synced timestamp on hover

Cards are sorted by match rate ascending by default (worst first), so the admin sees the problems before the things that already look fine. A toggle switches to alphabetical order.

Aggregate header above the cards summarises the whole project:

- Total repositories
- Repositories with healthy match rate (green)
- Repositories needing attention (amber + red)
- Project-wide match rate (weighted by ticket count)

### Service / Repository Page — Jira Comparison Tab

The repository detail page gains a new tab **"Jira coverage"**, alongside the existing Changes / History / Settings tabs. The tab is visible to both Admin and Viewer roles.

Content:

1. **Header strip**
   - Repository name, current tag, computed next fix version
   - Match-rate health pill
   - "Re-sync" button (Admin only) and last-synced timestamp (visible to all)

2. **Summary cards** (four cards in a row)
   - Commits since last tag
   - Unique Jira tickets referenced by commits
   - Tickets in Jira fix version
   - Match rate %

3. **Three-bucket breakdown** — same layout as the existing release-time reconciliation view, scoped to this single repo:
   - **In both** — collapsible, default collapsed when ≥ 5 items. Ticket key (linked to Jira), summary, commit count, contributors avatars
   - **Jira only** — expanded by default. Ticket key, summary, Jira status with status-category colour, assignee avatar, link to Jira
   - **Git only** — expanded by default. Ticket key (Jira link if available), commit description, commit count, an "Add to fix version" action (Admin only) that adds the ticket to `<RepositoryName>_<NextVersion>` in Jira, creating the fix version if it doesn't yet exist

4. **Unmatched commits panel** (collapsed by default)
   - Commits with no parseable Jira ticket ID (unconventional or no scope)
   - SHA, author, message — admin can see what's slipping through the cracks

### Caching & Freshness

- The comparison result is cached server-side per repo, keyed by `(repositoryId, computedNextVersion)`, with a TTL of 5 minutes
- A background job refreshes stale caches every 10 minutes for repositories that have been viewed in the last 24 hours, so the project page is fast
- The "Re-sync" button forces a refresh and updates `LastSyncedAt`
- A repo's cache MUST be invalidated when:
  - Its commits are re-synced
  - Its latest tag changes (next version recomputes)
  - The Jira connection settings change

### Permissions

- **Viewer**: can see all comparison data on both pages, cannot trigger re-sync, cannot use "Add to fix version"
- **Admin**: full access, including re-sync and add-to-fix-version

### Relationship to Existing Release Reconciliation

The existing release-time reconciliation (project-scoped, fix-version-name-pattern-driven) stays as-is. This new capability is **repo-scoped** and uses a **fixed naming convention** (`<RepoName>_<Version>`). The two coexist:

- **This feature** answers "is each repo's day-to-day work properly tracked in Jira?"
- **Release reconciliation** answers "when I cut the project release, does it match what Jira says shipped?"

If the same Jira ticket appears in multiple repos' fix versions (legitimate for cross-cutting work), each repo evaluates independently — no de-duplication across repos on the project page.

### Non-goals (v1 of this feature)

- No editing of ticket fields beyond adding to fix version (consistent with existing Jira capability)
- No automatic creation of next-version Git tag — this feature only reads the current tag
- No support for non-semver versioning schemes (CalVer, date-based) — flagged as warning
- No email/Slack alerts on declining match rate — purely on-screen for v1
