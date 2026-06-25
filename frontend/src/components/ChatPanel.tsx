import { useEffect, useRef, useState } from 'react'
import { useAuthState } from '../auth'
import {
  getChatStatus, postChat,
  type BaselineView, type Refined, type ChatMessage, type ChatStatus,
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

// Frontend-only enrichment of a ChatMessage with the refined card payload so
// the assistant turn can render probability bars + scoreline. Only the {role,
// text} subset is shipped back to the BFF on the next turn — the model rebuilds
// its mental model from the natural-language reply (`text` = `refined.why`).
type ChatBubble =
  | { role: 'user'; text: string }
  | { role: 'assistant'; text: string; status: string; refined: Refined | null }

const QUOTA_FULL_BY_TIER: Record<string, number> = {
  anon: 3,
  free: 5,
  matchday: 30,
  world_cup_tournament: 30,
}

export default function ChatPanel({
  matchId, baseline,
}: { matchId: string; baseline: BaselineView }) {
  const { authed, getToken, signIn } = useAuthState()
  const [bubbles, setBubbles] = useState<ChatBubble[]>([])
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState<ChatStatus | null>(null)
  const endRef = useRef<HTMLDivElement | null>(null)

  // Hydrate quota state on mount + whenever auth flips. The anon endpoint
  // doesn't require a token but sends credentials so the cookie is established
  // here on the first ever load, before the user types anything.
  useEffect(() => {
    let cancelled = false
    getToken().then((t) => getChatStatus(matchId, t)).then((s) => {
      if (!cancelled) setStatus(s)
    }).catch(() => {})
    return () => { cancelled = true }
  }, [authed, matchId])

  // Auto-scroll to the latest bubble after each turn, and again when the
  // thinking indicator appears so the user sees the activity below their note.
  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [bubbles.length, busy])

  // When the user signs in mid-session their anon quota slot freezes and the
  // signed-in counter takes over. Keep the existing thread visible — the
  // bubbles are session-only and don't carry quota info anyway.

  async function send() {
    const text = input.trim()
    if (!text || busy) return
    setBusy(true)
    try {
      const nextBubbles: ChatBubble[] = [...bubbles, { role: 'user', text }]
      setBubbles(nextBubbles)
      setInput('')

      const token = await getToken()
      const messages: ChatMessage[] = nextBubbles.map((b) => ({ role: b.role, text: b.text }))
      const res = await postChat(matchId, { messages }, token)

      event('chat_message_sent', {
        matchId,
        turnIndex: nextBubbles.length,
        status: res.status,
        applied: res.applied,
        quotaRemaining: res.quotaRemaining,
        tier: res.tier,
      })

      const assistantText = res.refined?.why
        ?? res.message
        ?? "I couldn't refine that — please try again."
      setBubbles((prev) => [...prev, {
        role: 'assistant',
        text: assistantText,
        status: res.status,
        refined: res.refined,
      }])
      setStatus((prev) => ({
        tier: res.tier,
        quotaRemaining: res.quotaRemaining,
        userMessageMax: prev?.userMessageMax ?? (res.tier === 'anon' ? 150 : 750),
      }))
    } finally {
      setBusy(false)
    }
  }

  const tier = status?.tier ?? 'anon'
  const cap = QUOTA_FULL_BY_TIER[tier] ?? 3
  const remaining = status?.quotaRemaining ?? cap
  const exhausted = remaining <= 0
  const showSignInPrompt = exhausted && tier === 'anon'
  // Tier-aware per-message ceiling. Falls back to a tight anon default
  // until the hydration call lands.
  const charMax = status?.userMessageMax ?? 150
  const charsLeft = charMax - input.length

  const counterText =
    tier === 'anon'
      ? remaining > 0 ? `${remaining} free message${remaining === 1 ? '' : 's'} left as guest`
                     : 'No free messages left today'
      : remaining > 0 ? `${remaining} message${remaining === 1 ? '' : 's'} left today`
                     : "You're out for today"

  return (
    <section className="chat" data-testid="chat-panel">
      <div className="chat-thread" data-testid="chat-thread">
        {bubbles.length === 0 && (
          <p className="chat-hint">
            Add a note the model doesn't know — an injury, a tactical wrinkle, the weather.
            The prediction updates and the assistant tells you what changed.
          </p>
        )}

        {bubbles.map((b, i) => b.role === 'user' ? (
          <div className="chat-bubble chat-user" data-testid="chat-user-bubble" key={i}>
            {b.text}
          </div>
        ) : (
          <div className="chat-assistant" data-testid="chat-assistant-bubble" key={i}>
            {b.status === 'success' && b.refined && (
              <div className="chat-card">
                <div className="chat-card-row">
                  <div className="mini">
                    <h4>Baseline</h4>
                    <Bars home={baseline.home} draw={baseline.draw} away={baseline.away} />
                    <p className="mini-score">{baseline.predHome}–{baseline.predAway}</p>
                  </div>
                  <div className="mini refined">
                    <h4>Refined</h4>
                    <Bars home={b.refined.home} draw={b.refined.draw} away={b.refined.away} />
                    <p className="mini-score">{b.refined.predHome}–{b.refined.predAway}</p>
                  </div>
                </div>
              </div>
            )}
            <p className="chat-text">{b.text}</p>
          </div>
        ))}

        {/* Thinking bubble — same layout slot as a real assistant turn so the
            jump-from-button-to-bubble doesn't feel jarring. Replaced by the
            actual response once the POST resolves. */}
        {busy && (
          <div className="chat-assistant chat-thinking" data-testid="chat-thinking">
            <div className="chat-thinking-bubble">
              <span className="chat-thinking-label">Thinking</span>
              <span className="chat-thinking-dots" aria-hidden="true">
                <span /><span /><span />
              </span>
            </div>
          </div>
        )}

        <div ref={endRef} />
      </div>

      <div className="chat-quota" data-testid="chat-quota">{counterText}</div>

      {showSignInPrompt ? (
        <div className="chat-signin-prompt" data-testid="chat-signin-prompt">
          <p>Out of free tries. Sign in for 5 more.</p>
          <button data-testid="chat-signin" onClick={signIn}>Sign in</button>
        </div>
      ) : (
        <>
          <div className="chat-input-row">
            <textarea
              className="chat-input"
              data-testid="chat-input"
              rows={2}
              maxLength={charMax}
              placeholder={exhausted
                ? 'Quota resets at midnight.'
                : "Tell the model what it's missing…"}
              value={input}
              disabled={busy || exhausted}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault()
                  send()
                }
              }}
            />
            <button
              className="chat-send"
              data-testid="chat-send"
              disabled={busy || exhausted || !input.trim()}
              onClick={send}
            >
              Send
            </button>
          </div>
          {input.length > 0 && (
            <div className={`chat-charcount${charsLeft < 20 ? ' chat-charcount-warn' : ''}`} data-testid="chat-charcount">
              {input.length} / {charMax}
            </div>
          )}
        </>
      )}
    </section>
  )
}
