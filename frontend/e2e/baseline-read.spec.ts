import { expect, test } from '@playwright/test'

// A real WC fixture shape, served by the stubbed BFF. Mirrors the camelCase
// contract of the BFF /matches and /matches/{id} endpoints.
const MATCH_ID = '11111111-1111-1111-1111-111111111111'
const ARTICLE_URL = 'https://news.test/brazil-form'

const list = [
  {
    id: MATCH_ID,
    league: 'World Cup',
    homeTeam: 'Brazil',
    awayTeam: 'Argentina',
    kickoffUtc: new Date(Date.now() + 3 * 24 * 3600 * 1000).toISOString(),
    status: 'NS',
    hasBaseline: true,
  },
]

const detail = {
  id: MATCH_ID,
  league: 'World Cup',
  homeTeam: 'Brazil',
  awayTeam: 'Argentina',
  kickoffUtc: '2026-06-14T19:00:00Z',
  status: 'NS',
  baseline: {
    version: 1,
    home: 0.55,
    draw: 0.27,
    away: 0.18,
    predHome: 2,
    predAway: 1,
    why: "Brazil's form and home-continent advantage tilt this.",
    citations: [
      {
        articleId: 'a1',
        headline: 'Brazil unbeaten in six',
        outlet: 'BBC',
        url: ARTICLE_URL,
        snippet: "Tite's side arrive on a strong run.",
      },
    ],
  },
}

test('anonymous: match list → detail → baseline card → reveal citations', async ({ page }) => {
  await page.route('**/matches', (r) =>
    r.fulfill({ json: list, contentType: 'application/json' }))
  await page.route('**/matches/*', (r) =>
    r.fulfill({ json: detail, contentType: 'application/json' }))

  await page.goto('/')

  // List renders the seeded fixture.
  const row = page.getByTestId('match-row')
  await expect(row).toContainText('Brazil')
  await expect(row).toContainText('Argentina')

  // Navigate to detail (SPA — no reload).
  await row.click()
  await expect(page).toHaveURL(new RegExp(`/match/${MATCH_ID}$`))

  const card = page.getByTestId('baseline-card')
  await expect(card).toBeVisible()
  await expect(page.getByTestId('scoreline')).toContainText('2')
  await expect(page.getByTestId('scoreline')).toContainText('1')
  await expect(page.getByTestId('prob-home')).toContainText('55%')
  await expect(page.getByTestId('why')).toContainText("Brazil's form")

  // Citations are tap-to-reveal: hidden until the toggle is pressed.
  await expect(page.getByTestId('citations')).toHaveCount(0)
  await page.getByTestId('citations-toggle').click()

  const cite = page.getByTestId('citation')
  await expect(cite).toContainText('Brazil unbeaten in six')
  await expect(cite.getByRole('link')).toHaveAttribute('href', ARTICLE_URL)
})
