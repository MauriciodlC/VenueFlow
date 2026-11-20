import { expect, test } from '@playwright/test'
import { demoAccounts, demoVenues, signIn } from './support'

test.describe('host venue drafts', () => {
  test('a host creates a draft, completes every step and submits it for review', async ({ page }, testInfo) => {
    const name = `E2E studio ${Date.now()}`
    await signIn(page, demoAccounts.host, '/host')
    await expect(page.getByRole('heading', { name: 'Host workspace' })).toBeVisible()

    await page.getByRole('link', { name: 'New venue' }).click()
    await page.getByLabel('Venue name').fill(name)
    await page.getByLabel('Timezone').selectOption('Europe/Madrid')
    await page.getByRole('button', { name: 'Create draft' }).click()
    await expect(page).toHaveURL(/\/host\/venues\/[0-9a-f-]{36}$/)
    await expect(page.getByRole('status')).toHaveText('Saved.')

    await page.getByRole('button', { name: '2. Location' }).click()
    await page.getByLabel('City').fill('Madrid')
    await page.getByLabel('Street address').fill('Calle de Prueba 7 (synthetic)')

    await page.getByRole('button', { name: '3. Description & access' }).click()
    await page.getByLabel('Description').fill('Bright test studio with two long tables.')
    await page.getByRole('button', { name: 'Add Step-free entrance to accessibility notes' }).click()
    await page.getByRole('button', { name: 'Add Accessible toilet to accessibility notes' }).click()
    await expect(page.locator('textarea[name="accessibilityNotes"]')).toHaveValue('Step-free entrance. Accessible toilet.')

    await page.getByRole('button', { name: '4. Rooms' }).click()
    await page.getByRole('button', { name: 'Add room' }).click()
    await page.getByLabel('Room name').fill('Main room')
    await page.getByLabel('Capacity').fill('16')

    await page.getByRole('button', { name: '5. Preview & submit' }).click()
    const preview = page.locator('.listing-preview')
    await expect(preview.getByRole('heading', { name })).toBeVisible()
    await expect(preview).toContainText('Madrid · Europe/Madrid · up to 16 people')
    await expect(preview).toContainText('Step-free entrance. Accessible toilet.')
    // Guests only get the street address with their booking confirmation.
    await expect(preview).not.toContainText('Calle de Prueba')
    await testInfo.attach('draft-preview', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' })

    await page.getByRole('button', { name: 'Submit for review' }).click()
    await expect(page.getByRole('status')).toContainText('Submitted for review')
    await expect(page.getByText('This venue is in review, so it can no longer be edited here.')).toBeVisible()

    await page.getByRole('link', { name: '← Back to your venues' }).click()
    const row = page.getByRole('row').filter({ hasText: name })
    await expect(row).toContainText('Madrid')
    await expect(row).toContainText('In review')
  })

  test('a draft cannot be opened or saved by another host', async ({ page }) => {
    const url = `/host/venues/${demoVenues.riversideStudio}`
    await signIn(page, demoAccounts.otherHost, '/host')
    await expect(page.getByText('Loading your venues…')).toHaveCount(0)
    await expect(page.getByRole('row').filter({ hasText: 'Riverside Makers Studio' })).toHaveCount(0)

    await page.goto(url)
    await expect(page.getByRole('alert')).toHaveText('This venue does not exist or belongs to another host.')
    await expect(page.getByRole('button', { name: 'Save draft' })).toHaveCount(0)
    await expect(page.locator('input[name="name"]')).toBeDisabled()

    // Going around the UI does not help: the API answers 404 as for an unknown id.
    const response = await page.request.put(`/api/host/venues/${demoVenues.riversideStudio}`, {
      headers: { 'If-Match': '*' },
      data: { name: 'Hijacked', timeZoneId: 'UTC' },
    })
    expect(response.status()).toBe(404)
    expect((await response.json()).code).toBe('venue_not_found')
  })

  test('a guest is sent away from the host workspace and the API refuses the edit', async ({ page }) => {
    await signIn(page, demoAccounts.guest)

    await page.goto(`/host/venues/${demoVenues.riversideStudio}`)
    await expect(page).toHaveURL(/\/$/)
    await expect(page.getByRole('link', { name: 'Host workspace' })).toHaveCount(0)

    const response = await page.request.put(`/api/host/venues/${demoVenues.riversideStudio}`, {
      headers: { 'If-Match': '*' },
      data: { name: 'Hijacked', timeZoneId: 'UTC' },
    })
    expect(response.status()).toBe(403)
  })

  test('the owner still sees the seeded venue unchanged', async ({ page }) => {
    await signIn(page, demoAccounts.host, '/host')

    const row = page.getByRole('row').filter({ hasText: 'Riverside Makers Studio' })
    await expect(row).toContainText('Published')
    await row.getByRole('link', { name: 'Riverside Makers Studio' }).click()
    await expect(page.getByText('This venue is published, so it can no longer be edited here.')).toBeVisible()
    await expect(page.locator('input[name="name"]')).toHaveValue('Riverside Makers Studio')
  })
})
