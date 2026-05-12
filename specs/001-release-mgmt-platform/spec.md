# Feature Specification: Repository Release Management Platform

**Feature Branch**: `001-release-mgmt-platform`
**Created**: 2026-05-12
**Status**: Draft
**Input**: User description: "Build a Repository Release Management Platform that helps engineering teams track changes across logical projects spanning multiple Git repositories, reconcile what shipped against what was planned in Jira, generate release notes, and publish them to Confluence."

## Clarifications

### Session 2026-05-12

- Q: Does the platform require automatic background sync, or is sync on-demand only? → A: On-demand only — Admins manually trigger "Sync now"; SC-003 means data is current as of the last Admin-triggered sync.
- Q: How is the version number determined when creating a release? → A: Auto-suggested from commit analysis (breaking → major, feat → minor, fix/other → patch), manually overridable by the user.
- Q: Does the platform push version tags to Git repositories after a release is published? → A: No — the platform never creates or pushes tags; version tags are managed entirely by users outside the platform.
- Q: Can a user edit release notes and re-publish to Confluence after a release is already published? → A: No — once published, release notes and the Confluence page are locked; further edits must be made directly in Confluence.
- Q: Should non-conventional ("unscoped") commits be included in generated release notes? → A: No — unscoped commits appear in the change view only and are excluded from generated release notes entirely.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Configures Integrations and Creates a Project (Priority: P1)

An Admin connects the platform to an Azure DevOps organisation, syncs repositories, defines a logical project (e.g., "Apply"), assigns repositories to it, and links Jira and Confluence targets — completing the full setup before any team member can use the platform productively.

**Why this priority**: Nothing else in the platform works until at least one integration is configured and one project is defined. This is the foundational onboarding story on which all other scenarios depend.

**Independent Test**: Can be fully tested by an Admin completing the entire setup flow from a fresh installation through to a project with at least one assigned repository and all three integrations (Azure DevOps, Jira, Confluence) configured and verified.

**Acceptance Scenarios**:

1. **Given** a fresh installation with no integrations configured, **When** an Admin enters an Azure DevOps organisation URL and personal access token and clicks "Test Connection", **Then** the system confirms the credentials are valid and lists repositories discoverable from that organisation.
2. **Given** a validated Azure DevOps connection, **When** the Admin clicks "Sync now", **Then** all repositories from the organisation appear in the repository list, each showing its default branch, web URL, and last sync time.
3. **Given** a list of synced repositories, **When** the Admin marks selected repositories as "tracked", **Then** those repositories become available in project assignment selections.
4. **Given** tracked repositories are available, **When** the Admin creates a logical project with a name, description, and badge colour, and assigns repositories to it, **Then** the project appears in the project list and each assigned repository shows the projects it belongs to.
5. **Given** a logical project, **When** the Admin configures Jira and Confluence connections (each with a test-connection action) and links them to the project, **Then** the project is fully configured for release creation and Jira reconciliation.

---

### User Story 2 - Tech Lead Views Changes Since Last Release (Priority: P2)

A tech lead opens a project and immediately sees all commits and tickets — across all the project's repositories — that have not yet been released, grouped by Jira ticket, and filterable by change type and contributor.

**Why this priority**: This is the core daily-use scenario. It delivers immediate visibility into unreleased work without requiring a full release workflow, and is the primary lens through which commit data becomes useful to engineering teams.

**Independent Test**: Can be fully tested by a Viewer opening a project whose repositories contain commits since their last version tag and verifying the grouped-ticket view, unscoped bucket, and filter behaviour.

**Acceptance Scenarios**:

1. **Given** a project with repositories that have commits since their last release tag, **When** a Viewer opens the project, **Then** they see summary cards displaying total commits, unique tickets, breaking changes, and contributors across all repositories in that project.
2. **Given** the project change view in Tickets mode (default), **When** the Viewer inspects the list, **Then** commits are grouped by Jira ticket ID, each group showing the ticket ID (linked to Jira if configured), representative title, dominant change type, commit count, and contributor count.
3. **Given** a ticket group, **When** the Viewer expands it, **Then** they see the individual commits with author, message, and unique commit identifier.
4. **Given** the change view, **When** a commit does not follow the standardised commit message format, **Then** it appears in a separate "Unscoped" section with a visual warning indicator, distinct from conventional commit entries.
5. **Given** the change view, **When** the Viewer selects the "Commits" view mode, **Then** all commits appear in flat chronological order. **When** they select "Contributors", **Then** commits are grouped by author.
6. **Given** the change view, **When** the Viewer applies a filter by change type (e.g., "feat") or searches by ticket ID, **Then** the list updates to show only matching entries.

---

### User Story 3 - Tech Lead Creates a Release and Publishes to Confluence (Priority: P3)

A tech lead initiates a release for a project, reviews auto-generated release notes grouped by breaking changes, features, fixes, and other changes, optionally reconciles against the Jira fix version, edits the notes in a markdown editor, previews the rendered output, and publishes to Confluence — all within a guided wizard.

**Why this priority**: This is the primary deliverable of the platform — a traceable, published record of what shipped. It directly reduces the manual effort involved in writing and distributing release notes across teams.

**Independent Test**: Can be fully tested end-to-end by creating a release for a project with at least one repository containing commits since a version tag, completing all wizard steps, and verifying a Confluence page is created or updated with the release notes.

**Acceptance Scenarios**:

1. **Given** a project with pending changes, **When** a tech lead clicks "Create release", **Then** a step-by-step wizard opens with a version number field pre-populated with a semver suggestion derived from the parsed commit types (breaking → major bump, feat → minor bump, fix/other → patch bump), which the tech lead may edit before proceeding to confirm the change range for each repository.
2. **Given** the release wizard, **When** the tech lead selects a template and proceeds, **Then** release notes are generated with tickets grouped into Breaking Changes, Features, Fixes, and Other sections; a ticket with any breaking change appears in Breaking Changes regardless of other commit types.
3. **Given** generated release notes, **When** the tech lead edits them in the markdown editor and clicks "Preview", **Then** a rendered preview of the Confluence page output is displayed before publishing.
4. **Given** the preview, **When** the tech lead clicks "Publish", **Then** a Confluence page is created in the configured space, the release record stores the resulting Confluence page URL, and the release is marked as published and locked for further editing within the platform.
5. **Given** the release wizard, **When** the tech lead optionally runs Jira reconciliation, **Then** the reconciliation report categorises tickets into: matched (present in both the Jira fix version and Git commits), Jira-only, and Git-only, with a match-rate percentage.
6. **Given** the reconciliation report showing a Git-only ticket, **When** an Admin clicks "Add to Jira fix version", **Then** the ticket is added to the Jira fix version; Viewers cannot perform this action.

---

### User Story 4 - Admin Manages Release Note Templates (Priority: P4)

An Admin creates and edits reusable release note templates with structured variable placeholders. A live preview pane shows how the template renders with sample data. One template is marked as the default per project.

**Why this priority**: Templates standardise the format and content of release notes across teams. Without them, every release would require manual reformatting.

**Independent Test**: Can be tested by an Admin creating a template, verifying the live preview updates as the template is edited, and confirming the template is available for selection during release creation.

**Acceptance Scenarios**:

1. **Given** the Templates settings page, **When** an Admin creates a new template with a name and body using available variable placeholders (project name, version, ticket list, commit list, contributors, repositories), **Then** the live preview pane renders a sample output in real time.
2. **Given** multiple templates exist, **When** an Admin marks one as the default for a project, **Then** that template is pre-selected when a tech lead creates a new release for that project.

---

### User Story 5 - Admin Manages Users (Priority: P5)

An Admin creates user accounts with username, password, and a role (Admin or Viewer). A one-time first-run wizard creates the initial Admin account; subsequent accounts are managed through the users settings page.

**Why this priority**: Required for multi-user teams; without user management, access control cannot be enforced.

**Independent Test**: Can be tested by an Admin creating a Viewer account, logging in as that Viewer, and confirming that all write and configuration actions are blocked.

**Acceptance Scenarios**:

1. **Given** an Admin is logged in, **When** they create a new user account with a username, password, and role, **Then** the new user can log in with those credentials and their role determines what actions are available.
2. **Given** a Viewer is logged in, **When** they navigate to any configuration page or attempt any write action, **Then** the action is unavailable and an appropriate message is shown.
3. **Given** a fresh installation with no users, **When** the platform starts for the first time, **Then** a one-time setup wizard prompts creation of the initial Admin account; the wizard is permanently unavailable after first use.

---

### Edge Cases

- What happens when a repository has no version tags? The system treats the entire commit history as unreleased changes and displays a notice that no baseline release tag was found.
- What happens when a commit message does not follow the standardised format? It is placed in the "Unscoped" bucket with a warning; it is not discarded.
- What happens when the Confluence or Jira connection fails during release publishing? The user sees a clear error message; the release record is not marked as published until Confluence confirms the page was created or updated.
- What happens when the same repository is assigned to multiple projects? Changes appear independently under each project; there is no cross-project deduplication of commits or tickets.
- What happens when a Jira ticket is referenced in commits across multiple repositories within one project? The ticket is deduplicated and shown once at the project level, with all contributing repositories listed.
- What happens when a Viewer attempts the "Add to Jira fix version" action in the reconciliation view? The action is hidden or disabled for Viewers; only Admins can mutate Jira data.
- What happens when two repositories within a project have different last release tags? The change range is computed independently per repository using each repo's own latest version tag.
- What happens when a user tries to edit a published release's notes within the platform? The markdown editor is disabled (read-only) for published releases; the platform displays the Confluence page URL for users who need to make further changes directly in Confluence.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow an Admin to configure one or more Azure DevOps connections using an organisation URL and personal access token, including a test-connection action that validates the credentials and lists discoverable repositories.
- **FR-002**: System MUST synchronise all repositories from a connected Azure DevOps organisation on demand ("Sync now") and display each repository's name, default branch, web URL, last sync time, and logical project memberships.
- **FR-003**: System MUST allow Admins to mark repositories as "tracked" or "untracked"; only tracked repositories appear in project assignment selections.
- **FR-004**: System MUST allow Admins to create logical projects with a name, description, and badge colour, and assign one or more tracked repositories to each project; a repository may belong to multiple projects.
- **FR-005**: System MUST allow one repository per project to be designated as the "version primary", whose latest version tag determines the project's displayed current version.
- **FR-006**: System MUST parse all fetched commits according to the Conventional Commits 1.0.0 standard, extracting change type, scope (treated as a Jira ticket ID when it matches the expected ticket ID pattern), breaking-change indicator, and description.
- **FR-007**: System MUST aggregate commits by Jira ticket ID and display grouped results showing ticket ID, representative title, dominant change type (precedence: breaking → feat → fix → first non-chore type → chore), commit count, contributor count, and an expandable list of individual commits.
- **FR-008**: System MUST surface commits that do not follow the standardised commit format in a separate "Unscoped" section with a visual warning, distinct from grouped ticket entries.
- **FR-009**: System MUST display, for each repository and each logical project, all commits and ticket groups in the range from the last version tag to the current head of the default branch.
- **FR-010**: System MUST provide three view modes for the change list: grouped by ticket (default), flat chronological commit list, and grouped by contributor.
- **FR-011**: System MUST allow filtering the change list by change type, contributor, and ticket ID search string.
- **FR-012**: System MUST allow Admins to configure one Confluence Cloud connection (base URL, account email, API token) with a test-connection action.
- **FR-013**: System MUST allow Admins to configure per-project Confluence targets, specifying a space key and a parent page reference for published release notes.
- **FR-014**: System MUST allow Admins to configure one Jira Cloud connection (base URL, account email, API token) with a test-connection action.
- **FR-015**: System MUST allow Admins to link each logical project to one or more Jira project keys and configure a fix-version name pattern using a version placeholder (e.g., "Apply {version}").
- **FR-016**: System MUST provide an optional "auto-create fix version in Jira" setting per project, which creates the Jira fix version when a release is published if one does not already exist.
- **FR-017**: System MUST provide an optional subtask-handling setting per project (off by default) that, when enabled, counts a subtask commit as matching the parent ticket if the parent is in the Jira fix version.
- **FR-018**: System MUST provide a release creation wizard guiding users through: entering a version number (auto-suggested from parsed commit types using semver bump rules — breaking → major, feat → minor, fix/other → patch — and manually overridable), confirming the change range, selecting a template, editing generated notes in a markdown editor, previewing the rendered output, optionally running Jira reconciliation, and publishing to Confluence.
- **FR-019**: System MUST generate release notes grouping tickets into sections by priority: Breaking Changes, Features, Fixes, Other; a ticket with any breaking-change commit appears in Breaking Changes regardless of other commit types. Non-conventional commits (those in the "Unscoped" bucket) are excluded from generated release notes.
- **FR-020**: System MUST create a Confluence page with the release notes upon publishing and store the resulting Confluence page URL on the release record. If an idempotent retry of a failed publish finds the page already exists, it updates the page content rather than creating a duplicate. Once a release is marked as published, its notes and Confluence page are locked; further edits must be made directly in Confluence.
- **FR-021**: System MUST produce a Jira reconciliation report categorising tickets into matched (present in both Jira fix version and Git commits), Jira-only, and Git-only buckets, with a match-rate percentage and summary counts.
- **FR-022**: System MUST persist the reconciliation snapshot on the release record so it can be reviewed at any time without re-querying Jira.
- **FR-023**: System MUST allow Admins to add a Git-only ticket to the Jira fix version directly from the reconciliation view; Viewers cannot perform this action.
- **FR-024**: System MUST allow Admins to create, edit, and delete release note templates using structured variable placeholders (project name, version, ticket list, commit list, contributors, repositories), with a live preview pane rendering sample output in real time.
- **FR-025**: System MUST allow one template to be marked as the default per project; this template is pre-selected during release creation.
- **FR-026**: System MUST enforce two access roles — Admin (full read and write access) and Viewer (read-only; cannot modify configuration or trigger Jira mutations) — across all platform actions.
- **FR-027**: System MUST support username and password authentication with session tokens that expire after 8 hours and support token refresh to avoid forcing re-login during active sessions.
- **FR-028**: System MUST provide a one-time first-run setup wizard for creating the initial Admin account; the wizard is permanently disabled after first use.

### Key Entities

- **Git Provider Connection**: Represents a link to an external code hosting service. Holds the organisation URL, encrypted access credential, last sync time, and connection status.
- **Repository**: A code repository discovered via a Git provider connection. Tracks default branch, web URL, tracked/untracked status, and logical project memberships.
- **Logical Project**: A named grouping of repositories defined within this platform. Has a name, description, badge colour, Jira project key links, fix-version name pattern, Confluence target, and default release note template.
- **Commit**: A single recorded code change with author, timestamp, message, parsed change type, ticket scope, and breaking-change flag.
- **Ticket Group**: An aggregation of one or more commits sharing a Jira ticket ID, with a computed dominant change type, representative title, and list of contributing repositories.
- **Release**: A versioned snapshot of a project's changes, including version number, change range per repository, generated release notes, edited release notes, publication status, Confluence page URL, and reconciliation snapshot (if run).
- **Reconciliation Snapshot**: A point-in-time comparison between Jira fix version tickets and Git-commit tickets, with matched, Jira-only, and Git-only buckets, match-rate statistics, and last-synced timestamp.
- **Release Note Template**: A named, reusable template with structured variable placeholders for generating release notes. One template is marked as default per project.
- **User**: A platform account with username, hashed password, role (Admin or Viewer), and active/inactive status.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An Admin can complete the full initial setup — connecting Azure DevOps, Jira, and Confluence; syncing repositories; defining a project; assigning repositories; and configuring all integration settings — in under 15 minutes from a fresh installation.
- **SC-002**: A tech lead can create a release for a project spanning three or more repositories, run Jira reconciliation, and publish release notes to Confluence in under 5 minutes.
- **SC-003**: Viewers can see "changes since last release" data for any project at any time without any additional Admin action; data freshness reflects the most recent Admin-triggered "Sync now" operation.
- **SC-004**: Reconciliation of a sprint containing 20–50 Jira tickets completes and displays results in under 10 seconds.
- **SC-005**: All configuration and write actions are inaccessible to Viewer-role users, with 100% enforcement — no write action succeeds from a Viewer account under any circumstances.
- **SC-006**: Release notes generated from a project with multiple repositories accurately place each ticket in exactly one section (the highest-priority section it qualifies for) with no ticket appearing in more than one section.
- **SC-007**: The Confluence page URL is stored on the release record and accessible to all authenticated users immediately after a successful publish action.

## Assumptions

- The platform is deployed for a single organisation (single-tenant); multi-organisation support is out of scope for this version.
- Users have access to Azure DevOps personal access tokens with sufficient permissions to list and read repositories in their organisation.
- The Jira and Confluence accounts used for integration have sufficient API permissions to read projects, fix versions, and create or update pages respectively.
- Repositories are expected to follow the Conventional Commits 1.0.0 standard; the platform accommodates non-compliant commits via the "Unscoped" bucket but does not enforce the convention.
- Version tagging in repositories follows semantic versioning (e.g., v1.2.3); the platform identifies the latest semver tag per repository as the release baseline.
- GitHub, GitLab, and Bitbucket are not supported in this version; the platform architecture accommodates adding them later without changes to existing functionality.
- There is no single sign-on or OAuth integration; all authentication is username and password.
- The platform is not responsible for triggering deployments, sending notifications to chat tools, or transitioning Jira ticket statuses.
- Only Jira Cloud is supported; Jira Data Center and Jira Server are out of scope for this version.
- All external access credentials (personal access tokens, API tokens) are stored encrypted at rest.
- Cross-project Jira tickets (whose key prefix does not match any configured Jira project key for the logical project) are ignored during reconciliation in this version.
- The platform never creates, pushes, or modifies Git tags; version tags are managed entirely by developers outside the platform. The "changes since last release" view therefore reflects the most recently pushed semver tag in each repository.
