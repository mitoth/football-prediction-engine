import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getMatch, type MatchDetail as Detail } from '../api'

const pct = (p: number) => `${Math.round(p * 100)}%`

export default function MatchDetail() {
  const { id } = useParams<{ id: string }>()
  const [match, setMatch] = useState<Detail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showCitations, setShowCitations] = useState(false)

  useEffect(() => {
    if (!id) return
    getMatch(id).then(setMatch).catch((e) => setError(String(e)))
  }, [id])

  if (error) return <p className="state" data-testid="error">Couldn’t load match: {error}</p>
  if (!match) return <p className="state" data-testid="loading">Loading…</p>

  const b = match.baseline

  return (
    <article className="detail" data-testid="match-detail">
      <Link to="/" className="back">← All matches</Link>
      <header>
        <span className="league">{match.league}</span>
        <h2>{match.homeTeam} <em>vs</em> {match.awayTeam}</h2>
      </header>

      {!b ? (
        <p className="state" data-testid="no-baseline">
          Prediction not generated yet — check back closer to kickoff.
        </p>
      ) : (
        <section className="baseline-card" data-testid="baseline-card">
          <div className="scoreline" data-testid="scoreline">
            {b.predHome}<span>–</span>{b.predAway}
          </div>

          <div className="probs" data-testid="probs">
            {([
              ['Home', b.home, 'home'],
              ['Draw', b.draw, 'draw'],
              ['Away', b.away, 'away'],
            ] as const).map(([label, p, key]) => (
              <div className="prob" key={key} data-testid={`prob-${key}`}>
                <span className="prob-label">{label}</span>
                <span className="bar"><span className="fill" style={{ width: pct(p) }} /></span>
                <span className="prob-val">{pct(p)}</span>
              </div>
            ))}
          </div>

          <p className="why" data-testid="why">{b.why}</p>

          <button
            className="cite-toggle"
            data-testid="citations-toggle"
            aria-expanded={showCitations}
            onClick={() => setShowCitations((s) => !s)}
          >
            {showCitations ? 'Hide sources' : `Show sources (${b.citations.length})`}
          </button>

          {showCitations && (
            <ul className="citations" data-testid="citations">
              {b.citations.length === 0 && <li className="state">No sources cited.</li>}
              {b.citations.map((c) => (
                <li key={c.articleId} data-testid="citation">
                  <a href={c.url} target="_blank" rel="noreferrer noopener">
                    {c.headline}
                  </a>
                  <span className="outlet">{c.outlet}</span>
                  <span className="snippet">{c.snippet}</span>
                </li>
              ))}
            </ul>
          )}

          <p className="ver">Baseline v{b.version}</p>
        </section>
      )}
    </article>
  )
}
