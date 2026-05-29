import { useEffect, useState } from 'react'
import { useAuthState } from '../auth'
import {
  deleteRefine, getMe, postRefine, putRefine,
  type BaselineView, type Chip, type MeView, type Refined,
} from '../api'
import { event } from '../analytics'

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
}

export default function RefinePanel({
  matchId, baseline,
}: { matchId: string; baseline: BaselineView }) {
  const { authed, getToken, signIn } = useAuthState()
  const [me, setMe] = useState<MeView | null>(null)
  const [text, setText] = useState('')
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
        <p>Add what you know — an injury, a tactical wrinkle, the weather.<br />
        The prediction updates and the "why" explains what changed.</p>
        <button data-testid="refine-signin" onClick={() => {
          event('signin_clicked', { source: 'refine-panel', matchId })
          signIn()
        }}>
          Sign in to update predictions — free, 3 a day
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
      const body = { inputType: 'text' as const, text: text.trim() }
      // Editing an existing chip is a free re-run (PUT); a new chip is POST.
      const res = chip && editing
        ? await putRefine(matchId, body, t)
        : await postRefine(matchId, body, t)
      event('refinement_submitted', {
        matchId,
        action: chip && editing ? 'edit' : 'new',
        status: res.status,                  // success | rejected_gibberish | off_topic | quota_exhausted
        applied: res.applied,
        quotaRemaining: res.quotaRemaining,
      })
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
          {3 - me!.quotaRemaining} / 3 used today
        </p>
      )}
      {exhausted && (
        <p className="upgrade" data-testid="upgrade-teaser">
          You’re getting the hang of this. Unlimited refinements all tournament for $5?
        </p>
      )}

      {chip && (
        <div className="chip" data-testid="chip">
          <span>You added: {chip.text ?? ''}</span>
          <button data-testid="chip-edit" onClick={() => {
            setEditing(true)
            setText(chip.text ?? '')
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
          <textarea
            data-testid="refine-input"
            rows={3}
            placeholder="Add a note the model doesn’t know — e.g. star striker out, switching formation, heavy pitch"
            value={text}
            onChange={(e) => setText(e.target.value)}
          />
          <button data-testid="refine-submit" disabled={busy || !text.trim()}
            onClick={submit}>
            {busy ? 'Thinking…' : editing
              ? 'Re-run with my edit'
              : 'Update prediction with my note'}
          </button>
        </div>
      )}
    </section>
  )
}
