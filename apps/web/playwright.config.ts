import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests against the real stack: the Vue dev server (started here
 * unless one is already running) proxying to the API from `scripts/dev.sh api`,
 * backed by the seeded local database. Run them with `scripts/dev.sh e2e`,
 * which also passes the demo-account password.
 */
const baseURL = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5173'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'pnpm dev',
    url: baseURL,
    reuseExistingServer: true,
    timeout: 60_000,
  },
})
