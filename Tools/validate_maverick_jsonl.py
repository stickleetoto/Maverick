#!/usr/bin/env python3
"""Validate MAVERICK JSONL demonstration files for basic schema and action diversity.
This tool is intentionally dependency-free.
"""
import argparse
import json
import math
from pathlib import Path


def iter_files(path: Path):
    if path.is_file():
        yield path
    else:
        for p in sorted(path.rglob('*.jsonl')):
            yield p


def mag(values):
    total = 0.0
    for v in values:
        try:
            total += float(v) * float(v)
        except Exception:
            pass
    return math.sqrt(total)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('path', help='JSONL file or folder')
    ap.add_argument('--min-lines', type=int, default=50)
    args = ap.parse_args()
    root = Path(args.path)
    files = list(iter_files(root))
    if not files:
        print('NO_JSONL_FILES')
        return 2

    total = 0
    invalid = 0
    physical_action_mags = []
    sensor_action_mags = []
    keys = {}
    for f in files:
        with f.open('r', encoding='utf-8', errors='replace') as fh:
            for i, line in enumerate(fh, 1):
                line = line.strip()
                if not line:
                    continue
                total += 1
                try:
                    obj = json.loads(line)
                except Exception:
                    invalid += 1
                    continue
                for k in obj.keys():
                    keys[k] = keys.get(k, 0) + 1
                pa = obj.get('physical_action') or obj.get('player_action') or obj.get('action')
                if isinstance(pa, dict):
                    physical_action_mags.append(mag(pa.values()))
                elif isinstance(pa, list):
                    physical_action_mags.append(mag(pa))
                sa = obj.get('sensor_action')
                if isinstance(sa, dict):
                    sensor_action_mags.append(mag(sa.values()))
                elif isinstance(sa, list):
                    sensor_action_mags.append(mag(sa))

    def avg(xs):
        return sum(xs) / len(xs) if xs else 0.0

    print(f'files={len(files)}')
    print(f'lines={total}')
    print(f'invalid_json={invalid}')
    print(f'avg_physical_action_magnitude={avg(physical_action_mags):.4f}')
    print(f'avg_sensor_action_magnitude={avg(sensor_action_mags):.4f}')
    print('top_keys=' + ','.join(k for k, _ in sorted(keys.items(), key=lambda kv: -kv[1])[:20]))

    if total < args.min_lines:
        print('QUALITY=TOO_SMALL')
        return 1
    if invalid > 0:
        print('QUALITY=HAS_INVALID_JSON')
        return 1
    if avg(physical_action_mags) < 0.02:
        print('QUALITY=LOW_PHYSICAL_ACTION_DIVERSITY')
        return 1
    print('QUALITY=OK')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
