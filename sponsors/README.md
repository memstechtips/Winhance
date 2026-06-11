# Winhance Sponsors & Supporters Data

This folder is the **single source of truth** for sponsor and supporter recognition
across every surface that shows it:

- the **in-app sponsors page** in Winhance (business sponsor cards + the most recent individual supporters, capped),
- the sponsor showcase on the **winhance.net** download page,
- the sponsors & supporters wall on **store.memstechtips.com/winhance/**,
- the sponsors line in **release notes**.

There is **one unified sponsor space per surface** — no separate "Top Sponsors"
box or carousel. Emerald sponsors simply sort first and carry emerald styling.

## Where the live data lives — the `sponsors` branch

**This data's home is the dedicated `sponsors` branch** (an orphan branch holding
only this folder), not `main`. Every consumer fetches from it:

```
https://raw.githubusercontent.com/memstechtips/Winhance/sponsors/sponsors/sponsors.json
```

New sponsors and supporters appear on every surface without a release. A snapshot
of this folder (from the `sponsors` branch) is bundled with every release and used
as the **offline fallback** when the fetch fails. `main` keeps a copy of the README
for discoverability; the branch is authoritative for data.

### How updates happen

- **Individual supporters: automated.** A scheduled job (`sponsors-sync`, every
  6 hours on the agent box) reads new paid store orders, finds the supporters-wall
  opt-in, sanitises the FULL name ("First Last" — Marco 2026-06-11, the opt-in label says so), dedupes, prepends (newest first) and
  pushes here. Names are treated as untrusted data: sanitised, length-capped,
  rendered as text only.
- **Public GitHub sponsors: automated** (same sync job, 2026-06-12). A PUBLIC
  sponsorship at github.com/sponsors/memstechtips is treated as the listing
  opt-in (private sponsorships are invisible to the job and never listed).
  Display name (sanitised) or login; the entry carries a `gh` field (the login)
  for cross-run dedupe — renderers ignore it. `since` is the discovery month.
  Only User sponsors sync (Organization sponsors need a token scope the agent
  deliberately doesn't have — they're handled manually if one appears).
- **Business sponsors: never automated.** The job detects a sponsor-tier purchase
  and notifies Marco; the card (logo arrives by email) is added here only after
  Marco approves it.
- Marco edits via PR or direct push; the agent pushes data-only commits as
  Memory's Agent.

## Schema (`sponsors.json`)

```jsonc
{
  "updated": "YYYY-MM-DD",
  "sponsors": [
    {
      "id": "your-it",               // slug, unique; logo filename derives from it
      "name": "Your IT",
      "tier": "gold",                // bronze | silver | gold | emerald
      "city": "Austin, TX",          // shown on the card
      "country": "US",
      "contact": "contact@yourit.com", // optional; shown on silver and up
      "url": "https://www.yourit.com", // clickable on gold and up
      "logo": "logos/your-it.png",   // square, ideally 512x512 PNG, transparent or dark-friendly
      "since": "2026-06",
      "example": true                // OPTIONAL: renders with an "Example — this could
                                     // be you" badge; omit for real sponsors
    }
  ],
  "supporters": [
    { "name": "Marco d.", "since": "2026-06" },          // individuals, OPT-IN, names only
    { "name": "Jane Doe", "since": "2026-06", "gh": "janedoe" }  // via public GitHub sponsorship; `gh` = login, dedupe only
  ]
}
```

The `supporters` array is maintained **newest donation first** — a returning
donor's entry moves back to the top (`since` stays their first donation).
Renderers display it in array order. The file holds at most **200** supporters
(the sync job trims on every write); surfaces show fewer and end with a vague
"and many more" line — never a computed total (counts × the $5 minimum would
leak a revenue floor). The legacy `topSponsors` array is retired:
Emerald sponsors live in `sponsors` with `"tier": "emerald"`.

### Tier colors (canonical — every surface uses these exact values)

| Tier | Hex | Use |
|---|---|---|
| bronze | `#CD7F32` | card border + tier label |
| silver | `#AEB6C2` | card border + tier label |
| gold | `#FFD700` | card border + tier label (matches the brand gold) |
| emerald | `#50C878` | card border + tier label, with a subtle glow |

The store theme defines these as CSS variables (`--tier-bronze` …); winhance.net
and the WinUI 3 in-app sponsors page must use the same hex values so a sponsor's
tier is identifiable by the same color everywhere.

### Display rules (all surfaces)

- **Order:** emerald → gold → silver → bronze; array order is kept within a tier.
- **Store wall** (`store.memstechtips.com/winhance/`): shows the top **6** cards,
  then a **"See all sponsors"** button expands the full wall in place. Individual
  supporters render below in their own strip, newest first, capped at **150**
  with a "most recent" note when truncated. Business sponsors are never capped.
- **winhance.net download page:** the same top-**6** box (or as many as the space
  fits), with a **"View all sponsors"** link to the store wall. No carousel.
- **In-app sponsors page:** **gold + emerald business cards only** (the in-app card is a Gold-and-up perk — bronze/silver stay web-only), same order and tier colors as the wall, in its own scrollable region. Below it, a **"Recent Supporters"** section: the **48** most recent individual supporters as name chips (own scrollable region) with an "and many more — thank you" line, plus the how-to line ("Support with $5 or more and tick the supporters box at checkout to be listed"). Every surface must document HOW to get listed (Marco, 2026-06-11).
- **Card contents by tier:** bronze cards show logo + name only; city and the
  contact line render on silver and up; the clickable website link on gold and up.

### Tier → surface mapping

| Tier | Store sponsors wall (store.memstechtips.com/winhance/) | winhance.net download-page showcase | In-app sponsors page | Release notes |
|---|---|---|---|---|
| bronze | logo + name | — | — | — |
| silver | full card (logo, name, city, contact) | ✓ | — | — |
| gold | ✓ + clickable website link | ✓ | full card (logo, city, contact, link) | mention |
| emerald | first position | first position | first position | mention |

Bronze cards appear on the **store sponsors wall only**; silver and up also get the
winhance.net download-page showcase.

Individual **supporters** appear on the web supporters wall (capped 150) and the in-app page (capped 48) — names only, never amounts.

## Lifecycle: one-time, lapsed, and past sponsors

- **Individual supporters are permanent.** A thank-you doesn't expire — one-time or
  recurring makes no difference. `since` = the date of their (first) donation. There
  is no "past supporters" section.
- **Business sponsor cards are active inventory.** The paid surfaces (download-page
  showcase, in-app page, first-position placement) belong to *active* sponsorships: a monthly that
  cancels, or a 1-year purchase that isn't renewed, lapses. Set the optional
  `"until": "YYYY-MM"` field on the entry — renderers must then drop the card from
  all paid surfaces and show the sponsor as a **name-only line in a "Past sponsors"
  list on the web sponsors page only** (no logo, no link, not in-app). Gratitude is
  permanent; ad placement is for current sponsors.
- A 1-year sponsorship is active until 12 months after `since`; give a grace period
  before setting `until` (renewal conversations happen by email, not by cron).
- Removal on request still overrides everything, including the past-sponsors list.

## Hard rules

1. **Never** record or publish amounts, totals, or counts — names/logos and dates only.
2. Everything here is **opt-in**. Remove anyone on request, immediately.
3. Every surface that renders this data must carry the disclaimer:
   *"Sponsors support Winhance's development. A listing recognises that support —
   it is not an endorsement by Winhance of any sponsor's products or services."*
4. Updates are **data-only commits**: this folder only, nothing else in the same
   commit. They land on the `sponsors` branch — supporter opt-ins via the
   sponsors-sync job, business cards pushed by the agent only after Marco
   approves them.

## Adding a sponsor

1. Drop the square logo into `logos/` named `<id>.png`.
2. Add the entry to `sponsors.json`, bump `updated`. **Only include the fields the
   tier promises** — bronze: `logo` + `name` only; silver: + `city` + `contact`;
   gold/emerald: + `url`. (`id`, `tier`, `country`, `since` are always present;
   renderers also gate by tier as defense in depth, but the data itself must not
   carry more than the sponsor paid for.)
3. Commit and push to the `sponsors` branch (business cards: Marco approves first).
