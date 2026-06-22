#!/usr/bin/env python3
"""Generate typed icon accessors (MaterialIcons / FluentIcons) for the new catalog model.

Bootstraps the accessor set from the glyphs the OLD catalog already uses: for each setting it pairs
`Icon = "<glyph>"` with its sibling `IconPack = "<pack>"` (default Material), then emits one
`public static readonly Icon <Member> = new(IconPack.<Pack>, "<glyph>");` per distinct glyph. Compile-checked
glyph names replace eyeballing. Re-run when a new glyph is introduced. Pure text - no build needed.
"""
import glob
import os
import re

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODEL_DIRS = [
    os.path.join(REPO, "src/Winhance.Core/Features/Customize/Models"),
    os.path.join(REPO, "src/Winhance.Core/Features/Optimize/Models"),
]
OUT_DIR = os.path.join(REPO, "src/Winhance.Core/Features/Common/Catalog")

ICON_RE = re.compile(r'Icon\s*=\s*"([^"]+)"')
PACK_RE = re.compile(r'IconPack\s*=\s*"([^"]+)"')


def collect():
    """Returns {pack: set(glyph)}."""
    packs = {"Material": set(), "Fluent": set()}
    for d in MODEL_DIRS:
        for path in sorted(glob.glob(os.path.join(d, "*.cs"))):
            with open(path, encoding="utf-8") as f:
                text = f.read()
            # One chunk per setting; each setting carries at most one Icon + one IconPack.
            for chunk in text.split("new SettingDefinition")[1:]:
                m = ICON_RE.search(chunk)
                if not m:
                    continue
                glyph = m.group(1)
                pm = PACK_RE.search(chunk)
                pack = pm.group(1) if pm else "Material"
                if pack not in packs:
                    pack = "Material"
                packs[pack].add(glyph)
    return packs


def member_name(glyph, used):
    base = re.sub(r"[^A-Za-z0-9_]", "_", glyph)
    if not base or base[0].isdigit():
        base = "_" + base
    name, n = base, 1
    while name in used and used[name] != glyph:
        n += 1
        name = f"{base}_{n}"
    used[name] = glyph
    return name


def emit(pack, glyphs):
    lines = [
        "namespace Winhance.Core.Features.Common.Catalog;",
        "",
        f"/// <summary>Generated typed accessors for the {pack} icon glyphs used in the catalog. Regenerate via",
        "/// extras/gen-catalog-icons.py when a new glyph is introduced; the glyph name is compile-checked.</summary>",
        f"public static class {pack}Icons",
        "{",
    ]
    used = {}
    for glyph in sorted(glyphs):
        name = member_name(glyph, used)
        lines.append(f'    public static readonly Icon {name} = new(IconPack.{pack}, "{glyph}");')
    lines.append("}")
    lines.append("")
    return "\n".join(lines)


def main():
    packs = collect()
    for pack, glyphs in packs.items():
        out = os.path.join(OUT_DIR, f"{pack}Icons.cs")
        with open(out, "w", encoding="ascii", newline="\n") as f:
            f.write(emit(pack, glyphs))
        print(f"{pack}Icons.cs: {len(glyphs)} glyphs")


if __name__ == "__main__":
    main()
