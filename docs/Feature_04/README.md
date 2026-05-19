# Per-Repo Jira Visibility — Feature Addendum

Three files that extend the existing release-manager spec with a new "Jira coverage" view on the project page (per-repo cards) and the service/repository page (full tab).

## What's different from the existing Jira reconciliation

The existing Jira reconciliation runs inside the release wizard, is project-scoped, and uses a configurable fix-version pattern. This feature is **always-on**, **repo-scoped**, and uses a **fixed naming convention**: `<RepositoryName>_<NextVersion>`, where `NextVersion` is the current tag with the minor segment incremented (`1.30.0` → `1.31.0`).

They coexist — different questions, different timing.

## Files

| File                            | Spec Kit step | Purpose                                                                      |
|---------------------------------|---------------|------------------------------------------------------------------------------|
| `01-specify-addendum.md`        | `/specify`    | Functional requirements: pages, buckets, permissions, caching rules           |
| `02-plan-addendum.md`           | `/plan`       | Data model, service interfaces, API endpoints, frontend components            |
| `03-tasks.md`                   | `/tasks`      | Five sub-milestones (F1–F5) each with a smoke test                            |

## How to feed these to Claude Code

You already have a Spec Kit project initialised. Two ways to handle a new feature:

**Option A — single feature update (recommended for this size)**

Open Claude Code in the existing repo and run:

```
/specify Append the following capability to the existing spec.
         Do not regenerate or modify other sections.

<paste contents of 01-specify-addendum.md>
```

Then `/clarify`, then:

```
/plan Append the following technical decisions to the existing plan.
      Reuse existing services and DTOs where noted.

<paste contents of 02-plan-addendum.md>
```

Then:

```
/tasks Generate tasks for the F1–F5 milestones below.
       Each task must include the smoke test as an acceptance criterion.

<paste contents of 03-tasks.md>
```

Finally `/analyze`, then `/implement F1` and review before continuing.

**Option B — feature branch**

If you prefer isolation, create a new feature branch first (`feature/jira-coverage`) and run the same commands. Spec Kit will merge the addendum into a single `spec.md` either way.

## Decisions locked in

- Version bump: **minor segment** (`1.30.0` → `1.31.0`)
- Fix version name: **`<RepoName>_<Version>` exactly**, repo name as it appears in Git provider
- Comparison shows: counts, three buckets, match-rate health pill, per-ticket lists with status and assignee
- Cache TTL 5 min, background refresh every 10 min for recently-viewed repos
- Admin-only: re-sync, add-to-fix-version. Viewer: read-only.

## Open questions to revisit later

- Should match-rate trend over time be tracked? (out of scope for v1)
- Slack/Teams alert when a repo's match rate drops below a threshold? (v2)
- Support for CalVer or non-semver tags? (v2 if requested)
