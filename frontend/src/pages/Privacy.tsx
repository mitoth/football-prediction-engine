import LegalLayout from './LegalLayout'

// Privacy Policy. Draft. Reviewed against: GDPR Articles 13/15/17, NewsData.io
// ToS snapshot, Anthropic Commercial Terms snapshot. NOT yet reviewed by a
// lawyer — flag for legal review before paid Stripe traffic.
export default function Privacy() {
  return (
    <LegalLayout title="Privacy Policy" lastUpdated="2026-05-25">
      <p>
        This Privacy Policy explains what information MatchForecast collects,
        how we use it, who we share it with, and the choices you have. It applies
        to anyone who visits the MatchForecast website (the "Service").
      </p>

      <h2>1. Who we are</h2>
      <p>
        MatchForecast is operated by an individual developer pre-incorporation
        as of the date above (a formal entity will be named here once
        registered). For privacy questions, email <a href="mailto:privacy@wcaipredictions.com">privacy@wcaipredictions.com</a>{' '}
        — this is the contact for data export, data delete, and any other
        questions arising under the GDPR or any equivalent data protection
        law.
      </p>

      <h2>2. What we collect</h2>
      <p><strong>If you only browse anonymously</strong>, we do not store any
      personal data about you. The match list and baseline predictions are
      served from a cache and do not record visits in a user-identifying way.</p>

      <p><strong>If you sign in</strong>, we store the minimum needed to give
      you the product:</p>
      <ul>
        <li>A Clerk-issued user ID, which we copy from the Clerk session token. We do not store your email or password — those live in Clerk's systems (see section 5).</li>
        <li>Your account timezone, so your daily refinement quota resets at the right local midnight.</li>
        <li>Each refinement note you submit (free-text only — URL refinements are disabled).</li>
        <li>Each prediction snapshot tied to your account: the baseline version, your input, the refined outcome, when it was created.</li>
        <li>A quota ledger row per day to enforce the free tier's three-refinements limit.</li>
        <li>If you purchase a paid pass, an entitlement row recording the pass type, validity window, and Stripe reference. We do not store full card details — Stripe holds those (see section 5).</li>
      </ul>

      <h2>3. How we use it</h2>
      <p>We use the data above only to:</p>
      <ul>
        <li>Authenticate you and authorise requests (Clerk session validation).</li>
        <li>Generate, refine, and display predictions for the matches you view.</li>
        <li>Enforce your daily quota and your paid-pass entitlement.</li>
        <li>Investigate abuse or fraud, only if a specific report or anomaly requires it.</li>
        <li>Comply with our legal obligations.</li>
      </ul>
      <p>We do not sell your data, and we do not use it for advertising or
      profiling. Refinement notes you submit are sent to Anthropic's Claude
      API as context for the prediction; see section 5.</p>

      <h2>4. Legal basis (GDPR)</h2>
      <p>For users in the EU / UK, our legal basis is:</p>
      <ul>
        <li><strong>Performance of contract</strong> (GDPR Article 6(1)(b)) for everything required to deliver the predictions and the quota system after you sign in.</li>
        <li><strong>Legitimate interests</strong> (Article 6(1)(f)) for fraud / abuse investigation, kept narrow and proportionate.</li>
        <li><strong>Consent</strong> (Article 6(1)(a)) where we ask for it — currently only the cookie banner's "non-essential cookies" toggle if you choose to enable optional analytics. At launch we ship no optional cookies.</li>
      </ul>

      <h2>5. Third-party processors</h2>
      <p>We use the following sub-processors. Each handles a specific slice of
      your data under their own terms:</p>
      <ul>
        <li><strong>Clerk</strong> — identity and authentication. Holds your email, password (or social-login identity), and session tokens. Their privacy policy: <a href="https://clerk.com/legal/privacy" target="_blank" rel="noreferrer noopener">clerk.com/legal/privacy</a>.</li>
        <li><strong>Anthropic (Claude API)</strong> — prediction compute. Receives the headlines and short snippets we license from NewsData.io plus your refinement note (free text only). Anthropic does not train on our API traffic by default. Their commercial terms: <a href="https://www.anthropic.com/legal/commercial-terms" target="_blank" rel="noreferrer noopener">anthropic.com/legal/commercial-terms</a>.</li>
        <li><strong>NewsData.io</strong> — news aggregator we license headlines and snippets from. NewsData does not receive any of your personal data from us; we only pull articles from them.</li>
        <li><strong>API-Football (API-Sports)</strong> — fixture, team, and standings data. Receives no personal data from us.</li>
        <li><strong>Stripe</strong> (once paid passes go live) — payment processing. Holds your card details under their PCI-compliant systems. Their privacy policy: <a href="https://stripe.com/privacy" target="_blank" rel="noreferrer noopener">stripe.com/privacy</a>.</li>
        <li><strong>PostHog</strong> — product analytics. Receives anonymous event data (which pages you view, when you submit a refinement). Once you sign in, we also send PostHog your Clerk user ID along with the email address and name on your Clerk profile, so signed-in usage can be measured separately from anonymous traffic and so we can read humans rather than opaque IDs in our analytics dashboard. We run PostHog in memory-only mode — no analytics cookies are stored in your browser. Their privacy policy: <a href="https://posthog.com/privacy" target="_blank" rel="noreferrer noopener">posthog.com/privacy</a>.</li>
        <li><strong>Cloudflare Web Analytics</strong> — anonymous traffic and country-level geo stats. Cookieless. Cloudflare receives your IP and user-agent to compute the geo / device aggregates but does not link them to a user identifier. Their privacy policy: <a href="https://www.cloudflare.com/privacypolicy/" target="_blank" rel="noreferrer noopener">cloudflare.com/privacypolicy</a>.</li>
        <li><strong>Our hosting provider — Microsoft Azure (North Europe region).</strong> Hosts the backend services, managed PostgreSQL database, and Static Web App that serves this SPA. Microsoft's privacy statement: <a href="https://privacy.microsoft.com/privacystatement" target="_blank" rel="noreferrer noopener">privacy.microsoft.com/privacystatement</a>.</li>
      </ul>
      <p>Standard contractual clauses, sub-processor lists, and data-residency
      details for each provider are available on their sites.</p>

      <h2>6. Retention</h2>
      <p>We keep the data above only as long as needed:</p>
      <ul>
        <li>Refinement notes and prediction snapshots: <strong>until you delete your account</strong>, or two years after your last sign-in, whichever is sooner.</li>
        <li>Quota ledger rows: 90 days.</li>
        <li>Entitlement (paid-pass) rows: kept for as long as legally required for tax / accounting (typically 7 years), then anonymised.</li>
      </ul>

      <h2>7. Your rights</h2>
      <p>If you are in the EU, the UK, or any other jurisdiction that grants
      data-subject rights similar to the GDPR, you can:</p>
      <ul>
        <li>Ask us what we hold about you (Article 15) — use the in-app export endpoint at <code>GET /me/export</code> once you are signed in, or email us.</li>
        <li>Ask us to delete it (Article 17) — use the in-app delete endpoint at <code>DELETE /me</code> when signed in, or email us. Deletion is irreversible.</li>
        <li>Ask us to correct anything inaccurate.</li>
        <li>Lodge a complaint with your local data protection authority.</li>
      </ul>

      <h2>8. Cookies</h2>
      <p>MatchForecast uses only cookies that are strictly necessary for the
      product to work:</p>
      <ul>
        <li><strong>Clerk session cookie</strong> — set when you sign in, used to keep you signed in across page loads.</li>
        <li><strong><code>mf_anon_id</code></strong> — set when you send your first chat-mode refinement without signing in. A random identifier paired with your IP address to count the 3 free messages we give anonymous visitors each day. No tracking, no profile, expires after 30 days. Once you sign in, the signed-in counter takes over and this cookie is no longer consulted.</li>
      </ul>
      <p>We do not use analytics, advertising, or tracking cookies. If we add
      any in future, we will surface a consent banner and let you opt in or
      out per category before any non-essential cookie is set.</p>
      <p>The IP address associated with anonymous chat messages is stored on
      the same row as the message + on the daily quota counter. Both are
      pruned with the rest of the chat audit log after 7 days.</p>

      <h2>9. Age</h2>
      <p>MatchForecast is not directed at children under 13 and we do not
      knowingly collect data from anyone under 13. If you believe a child
      has signed up, email us and we will remove the account.</p>

      <h2>10. International transfers</h2>
      <p>Our sub-processors (Anthropic, Clerk, Stripe, NewsData.io,
      API-Football) operate in or transfer data to the United States.
      For EU / UK users, the relevant safeguard is the EU Standard Contractual
      Clauses (and, where applicable, the UK Addendum) published by each
      provider.</p>

      <h2>11. Security</h2>
      <p>API keys for all sub-processors live in a secret store, never in
      client-side code. We use HTTPS for all traffic, role-based database
      access, and a JWT-based auth gate (Clerk-issued) on every authed
      endpoint. We rotate keys on schedule and on suspicion of compromise.</p>

      <h2>12. Changes to this policy</h2>
      <p>We will update this page when our processing changes. The
      "Last updated" date at the top tells you when. We will surface a
      banner on your next visit if a change materially affects you.</p>

      <h2>13. Contact</h2>
      <p>Privacy / GDPR: <a href="mailto:privacy@wcaipredictions.com">privacy@wcaipredictions.com</a>{' '}
      · DMCA / copyright: <a href="mailto:legal@wcaipredictions.com">legal@wcaipredictions.com</a>{' '}
      · General: <a href="mailto:hello@wcaipredictions.com">hello@wcaipredictions.com</a></p>

      <p className="legal-note">
        This policy is a working draft prepared by the developer. It has not
        been reviewed by a qualified lawyer. Have a privacy lawyer in your
        jurisdiction review it before opening paid signups.
      </p>
    </LegalLayout>
  )
}
