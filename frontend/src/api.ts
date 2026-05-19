// Thin BFF client. VITE_API_URL is injected by the Aspire AppHost (the BFF
// https endpoint); empty in a bare `vite preview`, where Playwright stubs
// the network instead.
const apiUrl = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') ?? ''

export interface MatchListItem {
  id: string
  league: string
  homeTeam: string
  awayTeam: string
  kickoffUtc: string
  status: string
  hasBaseline: boolean
}

export interface Citation {
  articleId: string
  headline: string
  outlet: string
  url: string
  snippet: string
}

export interface BaselineView {
  version: number
  home: number
  draw: number
  away: number
  predHome: number
  predAway: number
  why: string
  citations: Citation[]
}

export interface MatchDetail {
  id: string
  league: string
  homeTeam: string
  awayTeam: string
  kickoffUtc: string
  status: string
  baseline: BaselineView | null
}

async function getJson<T>(path: string): Promise<T> {
  const res = await fetch(`${apiUrl}${path}`)
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json() as Promise<T>
}

export const getMatches = () => getJson<MatchListItem[]>('/matches')
export const getMatch = (id: string) => getJson<MatchDetail>(`/matches/${id}`)
