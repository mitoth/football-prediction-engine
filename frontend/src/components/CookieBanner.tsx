import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'

// EU / UK cookie-consent banner. We only set strictly-necessary cookies today
// (Clerk session), but UK PECR + EU ePrivacy still expect us to inform the
// user that those cookies exist. The banner does not gate the product — it
// informs and lets the user acknowledge once.
//
// The choice is stored in localStorage so it does not re-appear on every load.
// If we add non-essential cookies later (analytics, A/B), this banner is the
// hook to convert into an opt-in toggle per category.

const STORAGE_KEY = 'matchforecast.cookie-consent.v1'

export default function CookieBanner() {
  const [shown, setShown] = useState(false)

  useEffect(() => {
    try {
      if (!localStorage.getItem(STORAGE_KEY)) setShown(true)
    } catch {
      // localStorage blocked (private mode / cookies-off) — skip the banner.
    }
  }, [])

  function dismiss() {
    try { localStorage.setItem(STORAGE_KEY, '1') } catch { /* noop */ }
    setShown(false)
  }

  if (!shown) return null

  return (
    <aside className="cookie-banner" role="dialog" aria-live="polite" data-testid="cookie-banner">
      <p>
        MatchForecast uses only the cookies needed for sign-in and your daily
        refinement quota. We don't run advertising, analytics, or tracking
        cookies.{' '}
        <Link to="/privacy" data-testid="cookie-banner-learn">Learn more</Link>.
      </p>
      <button data-testid="cookie-banner-dismiss" onClick={dismiss}>Got it</button>
    </aside>
  )
}
