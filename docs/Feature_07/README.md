# Release Manager — Milestone 13 Addendum

Five features extending the existing spec. Apply after Milestone 12 smoke test passes.

## What's in this bundle

| File | Purpose |
|---|---|
| `06-addendum-milestone-13.md` | All Spec Kit prompts: `/specify`, `/plan`, `/tasks` sections ready to paste into Claude Code |

## Features included

| Feature | What it does |
|---|---|
| **A — Setup API key** | Protects `/auth/setup` with `X-Setup-Key` header; app refuses to start if key is absent and no users exist |
| **B — `service_owner` field** | Nullable text field on Repository; editable in Settings → Repositories; exposed in template context |
| **C — Release Summary template** | Built-in system Handlebars template; iterates all repos in the release; shows repo, owner, prev/next version, counts; auto-bound to new projects |
| **D — Frontend token refresh** | Silent 401-intercept → refresh → retry; proactive refresh 2 min before expiry; httpOnly cookie for refresh token; no thundering herd |
| **E — Delete Draft releases** | Kebab menu on list rows; "Delete draft" button on detail page; confirmation dialog; Admin-only; graceful 409 handling |

## How to apply in Claude Code

1. Open the `repo-release-manager` Spec Kit project in Claude Code.
2. Run `/specify` and paste the **`/specify` addendum** section from `06-addendum-milestone-13.md`.
3. Answer any `/clarify` questions — the "Open clarifications" table at the bottom of the file has all expected answers.
4. Run `/plan` and paste the **`/plan` addendum** section.
5. Run `/tasks` — validate it produces 5 sub-tasks (A–E) matching the Milestone 13 definition.
6. Run `/analyze` to confirm consistency with Milestones 1–12.
7. Run `/implement` for Milestone 13. Suggested order: **B → A → C → E → D** (data model first, auth second, template third, UI last, refresh token last because it touches both layers).

## Dependency map

```
Milestone 1  (JWT, /auth/setup, refresh tokens)
    └── Feature A  (setup key)
    └── Feature D  (frontend refresh — completes what M1 started)

Milestone 3  (Repositories)
    └── Feature B  (service_owner column)

Milestone 7  (Templates, ITemplateRenderingService)
Milestone 8  (Confluence publishing)
Milestone 12 (ProjectTemplateBindings, ReleaseRenderContext)
    └── Feature C  (system template, context extension)
    └── Feature B  (serviceOwner in context)

Milestone R3 (DELETE /releases/{id} endpoint)
    └── Feature E  (frontend delete UI)
```
