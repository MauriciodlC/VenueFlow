import { expect, type Page } from '@playwright/test'

/** Fixed ids from services/api/src/VenueFlow.Infrastructure/Seeding/DemoVenues.cs. */
export const demoVenues = {
  riversideStudio: '5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e01',
} as const

export const demoAccounts = {
  guest: 'guest@example.test',
  host: 'host@example.test',
  otherHost: 'other-host@example.test',
} as const

export function demoPassword(): string {
  const password = process.env.E2E_DEMO_PASSWORD
  if (!password) {
    throw new Error('Set E2E_DEMO_PASSWORD to DEMO_PASSWORD from infra/compose/.env (scripts/dev.sh e2e does this).')
  }
  return password
}

/** Signs in through the form and waits until the app has left the sign-in page. */
export async function signIn(page: Page, email: string, redirect = '/'): Promise<void> {
  await page.goto(`/sign-in?redirect=${encodeURIComponent(redirect)}`)
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(demoPassword())
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page).not.toHaveURL(/\/sign-in/)
}
