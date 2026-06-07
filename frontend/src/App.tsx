import { SignedIn, SignedOut, SignInButton, UserButton } from '@clerk/clerk-react'
import { Link, Route, Routes } from 'react-router-dom'
import MatchList from './pages/MatchList'
import MatchDetail from './pages/MatchDetail'
import Privacy from './pages/Privacy'
import Terms from './pages/Terms'
import CookieBanner from './components/CookieBanner'
import './App.css'

export default function App({ clerkConfigured }: { clerkConfigured: boolean }) {
  return (
    <div className="app">
      <header className="topbar">
        <Link to="/" className="brand">MatchForecast<span className="brand-dot">·</span><span className="brand-tag">WC 2026</span></Link>
        <div className="auth">
          {clerkConfigured ? (
            <>
              <SignedOut><SignInButton mode="modal" /></SignedOut>
              <SignedIn><UserButton /></SignedIn>
            </>
          ) : (
            // Phase 3 ships anonymous; sign-in arrives with the Clerk tenant (Phase 4).
            <span className="auth-note">Browsing anonymously</span>
          )}
        </div>
      </header>

      <main>
        <Routes>
          <Route path="/" element={<MatchList />} />
          <Route path="/match/:id" element={<MatchDetail />} />
          <Route path="/privacy" element={<Privacy />} />
          <Route path="/terms" element={<Terms />} />
        </Routes>
      </main>

      <footer className="app-footer" data-testid="app-footer">
        <p className="credits">
          Predictions by Claude (Anthropic). News headlines via{' '}
          <a href="https://newsdata.io" target="_blank" rel="noreferrer noopener">
            NewsData.io
          </a>
          . Fixture and standings data via{' '}
          <a href="https://www.api-football.com" target="_blank" rel="noreferrer noopener">
            API-Football
          </a>
          .
        </p>
        <p className="legal">
          MatchForecast is for entertainment, not betting advice. Article copyrights
          belong to their respective publishers; click any headline to read the
          original.
        </p>
        <p className="legal-links">
          <Link to="/privacy">Privacy</Link>
          <span aria-hidden="true"> · </span>
          <Link to="/terms">Terms</Link>
          <span aria-hidden="true"> · </span>
          <a href="mailto:hello@wcaipredictions.com">Contact</a>
        </p>
      </footer>

      <CookieBanner />
    </div>
  )
}
