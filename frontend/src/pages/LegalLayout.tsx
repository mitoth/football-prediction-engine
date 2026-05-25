import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

// Plain, readable legal page chrome — no chalk fonts here, no felt background
// inside the prose, so the lawyer-language stays easy to read. The tactics-board
// frame around it (topbar + footer) remains.
export default function LegalLayout({
  title, lastUpdated, children,
}: { title: string; lastUpdated: string; children: ReactNode }) {
  return (
    <article className="legal-page" data-testid="legal-page">
      <Link to="/" className="back">← Back to matches</Link>
      <header className="legal-header">
        <h1>{title}</h1>
        <p className="legal-updated">Last updated: {lastUpdated}</p>
      </header>
      <div className="legal-body">
        {children}
      </div>
    </article>
  )
}
