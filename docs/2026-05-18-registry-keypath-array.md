# Registry KeyPath array — design brief for `RegistrySetting`

> **Status:** Pre-plan brief. Hand this to a fresh-context agent to brainstorm and turn into a task-by-task plan. Not yet executable.
>
> **Owner:** Marco (memstechtips). Drafted 2026-05-18 by Memory's Agent after refactoring the related `SupportsCustomState` issue.

## TL;DR

Change `RegistrySetting.KeyPath` from `string` to `string[]`. One `RegistrySetting` entry can then target multiple registry paths that share the same `ValueName` and the same target value — eliminating an entire class of latent bugs (silent key-collisions when multiple `RegistrySetting` entries share a `ValueName`) and tightening the schema so multi-write intent is expressible without ambiguity.

Marco prefers **Option B**: breaking schema change, all-at-once. Ship one focused PR; auto-migrate existing settings; drop the legacy field.

## Background — why this is needed

### The collision we just fixed (full context)

Winhance issue #644 surfaced a bug where the "Touch Keyboard and Handwriting Panel Service" setting falsely reported `Disabled (Recommended)` on a fresh Win11 where none of the registry values actually matched. Root cause was a fallthrough in `ComboBoxResolver.ResolveRawValuesToIndex` (now fixed) but during the investigation we also discovered a structural problem:

The setting had three `RegistrySetting` entries, two of which shared `ValueName = "Start"`:

```csharp
RegistrySettings = new List<RegistrySetting>
{
    new RegistrySetting { KeyPath = "...\\TabletInputService", ValueName = "Start", ... },
    new RegistrySetting { KeyPath = "...\\TapiSrv",            ValueName = "Start", ... }, // unrelated service
    new RegistrySetting { KeyPath = "...\\Microsoft\\input",   ValueName = "IsInputAppPreloadEnabled", ... },
}
```

The resolver's `currentValues` dictionary is keyed by `ValueName`, so the second `Start` entry silently overwrote the first. Only `TapiSrv\Start`'s value ended up being compared against `ValueMappings["Start"]`, not `TabletInputService\Start`. Detection was structurally wrong before the fallthrough bug even ran.

That specific case turned out to be unintentional bundling — `TapiSrv` (Telephony Service) has no functional connection to the touch keyboard, so it was split into its own setting (`gaming-telephony-service`, landed 2026-05-18).

### The wider pattern

A codebase audit found **32 SettingDefinitions** with at least one duplicate `ValueName` in their `RegistrySettings` list. Of those:

- **~30 are intentional "mirror" writes** — the same value is written to multiple registry hives for cross-policy resilience. Examples:
  - `taskbar-meet-now` mirrors `HideSCAMeetNow` to 3 different hives.
  - `privacy-diagnostics` mirrors `AllowTelemetry` to 4 different locations (HKLM Policies, HKCU, etc.).
  - `gaming-connected-devices-platform-service` mirrors `Start` across `CDPSvc` + `CDPUserSvc` (two halves of the same Connected Devices Platform subsystem).
- **~2 were unintentional bundling of unrelated services** (touch-keyboard + TapiSrv was the canonical example; that's now split).

The mirror pattern is real, common, and intentional. The current encoding — multiple `RegistrySetting` entries with shared `ValueName` — is a clumsy way to express it:

1. **Reads awkwardly.** A reader has to scan the entire list to confirm which `ValueName`s are shared.
2. **Leaves detection ambiguous.** Today the resolver implicitly uses last-write-wins (only the last iteration's value for a given `ValueName` ends up in `currentValues`). That's undefined behaviour from the schema's perspective.
3. **Makes accidental collisions silent.** A developer adding a second service with `ValueName = "Start"` to an existing setting accidentally creates a detection bug. Exactly the touch-keyboard / TapiSrv shape.
4. **Multiplies boilerplate.** A 4-way mirror is 4 nearly-identical entries instead of one with an array.

A `string[]` `KeyPath` expresses the intent directly: "this is one logical setting, written to these N paths."

## Proposed design

### Schema change

`src/Winhance.Core/Features/Common/Models/RegistrySetting.cs` (or wherever the record lives):

```csharp
// Before
public required string KeyPath { get; init; }

// After
public required IReadOnlyList<string> KeyPath { get; init; }
```

A single-path entry becomes `KeyPath = ["..."]`. A mirror becomes `KeyPath = ["...", "...", "..."]`.

### Semantic decisions to make (the brainstorm)

#### 1. Detection rule — `ANY` vs `ALL`

When the resolver checks whether the current state matches an option's expected value:

- **ANY match** (recommended): the setting is "in target state" if any of the paths in the array holds the expected value. Matches Windows policy precedence — if any effective policy is set, the system behaves as if the setting is applied.
- **ALL match**: the setting is "in target state" only if every path in the array holds the expected value. Stricter — surfaces `CustomStateIndex` whenever the mirror is inconsistent (e.g. HKLM Policies set but HKCU not).

Recommendation: **ANY**, because that's the user-perceptible truth ("is the setting effectively applied?") and matches how Windows resolves policy conflicts. Worth confirming with a few real-world cases — `privacy-diagnostics` with its 4 paths is a good stress test.

#### 2. Apply rule — write to all paths or just the primary

When applying a value to a multi-path entry:

- **Write to all paths**: matches the current behaviour (each existing `RegistrySetting` in the list is written today). Defensive — covers cases where Windows reads from a different hive than we expect.
- **Write only to a primary path**: requires picking which path is the canonical write target. More fragile.

Recommendation: **write to all** — keep current behaviour.

#### 3. Per-path flags

`RegistrySetting` currently carries several flags that the new array shape needs to handle:

- `IsGroupPolicy` (bool) — affects autounattend XML generation hive routing.
- `LockKeyAccess` (bool) — locks the key after write.
- `IsPrimary` (bool) — used in places like config import/export.
- `DefaultValue` (object?) — per-path default.
- `ValueType` (RegistryValueKind) — per-path type (almost always uniform).

In the new shape, these flags would apply to the WHOLE entry (all paths share them). That means:

- A setting that genuinely needs mixed `IsGroupPolicy` across paths must keep multiple `RegistrySetting` entries — the array shape is for genuinely-homogeneous mirrors.
- The audit (next step) confirms whether the ~30 collision settings are flag-homogeneous.

### Migration plan (Option B — all-at-once)

1. **Audit** all 32 collision settings against per-path flag uniformity. Output: a CSV/markdown table listing which settings are safe to collapse vs which must stay as multiple entries.
2. **Update the `RegistrySetting` record** — change `KeyPath` type, update consumers.
3. **Update consumer code**:
   - Registry read service (`IWindowsRegistryService` / `WindowsRegistryService`) — iterate `KeyPath` when reading.
   - Registry write service — iterate `KeyPath` when writing.
   - `ComboBoxResolver.cs` `currentValues` build loop (lines ~110-125) — fold N paths into one `currentValues[ValueName]` entry; use the chosen detection rule (ANY → first match wins, ALL → require all to match).
   - `SystemSettingsDiscoveryService.cs` — same fold.
   - `BulkSettingsActionService.cs` — apply path iteration.
   - Autounattend XML generator — emit registry commands per path.
   - `ConfigExportService.cs` / `ConfigImportService.cs` (if either touches `KeyPath` directly).
4. **Programmatically migrate** all SettingDefinitions: single-path becomes `["X"]`; intentional-mirror groups collapse into one entry with `[path1, path2, ...]`. Touch-keyboard's old TapiSrv collision is already split out of the codebase (landed 2026-05-18) so no special handling.
5. **Update tests** — `ComboBoxResolverTests`, registry-service tests, autounattend tests, config import/export tests.
6. **Verify on Windows** (Marco) — the usual cycle, since Winhance can't build on Linux.

### Out of scope for this refactor

- Changing detection semantics for non-array cases (a setting with one entry whose `ValueName` doesn't collide with anything is unaffected).
- Changing `ValueMappings` shape on ComboBox options.
- Touching the `SupportsCustomState` removal (already shipped 2026-05-18).
- Changing per-path flag handling — multi-path entries enforce flag uniformity; mixed-flag groups keep the multi-entry form.

## Files known to read `RegistrySetting.KeyPath`

Grep `KeyPath` in:
- `src/Winhance.Core/Features/Common/Models/` (the record definition)
- `src/Winhance.Infrastructure/Features/Common/Services/` (registry read/write services, ComboBoxResolver, SystemSettingsDiscoveryService, ComboBoxSetupService, BulkSettingsActionService)
- `src/Winhance.UI/Features/AdvancedTools/Services/AutounattendXml*.cs`
- `src/Winhance.UI/Features/Common/Services/Config*.cs`
- `tests/Winhance.Infrastructure.Tests/` and `tests/Winhance.UI.Tests/`

All ~32 SettingDefinitions with duplicate `ValueName`s (full list in the audit step) are in `src/Winhance.Core/Features/{Optimize,Customize,Privacy}/Models/*.cs`.

## Related work / context

- **Already shipped 2026-05-18**:
  - `SupportsCustomState` property removed from `ComboBoxMetadata` — resolver always returns `CustomStateIndex` on mismatch (`refactor: Selection settings always show Custom state on mismatch` — commit `f31acc33`).
  - `gaming-telephony-service` split out of `gaming-touch-keyboard-service`.
- **Memory feedback entries** that may apply to this work:
  - `feedback_winhance_no_dotnet_use_reviewer` — Winhance can't build on Linux; verify via Opus-pinned sub-agent reviewer.
  - `feedback_winhance_wait_for_test_before_push` — non-trivial Winhance behaviour changes: implement locally only, no commit/push until Marco builds on Windows and confirms.
- The `add-winhance-setting` skill (`~/.claude/skills/add-winhance-setting/SKILL.md`) will need to be updated after this refactor lands to reflect the new `KeyPath = [...]` shape in its examples.

## Open questions for the brainstorm session

1. **Detection rule confirmation** — ANY vs ALL. Test against the trickiest existing settings (`privacy-diagnostics` 4-way, `taskbar-meet-now` 3-way) to see if either rule changes user-visible behaviour vs today's last-write-wins.
2. **`IsPrimary` semantics** — does `IsPrimary` make sense on a multi-path entry? If only one path can be "primary", which one in the array? Or does the whole multi-path entry get one `IsPrimary` flag and the position is irrelevant?
3. **Config import/export back-compat** — do user-saved `.winhance` configs reference `KeyPath`? (Probably not — configs only store `Id` + `SelectedIndex` / `IsSelected` — but worth verifying.)
4. **Autounattend XML output** — does the order of paths in the array matter for the generated XML? If yes, define the order; if not, document that it doesn't.
5. **How many settings actually need to stay as multi-entry** post-audit? If the number is small (<5), the migration is mostly mechanical. If large (>15), reconsider whether the array shape is the right shape.

## Suggested first actions for the implementing agent

1. Run the collision audit (Python script that walks all `*.cs` model files and reports settings with duplicate `ValueName` + their per-path flag values). The script already exists conceptually in the conversation that produced this brief — recreate it.
2. Decide detection rule with Marco using audit output as evidence.
3. Sketch the schema change + a single end-to-end migration of one representative setting (e.g. `taskbar-meet-now`) to validate the design before mass-migration.
4. Then plan the full task-by-task implementation.
