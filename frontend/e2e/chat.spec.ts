import { expect, test } from '@playwright/test'

// Chat-mode refinement smoke test. The bug that prompted this test: a
// production deploy left the BFF emitting `Access-Control-Allow-Origin: *`
// while the SPA was sending `credentials: 'include'`, so every fetch to
// /chat-status + /chat got blocked at the CORS layer and the chat UI silently
// hung after Send. The test stubs the BFF, but the wiring covered here —
// hydration → send → assistant bubble — was the path that broke.

const MATCH_ID = '11111111-1111-1111-1111-111111111111'

const list = [{
  id: MATCH_ID, league: 'World Cup', homeTeam: 'Brazil', awayTeam: 'Argentina',
  kickoffUtc: new Date(Date.now() + 3 * 24 * 3600 * 1000).toISOString(),
  status: 'NS', hasBaseline: true,
}]

const detail = {
  id: MATCH_ID, league: 'World Cup', homeTeam: 'Brazil', awayTeam: 'Argentina',
  kickoffUtc: new Date(Date.now() + 3 * 24 * 3600 * 1000).toISOString(), status: 'NS',
  baseline: {
    version: 1, home: 0.55, draw: 0.27, away: 0.18, predHome: 2, predAway: 1,
    why: 'Brazil form.', citations: [], generatedAt: new Date().toISOString(),
  },
}

const refined = {
  home: 0.7, draw: 0.2, away: 0.1, predHome: 3, predAway: 0,
  why: 'Refined: keeper out tilts this Brazil\'s way.',
  citations: [],
}

test('anon chat: hydrate quota → send → assistant card + decrement', async ({ page }) => {
  let chatStatusCalls = 0
  let chatPostCalls = 0
  let remaining = 3

  await page.route('**/matches', (r) => r.fulfill({ json: list }))
  await page.route(`**/matches/${MATCH_ID}`, (r) => r.fulfill({ json: detail }))
  await page.route(`**/matches/${MATCH_ID}/chat-status`, (r) => {
    chatStatusCalls++
    return r.fulfill({ json: { tier: 'anon', quotaRemaining: remaining, userMessageMax: 150 } })
  })
  await page.route(`**/matches/${MATCH_ID}/chat`, async (r) => {
    chatPostCalls++
    remaining -= 1
    return r.fulfill({
      json: {
        status: 'success', applied: true, quotaRemaining: remaining,
        tier: 'anon', refined, message: null,
      },
    })
  })

  await page.goto(`/match/${MATCH_ID}`)
  await expect(page.getByTestId('chat-panel')).toBeVisible()

  // Hydration call lands before we touch the input.
  await expect.poll(() => chatStatusCalls).toBeGreaterThan(0)
  await expect(page.getByTestId('chat-quota')).toContainText('3 free messages left')

  // Send a guest message.
  await page.getByTestId('chat-input').fill('Brazil keeper is injured')
  await page.getByTestId('chat-send').click()

  // User bubble appears immediately, assistant bubble after the (stubbed) POST.
  await expect(page.getByTestId('chat-user-bubble')).toContainText('keeper is injured')
  await expect(page.getByTestId('chat-assistant-bubble')).toBeVisible()
  await expect(page.getByTestId('chat-assistant-bubble')).toContainText('Refined:')
  await expect(page.getByTestId('chat-quota')).toContainText('2 free messages left')
  expect(chatPostCalls).toBe(1)
})

test('anon char cap: textarea blocks beyond 150 chars', async ({ page }) => {
  await page.route('**/matches', (r) => r.fulfill({ json: list }))
  await page.route(`**/matches/${MATCH_ID}`, (r) => r.fulfill({ json: detail }))
  await page.route(`**/matches/${MATCH_ID}/chat-status`, (r) =>
    r.fulfill({ json: { tier: 'anon', quotaRemaining: 3, userMessageMax: 150 } }))

  await page.goto(`/match/${MATCH_ID}`)
  await expect(page.getByTestId('chat-panel')).toBeVisible()

  const long = 'x'.repeat(300)
  await page.getByTestId('chat-input').fill(long)
  const value = await page.getByTestId('chat-input').inputValue()
  expect(value.length).toBe(150)
  await expect(page.getByTestId('chat-charcount')).toContainText('150 / 150')
})

test('anon quota exhausted: input replaced by sign-in prompt', async ({ page }) => {
  await page.route('**/matches', (r) => r.fulfill({ json: list }))
  await page.route(`**/matches/${MATCH_ID}`, (r) => r.fulfill({ json: detail }))
  await page.route(`**/matches/${MATCH_ID}/chat-status`, (r) =>
    r.fulfill({ json: { tier: 'anon', quotaRemaining: 0, userMessageMax: 150 } }))

  await page.goto(`/match/${MATCH_ID}`)
  await expect(page.getByTestId('chat-signin-prompt')).toBeVisible()
  await expect(page.getByTestId('chat-signin-prompt')).toContainText('Sign in for 5 more')
  await expect(page.getByTestId('chat-input')).toHaveCount(0)
})
