import LegalLayout from './LegalLayout'

// Terms of Service. Draft. Includes the Anthropic-required "may be false or
// misleading" disclaimer (§D.3 of their Commercial Terms) and the
// entertainment-not-betting framing (design §8 / NewsData ToS analysis).
// NOT yet reviewed by a lawyer.
export default function Terms() {
  return (
    <LegalLayout title="Terms of Service" lastUpdated="2026-05-25">
      <p>
        These Terms govern your use of MatchForecast (the "Service"). By using
        the Service you agree to these Terms. If you do not agree, do not use
        the Service.
      </p>

      <h2>1. What MatchForecast does</h2>
      <p>
        MatchForecast generates AI-assisted pre-match predictions for football
        matches and lets signed-in users refine those predictions with a
        short free-text note. The predictions are produced by Anthropic's
        Claude model from publicly available news headlines, snippets, and
        sports data we license from third parties.
      </p>

      <h2>2. Predictions are entertainment, not betting advice</h2>
      <p><strong>The numbers MatchForecast shows are model predictions, not the
      result of the match.</strong> They are intended for entertainment only.
      You should not use them as the basis for any gambling, betting,
      financial, or other consequential decision. We do not provide betting
      advice or odds.</p>
      <p>Anthropic, the provider of the underlying AI model, does not warrant
      that its outputs are accurate, complete, or error-free, and explicitly
      cautions that factual assertions in outputs should not be relied upon
      without independently checking their accuracy. We pass that caution
      through to you: <strong>predictions may be false, incomplete, misleading, or
      not reflective of recent events</strong>. Verify anything that matters before
      acting on it.</p>

      <h2>3. Your account</h2>
      <p>You can browse predictions anonymously. To submit a refinement you
      need an account, provided through our authentication partner Clerk. You
      must be at least 13 years old to create an account, and you must keep
      your sign-in credentials secure. You are responsible for activity that
      happens under your account.</p>

      <h2>4. Free tier and paid passes</h2>
      <p>The free tier includes up to three successful refinements per day.
      Refinements that the model rejects as gibberish or off-topic do not
      consume your quota. Once Stripe-backed paid passes are enabled:</p>
      <ul>
        <li><strong>$0.99 matchday pass</strong> — unlimited refinements for the calendar matchday selected.</li>
        <li><strong>$5 World Cup tournament pass</strong> — unlimited refinements for the FIFA World Cup 2026 tournament window.</li>
      </ul>
      <p>A fair-use ceiling of approximately thirty successful refinements per
      day applies to all paid passes to discourage automated abuse and protect
      our model-spend budget.</p>

      <h2>5. Refunds</h2>
      <p>Paid passes are digital goods delivered immediately. Refunds are
      offered on a discretionary basis within fourteen days of purchase if
      the pass has not been used. Stripe handles the payment dispute process;
      contact us at <a href="mailto:billing@matchforecast.app">billing@matchforecast.app</a> before opening a chargeback.</p>

      <h2>6. Acceptable use</h2>
      <p>You agree not to:</p>
      <ul>
        <li>Scrape or republish predictions or article snippets in bulk.</li>
        <li>Submit refinement notes intended to bypass the model's safety guidance, prompt-inject the model, or contain illegal content.</li>
        <li>Use MatchForecast for any betting or gambling product (re-publishing our predictions on a bookmaker's site requires a separate written agreement that does not currently exist).</li>
        <li>Probe, scan, or attempt to break the service's security.</li>
      </ul>
      <p>We may suspend or terminate accounts that violate these rules,
      keeping records as required by law.</p>

      <h2>7. Content ownership</h2>
      <p>The predictions are our content; you may quote them with attribution
      ("predicted by MatchForecast") for personal, non-commercial purposes.
      The article headlines and snippets shown alongside each prediction are
      licensed to us by NewsData.io; the underlying articles remain the
      property of their publishers. Click any headline to read the original
      on the publisher's site.</p>

      <h2>8. Third-party services</h2>
      <p>MatchForecast relies on third-party services (Clerk, Anthropic,
      NewsData.io, API-Football, Stripe, our hosting provider) listed in our
      <a href="/privacy">Privacy Policy</a>. Each operates under its own
      terms; your use of our Service is conditional on those terms continuing
      to apply.</p>

      <h2>9. No warranty</h2>
      <p>The Service is provided "as is" and "as available". We disclaim all
      warranties express or implied, including warranties of accuracy,
      fitness for a particular purpose, and non-infringement. We do not
      guarantee uptime, availability, or that any specific prediction is
      correct.</p>

      <h2>10. Limitation of liability</h2>
      <p>To the maximum extent permitted by law, our total liability to you
      under or arising out of these Terms is capped at the greater of (a) the
      amount you have paid us in the twelve months preceding the event giving
      rise to the claim, or (b) USD 50. We are not liable for indirect,
      incidental, special, consequential, or exemplary damages, including loss
      of profit or wagers.</p>

      <h2>11. Termination</h2>
      <p>You may close your account at any time using the in-app delete
      endpoint at <code>DELETE /me</code> or by emailing us. We may suspend
      or close accounts that violate these Terms. Sections 2, 7, 9, 10, and
      12 survive termination.</p>

      <h2>12. Changes</h2>
      <p>We may update these Terms. We will surface a banner if a change
      materially affects you. Continued use after a change is acceptance of
      the new Terms.</p>

      <h2>13. Governing law</h2>
      <p>These Terms are governed by the laws of the developer's country of
      residence; jurisdiction for disputes is the courts of that country.
      The specific country will be named here once the operating entity is
      formally incorporated.</p>

      <h2>14. Contact</h2>
      <p>General: <a href="mailto:hello@matchforecast.app">hello@matchforecast.app</a>{' '}
      · Billing: <a href="mailto:billing@matchforecast.app">billing@matchforecast.app</a>{' '}
      · Legal / DMCA: <a href="mailto:legal@matchforecast.app">legal@matchforecast.app</a></p>

      <p className="legal-note">
        These Terms are a working draft prepared by the developer. They have
        not been reviewed by a qualified lawyer. Have a lawyer in your
        jurisdiction review them before opening paid signups.
      </p>
    </LegalLayout>
  )
}
