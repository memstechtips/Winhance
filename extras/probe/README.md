# Fresh-install defaults probe

Ground-truth collection for the Windows-defaults audit: what the registry *actually* looks like on a
clean Windows install, so the catalog's `WindowsDefault` detection can be checked against reality
instead of against itself.

## Why this exists

A Windows default can be "on" while the registry value Winhance reads **does not exist yet** —
Windows only writes the value once something explicitly sets it. So there are three states, not two:

| Registry | Meaning |
|---|---|
| value present, matches a state's `Set` | unambiguous, detection is right |
| value present, matches nothing | genuinely Custom |
| **value absent** | **ambiguous — Windows has a built-in default the registry does not record** |

The catalog resolves the third row with `StateValue.OrAbsent()`. Where a `WindowsDefault` state
lacks it *and* the setting has an `IsFallback` sibling, an absent value silently resolves to the
fallback state — the setting renders as the wrong thing rather than as Custom. Whether that is
actually happening for a given setting can only be answered by reading a real clean install.

## Files

| File | What it is |
|---|---|
| `Probe-WinhanceDefaults.template.ps1` | The probe source. **Edit this one.** |
| `catalog-probe-manifest.json` | Full `SettingCatalog` dump — targets, states, roles, `OrAbsent` flags, `catalogHash`. |
| `Probe-WinhanceDefaults.ps1` | **Generated.** The template with the manifest embedded. Do not hand-edit. |
| `reconcile-defaults.py` | Step 2: replays the detection engine over a probe file and classifies each setting `(a)`/`(b)`/`(c)` vs its `WindowsDefault` role. Read-only. Run: `reconcile-defaults.py catalog-probe-manifest.json <probe1.json> [probe2.json ...]`. |

The probe output also carries `catalogHash` (ties a returned file to the exact catalog revision) and
`powerCfgDefaults` (each powercfg setting's default AC/DC indices per scheme, read read-only from the SYSTEM
hive `DefaultPowerSchemes` — no unhide-write, so v1's powercfg-skip doesn't apply to reading *defaults*).

## Regenerating

```
winhance-harness CatalogProbeManifest
```

Runs `tests/Winhance.Infrastructure.Tests/Catalog/CatalogProbeManifestGeneratorTests.cs` on the
Windows worker, which iterates `SettingCatalog.All` in real C# (never regex over the catalog source),
writes `catalog-probe-manifest.json`, and splices the compact form into the template to produce
`Probe-WinhanceDefaults.ps1`.

Re-run it after any catalog change that adds or moves a target, state, or `WindowsDefault` role.

## Running the probe on a target machine

1. Copy **only** `Probe-WinhanceDefaults.ps1` to the machine. Nothing needs installing — it uses the
   in-box Windows PowerShell 5.1.
2. Open a **normal, non-elevated** PowerShell window.
3. ```
   powershell -ExecutionPolicy Bypass -File .\Probe-WinhanceDefaults.ps1
   ```
4. Send back the `winhance-defaults-probe_<build>_<edition>_<host>_<timestamp>.json` it writes.

### Do not run it elevated

Most catalog targets are under `HKEY_CURRENT_USER`. An elevated shell reads the *administrator's*
HKCU, which is a different hive, so every per-user reading would be wrong in a way that looks
entirely plausible in the output. The script refuses to start if it detects elevation.

(The app itself compensates for this — `WindowsRegistryService.ParseKeyPath` redirects HKCU to
`HKU\<interactive user SID>` under over-the-shoulder elevation. The probe deliberately does **not**
compensate; it refuses instead, because guessing the intended hive is exactly the sort of silent
assumption this exercise is trying to eliminate.)

### What it does and does not touch

Strictly read-only. Keys are opened with `OpenSubKey(path, false)`; nothing is written, created or
deleted anywhere except the output `.json`.

`powercfg` targets are **not probed**. Reading a hidden powercfg setting first requires *unhiding* it,
which writes `Attributes = 0` to the registry — that would no longer be a clean-install reading. Those
targets are recorded as `NotProbed`. Scheduled-task targets are read (read-only) via
`Get-ScheduledTask`, in a pass kept separate from the registry pass so a failure there cannot affect
the registry results.

## Output shape

```jsonc
{
  "machine": { "buildNumber": 26100, "ubr": 4061, "editionId": "Professional",
               "hasBattery": false, "isElevated": false, ... },
  "counts":  { "Present": 0, "ValueAbsent": 0, "KeyMissing": 0, ... },
  "settings": [
    { "id": "explorer-customization-desktop-icon-recycle-bin",
      "targets": [
        { "key": "{645FF040-...}", "status": "ValueAbsent",
          "effectiveValue": null, "effectivePath": null,
          "paths": [ { "path": "HKEY_CURRENT_USER\\...\\NewStartPanel", "status": "ValueAbsent" },
                     { "path": "HKEY_CURRENT_USER\\...\\ClassicStartMenu", "status": "KeyMissing" } ] }
      ] }
  ],
  "scheduledTasks": [ ... ],
  "absentWindowsDefaultSuspects": [ ... ]
}
```

Per-target status is one of `Present`, `KeyPresent` (a value-name-less target, whose state *is* key
existence), `ValueAbsent` (key exists, value is not written), `KeyMissing` (parent key absent),
`Error`, or `NotProbed`.

Each mirror path is recorded individually **as well as** folded to the effective reading. The fold
mirrors `RegTargetReader.OrderHklmFirst` — HKLM paths first, first non-null wins — but the per-path
detail is the point: knowing that Windows writes `NewStartPanel` and not `ClassicStartMenu` is a
large part of what this is for.

Values needing a catalog-side reduction (REG_BINARY byte index / bitmask, packed composite strings)
are recorded **raw**. The reduction is replayed later against `Winhance.Core`'s `RegTargetReader`
rather than re-implemented in PowerShell, where it could silently diverge.

`absentWindowsDefaultSuspects` is the headline: settings whose `WindowsDefault` state does not accept
absence, yet whose value is absent on that machine. It is computed from **presence only** — no value
matching — so it cannot be wrong for the subtle reasons a hand-rolled matcher would be. Confirming a
suspect is a real bug, and deciding whether the fix is `.OrAbsent()` (detection wrong, role right) or
moving the role (role wrong), is the reconciliation step that happens against the manifest afterwards.

Every absent reading is recorded, including ones that are **not** evidence of a bug; those carry
`countsTowardFinding: false` plus the reason (`applyOnly`, `targetAppliesHere`, `settingAppliesHere`,
`perSubKeyTarget`). Only the ones that count are in the console headline. Nothing is dropped silently —
a missing suspect and a clean result would otherwise look identical.

`catalogHash` (also in the manifest) identifies which catalog revision a returned probe file was
produced against. There is deliberately no timestamp, so regenerating unchanged data is a no-op diff.

## Notes for the reconciliation step

Recorded here because they are easy to get wrong and expensive to discover late:

- **`ValueName: null` and `ValueName: ""` are different things.** Null means the state *is* key
  existence (`RegTargetReader` branches on `is null`). Empty string means the key's **(Default) value**
  and is read like any other — four catalog targets do this. The manifest carries
  `keyExistenceOnly` as its own boolean because PowerShell cannot tell them apart after a `[string]`
  cast (`[string]$null` is `""`).
- **Target `key` is a join handle, not a registry name**, and four targets have an empty one. Join on
  `joinKey`, which substitutes a sentinel.
- **Reimplement `CatalogValueComparer` faithfully, all three tiers** (`byte[]` sequence equality →
  `Equals` → `Convert.ToInt64` → `ToString`/`OrdinalIgnoreCase`). A type-strict comparer gets ~23
  accepted values wrong by construction: bitmask targets reduce to a `bool` and `ByteOnly` targets to a
  `byte`, while the catalog authors those states with `int` values. They only match because of the
  `Convert.ToInt64` tier. Same tier makes a REG_SZ `"1"` equal to an int `1` — that is Winhance's live
  behaviour, so reproduce it rather than "fixing" it.
- **Most settings do not resolve through `StateDetectionEngine`.** When all targets are registry and
  exactly one read target is non-group-policy, `CatalogDiscovery.DetectState` uses the *precedence*
  path: one deciding target settles it and states not carrying that key are skipped entirely. The
  reconciliation has to branch on shape, or the "role is wrong" conclusion is computed against the
  wrong algorithm for the bulk of the catalog. `kind`, `isGroupPolicy`, `applyOnly` and target order
  are all in the manifest to determine this.
- **Conclusion (a) splits three ways on `isFallback`**, and the split matters: the fallback may be the
  `WindowsDefault` state itself (absence already resolves correctly — `.OrAbsent()` is cosmetic), a
  *different* state (absence silently resolves to the wrong answer — the real bug class), or absent
  entirely (absence resolves to Custom).
- **Exclude the unanalysable classes** using the manifest fields that identify them: `detector`
  (custom `IStateDetector` — note one of them has a detector *and* populated `Set`s, so it looks
  analysable), `optionSource`, `numeric`, `control: "Action"`, `perNetworkInterface` / `perMonitor`
  (the "all subkeys must match" semantics `RegTargetReader` does not implement), and
  `availability.builds` / target `appliesTo` against the probed machine's build.

## Collection matrix

A "fresh install" is not one thing — OOBE choices, edition, and patch level all move defaults, so the
output records build, UBR, edition, install type, locale, battery presence and machine model. Target
set: clean Windows 10 22H2, clean Windows 11 25H2, and a laptop (for the battery-gated Power
settings).
