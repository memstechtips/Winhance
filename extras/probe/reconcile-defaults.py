#!/usr/bin/env python3
"""Reconcile fresh-install probe output against the catalog's WindowsDefault roles.

Step 2 of the Windows-defaults audit. Reads the committed catalog manifest plus one or more probe
JSON files and, for every analysable setting, replays Winhance's real detection resolution against
the machine's readings, then compares the resolved state to the build-appropriate WindowsDefault
role. Emits the three conclusions the audit needs:

  (a) DETECTION bug - the value is absent and the WindowsDefault state does not accept absence, so it
      resolves to something other than the default. Fix is .OrAbsent() on that state (the ROLE is
      right). Split by what it currently resolves to:
        - fallback-different : silently mislabels as a DIFFERENT state  (the real, user-visible bug)
        - shows-custom       : falls through to Custom (no fallback)    (honest but wrong-ish)
  (b) ROLE bug candidate - a PRESENT value matched a state that is NOT the WindowsDefault one. Either
      the role is on the wrong state, or the machine is not a clean default here (express-OOBE, a
      tweak). Needs human review, never an auto-fix.
  (c) correct - resolves to the WindowsDefault state (directly, or via a fallback that IS the default,
      in which case .OrAbsent() would be cosmetic).

Faithful to the C# it mirrors (do not "improve" these - divergence here silently corrupts the audit):
  CatalogDiscovery.DetectState  - precedence-vs-whole-pattern branch, build-live target filtering
  CatalogDiscovery.DetectByPrecedence
  StateDetectionEngine.Detect
  StateValue.Matches            - absent / anyPresent / accepted-values
  CatalogValueComparer.AreEqual - byte[] seq-eq -> Equals -> Convert.ToInt64 -> ToString/OrdinalIC
  RegTargetReader               - bitmask/byteOnly/composite reductions the probe left raw

Read-only. Writes nothing; prints a report (or --json for the machine-readable form).

Usage:
    reconcile-defaults.py <manifest.json> <probe1.json> [probe2.json ...] [--json]
"""

import json
import sys


# ----- CatalogValueComparer.AreEqual -------------------------------------------------------------

def _to_int64(x):
    if isinstance(x, bool):
        return 1 if x else 0
    if isinstance(x, int):
        return x
    if isinstance(x, str):
        return int(x)          # ValueError on non-numeric -> caller falls to string compare
    raise ValueError("not int-convertible")


def are_equal(a, b):
    if a is None and b is None:
        return True
    if a is None or b is None:
        return False
    a_bytes = isinstance(a, tuple) and a and a[0] == "$bytes"
    b_bytes = isinstance(b, tuple) and b and b[0] == "$bytes"
    if a_bytes and b_bytes:
        return a[1].upper() == b[1].upper()
    if a_bytes or b_bytes:
        return False
    try:
        if a == b:            # covers str==str, int==int, and Python's True==1
            return True
    except Exception:
        pass
    try:
        return _to_int64(a) == _to_int64(b)   # "1" == 1, True == 1, byte == int
    except Exception:
        return str(a).lower() == str(b).lower()


def _conv_value(v):
    """A manifest/probe JSON value -> the Python shape are_equal expects. byte[] -> ('$bytes', hex)."""
    if isinstance(v, dict) and "$bytes" in v:
        return ("$bytes", v["$bytes"])
    return v


# ----- StateValue.Matches ------------------------------------------------------------------------

def sv_matches(sv, current, present, force_absent=False):
    # force_absent simulates .OrAbsent() on this state: C# only flips AcceptsAbsent, so it must
    # ONLY relax the absent branch - a present reading still has to match the accepted values.
    if not present:
        return sv["acceptsAbsent"] or force_absent
    if sv["acceptsAnyPresent"]:
        return True
    return any(are_equal(current, _conv_value(v)) for v in sv["values"])


# ----- reading extraction (mirrors RegTargetReader over the probe's per-target record) ------------

_ABSENT_STATUSES = {"ValueAbsent", "KeyMissing", "Error"}


def reading_for_registry(trec, mtarget):
    """(value, present) for a registry target, applying the reductions the probe left raw.
    Returns (None, None) sentinel present=None when NotProbed (caller must skip the setting)."""
    st = trec["status"]
    if st == "NotProbed":
        return (None, None)
    if st in _ABSENT_STATUSES:
        return (None, False)
    if st == "KeyPresent":            # ValueName-less target: state IS key existence
        return (None, True)
    if st != "Present":
        return (None, False)

    raw = _conv_value(trec["effectiveValue"])

    bit, bidx, bonly, comp = (mtarget["bitMask"], mtarget["byteIndex"],
                              mtarget["byteOnly"], mtarget["compositeStringKey"])

    if bit is not None and bidx is not None:
        if isinstance(raw, tuple) and raw[0] == "$bytes":
            blob = bytes.fromhex(raw[1])
            return (((blob[bidx] & bit) == bit), True) if len(blob) > bidx else (None, False)
        return (None, False)

    if bonly and bidx is not None:
        if isinstance(raw, tuple) and raw[0] == "$bytes":
            blob = bytes.fromhex(raw[1])
            return (blob[bidx], True) if len(blob) > bidx else (None, False)
        return (None, False)

    if comp:
        if isinstance(raw, str):
            for entry in raw.split(";"):
                if not entry:
                    continue
                eq = entry.find("=")
                if eq > 0 and entry[:eq].lower() == comp.lower():
                    return (entry[eq + 1:], True)
            return (None, False)
        return (None, False)

    return (raw, True)


# ----- detection replay --------------------------------------------------------------------------

def build_readings(setting, probe, tasks_by_key):
    """joinKey -> (value, present) for targets LIVE on this build. Returns (readings, flags).
    flags: has_unprobed_powercfg, active_keys, reg_read_targets (live, non-applyOnly RegTargets),
    all_registry."""
    build = probe["_build"]
    revision = probe["_ubr"]
    trec_by_key = {t["joinKey"]: t for t in probe["_settings_by_id"][setting["id"]]["targets"]}

    readings = {}
    active_keys = set()
    reg_read_targets = []
    all_registry = True
    has_unprobed_powercfg = False

    for mt in setting["targets"]:
        if not build_in_ranges(mt["appliesTo"], build, revision):
            continue  # target not live on this build -> not added at all (mirrors DetectState)
        jk = mt["joinKey"]
        active_keys.add(jk)
        kind = mt["kind"]
        if kind == "Registry":
            val, pres = reading_for_registry(trec_by_key[jk], mt)
            if pres is None:                     # NotProbed registry target (shouldn't happen) -> skip setting
                has_unprobed_powercfg = True
            else:
                readings[jk] = (val, pres)
                if not mt["applyOnly"]:
                    reg_read_targets.append(mt)
        elif kind == "Task":
            enabled = tasks_by_key.get((setting["id"], jk))
            readings[jk] = (enabled, enabled is not None)
            all_registry = False
        else:  # PowerCfg - not probed in v1
            has_unprobed_powercfg = True
            all_registry = False

    return readings, {
        "has_unprobed_powercfg": has_unprobed_powercfg,
        "active_keys": active_keys,
        "reg_read_targets": reg_read_targets,
        "all_registry": all_registry,
    }


def detect(setting, readings, flags, wd_relax_labels=None):
    """Returns (label_or_None, how) with how in {'matched','fallback','custom'}.
    wd_relax_labels: if given, those states' Set entries are treated as accepting absence
    (the .OrAbsent() what-if)."""
    relax = wd_relax_labels or set()
    reg_read = flags["reg_read_targets"]
    non_gp = [t for t in reg_read if not t["isGroupPolicy"]]
    precedence_shaped = len(non_gp) == 1

    if flags["all_registry"] and len(reg_read) > 0 and precedence_shaped:
        return _detect_precedence(setting, readings, reg_read, relax)
    return _detect_engine(setting, readings, flags["active_keys"], relax)


def _present(readings, key):
    return readings.get(key, (None, False))[1]


def _detect_precedence(setting, readings, reg_read, relax):
    deciding = (next((t for t in reg_read if t["isGroupPolicy"] and _present(readings, t["joinKey"])), None)
                or next((t for t in reg_read if _present(readings, t["joinKey"])), None)
                or reg_read[0])
    dkey = deciding["joinKey"]
    fallback = None
    for state in setting["states"]:
        if state["isFallback"]:
            fallback = state
        if dkey in state["set"]:
            cur, pres = readings.get(dkey, (None, False))
            force = state["label"] in relax
            if sv_matches(state["set"][dkey], cur, pres, force_absent=force):
                return (state["label"], "matched")
    if fallback is not None:
        return (fallback["label"], "fallback")
    return (None, "custom")


def _detect_engine(setting, readings, active_keys, relax):
    fallback = None
    for state in setting["states"]:
        if state["isFallback"]:
            fallback = state
        any_checked = False
        all_match = True
        for tkey, sv in state["set"].items():
            if tkey not in active_keys:
                continue
            any_checked = True
            cur, pres = readings.get(tkey, (None, False))
            force = state["label"] in relax
            if not sv_matches(sv, cur, pres, force_absent=force):
                all_match = False
                break
        if not any_checked:
            continue
        if all_match:
            return (state["label"], "matched")
    if fallback is not None:
        return (fallback["label"], "fallback")
    return (None, "custom")


# ----- build ranges ------------------------------------------------------------------------------

def build_in_ranges(ranges, build, revision):
    if not ranges:
        return True
    for r in ranges:
        ge = build > r["minBuild"] or (build == r["minBuild"] and revision >= r["minRevision"])
        le = build < r["maxBuild"] or (build == r["maxBuild"] and revision <= r["maxRevision"])
        if ge and le:
            return True
    return False


def wd_labels_for_build(setting, build, revision):
    """Labels of states carrying a WindowsDefault role live on this build (build-aware)."""
    out = []
    for st in setting["states"]:
        for role in st["roles"]:
            if role["kind"] != "WindowsDefault":
                continue
            if build_in_ranges(role["appliesTo"], build, revision):
                out.append(st["label"])
                break
    return out


# ----- analysability -----------------------------------------------------------------------------

def skip_reason(setting):
    if setting["detector"]:
        return "custom-detector"
    if setting["optionSource"]:
        return "dynamic-options"
    if setting["numeric"]:
        return "numeric-slider"
    if setting["control"] == "Action":
        return "action"
    if not setting["states"]:
        return "no-states"
    return None


# ----- main --------------------------------------------------------------------------------------

def analyse(manifest, probes):
    settings = manifest["settings"]
    results = []      # per (setting, machine)
    skipped = {}

    for probe in probes:
        probe["_build"] = probe["machine"]["buildNumber"]
        probe["_ubr"] = probe["machine"]["ubr"]
        probe["_settings_by_id"] = {s["id"]: s for s in probe["settings"]}
        probe["_tasks"] = {(t["settingId"], t["key"]): t.get("enabled")
                           for t in probe.get("scheduledTasks", [])}

    for setting in settings:
        reason = skip_reason(setting)
        if reason:
            skipped[reason] = skipped.get(reason, 0) + 1
            continue

        for probe in probes:
            build, rev = probe["_build"], probe["_ubr"]
            wd = set(wd_labels_for_build(setting, build, rev))
            if not wd:
                skipped["no-windowsdefault-role"] = skipped.get("no-windowsdefault-role", 0) + 1
                continue

            readings, flags = build_readings(setting, probe, probe["_tasks"])
            if flags["has_unprobed_powercfg"]:
                skipped["awaiting-powercfg"] = skipped.get("awaiting-powercfg", 0) + 1
                continue

            label, how = detect(setting, readings, flags)
            relaxed_label, _ = detect(setting, readings, flags, wd_relax_labels=wd)

            fb = next((s["label"] for s in setting["states"] if s["isFallback"]), None)

            if label in wd:
                conclusion = "c-correct"
            elif how == "matched":
                conclusion = "b-role-review"           # present value matched a non-WD state
            elif relaxed_label in wd:
                # absence-driven: relaxing the WD state to accept absent would resolve to it
                conclusion = "a-fallback-different" if how == "fallback" else "a-shows-custom"
            else:
                conclusion = "other-custom"            # present-unmatched or not OrAbsent-fixable

            results.append({
                "id": setting["id"],
                "control": setting["control"],
                "feature": setting["feature"],
                "machine": f"{build}.{rev}",
                "edition": probe["machine"].get("editionId"),
                "wd": sorted(wd),
                "detected": label,
                "how": how,
                "fallback": fb,
                "relaxed": relaxed_label,
                "conclusion": conclusion,
            })

    return results, skipped


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    as_json = "--json" in argv
    if len(args) < 2:
        print(__doc__)
        return 1
    manifest = json.load(open(args[0]))
    probes = [json.load(open(p)) for p in args[1:]]

    results, skipped = analyse(manifest, probes)

    # group per setting across machines
    by_id = {}
    for r in results:
        by_id.setdefault(r["id"], []).append(r)

    def conclusion_of(rows):
        cs = {r["conclusion"] for r in rows}
        # A present value matching a NON-default state on ANY machine contradicts the role, so it
        # VETOES an absence-driven (a) verdict from another machine - .OrAbsent() on a wrong role
        # makes detection worse. Mixed evidence is its own bucket, never silently ranked.
        if any(c.startswith("a-") for c in cs) and "b-role-review" in cs:
            return "mixed-evidence"
        order = ["a-fallback-different", "b-role-review", "a-shows-custom", "other-custom", "c-correct"]
        for o in order:
            if o in cs:
                return o
        return "c-correct"

    buckets = {}
    for sid, rows in by_id.items():
        buckets.setdefault(conclusion_of(rows), []).append((sid, rows))

    if as_json:
        print(json.dumps({"results": results, "skipped": skipped}, indent=1))
        return 0

    labels = {
        "mixed-evidence":       "MIXED EVIDENCE - absence on one machine, role-contradicting value on another (review, no auto-fix)",
        "a-fallback-different": "(a) DETECTION BUG - silently mislabels as a different state (.OrAbsent fix)",
        "a-shows-custom":       "(a) detection gap - falls through to Custom (.OrAbsent fix)",
        "b-role-review":        "(b) ROLE REVIEW - a present value matched a NON-default state",
        "other-custom":         "genuine Custom / not OrAbsent-fixable",
        "c-correct":            "(c) correct",
    }
    print(f"machines: {', '.join(p['machine']['buildNumber'].__str__()+'.'+str(p['machine']['ubr'])+' '+str(p['machine'].get('editionId')) for p in probes)}")
    print(f"analysable settings: {len(by_id)}")
    print("skipped: " + ", ".join(f"{k}={v}" for k, v in sorted(skipped.items())))
    print()
    for key in ["mixed-evidence", "a-fallback-different", "a-shows-custom", "b-role-review", "other-custom", "c-correct"]:
        rows = buckets.get(key, [])
        print(f"== {labels[key]}: {len(rows)}")
        if key == "c-correct":
            continue
        for sid, rr in sorted(rows):
            perm = "  ".join(f"[{r['machine']} {r['edition'][:4] if r['edition'] else '?'}: det={r['detected']}/{r['how']} wd={'/'.join(r['wd'])}]" for r in rr)
            priv = " PRIVACY" if (rr[0]["feature"].lower().startswith("privacy") or sid.startswith("privacy")) else ""
            print(f"   {sid}{priv}")
            print(f"      {perm}")
        print()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
