#!/usr/bin/env python3
"""Create a simple index.json for MAVERICK session folders."""
import argparse
import json
from pathlib import Path
from datetime import datetime, timezone


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('root')
    ap.add_argument('--out', default='index.json')
    args = ap.parse_args()
    root = Path(args.root)
    records = []
    for p in sorted(root.rglob('*.jsonl')):
        line_count = 0
        first = None
        last = None
        with p.open('r', encoding='utf-8', errors='replace') as f:
            for line in f:
                line_count += 1
                if first is None:
                    first = line.strip()[:300]
                last = line.strip()[:300]
        records.append({
            'path': str(p.relative_to(root)),
            'bytes': p.stat().st_size,
            'lines': line_count,
            'first_preview': first or '',
            'last_preview': last or ''
        })
    out = {
        'schema': 'maverick.training_index.v0.7',
        'created_utc': datetime.now(timezone.utc).isoformat(),
        'root': str(root),
        'file_count': len(records),
        'records': records,
    }
    out_path = root / args.out
    out_path.write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding='utf-8')
    print(out_path)


if __name__ == '__main__':
    main()
