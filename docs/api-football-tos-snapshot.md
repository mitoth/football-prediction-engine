# API-Football — Terms snapshot

**Source:** https://www.api-football.com/terms
**Captured:** 2026-05-25 (partial — see note below)
**Entity:** API-Sports

> ⚠️ **Partial capture.** The live ToS page returns HTTP 403 to my automated
> fetcher (anti-bot protection). The clauses below are paraphrased from a
> public web-search summary of the same page, not the verbatim text. **Open
> https://www.api-football.com/terms in your browser, copy the full text into
> this chat, and I will replace this file with the verbatim snapshot — same
> drill as the NewsData ToS.** Until then this file is best-effort.

---

## What the search summary surfaced

### 1. No commercial-use license on the data itself

The search summary indicates API-Football does **not** grant a commercial-use
license over the data and that the customer is responsible for obtaining any
necessary licenses from the rights holders (leagues, federations, clubs)
themselves.

> "API-Football does not provide a 'license' for the use and publication of
> data on applications, websites or products, and any license or permission
> to publish the data must be requested by the user from the competent
> authorities. Additionally, they do not grant any commercial rights on
> sports competitions."

This is the major risk to verify against the live text. If accurate, it means:

- The fixtures, kickoff times, team names, group labels are facts and not
  themselves copyrightable in most jurisdictions, so displaying them is
  generally safe.
- **Team logos, league logos, club badges, trademarks** that the API hands
  back ARE owned by third parties. Displaying them on a paid commercial
  product without separate license is the risk. **We currently render only
  team names and league names as text — no logos, no badges, no trademarks.**
- For us, the practical risk is the **EU sui generis database right** — the
  arrangement of facts (fixtures, results) into a database is itself a
  protected work in the EU even if the facts aren't. API-Football is the
  database author; we have a license to use their API, but downstream
  commercial redistribution may need an extra carve-out.

### 2. Mass-media / betting carve-outs

Search summary specifically calls out additional licensing for:

- Betting platforms
- TV broadcasting
- Fantasy sports
- "Any mass media distribution"

MatchForecast displays AI predictions framed as **entertainment, not betting
advice** (footer disclaimer, design §8). That positioning is partly chosen to
sit on the safe side of this clause. Confirm in the live text whether "AI
prediction product" is treated more like fantasy / betting or like editorial.

### 3. Logos / images

> "Logos, images and trademarks delivered through the API are provided solely
> for identification and descriptive purposes, and API-Football does not own
> any of these visual assets and no intellectual property rights are claimed
> over them."

Their position: they pass the assets through, claim no rights, and don't
license you to use them commercially. Translation — don't display the logos
they hand back unless you separately verify the rights for each one.
**MatchForecast doesn't render logos at all today.** ✅

---

## What we're doing in MatchForecast that's relevant

| Use | Status |
|---|---|
| Display team names (text) | ✅ done — facts, low risk |
| Display league names (text) | ✅ done — facts, low risk |
| Display kickoff time + date | ✅ done — facts |
| Display group letter ("Group A") and matchweek ("Stage 1") | ✅ done — facts derived from the standings endpoint |
| Display team logos / badges | ❌ never rendered — by deliberate choice |
| Display competition logos | ❌ never rendered |
| Pass team/fixture data to an LLM for prediction | ✅ done — facts, low risk |
| Footer credit ("Fixture data via API-Football") | ✅ done — `App.tsx` |
| Read full ToS verbatim | ⏳ pending — paste live text into chat |

---

## What to do next

1. Open https://www.api-football.com/terms in a browser.
2. Copy the entire ToS body and paste it into this chat.
3. I will replace this file with the verbatim snapshot and re-do the verdict
   per hot-spot exactly as we did for NewsData.
4. If the verbatim clauses contradict anything in this stub, the live text
   wins.

Until step 3 is done, the launch-prep checklist item "Read + snapshot
API-Football ToS" stays at the partial-✅ state (search summary only, not
verbatim).
