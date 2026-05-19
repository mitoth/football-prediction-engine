import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getMatches, type MatchListItem } from '../api'

function kickoff(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    weekday: 'short', day: 'numeric', month: 'short',
    hour: '2-digit', minute: '2-digit',
  })
}

export default function MatchList() {
  const [matches, setMatches] = useState<MatchListItem[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getMatches().then(setMatches).catch((e) => setError(String(e)))
  }, [])

  if (error) return <p className="state" data-testid="error">Couldn’t load matches: {error}</p>
  if (!matches) return <p className="state" data-testid="loading">Loading matches…</p>
  if (matches.length === 0)
    return <p className="state" data-testid="empty">No upcoming matches yet.</p>

  return (
    <ul className="match-list" data-testid="match-list">
      {matches.map((m) => (
        <li key={m.id}>
          <Link to={`/match/${m.id}`} className="match-row" data-testid="match-row">
            <span className="league">{m.league}</span>
            <span className="teams">{m.homeTeam} <em>vs</em> {m.awayTeam}</span>
            <span className="meta">
              <time>{kickoff(m.kickoffUtc)}</time>
              {m.hasBaseline && <span className="badge">Prediction ready</span>}
            </span>
          </Link>
        </li>
      ))}
    </ul>
  )
}
