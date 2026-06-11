# Winhance Sponsors & Supporters Data

This folder is the **single source of truth** for sponsor and supporter recognition
across every surface that shows it:

- the **in-app sponsors page** in Winhance (business sponsors only — never individual names),
- the sponsor showcase on the **winhance.net** download page,
- the sponsors & supporters wall on **store.memstechtips.com/winhance/**,
- the sponsors line in **release notes**.

There is **one unified sponsor space per surface** — no separate "Top Sponsors"
box or carousel. Platinum sponsors simply sort first and carry platinum styling.

## How the app consumes it

Winhance fetches the live file from the `main` branch
(`https://raw.githubusercontent.com/memstechtips/Winhance/main/sponsors/sponsors.json`)
so new sponsors appear in the app without a release. A snapshot of this folder is
bundled with every release and used as the **offline fallback** when the fetch fails.

## Schema (`sponsors.json`)

```jsonc
{
  "updated": "YYYY-MM-DD",
  "sponsors": [
    {
      "id": "your-it",               // slug, unique; logo filename derives from it
      "name": "Your IT",
      "tier": "gold",                // bronze | silver | gold | platinum
      "city": "Austin, TX",          // shown on the card
      "country": "US",
      "contact": "contact@yourit.com", // optional; shown on gold/platinum cards
      "url": "https://www.yourit.com", // clickable on silver and up
      "logo": "logos/your-it.png",   // square, ideally 512x512 PNG, transparent or dark-friendly
      "since": "2026-06",
      "example": true                // OPTIONAL: renders with an "Example — this could
                                     // be you" badge; omit for real sponsors
    }
  ],
  "supporters": [
    { "name": "Marco d.", "since": "2026-06" }  // individuals, OPT-IN, names only
  ]
}
```

The `supporters` array is maintained **newest donation first** — a returning
donor's entry moves back to the top (`since` stays their first donation).
Renderers display it in array order. The legacy `topSponsors` array is retired:
Platinum sponsors live in `sponsors` with `"tier": "platinum"`.

### Tier colors (canonical — every surface uses these exact values)

| Tier | Hex | Use |
|---|---|---|
| bronze | `#CD7F32` | card border + tier label |
| silver | `#AEB6C2` | card border + tier label |
| gold | `#FFD700` | card border + tier label (matches the brand gold) |
| platinum | `#E5E4E2` | card border + tier label, with a subtle glow |

The store theme defines these as CSS variables (`--tier-bronze` …); winhance.net
and the WinUI 3 in-app sponsors page must use the same hex values so a sponsor's
tier is identifiable by the same color everywhere.

### Display rules (all surfaces)

- **Order:** platinum → gold → silver → bronze; array order is kept within a tier.
- **Store wall** (`store.memstechtips.com/winhance/`): shows the top **6** cards,
  then a **"See all sponsors"** button expands the full wall in place. Individual
  supporters render below in their own strip, newest first, capped at **150**
  with a "most recent" note when truncated. Business sponsors are never capped.
- **winhance.net download page:** the same top-**6** box (or as many as the space
  fits), with a **"View all sponsors"** link to the store wall. No carousel.
- **In-app sponsors page:** business sponsors only, same order, same tier colors.
- **Card contents by tier:** every card shows logo, name, city; the website link
  renders on silver and up; the contact line on gold and up.

### Tier → surface mapping

| Tier | Sponsors pages (store + winhance.net) | winhance.net download-page showcase | In-app sponsors page | Release notes |
|---|---|---|---|---|
| bronze | logo + name | — | — | — |
| silver | ✓ | full card (logo, city, link) | — | — |
| gold | ✓ | ✓ | full card (logo, city, contact, link) | mention |
| platinum | first position | first position | first position | mention |

Individual **supporters** appear on the web supporters wall only — never in the app.

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
   commit. They go to `main` via PR (Marco merges).

## Adding a sponsor

1. Drop the square logo into `logos/` named `<id>.png`.
2. Add the entry to the right array in `sponsors.json`, bump `updated`.
3. Open a data-only PR into `main`.
