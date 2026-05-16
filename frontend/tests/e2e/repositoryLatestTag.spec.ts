import { test, expect } from '@playwright/test'

const ADMIN_EMAIL = 'admin@example.com'
const ADMIN_PASSWORD = 'Password123!'

test.describe('Repository Latest Tag — Admin pin-tag flow', () => {
  test.beforeEach(async ({ page }) => {
    // Log in as Admin
    await page.goto('/login')
    await page.getByLabel(/email/i).fill(ADMIN_EMAIL)
    await page.getByLabel(/password/i).fill(ADMIN_PASSWORD)
    await page.getByRole('button', { name: /log in/i }).click()
    await expect(page).toHaveURL(/\/projects|\/dashboard/)
  })

  test('Admin can pin a tag and project screen shows the badge', async ({ page }) => {
    // Navigate to Settings → Repositories
    await page.goto('/settings/repositories')
    await expect(page.getByRole('heading', { name: /repositories/i })).toBeVisible()

    // Click first tracked repository row to open detail sheet
    const repoRow = page.getByRole('row').filter({ hasText: /tracked/i }).first()
    await repoRow.click()
    await expect(page.getByRole('heading', { name: /latest tag/i })).toBeVisible()

    // Click "Set latest tag" / "Fetch tags"
    await page.getByRole('button', { name: /set latest tag|change tag/i }).click()

    // Tag picker dialog opens — wait for tag list to load
    await expect(page.getByRole('dialog')).toBeVisible()
    await expect(page.getByRole('row').nth(1)).toBeVisible({ timeout: 10_000 })

    // Select first tag in the list
    await page.getByRole('row').nth(1).click()

    // Confirm the selection
    await page.getByRole('button', { name: /confirm/i }).click()

    // Success: dialog closes and sheet shows pinned tag
    await expect(page.getByRole('dialog')).not.toBeVisible()
    const pinnedTagName = await page.getByRole('code').first().textContent()
    expect(pinnedTagName).toBeTruthy()

    // Navigate to project screen — verify "Latest tag" column shows badge
    await page.goto('/projects')
    await page.getByRole('link').first().click()
    await expect(page.getByTestId('latest-tag-badge')).toBeVisible()

    // Return to settings and clear the tag
    await page.goto('/settings/repositories')
    await repoRow.click()
    await page.getByRole('button', { name: /clear/i }).click()

    // Confirmation dialog
    await expect(page.getByRole('alertdialog')).toBeVisible()
    await page.getByRole('button', { name: /clear tag/i }).click()

    // Sheet now shows "Not set"
    await expect(page.getByText(/not set/i)).toBeVisible()

    // Project screen shows "—" with amber dot
    await page.goto('/projects')
    await page.getByRole('link').first().click()
    await expect(page.getByTestId('amber-dot')).toBeVisible()
  })
})
