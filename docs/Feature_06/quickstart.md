# Quickstart: Project Page Templates

**Phase**: 1
**Status**: Final

A ten-step click-through that validates the feature end-to-end against a real Confluence Cloud target. Mirrors the smoke test in `tasks.md` T063.

---

## Prerequisites

1. A running deployment with Milestones 1–11 complete.
2. At least one logical project (we'll use **Payments**) with one version-primary repository whose latest semver tag is `1.30.0`, and 2–3 commits since that tag distributed across 5+ Jira tickets.
3. A configured Confluence Cloud connection with at least one space (we'll use space key `ENG`) and a parent page (we'll use the **Payments Releases** page).
4. A configured Jira connection with at least one project mapped to **Payments**.
5. Two release-note templates created in **Settings → Templates**:
   - **Release Notes — Default** — body includes `{{#each repositories}}…{{nextTag}}…{{/each}}`, `{{tickets.features}}`, `{{#if reconciliation}}…{{/if}}`, and `{{custom.slackChannel}}`.
   - **Smoke Test Checklist — Default** — body lists post-deploy smoke tests; references `{{custom.oncall}}`.

---

## Steps

### 1. Bind templates to the project

Sign in as Admin. Navigate to **Settings → Projects → Payments → Pages**.

- Click **Add page template**.
- Template: **Release Notes — Default**.
- Kind: **Release Notes**.
- Page title: `{{project.name}} {{version}} — Release Notes`.
- Parent page: leave blank (use project default).
- Link from release notes: **off**.
- Sort order: **1**.

Click **Save**. Click **Add page template** again.

- Template: **Smoke Test Checklist — Default**.
- Kind: **Checklist**.
- Page title: `{{project.name}} {{version}} — Smoke Tests`.
- Parent page: leave blank.
- Link from release notes: **on**.
- Sort order: **2**.

Click **Save**.

**Expected**: The Pages table shows two rows in the order Release Notes, Smoke Tests. Drag the rows to verify reorder works, then return to the original order.

### 2. Add custom variables

Still on the Pages tab, scroll to **Custom variables**.

- Add `slackChannel` = `#payments-releases`.
- Add `oncall` = `payments-oncall`.

Click **Save variables**.

**Expected**: A banner confirms the variables were saved. The page refreshes and both rows are present.

### 3. Set the version bump strategy

Navigate to **Settings → Projects → Payments → General**. Set **Version bump strategy** to **Minor**. Save.

### 4. Start the release wizard

Navigate to the **Payments** project page. Click **Create release**.

**Expected**: The wizard opens on **Step 1 — Prepare pages**. The header summary shows:

- Project: Payments.
- Version: **1.31.0** (auto-resolved from `1.30.0` + minor bump).
- Previous version: **1.30.0**.
- A repositories table listing each repo with its previous tag, computed next tag, commit count, and ticket count.
- Bucket counts: Breaking / Features / Fixes / Other.

Two tabs are present: **Release Notes** and **Smoke Tests**. The page-title fields show the rendered titles `Payments 1.31.0 — Release Notes` and `Payments 1.31.0 — Smoke Tests`.

### 5. Inspect rendered bodies

Click the **Release Notes** tab.

**Expected**:

- The body contains a repositories table with each repo's name, previous tag, and next tag.
- The Features section lists Jira ticket keys with summaries.
- The reconciliation block is **absent** (reconciliation has not been run yet).
- `{{custom.slackChannel}}` is rendered as `#payments-releases`.

Click the **Smoke Tests** tab.

**Expected**: The smoke-test checklist body is rendered. `{{custom.oncall}}` is rendered as `payments-oncall`.

### 6. Edit a page in-wizard

On the **Smoke Tests** tab, edit one of the checklist items. Click **Next** to move to Step 2 (Confirm change range). Click **Back** to return to Prepare pages.

**Expected**: Your edit is still present.

### 7. Run Jira reconciliation

Move through Steps 2–4 of the wizard until you reach **Step 5 — Reconcile with Jira**. Click **Run reconciliation**.

**Expected**: The reconciliation view populates with matched, Jira-only, and Git-only buckets. A new button appears: **Refresh pages with reconciliation data**.

Click that button.

**Expected**: A per-tab prompt appears for the Smoke Tests tab (which you edited): "You have unsaved edits on this tab — keep them or discard?" Choose **Keep my edits**.

Return to Step 3 (Edit pages). Open the **Release Notes** tab.

**Expected**: The reconciliation summary block is now present in the body (matched/Jira-only/Git-only counts and the match-rate percentage), because the template's `{{#if reconciliation}}` block is now satisfied. Your edit to the Smoke Tests tab is preserved.

### 8. Validate title rules

On the **Release Notes** tab, manually clear the page-title field. Try to advance to **Publish**.

**Expected**: A blocking error is shown: "Page title cannot be empty." Restore the title.

### 9. Publish

Move to **Step 6 — Publish**. Click **Publish**.

**Expected**: A progress UI shows two steps:

- Publishing **Release Notes** → success, with the published Confluence URL.
- Publishing **Smoke Tests** → success, with the published Confluence URL.
- Final step: updating **Release Notes** with the related-pages link → success.

After publish, the Release record's detail screen lists both Confluence page URLs.

### 10. Verify in Confluence

Open **Confluence → ENG space → Payments Releases**.

**Expected**:

- Two new child pages: `Payments 1.31.0 — Release Notes` and `Payments 1.31.0 — Smoke Tests`.
- The Release Notes page has a "Related pages" section near the bottom containing a link to the Smoke Tests page. Inspect the page source: the section is delimited by `<!-- related-pages:start -->` and `<!-- related-pages:end -->`.
- Re-running publish in the wizard produces no duplicate pages and no duplicated related-pages section.

---

## Acceptance gates for this quickstart

The feature passes the quickstart when **all** of the following hold:

- Steps 1–4 complete without admin intervention beyond what's described.
- Step 5 shows resolved variables (version, previous version, per-repo tags, custom variables) populated from the project's real Git and Jira data.
- Step 7's "Refresh pages with reconciliation data" preserves manual edits when the admin chooses **Keep my edits**.
- Step 8's blank-title validation blocks publish.
- Step 9 produces two cross-linked Confluence pages.
- Step 10's republish is idempotent.

---

## Common failure modes (and what they tell you)

| Symptom | Likely cause | Where to look |
|---|---|---|
| "No Release Notes binding" error in Step 4 | Seed migration didn't run, or admin removed the seeded binding without replacement | `BindingMigrationTests`; T014; FR-002 |
| Version resolves to `1.30.1` instead of `1.31.0` | Bump strategy is `Patch`, not `Minor` | Step 3 setup; `VersionResolver` |
| `{{custom.slackChannel}}` renders empty | Custom variable not saved, or key typo in template | Step 2; FR-008 |
| Reconciliation block stays absent after Step 7 | "Refresh pages with reconciliation data" button not clicked, or staleness hash differs | R-004; T052 |
| Duplicate related-pages section after republish | Marker block detection broke | R-001; T042 |
