import { SignedIn, SignedOut, SignInButton, UserButton } from '@clerk/clerk-react'
import { Link, Route, Routes } from 'react-router-dom'
import MatchList from './pages/MatchList'
import MatchDetail from './pages/MatchDetail'
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
        </Routes>
      </main>
    </div>
  )
}
