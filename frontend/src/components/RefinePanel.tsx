import { useEffect, useState } from 'react'
import { useAuthState } from '../auth'
import {
  deleteRefine, getMe, postRefine, putRefine,
  type BaselineView, type Chip, type MeView, type Refined,
} from '../api'

const pct = (p: number) => `${Math.round(p * 100)}%`

function Bars({ home, draw, away }: { home: number; draw: number; away: number }) {
  return (
    <div className="mini-probs">
      {([['H', home], ['D', draw], ['A', away]] as const).map(([k, v]) => (
        <div className="mini-prob" key={k}>
          <span>{k}</span>
          <span className="bar"><span className="fill" style={{ width: pct(v) }} /></span>
          <span className="prob-val">{pct(v)}</span>
        </div>
      ))}
    </div>
  )
}

const NOT_APPLIED: Record<string, string> = {
  rejected_gibberish: 'That didn’t read as usable context — no credit used.',
  off_topic: 'That isn’t about this match — no credit used.',
  dead_url: 'Couldn’t read that link — no credit used.',
}

export default function RefinePanel({
  matchId, baseline,
}: { matchId: string; baseline: BaselineView }) {
  const { authed, getToken, signIn } = useAuthState()
  const [me, setMe] = useState<MeView | null>(null)
  const [text, setText] = useState('')
  const [mode, setMode] = useState<'text' | 'url'>('text')
  const [editing, setEditing] = useState(false)
  const [busy, setBusy] = useState(false)
  const [exhausted, setExhausted] = useState(false)

  useEffect(() => {
    if (!authed) return
    getToken().then((t) => getMe(matchId, t)).then(setMe).catch(() => {})
  }, [authed, matchId])

  if (!authed) {
    return (
      <section className="refine greyed" data-testid="refine-panel">
        <p>Tell the model what it’s missing — an injury, a tactical note, a link.</p>
        <button data-testid="refine-signin" onClick={signIn}>
          Sign in to refine — free, 3 a day
        </button>
      </section>
    )
  }

  const chip: Chip | null = me?.chip ?? null
  const refined: Refined | null = me?.refined ?? null
  const free = me?.tier === 'free'

  async function submit() {
    if (!text.trim() || busy) return
    setBusy(true)
    try {
      const t = await getToken()
      const body = mode === 'url'
        ? { inputType: 'url' as const, url: text.trim() }
        : { inputType: 'text' as const, text: text.trim() }
      // Editing an existing chip is a free re-run (PUT); a new chip is POST.
      const res = chip && editing
        ? await putRefine(matchId, body, t)
        : await postRefine(matchId, body, t)
      if (res.status === 'quota_exhausted') { setExhausted(true); return }
      setExhausted(false)
      setEditing(false)
      setText('')
      setMe(await getMe(matchId, t))
    } finally {
      setBusy(false)
    }
  }

  async function remove() {
    setBusy(true)
    try {
      const t = await getToken()
      await deleteRefine(matchId, t)
      setMe(await getMe(matchId, t))
      setEditing(false)
      setText('')
    } finally {
      setBusy(false)
    }
  }

  const showForm = !chip || editing
  const notApplied = chip && chip.status !== 'success' ? NOT_APPLIED[chip.status] : null

  return (
    <section className="refine" data-testid="refine-panel">
      {free && !exhausted && (
        <p className="quota" data-testid="quota-counter">
          {me!.quotaRemaining} of 3 refinements left today
        </p>
      )}
      {exhausted && (
        <p className="upgrade" data-testid="upgrade-teaser">
          You’re getting the hang of this. Unlimited refinements all tournament for $5?
        </p>
      )}

      {chip && (
        <div className="chip" data-testid="chip">
          <span>You added: {chip.text ?? chip.url}</span>
          <button data-testid="chip-edit" onClick={() => {
            setEditing(true)
            setMode(chip.inputType === 'url' ? 'url' : 'text')
            setText(chip.text ?? chip.url ?? '')
          }}>Edit</button>
          <button data-testid="chip-remove" onClick={remove}>Remove</button>
        </div>
      )}

      {notApplied && <p className="refine-note" data-testid="refine-status">{notApplied}</p>}

      {chip?.status === 'success' && refined && (
        <div className="side-by-side" data-testid="refined-card">
          <div className="mini" data-testid="baseline-mini">
            <h4>Baseline</h4>
            <Bars home={baseline.home} draw={baseline.draw} away={baseline.away} />
            <p className="mini-score">{baseline.predHome}–{baseline.predAway}</p>
          </div>
          <div className="mini refined">
            <h4>Refined</h4>
            <Bars home={refined.home} draw={refined.draw} away={refined.away} />
            <p className="mini-score">{refined.predHome}–{refined.predAway}</p>
          </div>
          <p className="why refined-why" data-testid="refined-why">{refined.why}</p>
        </div>
      )}

      {showForm && !exhausted && (
        <div className="refine-form">
          <div className="mode">
            <button className={mode === 'text' ? 'on' : ''}
              onClick={() => setMode('text')}>Note</button>
            <button className={mode === 'url' ? 'on' : ''}
              data-testid="refine-url-toggle"
              onClick={() => setMode('url')}>Link</button>
          </div>
          <textarea
            data-testid="refine-input"
            rows={mode === 'url' ? 1 : 3}
            placeholder={mode === 'url'
              ? 'Paste an article link'
              : 'e.g. Star striker ruled out with injury'}
            value={text}
            onChange={(e) => setText(e.target.value)}
          />
          <button data-testid="refine-submit" disabled={busy || !text.trim()}
            onClick={submit}>
            {busy ? 'Thinking…' : editing ? 'Update' : 'Refine prediction'}
          </button>
        </div>
      )}
    </section>
  )
}
