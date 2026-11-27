#!/usr/bin/env python3
"""Regenerate src/Winhance.Core/Features/Common/Catalog/FluentIcons.cs from the accessors the source uses.

Write `FluentIcons.<Name>` where the icon is needed, then run this: it emits one accessor per distinct name
found under src/Winhance.Core and drops the ones nothing uses. The name must be a member of the FluentIcons.WinUI `Icon`
enum; CatalogIconNameTests fails the gate otherwise. Pure text - no build needed.
"""
import glob
import os
import re

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "src/Winhance.Core/Features/Common/Catalog/FluentIcons.cs")
USE_RE = re.compile(r"\bFluentIcons\.([A-Z][A-Za-z0-9]*)\b")

HEADER = [
    "namespace Winhance.Core.Features.Common.Catalog;",
    "",
    "// Generated - regenerate via extras/gen-catalog-icons.py when a new glyph is introduced. The accessor is",
    "// compile-checked; the glyph name is a bare string (this assembly does not reference the icon package), so",
    "// CatalogIconNameTests pins them.",
    "public static class FluentIcons",
    "{",
]


def collect():
    names = set()
    for path in glob.glob(os.path.join(REPO, "src", "Winhance.Core", "**", "*.cs"), recursive=True):
        if os.path.abspath(path) == OUT:
            continue
        with open(path, encoding="utf-8-sig") as f:
            names.update(USE_RE.findall(f.read()))
    return sorted(names)


def main():
    lines = HEADER + [f'    public static readonly Icon {n} = new(IconPack.Fluent, "{n}");' for n in collect()] + ["}", ""]
    with open(OUT, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    main()
