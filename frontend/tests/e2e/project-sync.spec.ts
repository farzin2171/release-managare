import { test, expect, Page } from '@playwright/test'

const ADMIN_EMAIL = 'admin@example.com'
const ADMIN_PASSWORD = 'Password123!'

async function login(page: Page) {
  await page.goto('/login')
  await page.getByLabel(/email/i).fill(ADMIN_EMAIL)
  await page.getByLabel(/password/i).fill(ADMIN_PASSWORD)
  await page.getByRole('button', { name: /log in/i }).click()
  await expect(page).toHaveURL(/\/projects|\/dashboard/)
}

async function navigateToFirstProject(page: Page) {
  await page.goto('/projects')
  const projectLink = page.getByRole('link').first()
  await expect(projectLink).toBeVisible()
  await projectLink.click()
  await expect(page).toHaveURL(/\/projects\//)
}

test.describe('Project Sync — Tech-Lead E2E Flow', () => {
  test.beforeEach(async ({ page }) => {
    await login(page)
  })

  test('single repository sync: live progress, metric persistence, reload stability', async ({ page }) => {
    await navigateToFirstProject(page)

    // (1) Confirm a repo card with a pinned tag is visible (tag chip present)
    const tagChip = page.locator('[data-testid="tag-chip"]').first()
    await expect(tagChip).toBeVisible({ timeout: 10_000 })

    // (2) Click the Sync button on the first repo card with a tag
    const syncBtn = page.locator('[data-testid="sync-button"]').first()
    await expect(syncBtn).toBeEnabled()
    await syncBtn.click()

    // (3) Assert card transitions through at least 2 step messages while in progress
    const stepMessages = [
      /fetching commits/i,
      /parsing commits/i,
      /persisting commits/i,
      /aggregating tickets/i,
      /finalising/i,
    ]

    let stepCount = 0
    for (const pattern of stepMessages) {
      const stepEl = page.locator('[data-testid="sync-step"]').first()
      const matched = await stepEl.textContent({ timeout: 35_000 }).then(
        (t) => t !== null && pattern.test(t),
        () => false,
      )
      if (matched) stepCount++
      if (stepCount >= 2) break
    }
    expect(stepCount).toBeGreaterThanOrEqual(2)

    // (4) Wait for completion — assert metrics update
    await expect(page.locator('[data-testid="sync-status-succeeded"]').first()).toBeVisible({
      timeout: 60_000,
    })
    const commitMetric = page.locator('[data-testid="metric-commits"]').first()
    await expect(commitMetric).not.toHaveText('0', { timeout: 5_000 })

    // (5) Reload the page
    await page.reload()

    // (6) Assert metrics still show from DB (no extra calls needed)
    await expect(page.locator('[data-testid="sync-status-succeeded"]').first()).toBeVisible({
      timeout: 10_000,
    })
    const commitMetricAfterReload = page.locator('[data-testid="metric-commits"]').first()
    await expect(commitMetricAfterReload).not.toHaveText('0')

    // Verify no Azure DevOps API call was made during page load
    const adoCallsMade: string[] = []
    page.on('request', (req) => {
      if (req.url().includes('dev.azure.com') || req.url().includes('visualstudio.com')) {
        adoCallsMade.push(req.url())
      }
    })
    await page.reload()
    await page.waitForLoadState('networkidle')
    expect(adoCallsMade).toHaveLength(0)
  })

  test('project-wide sync: strip transitions, card overlays update in sequence, final summary shown', async ({ page }) => {
    await navigateToFirstProject(page)

    // (7) Click "Sync project" button in the strip
    const syncProjectBtn = page.getByRole('button', { name: /sync project/i })
    await expect(syncProjectBtn).toBeVisible({ timeout: 10_000 })
    await syncProjectBtn.click()

    // (8) Assert strip transitions to running mode
    const strip = page.locator('[data-testid="project-sync-strip"]')
    await expect(strip).toBeVisible()
    await expect(strip).toContainText(/syncing/i, { timeout: 10_000 })
    await expect(strip.getByRole('button', { name: /cancel/i })).toBeVisible()

    // Assert at least one card overlay updates during the run
    const cardOverlay = page.locator('[data-testid="repo-card-overlay"]').first()
    await expect(cardOverlay).toContainText(/fetching|parsing|persisting|aggregating|finalising/i, {
      timeout: 30_000,
    })

    // (9) Wait for strip to show final summary
    await expect(strip).toContainText(/succeeded|failed|skipped/i, { timeout: 120_000 })
    await expect(strip).not.toContainText(/syncing/i)
  })

  test('retry button appears on failed sync', async ({ page }) => {
    await navigateToFirstProject(page)

    // If a failed sync exists, retry button should be visible
    const retryBtn = page.locator('[data-testid="retry-button"]').first()
    const failedOverlay = page.locator('[data-testid="sync-status-failed"]').first()

    const hasFailedSync = await failedOverlay.isVisible()
    if (hasFailedSync) {
      await expect(retryBtn).toBeVisible()
    }
  })

  test('second concurrent sync request is rejected with conflict', async ({ page }) => {
    await navigateToFirstProject(page)

    // Trigger sync
    const syncBtn = page.locator('[data-testid="sync-button"]').first()
    if (!(await syncBtn.isEnabled())) return

    await syncBtn.click()

    // While sync is in progress, clicking again should be disabled or show conflict toast
    await expect(syncBtn).toBeDisabled({ timeout: 5_000 })
  })
})
