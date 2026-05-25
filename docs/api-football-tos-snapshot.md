# API-Football — Terms snapshot

**Source:** https://www.api-football.com/terms
**Captured:** 2026-05-25
**Entity:** API-Sports

> Partial capture. The user pasted the verbatim **logos / images / trademarks**
> clause below (the highest-risk clause for our use). The rest of the ToS has
> not yet been verbatim-captured because the live page returns HTTP 403 to my
> automated fetcher. Paste any other clause that matters into chat to upgrade
> this snapshot.

---

## Verbatim clauses captured

### Logos, images, and trademarks (paid plan)

> Logos, images and trademarks delivered through the API are provided solely
> for identification and descriptive purposes (e.g., identifying leagues,
> teams, players or venues). We does not own any of these visual assets, and
> no intellectual property rights are claimed over them. Some images or data
> may be subject to intellectual property or trademark rights held by third
> parties (including but not limited to leagues, federations, or clubs). The
> use of such content in your applications, websites, or products may require
> additional authorization or licensing from the respective rights holders.
> You are fully responsible for ensuring that your usage of any logos, images,
> or branded content complies with applicable laws in your country or the
> countries where your services are made available.

---

## Verdict per hot-spot

### 1. Logos / badges / team crests — ⚠️ DO NOT RENDER

API-Football explicitly disclaims ownership of any visual assets they hand
back. They warn that "use of such content in your applications, websites, or
products **may require additional authorization or licensing from the
respective rights holders**." And they put 100% of the compliance burden on
us: "**You are fully responsible** for ensuring that your usage of any logos,
images, or branded content complies with applicable laws."

**MatchForecast status:** ✅ safe. We never render any logos, badges, or
images from the API response. We render team names and league names as text
only.

**Hard rule for the codebase:** if a future feature wants to show a club
crest or league logo, **don't pull it from the API-Football response**.
Either skip the asset entirely or license it independently from each rights
holder (FIFA, UEFA, the EPL, individual clubs). The current "no logos
anywhere" position avoids the whole problem cleanly.

### 2. Team names + league names + group letters (text only) — ✅ safe

Team names (e.g. "Arsenal", "Mexico") and league names (e.g. "World Cup",
"Premier League") are trademarks but their use is **nominative fair use** —
we are using the names to identify the entities themselves, not to imply
endorsement or to brand our own product. Standard trademark doctrine in
US / UK / EU jurisdictions permits this. The API-Football ToS does not
restrict text use of team / league names.

Same logic applies to group letters ("Group A"), competition stages
("Round of 16"), and matchweeks ("Regular Season - 12").

**MatchForecast status:** ✅ safe.

### 3. Fixtures, kickoff times, standings, scores — ✅ safe

Facts are not copyrightable in the US / UK. EU has a sui-generis database
right that protects the *arrangement* of a database of facts, but
API-Football's license to us covers our use of their database. Re-displaying
the facts (fixture list, kickoff time, group standings) inside our own
product is the kind of derivative work explicitly enabled by the API.

**MatchForecast status:** ✅ safe.

### 4. Branded content — ⚠️ watch for drift

The clause says "**logos, images, or branded content**." "Branded content"
is broader than just logos / images and could be argued to include:

- Stylized team-name renderings (e.g. using the same font as the club's brand)
- Composition of name + colour + crest that resembles the club's official branding
- Promotional language ("the official MatchForecast for Arsenal fans")

**Hard rule for the codebase:**

- Do not style team names with brand-specific fonts or colours
- Do not imply official endorsement of any team / league / federation
- Do not use team names in marketing copy as if endorsing the product
  ("MatchForecast — the official AI prediction app for the FIFA World Cup"
  is a bad headline; "MatchForecast — AI football predictions for the
  2026 World Cup" is fine)

**MatchForecast status:** ✅ safe. Our wordmark is "MatchForecast"; our
visual identity (chalk on green felt) is not borrowed from any club / league.

### 5. Mass-media, betting, fantasy use — ⚠️ may need extra license

From the earlier web-search summary (not verbatim — re-confirm in your copy
of the live ToS): "The use of data for betting platforms, television
broadcasting, fantasy sports platforms, or any mass media distribution may
require additional licenses from the relevant rights holders."

**MatchForecast status:** we are explicitly **not** a betting platform
(footer disclaimer + entertainment framing). We are not TV. We are not
fantasy. We are a paid consumer AI prediction product. That sits in the
grey zone but on the safer side of the carve-out as long as we don't drift
into "place a bet" CTAs or odds displays.

### 6. AI / LLM use — ❓ no clause captured yet

We have not yet captured a verbatim clause on LLM input or training use.
Whatever's in the live ToS likely doesn't carve out AI specifically. Default
assumption: our paid license to the data covers feeding fixtures + team
names to an LLM as factual context for a prediction (we are not training,
not redistributing the raw API, not modelling on it).

**Worth doing:** include a one-line confirmation question in any future
support email to API-Football, same shape as the NewsData LLM-use email.

---

## What we're doing in MatchForecast (current vs. ToS)

| Use | Status | Notes |
|---|---|---|
| Display team names (text) | ✅ done | Nominative use, low risk |
| Display league names (text) | ✅ done | Nominative use, low risk |
| Display kickoff time + date | ✅ done | Facts |
| Display group letter ("Group A") | ✅ done | Facts (via /standings endpoint) |
| Display matchweek / stage label | ✅ done | Facts |
| Display team logos / badges | ❌ never | Hard rule per the verbatim clause above |
| Display competition logos | ❌ never | Same hard rule |
| Pass team names + fixtures to an LLM | ✅ done | Facts, not branded content |
| Use brand-specific fonts / colours per team | ❌ never | Avoid "branded content" drift |
| Imply official endorsement | ❌ never | Avoid mass-media carve-out trigger |
| Footer credit ("Fixture data via API-Football") | ✅ done | Not strictly required by the clause we captured, but cheap defensibility |

---

## Remaining open items

- [ ] Capture verbatim text of the **commercial-use license** clause (we currently only have a web-search paraphrase).
- [ ] Capture verbatim text of the **rate-limit / cache** clause (Pro plan limits).
- [ ] Capture verbatim text of the **AI / LLM use** clause if any exists.
- [ ] Capture verbatim text of the **mass-media / betting / fantasy** carve-out.
- [ ] Send a short confirmation email to API-Sports (`contact@api-sports.io`) asking them to confirm in writing that displaying team names + fixture data alongside an AI-generated prediction is within our Pro-plan license.

Paste any of those clauses into chat and I will append them here verbatim and re-verdict.
