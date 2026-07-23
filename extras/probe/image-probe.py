#!/usr/bin/env python3
"""Offline image probe: read every catalog registry target from an extracted WIM image's hives.

Counterpart to Probe-WinhanceDefaults.ps1, but for the *shipped image* instead of a live
first-logon system. Reads Users/Default/NTUSER.DAT (HKCU for new profiles), SOFTWARE and SYSTEM
from one extracted edition index, and emits JSON in the SAME shape as the live probe so the
downstream tooling can treat an image as just another machine. The machine block carries
"source": "image" plus the WIM identity, and hive SHA-256 hashes instead of a timestamp
(re-runs over unchanged hives are byte-identical no-op diffs).

Semantics mirrored from the C# (do not "improve"):
  RegTargetReader.Read       - ValueName null = key existence; "" = the (Default) value;
                               mirror-path fold is HKLM-first, first non-null wins.
  RegTargetReader.OrderHklmFirst - stable sort, HKLM paths first.
Values are recorded RAW (no bitmask/byte/composite reduction) exactly like the live probe;
reductions are replayed downstream against the manifest.

Offline-only mappings (documented divergences from a live system):
  HKLM\\SYSTEM\\CurrentControlSet -> ControlSet00N per the SYSTEM hive's Select\\Current value.
  HKEY_CLASSES_ROOT -> merged view: NTUSER Software\\Classes first (user half wins where it has
    the key/value), else SOFTWARE\\Classes. The winning hive is recorded in the path note.
  REG_EXPAND_SZ is NOT expanded (no environment offline); recorded raw with a note.
  DWORD/QWORD decode as SIGNED ints to match PowerShell/.NET GetValue (0xFFFFFFFF -> -1).
  Task / PowerCfg targets: status "NotProbed" (not readable from registry hives reliably).

Usage:
    image-probe.py <manifest.json> <hive-dir> <output.json> \
        [--iso NAME] [--index N] [--wim-name NAME]
<hive-dir> must contain NTUSER.DAT, SOFTWARE, SYSTEM.
"""

import hashlib
import json
import os
import sys

import hivex

REG_TYPE_NAMES = {
    0: "None", 1: "String", 2: "ExpandString", 3: "Binary", 4: "DWord",
    5: "DWordBigEndian", 6: "Link", 7: "MultiString", 8: "ResourceList",
    9: "FullResourceDescriptor", 10: "ResourceRequirementsList", 11: "QWord",
}


class Hive:
    """Case-insensitive path/value access over one hivex hive."""

    def __init__(self, path):
        self.h = hivex.Hivex(path)
        self._child_cache = {}

    def node_at(self, path):
        """Node id for a backslash path relative to the hive root, or None."""
        node = self.h.root()
        for part in path.split("\\"):
            if not part:
                continue
            node = self._child(node, part)
            if node is None:
                return None
        return node

    def _child(self, node, name):
        key = (node, name.casefold())
        if key in self._child_cache:
            return self._child_cache[key]
        found = None
        for c in self.h.node_children(node):
            if self.h.node_name(c).casefold() == name.casefold():
                found = c
                break
        self._child_cache[key] = found
        return found

    def value(self, node, value_name):
        """(exists, type_id, raw_bytes) for a value name ("" = the (Default) value)."""
        for v in self.h.node_values(node):
            if self.h.value_key(v).casefold() == value_name.casefold():
                t, data = self.h.value_value(v)
                return (True, t, data)
        return (False, None, None)


def decode_value(type_id, data):
    """Registry bytes -> the JSON shape the live probe uses (signed ints, {"$bytes": hex})."""
    if type_id == 4:  # DWORD little-endian
        return int.from_bytes(data[:4].ljust(4, b"\0"), "little", signed=True)
    if type_id == 5:  # DWORD big-endian
        return int.from_bytes(data[:4].ljust(4, b"\0"), "big", signed=True)
    if type_id == 11:  # QWORD
        return int.from_bytes(data[:8].ljust(8, b"\0"), "little", signed=True)
    if type_id in (1, 2, 6):  # String / ExpandString / Link: UTF-16LE, strip one trailing NUL
        s = data.decode("utf-16-le", errors="replace")
        return s[:-1] if s.endswith("\0") else s
    if type_id == 7:  # MultiString: NUL-separated, double-NUL terminated; empty -> []
        s = data.decode("utf-16-le", errors="replace")
        return [p for p in s.split("\0") if p != ""]
    # Binary and everything else: raw bytes
    return {"$bytes": data.hex().upper(), "length": len(data)}


def order_hklm_first(paths):
    return sorted(paths, key=lambda p: not p.upper().startswith("HKEY_LOCAL_MACHINE"))


class ImageRegistry:
    """Resolves full catalog paths (HKCU\\... / HKLM\\... / HKCR\\...) over the three hives."""

    def __init__(self, hive_dir):
        self.ntuser = Hive(os.path.join(hive_dir, "NTUSER.DAT"))
        self.software = Hive(os.path.join(hive_dir, "SOFTWARE"))
        self.system = Hive(os.path.join(hive_dir, "SYSTEM"))
        self.control_set = self._current_control_set()

    def _current_control_set(self):
        node = self.system.node_at("Select")
        if node is not None:
            exists, t, data = self.system.value(node, "Current")
            if exists and t == 4:
                return "ControlSet%03d" % int.from_bytes(data[:4], "little")
        return "ControlSet001"

    def resolve(self, full_path):
        """[(hive, relative_path, source_label)] candidates for a catalog path, in merge order."""
        parts = full_path.split("\\")
        root = parts[0].upper()
        rest = "\\".join(parts[1:])
        if root == "HKEY_CURRENT_USER":
            return [(self.ntuser, rest, "NTUSER.DAT")]
        if root == "HKEY_CLASSES_ROOT":
            # Live HKCR merges HKCU\Software\Classes over HKLM\SOFTWARE\Classes.
            return [(self.ntuser, "Software\\Classes\\" + rest, "NTUSER.DAT(Classes)"),
                    (self.software, "Classes\\" + rest, "SOFTWARE(Classes)")]
        if root == "HKEY_LOCAL_MACHINE":
            sub = parts[1].upper() if len(parts) > 1 else ""
            tail = "\\".join(parts[2:])
            if sub == "SOFTWARE":
                return [(self.software, tail, "SOFTWARE")]
            if sub == "SYSTEM":
                tparts = tail.split("\\")
                if tparts and tparts[0].upper() == "CURRENTCONTROLSET":
                    tail = "\\".join([self.control_set] + tparts[1:])
                return [(self.system, tail, "SYSTEM")]
        return []

    def read_path(self, full_path, value_name, key_existence_only):
        """One per-path record in the live probe's shape."""
        rec = {"path": full_path, "status": None, "value": None, "rawValue": None,
               "valueKind": None, "subKeys": None, "error": None}
        candidates = self.resolve(full_path)
        if not candidates:
            rec["status"] = "Error"
            rec["error"] = "unmapped hive prefix (offline)"
            return rec

        key_found_source = None
        for hive, rel, source in candidates:
            node = hive.node_at(rel)
            if node is None:
                continue
            if key_existence_only:
                rec["status"] = "KeyPresent"
                rec["error"] = None if source in ("NTUSER.DAT", "SOFTWARE", "SYSTEM") \
                    else "source=" + source
                return rec
            key_found_source = key_found_source or source
            exists, t, data = hive.value(node, value_name)
            if exists:
                rec["status"] = "Present"
                rec["value"] = decode_value(t, data)
                rec["valueKind"] = REG_TYPE_NAMES.get(t, str(t))
                if source not in ("NTUSER.DAT", "SOFTWARE", "SYSTEM"):
                    rec["error"] = "source=" + source
                if t == 2:
                    rec["error"] = ((rec["error"] + "; ") if rec["error"] else "") + \
                        "ExpandString recorded unexpanded (offline)"
                return rec
        if key_existence_only:
            rec["status"] = "KeyMissing"
        elif key_found_source is not None:
            rec["status"] = "ValueAbsent"
            if key_found_source not in ("NTUSER.DAT", "SOFTWARE", "SYSTEM"):
                rec["error"] = "source=" + key_found_source
        else:
            rec["status"] = "KeyMissing"
        return rec


def probe_target(reg, target):
    """One target record in the live probe's shape (fold mirrors RegTargetReader)."""
    trec = {"key": target["key"], "joinKey": target["joinKey"], "kind": target["kind"],
            "status": None, "effectiveValue": None, "effectivePath": None,
            "valueKind": None, "note": None, "paths": []}
    if target["kind"] != "Registry":
        trec["status"] = "NotProbed"
        trec["note"] = "not readable from image hives"
        return trec

    value_name = target["valueName"]
    keo = target["keyExistenceOnly"]
    for p in order_hklm_first(target["paths"]):
        trec["paths"].append(reg.read_path(p, value_name if value_name is not None else "", keo))

    # Fold: first path whose reading is non-null / key-exists wins (paths already HKLM-first).
    if keo:
        hit = next((p for p in trec["paths"] if p["status"] == "KeyPresent"), None)
        trec["status"] = "KeyPresent" if hit else "KeyMissing"
        trec["effectivePath"] = hit["path"] if hit else None
        return trec
    hit = next((p for p in trec["paths"] if p["status"] == "Present"), None)
    if hit:
        trec["status"] = "Present"
        trec["effectiveValue"] = hit["value"]
        trec["effectivePath"] = hit["path"]
        trec["valueKind"] = hit["valueKind"]
    elif any(p["status"] == "ValueAbsent" for p in trec["paths"]):
        trec["status"] = "ValueAbsent"
    elif any(p["status"] == "Error" for p in trec["paths"]):
        trec["status"] = "Error"
        trec["note"] = "; ".join(p["error"] for p in trec["paths"] if p["error"])
    else:
        trec["status"] = "KeyMissing"
    return trec


def machine_block(reg, hive_dir, iso, index, wim_name):
    cv = reg.software.node_at("Microsoft\\Windows NT\\CurrentVersion")

    def sval(name):
        if cv is None:
            return None
        exists, t, data = reg.software.value(cv, name)
        return decode_value(t, data) if exists else None

    build = sval("CurrentBuildNumber")
    hashes = {}
    for f in ("NTUSER.DAT", "SOFTWARE", "SYSTEM"):
        with open(os.path.join(hive_dir, f), "rb") as fh:
            hashes[f] = hashlib.sha256(fh.read()).hexdigest()
    return {
        "source": "image",
        "iso": iso,
        "wimIndex": index,
        "wimName": wim_name,
        "buildNumber": int(build) if build else 0,
        "ubr": sval("UBR") or 0,
        "editionId": sval("EditionID"),
        "displayVersion": sval("DisplayVersion"),
        "releaseId": sval("ReleaseId"),
        "productName": sval("ProductName"),
        "installationType": sval("InstallationType"),
        "controlSet": reg.control_set,
        "hiveSha256": hashes,
    }


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    opts = {}
    it = iter(argv[1:])
    for a in it:
        if a in ("--iso", "--index", "--wim-name"):
            opts[a] = next(it)
    if len(args) < 3:
        print(__doc__)
        return 1
    manifest = json.load(open(args[0]))
    hive_dir, out_path = args[1], args[2]

    reg = ImageRegistry(hive_dir)
    machine = machine_block(reg, hive_dir, opts.get("--iso"),
                            int(opts["--index"]) if "--index" in opts else None,
                            opts.get("--wim-name"))

    settings = []
    counts = {}
    for s in manifest["settings"]:
        targets = [probe_target(reg, t) for t in s["targets"]]
        for t in targets:
            counts[t["status"]] = counts.get(t["status"], 0) + 1
        settings.append({"id": s["id"], "targets": targets})

    out = {
        "schemaVersion": 1,
        "catalogHash": manifest.get("catalogHash"),
        "machine": machine,
        "counts": counts,
        "settings": settings,
        "scheduledTasks": [],
    }
    with open(out_path, "w") as f:
        json.dump(out, f, indent=1)
    print(f"{machine['productName']} {machine['buildNumber']}.{machine['ubr']} "
          f"({machine['editionId']}, {machine['displayVersion']}): " +
          ", ".join(f"{k}={v}" for k, v in sorted(counts.items())))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
