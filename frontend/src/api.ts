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

// --- Phase 4: authed refinement ---

export interface Chip { inputType: string; text: string | null; url: string | null; status: string }
export interface Refined {
  home: number; draw: number; away: number
  predHome: number; predAway: number; why: string; citations: Citation[]
}
export interface MeView { tier: string; quotaRemaining: number; chip: Chip | null; refined: Refined | null }
export interface RefineResponse {
  status: string; applied: boolean; quotaRemaining: number
  chip: Chip | null; refined: Refined | null
}
export interface RefineInput { inputType: 'text' | 'url'; text?: string; url?: string }

type Token = string | null

async function authed<T>(path: string, token: Token, init?: RequestInit): Promise<T> {
  const res = await fetch(`${apiUrl}${path}`, {
    ...init,
    headers: {
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  })
  // 429 (quota) and 409 still carry a JSON body the UI wants to render.
  if (!res.ok && res.status !== 429 && res.status !== 409)
    throw new Error(`${res.status} ${res.statusText}`)
  return res.json() as Promise<T>
}

export const getMe = (id: string, token: Token) =>
  authed<MeView>(`/matches/${id}/me`, token)

export const postRefine = (id: string, body: RefineInput, token: Token) =>
  authed<RefineResponse>(`/matches/${id}/refine`, token,
    { method: 'POST', body: JSON.stringify(body) })

export const putRefine = (id: string, body: RefineInput, token: Token) =>
  authed<RefineResponse>(`/matches/${id}/refine`, token,
    { method: 'PUT', body: JSON.stringify(body) })

export const deleteRefine = (id: string, token: Token) =>
  authed<RefineResponse>(`/matches/${id}/refine`, token, { method: 'DELETE' })
