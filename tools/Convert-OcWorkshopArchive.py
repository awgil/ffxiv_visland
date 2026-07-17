#!/usr/bin/env python3
"""Convert an Overseas Casuals #Archive DiscordChatExporter HTML dump to workshop season JSON."""

from __future__ import annotations

import argparse
import html
import json
import re
import sys
from pathlib import Path

MESSAGE_RE = re.compile(
    r'data-message-id=(\d+)[\s\S]*?<span class=chatlog__markdown-preserve>([\s\S]*?)</span>',
)
SEASON_HEADER_RE = re.compile(r'^Season\s+(\d+)\s*\(([^)]+)\)', re.M)
CYCLE_RE = re.compile(r'^C(\d+)\s*:\s*(.+)$')
WS4_RE = re.compile(r'^WS4:\s*(.+)$')
IMG_RE = re.compile(r'<img[^>]*>', re.I)
TAG_RE = re.compile(r'<[^>]+>')


def official_name_to_bot_name(name: str) -> str:
    if name.startswith('Isleworks '):
        return name[10:]
    if name.startswith('Islefish '):
        return name[9:]
    if name.startswith('Island '):
        return name[7:]
    if name == 'Mammet of the Cycle Award':
        return 'Mammet Award'
    return name


def load_craft_map(path: Path) -> dict[str, int]:
    raw = json.loads(path.read_text(encoding='utf-8'))
    return {k: int(v) for k, v in raw.items()}


def resolve_craft_name(name: str, craft_map: dict[str, int]) -> int:
    for candidate in (name, official_name_to_bot_name(name)):
        if candidate in craft_map:
            return craft_map[candidate]
    matches = [key for key in craft_map if key and key.lower() in name.lower()]
    if not matches:
        raise KeyError(name)
    best = max(matches, key=len)
    if best != name:
        print(f"Resolved '{name}' -> '{best}'", file=sys.stderr)
    return craft_map[best]


def resolve_crafts(names: list[str], craft_map: dict[str, int]) -> list[int]:
    return [resolve_craft_name(name, craft_map) for name in names]


def clean_html(fragment: str) -> str:
    text = IMG_RE.sub(' ', fragment)
    text = re.sub(r'<br\s*/?>', '\n', text, flags=re.I)
    text = re.sub(r'</(?:li|ul|p)>', '\n', text, flags=re.I)
    text = re.sub(r'<(?:li|ul|p)[^>]*>', '\n', text, flags=re.I)
    text = TAG_RE.sub('', text)
    text = html.unescape(text)
    lines = []
    for raw in text.splitlines():
        line = re.sub(r'\s+', ' ', raw).strip()
        if line:
            lines.append(line)
    return '\n'.join(lines)


def split_crafts(items: str) -> list[str]:
    # Archive sometimes used "2x Foo - Bar; 1x Baz - Qux" for alternate workshops.
    # Keep the first workshop variant only.
    primary = items.split(';', 1)[0].strip()
    crafts: list[str] = []
    for part in re.split(r'\s*-\s*', primary):
        part = part.strip()
        if not part:
            continue
        part = re.sub(r'^\d+x\s+', '', part, flags=re.I).strip()
        if part:
            crafts.append(part)
    return crafts


def strip_cowrie_prefix(value: str) -> str | None:
    value = value.strip()
    if re.fullmatch(r'(?i)rest', value):
        return None
    # "5613 : crafts" or "7355 (8730 20 Groove): crafts"
    m = re.match(r'^\d+\s*(?:\([^)]*\))?\s*:\s*(.+)$', value)
    if m:
        return m.group(1).strip()
    m = re.match(r'^\d+\s+(.+)$', value)
    if m:
        return m.group(1).strip()
    return value


def parse_season_body(body: str) -> dict | None:
    header = SEASON_HEADER_RE.search(body)
    if not header:
        return None

    season = int(header.group(1))
    date = header.group(2).strip()
    cycles: dict[str, dict] = {}
    current: str | None = None

    for line in body.splitlines():
        cm = CYCLE_RE.match(line)
        if cm:
            current = cm.group(1)
            payload = cm.group(2).strip()
            if re.fullmatch(r'(?i)rest', payload):
                cycles[current] = {'rest': True}
                continue
            crafts = strip_cowrie_prefix(payload)
            if crafts is None:
                cycles[current] = {'rest': True}
            else:
                cycles[current] = {'main': split_crafts(crafts)}
            continue

        wm = WS4_RE.match(line)
        if wm and current is not None and current in cycles and not cycles[current].get('rest'):
            cycles[current]['ws4'] = split_crafts(wm.group(1))

    if not cycles:
        return None

    return {
        'season': season,
        'date': date,
        'cycles': dict(sorted(cycles.items(), key=lambda kv: int(kv[0]))),
    }


def extract_seasons(html_text: str) -> dict[int, dict]:
    seasons: dict[int, dict] = {}
    for match in MESSAGE_RE.finditer(html_text):
        body = clean_html(match.group(2))
        if not body.startswith('Season '):
            continue
        # Skip prose that happens to mention a season.
        if 'Recommendations' in body and 'C2:' not in body and not re.search(r'^C\d+:', body, re.M):
            continue
        parsed = parse_season_body(body)
        if not parsed:
            continue
        season = parsed['season']
        # Prefer later edits/posts if duplicates exist.
        seasons[season] = parsed
    return seasons


def resolve_cycle_crafts(cycles: dict, craft_map: dict[str, int]) -> dict:
    resolved: dict = {}
    for cycle, day in cycles.items():
        if day.get('rest'):
            resolved[cycle] = {'rest': True}
            continue
        entry: dict = {}
        if 'main' in day:
            entry['main'] = resolve_crafts(day['main'], craft_map)
        if 'ws4' in day:
            entry['ws4'] = resolve_crafts(day['ws4'], craft_map)
        resolved[cycle] = entry
    return resolved


def build_payload(seasons: dict[int, dict], start: int, end: int, craft_map: dict[str, int]) -> dict:
    selected = {}
    missing = []
    for season in range(start, end + 1):
        if season not in seasons:
            missing.append(season)
            continue
        entry = seasons[season]
        selected[str(season)] = {
            'date': entry['date'],
            'cycles': resolve_cycle_crafts(entry['cycles'], craft_map),
        }

    payload = {
        'source': 'Overseas Casuals #Archive',
        'range': [start, end],
        'cycleLength': end - start + 1,
        # OC Season 203 ran Jul 7-13 2026; used to map game weeks → season numbers.
        'anchorSeason': 203,
        'anchorStart': '2026-07-07',
        'seasons': selected,
    }
    if missing:
        payload['missing'] = missing
    return payload


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('html', type=Path, help='DiscordChatExporter HTML archive')
    parser.add_argument('-o', '--output', type=Path, required=True, help='Output JSON path')
    default_craft_map = Path(__file__).resolve().parent.parent / 'ffxiv_visland' / 'Workshop' / 'Data' / 'mji-craft-map.json'
    parser.add_argument('--craft-map', type=Path, default=default_craft_map,
                        help='Bot craft name -> MJICraftworksObject row id map')
    parser.add_argument('--start', type=int, default=104)
    parser.add_argument('--end', type=int, default=203)
    args = parser.parse_args()

    craft_map = load_craft_map(args.craft_map)
    html_text = args.html.read_text(encoding='utf-8')
    seasons = extract_seasons(html_text)
    payload = build_payload(seasons, args.start, args.end, craft_map)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')

    found = len(payload['seasons'])
    expected = args.end - args.start + 1
    print(f'Wrote {args.output}')
    print(f'Seasons in archive: {len(seasons)} (min={min(seasons)} max={max(seasons)})')
    print(f'Selected {args.start}-{args.end}: {found}/{expected}')
    if payload.get('missing'):
        print(f'Missing: {payload["missing"]}')
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
