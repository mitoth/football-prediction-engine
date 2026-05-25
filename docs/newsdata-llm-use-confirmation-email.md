# Email to NewsData.io — LLM-use confirmation

Send this verbatim to `hello@newsdata.io` from the same email tied to the paid
subscription. Keep their reply on file (forward to a `legal@` shared inbox or
print to PDF and commit it next to this file as
`newsdata-llm-use-confirmation-reply-YYYY-MM-DD.md`). The reply is the paper
trail that closes the only real ambiguity in the current ToS (it does not
mention LLM use explicitly).

---

**Subject:** Paid plan — confirmation of permitted use (LLM context + citation display)

Hi NewsData team,

I'm a paid subscriber building a football match-prediction product
(`MatchForecast`) that uses your API to surface relevant football news to my
users. I want to confirm in writing that the use I describe below sits inside
my paid-plan license before I open it up to paying customers.

How my product uses the data:

1. **Fetch.** A backend worker queries your `/news` endpoint (sports category,
   English) every 30 minutes and stores the **headline, outlet, URL, and the
   description / snippet** you return. We never store or display the full
   article text.
2. **LLM context.** For each upcoming football match, I select up to 8 of the
   most-recently fetched articles that mention either team and pass the
   **headline + your snippet** to Anthropic's Claude API as context for a
   match prediction. The article text is used as input to the model alongside
   the model's own knowledge; the model output is a prediction
   (probabilities + scoreline + a short explanation paragraph). The article
   text itself is not used as training data, fine-tuning data, or for any
   model-improvement purpose; each request is a fresh inference call.
3. **End-user display.** On the match's page, my users see the model's
   prediction and an expandable "What the model read" panel that lists the
   articles the model was given. Each entry shows the headline, the outlet,
   your snippet (capped at 250 characters), and a link out to the publisher's
   URL — clicking the headline opens the publisher's site in a new tab. We
   credit NewsData.io as the source of headlines and snippets directly in the
   citation panel and in the app footer.
4. **No re-distribution of the API.** End users never see the API key, never
   call your API directly, and cannot retrieve raw article lists through my
   service.

I have read the live ToS at https://newsdata.io/terms and believe the use
above is covered by the broad commercial-use clause in the "DATA LICENSE"
section, but the ToS does not address LLM context use specifically. Could you
confirm in reply that:

(a) using your headlines and snippets as **input context to an LLM that
generates a separate predictive output** is within the paid-plan license,
and

(b) displaying the headline + outlet + your snippet (≤ 250 chars) + publisher
URL inside the user-facing prediction page is permitted under the
sublicense / syndication clause?

Thanks for confirming. Happy to share further detail on the architecture
if useful.

Best,
[your name]
[paid-plan account email]
