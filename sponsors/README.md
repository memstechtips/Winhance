# Winhance Sponsors & Supporters Data

**The live data and the authoritative documentation live on the dedicated
[`sponsors` branch](https://github.com/memstechtips/Winhance/tree/sponsors/sponsors)**
— an orphan branch holding only this folder, updated continuously (supporter
opt-ins automated via the sponsors-sync job; business sponsor cards added after
Marco's approval).

Every surface fetches from the branch:

```
https://raw.githubusercontent.com/memstechtips/Winhance/sponsors/sponsors/sponsors.json
```

Release bundles snapshot the folder **from the `sponsors` branch** as the offline
fallback. The copy of `sponsors.json` and `logos/` here on `main` is a stale
point-in-time snapshot kept only so the folder is discoverable — do not edit it,
do not consume it. Schema, tier rules, colors, display caps, and the hard rules
(no amounts ever, opt-in only, the disclaimer) are all documented in the README
on the `sponsors` branch.
