import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getResults, type MatchResultRow, type ResultsPageView, type Verdict } from '../api'
import { event } from '../analytics'

function dateLabel(iso: string) {
  const d = new Date(iso)
  return d.toLocaleString(undefined, { day: 'numeric', month: 'short' })
}

// Per-row pill: strictest tier the row qualifies for. The aggregate cards
// above are cumulative (an exact score also counts as a correct goal diff
// and a correct 1/X/2), but each row only displays one pill — the
// strictest one it earned.
//
//   Exact      — predicted scoreline matched (2-1 → 2-1)
//   Margin     — same goal difference, scoreline differs (2-1 → 3-2)
//   1/X/2      — same outcome (W/D/L), margin differs (1-0 → 3-1)
//   Wrong      — opposite outcome (home win predicted, draw or away win actual)
const VERDICT_LABEL: Record<Verdict, string> = {
  exact: 'Exact',
  goal_diff: 'Margin',
  winner: '1/X/2',
  wrong: 'Wrong',
}

function VerdictPill({ verdict }: { verdict: Verdict }) {
  return (
    <span className={`verdict verdict-${verdict}`} data-testid={`verdict-${verdict}`}>
      {VERDICT_LABEL[verdict]}
    </span>
  )
}

function StatCard({
  label, hit, total, tone,
}: { label: string; hit: number; total: number; tone: string }) {
  const pct = total === 0 ? 0 : Math.round((hit / total) * 100)
  return (
    <div className={`stat-card stat-card-${tone}`} data-testid={`stat-${tone}`}>
      <div className="stat-card-label">{label}</div>
      <div className="stat-card-value"><strong>{hit}</strong> <span>/ {total}</span></div>
      <div className="stat-card-bar"><span style={{ width: `${pct}%` }} /></div>
      <div className="stat-card-pct">{pct}%</div>
    </div>
  )
}

function Row({ r }: { r: MatchResultRow }) {
  return (
    <li>
      <Link to={`/match/${r.matchId}`} className="result-row" data-testid="result-row">
        <span className="result-row-date">{dateLabel(r.kickoffUtc)}</span>
        <span className="result-row-league">{r.league}</span>
        <span className="result-row-teams">{r.homeTeam} <em>vs</em> {r.awayTeam}</span>
        <span className="result-row-scores">
          <span className="result-row-pred">
            <span className="result-row-tag">Predicted</span>
            <span className="result-row-score">{r.predHome}–{r.predAway}</span>
          </span>
          <span className="result-row-actual">
            <span className="result-row-tag">Actual</span>
            <span className="result-row-score">{r.actualHome}–{r.actualAway}</span>
          </span>
        </span>
        <VerdictPill verdict={r.verdict} />
      </Link>
    </li>
  )
}

export default function Results() {
  const [data, setData] = useState<ResultsPageView | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    event('results_view')
    getResults().then(setData).catch((e) => setError(String(e)))
  }, [])

  if (error) return <p className="state" data-testid="error">Couldn’t load results: {error}</p>
  if (!data) return <p className="state" data-testid="loading">Loading results…</p>

  const { aggregate: a, rows } = data
  if (rows.length === 0) {
    return (
      <div className="results-empty" data-testid="results-empty">
        <h2>No finished matches yet</h2>
        <p>
          Once the first World Cup matches wrap, this page will compare every
          prediction the model made against the actual final score.
        </p>
      </div>
    )
  }

  return (
    <div className="results" data-testid="results">
      <header className="results-header">
        <h2>Prediction accuracy</h2>
        <p className="results-subhead">
          How the model has done across {a.total} finished {a.total === 1 ? 'match' : 'matches'}.
        </p>
      </header>

      {/* Cumulative buckets. An exact score also counts as a correct 1/X/2
          AND a correct goal difference, so the three "correct" cards are
          nested precision tiers, not disjoint counts. "Wrong" is the
          inverse of 1/X/2 — match outcomes the model called the opposite
          way (or called a draw when someone won, etc). */}
      <div className="stat-cards" data-testid="stat-cards">
        <StatCard label="Exact score"             hit={a.exactScore}            total={a.total} tone="exact" />
        <StatCard label="Correct 1/X/2"           hit={a.correctWinner}         total={a.total} tone="winner" />
        <StatCard label="Correct goal difference" hit={a.correctGoalDifference} total={a.total} tone="goal_diff" />
        <StatCard label="Wrong"                   hit={a.wrong}                 total={a.total} tone="wrong" />
      </div>

      <ul className="result-list" data-testid="result-list">
        {rows.map((r) => <Row key={r.matchId} r={r} />)}
      </ul>
    </div>
  )
}
