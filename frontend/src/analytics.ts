import posthog from 'posthog-js'

// PostHog wrapper. Keeps event names + Clerk identification in one place so
// the rest of the app can call analytics.event(...) without importing PostHog
// directly. No-ops when VITE_POSTHOG_KEY is unset (local `vite preview`,
// Playwright runs) so tests don't ship anonymous telemetry.

const key  = import.meta.env.VITE_POSTHOG_KEY  as string | undefined
const host = (import.meta.env.VITE_POSTHOG_HOST as string | undefined) ?? 'https://eu.i.posthog.com'

let initialized = false

export function initAnalytics() {
  if (initialized || !key) return
  posthog.init(key, {
    api_host: host,
    // Cookieless mode — the cookie banner currently declares only the Clerk
    // session cookie. Switching to memory-only persistence lets us track
    // unique sessions without writing storage that legally counts as
    // analytics cookies.
    persistence: 'memory',
    // Auto-capture is on by default (click + pageview). Disable session
    // recording explicitly — privacy posture matters more than fidelity here.
    disable_session_recording: true,
    capture_pageview: true,
    capture_pageleave: true,
  })
  initialized = true
}

export function identify(clerkUserId: string | null, traits?: Record<string, unknown>) {
  if (!initialized) return
  if (clerkUserId) posthog.identify(clerkUserId, traits)
  else posthog.reset()
}

export function event(name: string, props?: Record<string, unknown>) {
  if (!initialized) return
  posthog.capture(name, props)
}

// Cloudflare Web Analytics — pure traffic + geo beacon, no cookies. Injected
// at runtime so unconfigured environments (E2E, local dev) ship no script.
// The token is the "beacon" value Cloudflare hands you in the Web Analytics
// dashboard after registering the SPA hostname.
export function initCloudflareBeacon() {
  const token = import.meta.env.VITE_CLOUDFLARE_BEACON_TOKEN as string | undefined
  if (!token) return
  const s = document.createElement('script')
  s.defer = true
  s.src = 'https://static.cloudflareinsights.com/beacon.min.js'
  s.setAttribute('data-cf-beacon', JSON.stringify({ token }))
  document.head.appendChild(s)
}
