import { SignedIn, SignedOut, SignInButton, UserButton } from '@clerk/clerk-react'
import './App.css'

const apiUrl = import.meta.env.VITE_API_URL as string | undefined

export default function App({ clerkConfigured }: { clerkConfigured: boolean }) {
  return (
    <main style={{ fontFamily: 'system-ui', maxWidth: 640, margin: '4rem auto', padding: '0 1rem' }}>
      <h1>WC Predictions</h1>
      <p>Phase 0 shell — World Cup 2026.</p>
      <p>BFF API: <code>{apiUrl ?? '(not set)'}</code></p>

      {clerkConfigured ? (
        <>
          <SignedOut>
            <SignInButton mode="modal" />
          </SignedOut>
          <SignedIn>
            <UserButton />
          </SignedIn>
        </>
      ) : (
        <p style={{ color: '#b45309' }}>
          Clerk not configured (no publishable key yet — Phase 0). Sign-in wires
          up once the Clerk tenant exists.
        </p>
      )}
    </main>
  )
}
