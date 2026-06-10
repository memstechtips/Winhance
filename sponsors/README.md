# Winhance Sponsors & Supporters Data

This folder is the **single source of truth** for sponsor and supporter recognition
across every surface that shows it:

- the **in-app sponsors page** in Winhance (business sponsors only — never individual names),
- the sponsors carousel and the **"Top Sponsors" box** on **winhance.net**,
- the sponsors & supporters wall on **store.memstechtips.com/winhance/**,
- the sponsors line in **release notes**.

## How the app consumes it

Winhance fetches the live file from the `main` branch
(`https://raw.githubusercontent.com/memstechtips/Winhance/main/sponsors/sponsors.json`)
so new sponsors appear in the app without a release. A snapshot of this folder is
bundled with every release and used as the **offline fallback** when the fetch fails.

## Schema (`sponsors.json`)

```jsonc
{
  "updated": "YYYY-MM-DD",
  "topSponsors": [ /* Platinum sponsors — same shape as sponsors; featured in the
                      winhance.net "Top Sponsors" box and first position everywhere */ ],
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

### Tier → surface mapping

| Tier | Sponsors pages (store + winhance.net) | winhance.net download-page carousel | In-app sponsors page | Release notes | "Top Sponsors" box (winhance.net home) |
|---|---|---|---|---|---|
| bronze | logo + name | — | — | — | — |
| silver | ✓ | full card (logo, city, link) | — | — | — |
| gold | ✓ | ✓ | full card (logo, city, contact, link) | mention | — |
| platinum | ✓ | first position | first position | mention | ✓ |

Platinum entries live in the `topSponsors` array; the "Top Sponsors" box is **separate
from and in addition to** the regular sponsor surfaces. Individual **supporters**
appear on the web supporters wall only — never in the app.

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
