import { expect, test } from '@playwright/test'

// §16 Phase 4 exit (UI): signed-in user submits a refinement → refined card
// side-by-side with a changed "why"; chip edit (free) / remove works; gibberish
// costs no credit; the quota wall shows the upgrade teaser. BFF fully stubbed
// via page.route, auth via the E2E token (no Clerk tenant needed) — the
// design's "mocked BFF" approach, deterministic and CI-friendly.

const MATCH_ID = '11111111-1111-1111-1111-111111111111'

const list = [{
  id: MATCH_ID, league: 'World Cup', homeTeam: 'Brazil', awayTeam: 'Argentina',
  kickoffUtc: '2026-06-14T19:00:00Z', status: 'NS', hasBaseline: true,
}]
const detail = {
  id: MATCH_ID, league: 'World Cup', homeTeam: 'Brazil', awayTeam: 'Argentina',
  kickoffUtc: '2026-06-14T19:00:00Z', status: 'NS',
  baseline: {
    version: 1, home: 0.55, draw: 0.27, away: 0.18, predHome: 2, predAway: 1,
    why: 'Brazil form.', citations: [],
  },
}
const refined = {
  home: 0.7, draw: 0.2, away: 0.1, predHome: 3, predAway: 0,
  why: 'Refined: the injured Argentina keeper shifts this Brazil’s way.',
  citations: [],
}

test('signed-in: refine → side-by-side + changed why; edit/remove; gibberish; quota wall', async ({ page }) => {
  // Server-side state the stub mutates.
  const state: { quotaRemaining: number; chip: unknown; refined: unknown } =
    { quotaRemaining: 3, chip: null, refined: null }
  let mode: 'success' | 'gibberish' = 'success'

  await page.addInitScript(() => { window.__E2E_TOKEN__ = 'e2e-token' })

  await page.route('**/matches', (r) => r.fulfill({ json: list }))
  await page.route(`**/matches/${MATCH_ID}`, (r) => r.fulfill({ json: detail }))
  await page.route(`**/matches/${MATCH_ID}/me`, (r) => r.fulfill({
    json: { tier: 'free', quotaRemaining: state.quotaRemaining, chip: state.chip, refined: state.refined },
  }))
  await page.route(`**/matches/${MATCH_ID}/refine`, (r) => {
    const m = r.request().method()
    if (m === 'DELETE') {
      state.chip = null; state.refined = null
      return r.fulfill({ json: { status: 'removed', applied: false, quotaRemaining: state.quotaRemaining, chip: null, refined: null } })
    }
    if (m === 'POST' && state.quotaRemaining <= 0)
      return r.fulfill({ status: 429, json: { status: 'quota_exhausted', applied: false, quotaRemaining: 0, chip: null, refined: null } })

    if (mode === 'gibberish') {
      state.chip = { inputType: 'text', text: 'asdf', url: null, status: 'rejected_gibberish' }
      state.refined = null
      return r.fulfill({ json: { status: 'rejected_gibberish', applied: false, quotaRemaining: state.quotaRemaining, chip: state.chip, refined: null } })
    }
    // success (POST consumes a credit; PUT edit is free)
    if (m === 'POST') state.quotaRemaining -= 1
    state.chip = { inputType: 'text', text: 'keeper injured', url: null, status: 'success' }
    state.refined = refined
    return r.fulfill({ json: { status: 'success', applied: true, quotaRemaining: state.quotaRemaining, chip: state.chip, refined } })
  })

  // Navigate list → detail (SPA).
  await page.goto('/')
  await page.getByTestId('match-row').click()
  await expect(page.getByTestId('baseline-card')).toBeVisible()

  // Authed (E2E token) → refine form is live, not the greyed sign-in.
  const panel = page.getByTestId('refine-panel')
  await expect(panel).toBeVisible()
  await expect(page.getByTestId('quota-counter')).toContainText('3 of 3')

  // 1. Successful text refinement → side-by-side + changed why + chip + quota 2.
  await page.getByTestId('refine-input').fill('keeper injured')
  await page.getByTestId('refine-submit').click()
  await expect(page.getByTestId('refined-card')).toBeVisible()
  await expect(page.getByTestId('baseline-mini')).toContainText('55%')
  await expect(page.getByTestId('refined-why')).toContainText('Refined:')
  await expect(page.getByTestId('chip')).toContainText('keeper injured')
  await expect(page.getByTestId('quota-counter')).toContainText('2 of 3')

  // 2. Edit the chip → free re-run (PUT), quota unchanged at 2.
  await page.getByTestId('chip-edit').click()
  await page.getByTestId('refine-input').fill('keeper definitely out')
  await page.getByTestId('refine-submit').click()
  await expect(page.getByTestId('refined-card')).toBeVisible()
  await expect(page.getByTestId('quota-counter')).toContainText('2 of 3')

  // 3. Remove → chip gone, form back.
  await page.getByTestId('chip-remove').click()
  await expect(page.getByTestId('chip')).toHaveCount(0)
  await expect(page.getByTestId('refine-input')).toBeVisible()

  // 4. Gibberish → not-applied note, no refined card, no credit (still 2).
  mode = 'gibberish'
  await page.getByTestId('refine-input').fill('asdf')
  await page.getByTestId('refine-submit').click()
  await expect(page.getByTestId('refine-status')).toContainText('no credit used')
  await expect(page.getByTestId('refined-card')).toHaveCount(0)
  await expect(page.getByTestId('quota-counter')).toContainText('2 of 3')
  await page.getByTestId('chip-remove').click()

  // 5. Quota wall → upgrade teaser.
  mode = 'success'
  state.quotaRemaining = 0
  await page.getByTestId('refine-input').fill('one more note')
  await page.getByTestId('refine-submit').click()
  await expect(page.getByTestId('upgrade-teaser')).toBeVisible()
})
