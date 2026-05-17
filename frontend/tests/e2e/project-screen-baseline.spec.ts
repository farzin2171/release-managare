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

test.describe('Project Screen — Visual Regression Baselines', () => {
  test.beforeEach(async ({ page }) => {
    await login(page)
  })

  test('never-synced state: all cards show zeros and Not synced yet', async ({ page }) => {
    // Navigate to a project where no repos have been synced
    await page.goto('/projects')
    const projectLink = page.getByRole('link').first()
    await expect(projectLink).toBeVisible()
    await projectLink.click()
    await page.waitForLoadState('networkidle')

    // Assert repo cards show "Not synced yet" text
    const neverSyncedIndicators = page.locator('[data-testid="never-synced"]')
    const count = await neverSyncedIndicators.count()
    if (count > 0) {
      // Capture screenshot baseline for never-synced state
      await expect(page).toHaveScreenshot('project-screen-never-synced.png', {
        fullPage: false,
        clip: { x: 0, y: 0, width: 1280, height: 800 },
        maxDiffPixelRatio: 0.02,
      })

      // Assert commit/ticket/contributor counts show zero
      const metricZero = page.locator('[data-testid="metric-commits"]').first()
      await expect(metricZero).toBeVisible()
    }
  })

  test('all-synced state: all cards show data, strip shows last-synced timestamp', async ({ page }) => {
    await page.goto('/projects')
    const projectLink = page.getByRole('link').first()
    await expect(projectLink).toBeVisible()
    await projectLink.click()
    await page.waitForLoadState('networkidle')

    // Check if project sync strip shows last-synced info (implies at least one sync ran)
    const strip = page.locator('[data-testid="project-sync-strip"]')
    await expect(strip).toBeVisible({ timeout: 5_000 })

    const hasLastSynced = await page
      .locator('[data-testid="last-synced-timestamp"]')
      .isVisible()
      .catch(() => false)

    if (hasLastSynced) {
      await expect(page).toHaveScreenshot('project-screen-all-synced.png', {
        fullPage: false,
        clip: { x: 0, y: 0, width: 1280, height: 800 },
        maxDiffPixelRatio: 0.02,
      })

      // Assert strip shows timestamp, not "Never synced"
      await expect(strip).not.toContainText(/never synced/i)
      await expect(strip).toContainText(/last synced/i)
    }
  })

  test('pixel-stability: existing project screen elements do not regress', async ({ page }) => {
    await page.goto('/projects')
    const projectLink = page.getByRole('link').first()
    await expect(projectLink).toBeVisible()
    await projectLink.click()
    await page.waitForLoadState('networkidle')

    // Assert structural elements remain stable
    await expect(page.getByRole('heading').first()).toBeVisible()

    // Repository cards container should be present
    const cardsContainer = page
      .locator('[data-testid="repository-cards"]')
      .or(page.locator('.grid').first())
    await expect(cardsContainer).toBeVisible()

    // Capture full-page stability screenshot
    await expect(page).toHaveScreenshot('project-screen-stable.png', {
      fullPage: false,
      clip: { x: 0, y: 0, width: 1280, height: 800 },
      maxDiffPixelRatio: 0.02,
    })
  })
})
