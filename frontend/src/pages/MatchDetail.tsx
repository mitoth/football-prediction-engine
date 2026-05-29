import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getMatch, type MatchDetail as Detail } from '../api'
import RefinePanel from '../components/RefinePanel'
import { event } from '../analytics'

const pct = (p: number) => `${Math.round(p * 100)}%`

// Cap displayed snippet length — fair-use guard on top of NewsData's licensed
// excerpt. The full text NewsData hands us is normally short already, but a
// hard ceiling means no individual outlet ever sees more than 250 chars from
// us; the user clicks through to the publisher URL for the rest.
const SNIPPET_MAX = 250
function trim(s: string) {
  return s.length <= SNIPPET_MAX ? s : `${s.slice(0, SNIPPET_MAX).trimEnd()}…`
}

export default function MatchDetail() {
  const { id } = useParams<{ id: string }>()
  const [match, setMatch] = useState<Detail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showCitations, setShowCitations] = useState(false)

  useEffect(() => {
    if (!id) return
    getMatch(id).then((m) => {
      setMatch(m)
      event('match_viewed', {
        matchId: m.id,
        league: m.league,
        hasBaseline: !!m.baseline,
      })
    }).catch((e) => setError(String(e)))
  }, [id])

  if (error) return <p className="state" data-testid="error">Couldn’t load match: {error}</p>
  if (!match) return <p className="state" data-testid="loading">Loading…</p>

  const b = match.baseline

  return (
    <article className="detail" data-testid="match-detail">
      <Link to="/" className="back">← All matches</Link>
      <header>
        <span className="league">
          {match.league}{match.stage && <> · <em className="stage">{match.stage}</em></>}
        </span>
        <h2>{match.homeTeam} <em>vs</em> {match.awayTeam}</h2>
        <p className="kickoff" data-testid="kickoff">
          Kickoff {new Date(match.kickoffUtc).toLocaleString(undefined, {
            weekday: 'short', day: 'numeric', month: 'short',
            hour: '2-digit', minute: '2-digit',
          })}
        </p>
      </header>

      {!b ? (
        <p className="state" data-testid="no-baseline">
          Prediction not generated yet — check back closer to kickoff.
        </p>
      ) : (
        <>
        <section className="baseline-card" data-testid="baseline-card">
          <p className="card-eyebrow">Predicted final score · not the actual result</p>
          <div className="scoreline" data-testid="scoreline" aria-label="Predicted final score">
            {b.predHome}<span>–</span>{b.predAway}
          </div>

          <p className="probs-label">Predicted outcome</p>
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

          <p className="why" data-testid="why"><strong>Why we predict this:</strong> {b.why}</p>

          {b.citations.length > 0 && (
            <>
              <button
                className="cite-toggle"
                data-testid="citations-toggle"
                aria-expanded={showCitations}
                onClick={() => setShowCitations((s) => !s)}
              >
                {showCitations
                  ? 'Hide what the model read'
                  : `What the model read · ${b.citations.length} article${b.citations.length === 1 ? '' : 's'}`}
              </button>

              {showCitations && (
                <>
                  <ul className="citations" data-testid="citations">
                    {b.citations.map((c) => (
                      <li key={c.articleId} data-testid="citation">
                        <a href={c.url} target="_blank" rel="noreferrer noopener">
                          {c.headline}
                        </a>
                        <span className="outlet">{c.outlet}</span>
                        <span className="snippet">{trim(c.snippet)}</span>
                      </li>
                    ))}
                  </ul>
                  <p className="citations-credit" data-testid="newsdata-credit">
                    News headlines and snippets via{' '}
                    <a href="https://newsdata.io" target="_blank" rel="noreferrer noopener">
                      NewsData.io
                    </a>
                    . Tap a headline to read the full article on the publisher's site.
                  </p>
                </>
              )}
            </>
          )}

          <p className="ver">Baseline v{b.version} · AI prediction by Claude</p>
        </section>
        <RefinePanel matchId={match.id} baseline={b} />
        <p className="disclaimer" data-testid="disclaimer">
          All numbers above are model predictions, not the real result.
          For entertainment — not betting advice.
        </p>
        </>
      )}
    </article>
  )
}
