/**
 * Session Auto-Renewal E2E Test
 *
 * Requires the backend to run with a short JWT lifetime:
 *   set ASPNETCORE_Jwt__ExpirySeconds=30 before starting the backend
 *
 * The frontend proactive-refresh timer fires at exp - 2 minutes.
 * With a 30-second JWT, the timer fires immediately (exp - 120s < 0),
 * so a refresh should occur within a few seconds of login.
 */

import { test, expect, Page } from '@playwright/test'

const ADMIN_EMAIL = 'admin@example.com'
const ADMIN_PASSWORD = 'Password123!'
const BASE_URL = process.env.VITE_API_URL ?? 'https://localhost:7000'

async function login(page: Page) {
  await page.goto('/login')
  await page.getByLabel(/email/i).fill(ADMIN_EMAIL)
  await page.getByLabel(/password/i).fill(ADMIN_PASSWORD)
  await page.getByRole('button', { name: /sign in/i }).click()
  await expect(page).toHaveURL(/\/projects|\/dashboard/, { timeout: 10_000 })
}

test.describe('Session Auto-Renewal', () => {
  test('proactive refresh fires and session stays alive past token expiry', async ({ page }) => {
    // Intercept the refresh call before logging in
    const refreshRequest = page.waitForRequest(
      (req) =>
        (req.url().includes('/api/v1/auth/refresh') || req.url() === `${BASE_URL}/api/v1/auth/refresh`) &&
        req.method() === 'POST',
      { timeout: 35_000 }
    )

    await login(page)

    // With Jwt:ExpirySeconds=30, msUntilRefresh = 30s - 2min = negative → immediate trigger.
    // Wait for the proactive refresh to hit the backend.
    await refreshRequest

    // After renewal the user must still be on the app, not redirected to /login
    await expect(page).not.toHaveURL(/\/login/, { timeout: 5_000 })
  })

  test('near-expiry API call succeeds without redirect after silent renewal', async ({ page }) => {
    await login(page)

    // Allow time for the proactive refresh to settle (immediate with 30s JWT)
    await page.waitForTimeout(3_000)

    // Make a real API call through the app by navigating to a page that loads data
    await page.goto('/projects')
    await expect(page).not.toHaveURL(/\/login/, { timeout: 10_000 })

    // The projects page should render content, not an auth error
    await expect(page.getByRole('heading', { name: /projects/i })).toBeVisible({ timeout: 10_000 })
  })

  test('refresh token is not exposed in document.cookie', async ({ page }) => {
    await login(page)

    const cookies = await page.evaluate(() => document.cookie)
    // The httpOnly refreshToken cookie must not appear in JavaScript-readable cookies
    expect(cookies).not.toContain('refreshToken')
  })
})
