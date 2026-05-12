# /specify prompt

Build a Repository Release Management Platform that helps engineering teams track changes across logical projects spanning multiple Git repositories, reconcile what shipped against what was planned in Jira, generate release notes, and publish them to Confluence.

## Core Users

- **Admin**: Manages integrations (Git provider, Confluence, Jira), defines logical projects, assigns repositories to projects, configures release note templates, manages users. Can do everything a Viewer can.
- **Viewer**: Browses projects, views changes since last release tag across each project's repositories, reads generated release notes, runs reconciliation views. Read-only — cannot mutate any configuration or publish.

## Core Concepts

- **Git provider connection**: A connection to an external Git host. v1 implements Azure DevOps only; architecture supports adding GitHub, GitLab, etc. without breaking changes. A connection points at one organisation and authenticates with a personal access token.
- **Repository**: A code repository discovered and synced from a Git provider connection. Tracked locally in our database.
- **Project**: A logical grouping defined inside this application (NOT an Azure DevOps project). Examples: "Apply", "Illustrate", "Sync". Each project has a name, description, badge colour, and a set of assigned repositories. A project can be linked to a Jira project (or several) and to a Confluence space.
- **Project-repository assignment**: Many-to-many. A repo can belong to multiple projects (e.g., a shared library used by Apply and Sync). One repo per project can be marked as the "version primary" — its latest semantic tag drives the project's overall version.
- **Conventional commit**: Commit messages follow Conventional Commits 1.0.0 with Jira ticket IDs in the scope, e.g. `feat(PROJ-1234): add multi-currency support`.
- **Release**: A versioned snapshot of changes across all repositories assigned to a project, with generated release notes that can be edited and published to Confluence.
- **Reconciliation**: A comparison between tickets in a Jira fix version and tickets referenced in commits since the project's last release tag, bucketed into matched, Jira-only, and Git-only.

## Key Capabilities

### 1. Git Provider Management (Settings → Integrations)

- Admin configures one or more Git provider connections
- v1 implements Azure DevOps (organisation URL + PAT)
- Test-connection action verifies credentials and lists discoverable repos
- "Sync now" action fetches all repositories from the connection's organisation
- Per-connection: see last sync time, status

### 2. Repository Management (Settings → Repositories)

- After syncing, Admin sees all repos discovered from each connection
- Filter by Azure DevOps project, by tracked/untracked, by name search
- Bulk action: mark selected as tracked/untracked. Only tracked repos appear in project assignment dropdowns
- Per repo: default branch, web URL, last sync time, list of logical projects it belongs to

### 3. Project Management (Settings → Projects)

- Admin creates logical projects (Apply, Illustrate, Sync, etc.) with name, description, badge colour
- Admin assigns one or more tracked repositories to a project
- Admin marks one repo per project as "version primary" — its latest semver tag becomes the project's current version
- A repo can be assigned to multiple projects
- Admin sets a default release note template per project
- Admin configures Confluence target: space key, parent page ID
- Admin configures Jira linking (see capability 7)

### 4. Conventional Commit Parsing and Ticket Aggregation

The system MUST parse every fetched commit according to Conventional Commits 1.0.0, extracting:

- Type (`feat`, `fix`, `chore`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `style`, `revert`, or marked "unconventional")
- Scope — treated as the Jira ticket ID when it matches the pattern `[A-Z]{2,10}-\d+`
- Breaking-change indicator (`!` after type/scope, or `BREAKING CHANGE:` in body)
- Description

The system MUST aggregate commits by ticket. A single ticket may have multiple commits — group them and show:

- Ticket ID, clickable to open Jira (if Jira base URL is configured)
- Representative title (the longest or first commit description)
- Dominant change type, computed with this precedence: any breaking → "breaking"; otherwise any feat → "feat"; otherwise any fix → "fix"; otherwise the first non-chore type encountered; otherwise "chore"
- Count of commits and unique contributors
- Expandable list of individual commits with SHA, author, message

The system MUST surface unconventional commits separately in an "Unscoped" bucket with a warning visual treatment, so tech leads can spot when conventions aren't being followed.

### 5. Change Visibility

Per-repository view shows, for the range [last semver tag, HEAD]:

- Summary cards: total commits, unique tickets, breaking changes, contributors
- Three view modes: Tickets (default, grouped), Commits (flat chronological), Contributors (per-author)
- Filters: by change type, by contributor, by ticket ID search

Per-project view aggregates across all assigned repositories:

- Total commits and tickets across the project
- Per-repo summary cards showing each repo's pending changes
- Drill-down into any individual repo

### 6. Confluence Integration

- Admin configures one Confluence Cloud connection (base URL, email, API token)
- Per-project Confluence target: space key, parent page ID
- Test-connection action
- On release publish: create or update a Confluence page with rendered release notes
- Optional: also create a release checklist page from a configured template, linked to the release notes page
- Store Confluence page URL on the Release record for later reference

### 7. Jira Release Reconciliation

- Admin configures one Jira Cloud connection (base URL, email, API token) with test-connection action
- Per logical project: select one or more Jira project keys (e.g., Apply maps to `PROJ` and `SHARED`)
- Per logical project: configure a fix-version name pattern with `{version}` placeholder (e.g., "Apply {version}" produces "Apply 2.5.0")
- Per logical project: optional "auto-create fix version in Jira when publishing release"
- Per logical project: configurable subtask handling — when matching commits to tickets, optionally treat subtask commits as matching their parent ticket if the parent is in the fix version

Reconciliation view (accessible from any release):

- On run: fetch/refresh the Jira fix version and its tickets via JQL; gather all commits in the release range across the project's repos; compute three buckets
- Summary cards: matched count, Jira-only count, Git-only count, match-rate percentage
- Three sections:
  - **In both** — collapsible, default collapsed when ≥5 items. Shows ticket key (linked to Jira), summary, repository names, commit count
  - **Jira only** — expanded by default. Shows ticket key, summary, Jira status with category colour (To do / In progress / Done), assignee, link to Jira
  - **Git only** — expanded by default. Shows ticket key (Jira link if available), commit description, repository, commit count, and an "Add to Jira" action that adds the ticket to the fix version
- "Re-sync" button refreshes Jira data; display last-synced time
- Snapshot persisted so the result can be viewed later without re-querying Jira
- Cross-project tickets in commits (whose prefix doesn't match any configured Jira project key for this logical project) are ignored in v1

### 8. Release Generation

- From a project view, user clicks "Create release"
- Wizard steps: confirm change range → choose template → edit generated notes (markdown editor) → preview → optionally run Jira reconciliation → publish to Confluence
- Generated notes group entries by ticket, not by commit. Section order: Breaking changes, Features, Fixes, Other
- A ticket appears in the highest-priority section it touches (a ticket with any feat commit lands in Features, even if 80% of its commits are chore)
- When reconciliation has been run, the generator can enrich notes with Jira-sourced ticket summaries instead of relying solely on commit descriptions
- Preview shows rendered Confluence output before publishing
- After publishing: Release record stores Confluence page URL, generated notes, edited notes, and the reconciliation snapshot if any

### 9. Templates (Settings → Templates)

- Admin creates and edits release note templates with Handlebars or Scriban syntax
- Available template variables: `{{project.name}}`, `{{version}}`, `{{tickets}}` (structured collection), `{{commits}}` (raw), `{{contributors}}`, `{{repositories}}`
- Live preview pane with sample data
- One template marked as default

### 10. Access Control

- Username + password login (no SSO in v1)
- JWT bearer token with role claim, 8-hour expiry, refresh-token rotation
- First-time setup: a one-time wizard creates the initial Admin user
- Admin role required for all writes to integrations, projects, repositories, templates, users
- Viewer role can read all data including running reconciliation views (but cannot mutate Jira via "Add to Jira")

## Success Criteria

- An admin can connect Azure DevOps, Confluence, and Jira; sync repos; define a project; assign repos; configure all integrations in under 15 minutes
- A tech lead can create a release for a project spanning 3 repos, reconcile against Jira, and publish to Confluence in under 5 minutes
- Viewers always see live "changes since last release" data without needing admin assistance
- Reconciliation for a typical sprint (20-50 tickets) completes in under 10 seconds

## Explicit Non-Goals for v1

- GitHub, GitLab, Bitbucket connectors (architecture supports them; only Azure DevOps ships)
- Multi-tenancy / multiple organisations within one deployment
- CI/CD or deployment triggering
- Slack/Teams notifications
- SSO and OAuth (username/password + API tokens only)
- Jira Data Center / Server support (Jira Cloud only)
- Bidirectional Jira sync (we read tickets and add to fix versions; we do not edit ticket fields or transition statuses)
- Audit log UI (audit events still written to application logs)

## Open Behaviour Questions Already Decided

- Azure DevOps auth: PAT only in v1
- Primary repo's tag drives the project version; user explicitly bumps the version when creating a release
- Each repo in a project uses its own latest semver tag as the "since" point — release range is computed per-repo
- Subtask handling is opt-in per project, off by default
- Cross-project tickets ignored in v1
- A ticket with mixed commit types is placed in the highest-priority section it touches
