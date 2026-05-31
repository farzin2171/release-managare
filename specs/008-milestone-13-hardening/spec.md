# Feature Specification: Milestone 13 — Security, Service Ownership & UX Hardening

**Feature Branch**: `009-milestone-13-hardening`
**Created**: 2026-05-30
**Status**: Draft
**Input**: User description: "@docs/Feature_07/06-addendum-milestone-13.md"

## Overview

This milestone hardens the Release Manager platform across five distinct concerns: protecting the initial system setup from unauthorized access, enriching repository metadata with ownership information, introducing a built-in release summary page template, keeping users seamlessly logged in across long sessions, and allowing administrators to delete draft releases they no longer need.

---

## Clarifications

### Session 2026-05-30

- Q: When a user reopens the app with a still-valid but soon-to-expire session token, should the proactive renewal timer activate immediately based on the token's remaining lifetime? → A: Yes — the timer initializes from the existing token's expiry on every app load, not only after explicit login.
- Q: When cloning a system template and the default clone name already exists, how should naming be handled? → A: Auto-increment the suffix — second clone becomes "… (copy 2)", third becomes "… (copy 3)", etc.

---

## User Scenarios & Testing

### User Story 1 — Operator Secures Initial Deployment (Priority: P1)

An operator deploying Release Manager for the first time to a company cloud environment must ensure no unauthorized party can claim the system administrator role before the legitimate admin completes setup. The operator configures a secret setup key in the server environment before starting the application, then shares it with the designated admin. The admin uses the key once to create the admin account; the setup door is then permanently closed.

**Why this priority**: Without this protection, any person who reaches the server first can claim full administrative control — a critical security risk in shared or cloud environments.

**Independent Test**: Can be fully tested by deploying without a key (confirm refused start), then deploying with a key and calling the setup endpoint with and without the correct header.

**Acceptance Scenarios**:

1. **Given** the application starts with no secret key configured and no admin account exists, **When** the application starts, **Then** it refuses to start and logs a clear fatal error message.
2. **Given** the application is running with the secret key configured, **When** a caller submits a setup request without the key header, **Then** the system returns an "unauthorized" error.
3. **Given** the application is running with the secret key configured, **When** a caller submits a setup request with an incorrect key, **Then** the system returns an "unauthorized" error.
4. **Given** the application is running and no admin account exists, **When** a caller submits a setup request with the correct key, **Then** the admin account is created successfully.
5. **Given** an admin account already exists, **When** any caller submits a setup request (even with the correct key), **Then** the system returns a "setup already complete" conflict error.
6. **Given** a setup key is configured and an admin account exists, **When** the operator removes the key from the environment, **Then** the application continues to run normally (the key is no longer relevant).

---

### User Story 2 — Admin Assigns Service Ownership to Repositories (Priority: P2)

An admin needs to record which team or person is responsible for each repository/service. This information should be visible at a glance in the repositories screen and flow through automatically to generated release documentation so stakeholders know who to contact about each service.

**Why this priority**: Release coordination across many teams requires clear ownership visibility. Without it, stakeholders cannot quickly identify who to contact when a service has changes in a release.

**Independent Test**: Can be fully tested by opening Settings → Repositories, setting a "Service Owner" value for a repository, saving, and verifying it persists across page refreshes.

**Acceptance Scenarios**:

1. **Given** an admin is on the Settings → Repositories screen, **When** they edit a repository, **Then** they see a "Service Owner" text field (label: "Service Owner", placeholder: "e.g. Platform Team") accepting up to 120 characters.
2. **Given** an admin sets a service owner value and saves, **When** they refresh the page, **Then** the value is still present.
3. **Given** an admin clears the service owner field and saves, **When** they view the repository, **Then** the field shows empty/dash (—) indicating no owner.
4. **Given** a viewer role user views a repository, **When** they look at the service owner field, **Then** they see the value but have no edit capability.
5. **Given** a repository has a service owner set, **When** a release summary is generated including that repository, **Then** the service owner appears in the output.

---

### User Story 3 — Admin Uses Built-In Release Summary Template (Priority: P2)

An admin wants to generate a standardized one-page release summary for stakeholders showing all repositories in a release, their version changes, service owners, and commit/ticket counts — without having to author a template from scratch. A system-provided "Release Summary (Default)" template is available immediately upon installation and auto-bound to every new project.

**Why this priority**: This template delivers immediate value to new installations and standardizes the most commonly needed release communication artifact across all teams.

**Independent Test**: Can be fully tested by creating a new project, previewing the auto-bound Release Summary template for a release with multiple repositories, and verifying all rows and ownership data appear correctly.

**Acceptance Scenarios**:

1. **Given** a new installation, **When** an admin views the Templates list, **Then** they see "Release Summary (Default)" with a clear system indicator badge and no Edit or Delete buttons.
2. **Given** the system template exists, **When** an admin attempts to edit or delete it, **Then** the system rejects the action with a "system template is read-only" error.
3. **Given** an admin wants a customized version, **When** they click "Clone", **Then** a new editable copy named "Release Summary (Default) (copy)" is created. If that name is already taken, the copy is named "Release Summary (Default) (copy 2)", incrementing until a unique name is found.
4. **Given** a new project is created, **When** an admin views its template bindings, **Then** the "Release Summary (Default)" template is already bound to the project.
5. **Given** a release includes three repositories with different service owners, **When** the admin previews the Release Summary template, **Then** the output contains a table with three data rows, each showing the correct repository name, service owner, previous version, next version, commit count, and ticket count.
6. **Given** a repository has no service owner set, **When** the Release Summary is previewed, **Then** the service owner column displays "—" for that row.

---

### User Story 4 — User Stays Logged In Across Long Working Sessions (Priority: P2)

A release manager who spends an hour in a multi-step release wizard should not lose their work and be redirected to the login page when their session token expires. The system should silently renew their session in the background without interrupting their workflow.

**Why this priority**: Losing unsaved work mid-task due to silent session expiry is a significant usability failure that erodes trust and productivity.

**Independent Test**: Can be fully tested by shortening the session lifetime in the development configuration to 30 seconds, working through a wizard flow, and verifying the session renews silently without any redirect.

**Acceptance Scenarios**:

1. **Given** a user is actively using the application (or returns to an open browser tab) with a soon-to-expire session, **When** the session approaches expiry (within 2 minutes), **Then** the system silently renews the session in the background with no visible interruption.
2. **Given** a user makes an API request with an expired session but valid renewal token, **When** the request is made, **Then** the system automatically renews the session and retries the request — the user sees the result, not an error.
3. **Given** two actions are triggered simultaneously while the session is renewing, **When** the renewal completes, **Then** both actions complete successfully with only one renewal request sent to the server.
4. **Given** a user's renewal token has also expired or been revoked, **When** any API request is made, **Then** the user is redirected to the login page with a clear message: "Your session has expired. Please log in again."
5. **Given** a user's session renews successfully, **When** inspecting browser storage, **Then** the renewal token is not accessible via JavaScript (it is stored in a secure, server-only cookie).

---

### User Story 5 — Admin Deletes an Unwanted Draft Release (Priority: P3)

An admin who created a Draft release by mistake, or who abandoned a release mid-workflow, needs to remove it to keep the releases list clean. The delete action is only available for Draft releases (not Published ones) and requires an explicit confirmation to prevent accidents.

**Why this priority**: While lower urgency than security and data features, cluttered release lists with stale drafts create confusion and impede day-to-day operations.

**Independent Test**: Can be fully tested by creating a Draft release, clicking "Delete draft" from the list view and confirming, then verifying the release is removed from the list.

**Acceptance Scenarios**:

1. **Given** a Viewer role user views the releases list, **When** they look at any release row, **Then** no delete option is visible.
2. **Given** an Admin views a Published release row, **When** they inspect the row, **Then** no delete option is visible.
3. **Given** an Admin views a Draft release row, **When** they open the row's action menu, **Then** a "Delete draft" option is present.
4. **Given** an Admin clicks "Delete draft", **When** the confirmation dialog appears, **Then** it shows the message: "Delete draft release '<name>'? This cannot be undone." with Cancel and Delete (red/destructive) buttons.
5. **Given** an Admin confirms deletion, **When** deletion succeeds, **Then** the release row disappears from the list with a visual fade animation and a toast notification confirms the deletion.
6. **Given** an Admin cancels the deletion dialog, **When** they return to the list, **Then** the release is still present and unchanged.
7. **Given** an Admin confirms deletion but another user published the release between the click and confirmation, **When** the conflict is returned by the server, **Then** an error toast appears: "This release has been published and can no longer be deleted." and the row status updates to Published without a blank screen.
8. **Given** an Admin is on the Release Detail page for a Draft release, **When** they view the page header, **Then** a "Delete draft" button is present adjacent to the Edit button.
9. **Given** an Admin confirms deletion from the Release Detail page, **When** deletion succeeds, **Then** they are navigated back to the project's Releases list with a toast confirmation.
10. **Given** another user deletes a draft release while an Admin has its detail page open, **When** the Admin's next action triggers an API call, **Then** the detail page shows a "Release not found" message with a link back to the project releases list.

---

### Edge Cases

- What happens when the setup key environment variable is set but is less than 32 characters? The application should reject it at startup with a clear configuration error.
- What happens if two admins attempt to call the setup endpoint simultaneously (race condition on first use)? The first one succeeds; the second receives the "setup already complete" conflict response.
- What happens when a service owner value exceeds 120 characters? The input field prevents entry beyond 120 characters; the server also validates and rejects it.
- What happens if the Release Summary template seed row is accidentally deleted from the database? Manual re-seeding via migration re-run restores it; no automated self-healing is required.
- What happens when a user makes 10 simultaneous requests while the session is renewing? All requests queue behind a single renewal call, then all succeed when the new token is received.
- What happens when the detail page for a deleted draft release is accessed via a bookmarked URL? The page shows a "Release not found" message with a navigation link back to the project.

---

## Requirements

### Functional Requirements

**Setup Endpoint Security (Feature A)**

- **FR-A01**: The system MUST refuse to start if no admin account exists and no setup security key has been configured in the server environment, logging a clear fatal error message.
- **FR-A02**: The setup endpoint MUST require callers to present a pre-configured security key in the request header (`X-Setup-Key`).
- **FR-A03**: Requests to the setup endpoint with a missing or incorrect security key MUST be rejected with an "unauthorized" (401) response containing the error code `setup_key_invalid`.
- **FR-A04**: Requests to the setup endpoint when an admin account already exists MUST be rejected with a "conflict" (409) response containing the error code `setup_already_complete`, regardless of whether the correct key is provided.
- **FR-A05**: The security key MUST never appear in application logs, health check responses, or API documentation.
- **FR-A06**: Once at least one admin account exists, the setup endpoint MUST remain permanently disabled; removing the key from the environment at that point MUST NOT affect application operation.

**Service Owner Field (Feature B)**

- **FR-B01**: Each repository record MUST support an optional "Service Owner" free-text field of up to 120 characters.
- **FR-B02**: The Settings → Repositories screen MUST display the "Service Owner" field as an editable text input for Admin role users, with the label "Service Owner" and placeholder "e.g. Platform Team".
- **FR-B03**: Viewer role users MUST see the "Service Owner" value as read-only; the field MUST NOT be editable for Viewers.
- **FR-B04**: Repositories with no service owner set MUST display "—" in the UI.
- **FR-B05**: All repository data endpoints MUST include the `serviceOwner` field in their responses (null when not set).
- **FR-B06**: The `serviceOwner` value MUST be available as a non-null string variable in all release page templates (empty string when not set).

**Release Summary System Template (Feature C)**

- **FR-C01**: The system MUST include a built-in "Release Summary (Default)" template that is available from first installation without any admin configuration.
- **FR-C02**: System templates MUST be marked with a visible badge in the Templates list UI and MUST NOT show Edit or Delete buttons.
- **FR-C03**: Attempts to edit or delete a system template via the API MUST be rejected with a "forbidden" (403) response containing the error code `system_template_readonly`.
- **FR-C04**: Admins MUST be able to clone a system template; the clone MUST be fully editable and named "<original name> (copy)". If that name is already taken, the system MUST auto-increment the suffix: "(copy 2)", "(copy 3)", and so on until a unique name is found.
- **FR-C05**: Every newly created project MUST automatically have the "Release Summary (Default)" template bound to it.
- **FR-C06**: The Release Summary template MUST render a table with one row per repository in the release, showing: repository name, service owner (or "—"), previous version, next version, commit count, and ticket count.
- **FR-C07**: All release page templates MUST have access to the full list of repositories in the release (name, service owner, previous version, next version, commit count, ticket count).

**Session Auto-Renewal (Feature D)**

- **FR-D01**: When an authenticated request receives an "unauthorized" (401) response, the system MUST automatically attempt one silent session renewal before presenting any error to the user.
- **FR-D02**: If the silent renewal succeeds, the original request MUST be retried automatically; the user MUST experience the operation as if no expiry occurred.
- **FR-D03**: If the silent renewal fails, the user MUST be redirected to the login page with the message: "Your session has expired. Please log in again."
- **FR-D04**: The system MUST proactively renew the session 2 minutes before the current session token expires, even if no request is in progress. On every app load (including returning to a browser tab), the timer MUST be re-initialized based on the existing token's remaining lifetime, not only after an explicit login in the current session.
- **FR-D05**: If multiple requests are made simultaneously while a renewal is in progress, only one renewal request MUST be sent; all queued requests MUST be fulfilled once the new token arrives.
- **FR-D06**: The renewal token MUST be stored in a server-managed, JavaScript-inaccessible secure cookie; it MUST NOT be stored in browser local storage or any JavaScript-accessible location.

**Delete Draft Releases (Feature E)**

- **FR-E01**: The Releases list MUST show a "Delete draft" action (in a kebab/overflow menu) on Draft release rows, visible only to Admin role users.
- **FR-E02**: The "Delete draft" action MUST display a confirmation dialog before proceeding: "Delete draft release '<name>'? This cannot be undone."
- **FR-E03**: The confirmation dialog MUST offer Cancel (neutral) and Delete (destructive/red) buttons.
- **FR-E04**: On confirmed deletion, the row MUST be removed from the list with a fade animation and a toast notification: "Draft release '<name>' deleted."
- **FR-E05**: The Release Detail page for a Draft release MUST show a "Delete draft" button in the page header, visible only to Admin role users.
- **FR-E06**: On successful deletion from the Release Detail page, the user MUST be navigated back to the project's Releases list.
- **FR-E07**: If deletion fails because the release was published by another user mid-flight (409 conflict), an error toast MUST appear: "This release has been published and can no longer be deleted." and the row/page status MUST update to reflect the Published state without a blank screen or crash.
- **FR-E08**: If a user has the Release Detail page open for a release that another user deletes, the page MUST handle the resulting "not found" state gracefully by showing a "Release not found" message with a link back to the project's Releases list.

### Key Entities

- **Setup Security Key**: A secret string configured in the server environment that gates the one-time admin bootstrap process. Exists only in the environment — never stored in the database.
- **Repository**: A tracked source code repository. Now carries an optional "Service Owner" text field.
- **System Template**: A built-in, read-only release page template seeded at installation. Can be cloned but not edited or deleted.
- **Project Template Binding**: The link between a project and a template. New projects automatically receive a binding to the "Release Summary (Default)" system template.
- **Release Summary Context**: The data available when rendering any release template, now extended to include a list of all repositories in the release with their version and ownership details.
- **Session Token / Renewal Token**: The short-lived credential used to authenticate API calls (session token) and the long-lived credential used to obtain a new session token (renewal token). The renewal token is stored exclusively in a secure server-managed cookie.

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: Every new deployment that omits the setup security key fails to start with a clear error message — 100% of misconfigured deployments are caught before becoming a security risk.
- **SC-002**: The initial admin setup endpoint rejects all requests without the correct security key — 0% unauthorized setup completions possible.
- **SC-003**: Admins can set or clear the "Service Owner" field on any repository in under 30 seconds, and the value is reflected in the next release summary preview without additional steps.
- **SC-004**: The "Release Summary (Default)" template is available and bound to every new project immediately upon project creation — 0 additional configuration steps required by the admin.
- **SC-005**: Users in long-running wizard flows (up to 8 hours) complete their tasks without session-expiry redirects — session renewals are transparent with 0 perceived interruptions.
- **SC-006**: Simultaneous actions during a session renewal cause no duplicate renewal requests — exactly 1 renewal call is made regardless of how many concurrent actions were pending.
- **SC-007**: Admins can delete any Draft release in under 3 clicks (open menu → confirm) — the release disappears from the list within 2 seconds of confirmation.
- **SC-008**: The renewal token is never accessible via browser developer tools' JavaScript console — verified by `document.cookie` not containing the renewal token value.

---

## Assumptions

- The system is deployed in a standard HTTPS environment; the secure cookie mechanism for the renewal token requires HTTPS to function correctly. HTTP-only local development environments are treated as an exception handled by developer configuration.
- "Admin" and "Viewer" are the only two roles in the system; no super-admin or operator-level role exists above Admin.
- The setup security key is expected to be a randomly generated string of at least 32 characters; the system enforces this minimum length at startup.
- Existing projects (created before Milestone 13) are not automatically assigned the "Release Summary (Default)" template — only new projects created after this milestone receive the auto-binding. Existing project admins can add it manually.
- The "Service Owner" field is free text in this version; it is not validated against a user directory or team list.
- The setup security key can be removed from the environment after the initial admin account is created; the application does not re-check for it once at least one user exists.
- Draft releases can only be deleted — not archived or put into a "cancelled" state. Deletion is permanent.
- The 404 handling for deleted releases on the detail page applies only to the scenario where the release is deleted externally while the page is open; navigating to a non-existent release URL directly also shows the same "Release not found" state.
- The proactive session renewal timer (2 minutes before expiry) is reset on every successful renewal, preventing renewal storms for very-long-lived sessions.
