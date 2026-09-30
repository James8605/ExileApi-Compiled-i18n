"""Validate translations and the bundled OTF's character coverage; no dependencies."""
import json
import re
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        assert key not in result, f"Duplicate translation: {key}"
        assert key and isinstance(value, str) and value, f"Invalid translation: {key}"
        result[key] = value
    return result


catalog = json.loads((ROOT / "config/localization/zh-CN.json").read_text(encoding="utf-8"),
                     object_pairs_hook=unique_pairs)
for source, target in catalog.items():
    for pattern in (r"\{[0-9]+\}", r"\{(?:percent|current|total|currentes|currentlife)\}"):
        assert sorted(re.findall(pattern, source)) == sorted(re.findall(pattern, target)), source
    assert "\0" not in source + target, f"Unexpected NUL: {source}"
    assert "##" not in target, f"Translation must not change widget IDs: {source}"

font = (ROOT / "fonts/unifont.otf").read_bytes()
u16 = lambda offset: struct.unpack_from(">H", font, offset)[0]
u32 = lambda offset: struct.unpack_from(">I", font, offset)[0]
tables = {font[12 + i * 16:16 + i * 16]: u32(20 + i * 16) for i in range(u16(4))}
cmap = tables[b"cmap"]
covered = set()
for index in range(u16(cmap + 2)):
    entry = cmap + 4 + index * 8
    platform, encoding = u16(entry), u16(entry + 2)
    if platform != 0 and (platform != 3 or encoding not in (1, 10)):
        continue
    offset = cmap + u32(entry + 4)
    fmt = u16(offset)
    if fmt == 4:
        segments = u16(offset + 6) // 2
        end_base = offset + 14
        start_base = end_base + segments * 2 + 2
        delta_base = start_base + segments * 2
        range_base = delta_base + segments * 2
        for segment in range(segments):
            start, end = u16(start_base + segment * 2), u16(end_base + segment * 2)
            delta = u16(delta_base + segment * 2)
            range_offset = u16(range_base + segment * 2)
            for codepoint in range(start, end + 1):
                if range_offset:
                    glyph = u16(range_base + segment * 2 + range_offset + (codepoint - start) * 2)
                    if glyph:
                        glyph = (glyph + delta) & 0xFFFF
                else:
                    glyph = (codepoint + delta) & 0xFFFF
                if glyph:
                    covered.add(codepoint)
    elif fmt == 12:
        for group in range(u32(offset + 12)):
            start, end, glyph = struct.unpack_from(">III", font, offset + 16 + group * 12)
            covered.update(range(start + (glyph == 0), end + 1))
assert covered, "No Unicode character map found in the bundled font"
required = {ord(char) for value in catalog.values() for char in value if not char.isspace()}
missing = sorted(required - covered)
assert not missing, "Missing font glyphs: " + " ".join(f"U+{char:04X}" for char in missing)
print(f"Validated {len(catalog)} translations and {len(required)} font glyphs.")
