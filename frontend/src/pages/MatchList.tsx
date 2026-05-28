import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { getMatches, getSyncStatus, type MatchListItem, type SyncStatus } from '../api'

function timeOnly(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    hour: '2-digit', minute: '2-digit',
  })
}

// "in 12 min", "in 3 h", "12 min ago" — small humanizer for the refresh
// indicators. Avoids a date-fns dep for ~30 lines of UI logic. Within 60s of
// now (either side) we render "any moment" — the BFF clamps overdue next-run
// timestamps to "now", and a literal "0 min ago" reads as broken.
function relativeFromNow(iso: string | null, now: number): string {
  if (!iso) return '—'
  const diffMs = new Date(iso).getTime() - now
  const absMin = Math.round(Math.abs(diffMs) / 60000)
  const future = diffMs > 0
  if (absMin < 1) return 'any moment'
  if (absMin < 60) return future ? `in ${absMin} min` : `${absMin} min ago`
  const absH = Math.round(absMin / 60)
  if (absH < 24) return future ? `in ${absH} h` : `${absH} h ago`
  const absD = Math.round(absH / 24)
  return future ? `in ${absD} d` : `${absD} d ago`
}

// "2026-06-14" — stable group key
function dayKey(iso: string) {
  const d = new Date(iso)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
function dayLabel(iso: string) {
  const d = new Date(iso)
  const today = new Date()
  const tomorrow = new Date(); tomorrow.setDate(today.getDate() + 1)
  const sameDay = (a: Date, b: Date) =>
    a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
  if (sameDay(d, today)) return 'Today'
  if (sameDay(d, tomorrow)) return 'Tomorrow'
  return d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'short' })
}

// Day → competition → stage → matches. Three nesting levels so the user can
// see at a glance which competition + round a fixture sits in.
type StageGroup = { stage: string | null; items: MatchListItem[] }
type LeagueGroup = { league: string; stages: StageGroup[]; count: number }
type DayGroup   = { key: string; label: string; leagues: LeagueGroup[]; count: number }

function groupMatches(matches: MatchListItem[]): DayGroup[] {
  const sorted = [...matches].sort((a, b) => a.kickoffUtc.localeCompare(b.kickoffUtc))
  const days: DayGroup[] = []
  for (const m of sorted) {
    const dKey = dayKey(m.kickoffUtc)
    let day = days[days.length - 1]
    if (!day || day.key !== dKey) {
      day = { key: dKey, label: dayLabel(m.kickoffUtc), leagues: [], count: 0 }
      days.push(day)
    }
    let lg = day.leagues.find((l) => l.league === m.league)
    if (!lg) {
      lg = { league: m.league, stages: [], count: 0 }
      day.leagues.push(lg)
    }
    let sg = lg.stages.find((s) => s.stage === (m.stage ?? null))
    if (!sg) {
      sg = { stage: m.stage ?? null, items: [] }
      lg.stages.push(sg)
    }
    sg.items.push(m)
    lg.count += 1
    day.count += 1
  }
  return days
}

// Default: only the next 7 days of fixtures. Pressing "Show next week" adds
// 7 more days each time — the list opens narrow and the user pulls more in.
const WEEK_MS = 7 * 24 * 60 * 60 * 1000

export default function MatchList() {
  const [matches, setMatches] = useState<MatchListItem[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [weeks, setWeeks] = useState(1)
  const [sync, setSync] = useState<SyncStatus | null>(null)
  // Ticking clock so the "in N min" countdowns refresh without a full reload.
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    getMatches().then(setMatches).catch((e) => setError(String(e)))
    getSyncStatus().then(setSync).catch(() => { /* non-fatal — UI hides banner */ })
  }, [])

  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 30_000)
    return () => clearInterval(t)
  }, [])

  // Slice the upcoming fixtures to whatever the user has chosen to see.
  const horizonIso = useMemo(() => new Date(Date.now() + weeks * WEEK_MS).toISOString(), [weeks])
  const visible = useMemo(
    () => matches ? matches.filter((m) => m.kickoffUtc < horizonIso) : null,
    [matches, horizonIso])
  const remaining = matches && visible ? matches.length - visible.length : 0

  const days = useMemo(() => visible ? groupMatches(visible) : null, [visible])

  if (error) return <p className="state" data-testid="error">Couldn’t load matches: {error}</p>
  if (!matches || !visible || !days) return <p className="state" data-testid="loading">Loading matches…</p>
  if (matches.length === 0)
    return <p className="state" data-testid="empty">No upcoming matches yet.</p>
  if (visible.length === 0)
    return <p className="state" data-testid="empty">No matches in the next {weeks} {weeks === 1 ? 'week' : 'weeks'}.</p>

  return (
    <div className="match-groups" data-testid="match-list">
      {sync && (
        <aside className="sync-banner" data-testid="sync-banner">
          <span>
            News refresh <strong>{relativeFromNow(sync.newsNextFetchAt, now)}</strong>
            {' · '}
            New predictions <strong>{relativeFromNow(sync.baselineNextBuildAt, now)}</strong>
          </span>
          <small>(news every {sync.newsIntervalMinutes} min · predictions every {sync.baselineIntervalMinutes} min)</small>
        </aside>
      )}

      {days.map((d) => (
        <section key={d.key} className="match-day" data-testid="match-day">
          <h3 className="match-day-label" data-testid="match-day-label">
            <span>{d.label}</span>
            <span className="match-day-count">{d.count} {d.count === 1 ? 'match' : 'matches'}</span>
          </h3>

          {d.leagues.map((lg) => (
            <div key={lg.league} className="match-league" data-testid="match-league">
              <h4 className="match-league-label">
                <span>{lg.league}</span>
                <span className="match-league-count">{lg.count}</span>
              </h4>

              {lg.stages.map((sg) => (
                <div key={sg.stage ?? '_'} className="match-stage" data-testid="match-stage">
                  {sg.stage && (
                    <p className="match-stage-label">{sg.stage}</p>
                  )}
                  <ul className="match-list">
                    {sg.items.map((m) => (
                      <li key={m.id}>
                        <Link to={`/match/${m.id}`} className="match-row" data-testid="match-row">
                          <span className="teams">{m.homeTeam} <em>vs</em> {m.awayTeam}</span>
                          <span className="meta">
                            <time>{timeOnly(m.kickoffUtc)}</time>
                            {m.hasBaseline
                              ? (
                                <span className="badge" title={m.baselineGeneratedAt ?? ''}>
                                  Prediction {m.baselineGeneratedAt ? `· ${relativeFromNow(m.baselineGeneratedAt, now)}` : 'ready'}
                                </span>
                              )
                              : (
                                <span className="badge badge-pending">
                                  Prediction {sync?.baselineNextBuildAt ? relativeFromNow(sync.baselineNextBuildAt, now) : 'queued'}
                                </span>
                              )}
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          ))}
        </section>
      ))}

      {remaining > 0 && (
        <button
          className="show-more"
          data-testid="show-more"
          onClick={() => setWeeks((w) => w + 1)}
        >
          Show next week <span className="show-more-rem">· {remaining} more {remaining === 1 ? 'match' : 'matches'} ahead</span>
        </button>
      )}
    </div>
  )
}
