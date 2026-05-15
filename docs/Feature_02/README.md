# Latest Tag Selection — Spec Kit Feature Bundle

A focused, paste-ready feature spec for adding a "latest tag" pinning capability to your existing Repository Release Management Platform.

## What this feature adds

- A new `LatestTag` property (plus three audit fields) on the `Repositories` table
- In `Settings → Repositories`, a detail panel that fetches all tags from the Git provider and lets an Admin pin one
- In the project detail screen, a new "Latest tag" column showing each repo's pinned tag

## Files in this bundle

| Order | Command | File | What it does |
|-------|---------|------|---------------|
| 1 | `/specify` | `01-specify.md` | Creates the feature spec with user stories, acceptance criteria, and schema changes |
| 2 | `/clarify` | (interactive) | Address the open questions listed at the bottom of `01-specify.md` |
| 3 | `/plan` | `02-plan.md` | Lays out the backend + frontend implementation reusing existing patterns |
| 4 | `/tasks` | `03-tasks.md` | Use as a cross-check against Spec Kit's generated task list |
| 5 | `/analyze` | — | Standard consistency check |
| 6 | `/implement` | — | Run T1 first, review, then continue task-by-task |

## How to run it

```bash
# from the root of your existing repo-release-manager project
git checkout -b feature/latest-tag

# in Claude Code:
/specify   # paste 01-specify.md
/clarify   # answer the three open questions
/plan      # paste 02-plan.md
/tasks     # then compare against 03-tasks.md
/analyze
/implement # T1 first, smoke test, then continue
```

## Notes

- The feature is **purely additive** — `LatestTag` is nullable and nothing in the existing release flow depends on it being set.
- Existing release-notes generation continues to use semver inference until a tag is pinned; once pinned, the project screen and (later) the release wizard can use the explicit value.
- One real design choice worth flagging: the tag is stored on `Repositories`, not on `ProjectRepositories`. If two projects share a repo and need different baselines, that's a follow-up — recorded in the "Out of scope" section.
