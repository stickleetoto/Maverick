#!/usr/bin/env python3
"""
Analyze Eagle Physical AI integrated demonstration JSONL files.
No third-party packages required.

Usage:
  python Tools/analyze_integrated_demonstrations.py path/to/file_or_dir
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path
from typing import Dict, Iterable, List, Any


def iter_jsonl_files(path: Path) -> Iterable[Path]:
    if path.is_file():
        yield path
    elif path.is_dir():
        for p in sorted(path.rglob("*.jsonl")):
            yield p


def safe_float(x: Any, default: float = 0.0) -> float:
    try:
        if x is None:
            return default
        return float(x)
    except Exception:
        return default


def vector_magnitude(values: Any) -> float:
    if not isinstance(values, list) or not values:
        return 0.0
    total = 0.0
    count = 0
    for v in values:
        total += abs(safe_float(v))
        count += 1
    return total / max(1, count)


def analyze(files: List[Path]) -> Dict[str, Any]:
    total = 0
    sessions = set()
    tags: Dict[str, int] = {}
    flight_modes: Dict[str, int] = {}
    sensor_modes: Dict[str, int] = {}
    physical_mag_sum = 0.0
    sensor_mag_sum = 0.0
    reward_sum = 0.0
    strike_positive = 0
    sensor_interactions = 0
    missing_physical = 0
    missing_sensor = 0

    for file in files:
        with file.open("r", encoding="utf-8", errors="replace") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    row = json.loads(line)
                except json.JSONDecodeError:
                    continue
                total += 1
                sid = row.get("sessionId") or row.get("session_id") or "unknown"
                sessions.add(sid)
                tag = row.get("tag") or "untagged"
                tags[tag] = tags.get(tag, 0) + 1
                fm = row.get("flightMode") or "unknown"
                sm = row.get("sensorMode") or "unknown"
                flight_modes[fm] = flight_modes.get(fm, 0) + 1
                sensor_modes[sm] = sensor_modes.get(sm, 0) + 1

                pa = row.get("physicalAction")
                sa = row.get("sensorAction")
                if not isinstance(pa, list):
                    missing_physical += 1
                if not isinstance(sa, list):
                    missing_sensor += 1
                physical_mag_sum += vector_magnitude(pa)
                sensor_mag_sum += vector_magnitude(sa)
                reward_sum += safe_float(row.get("physicalReward"))
                if isinstance(pa, list) and len(pa) >= 5 and safe_float(pa[4]) > 0.5:
                    strike_positive += 1
                if vector_magnitude(sa) > 0.05:
                    sensor_interactions += 1

    avg_physical = physical_mag_sum / max(1, total)
    avg_sensor = sensor_mag_sum / max(1, total)
    quality = 0.0
    quality += min(1.0, avg_physical * 2.5) * 0.25
    quality += min(1.0, avg_sensor * 4.0) * 0.25
    quality += min(1.0, sensor_interactions / max(1, total) * 4.0) * 0.25
    quality += min(1.0, strike_positive / max(1, total) * 15.0) * 0.15
    quality += (1.0 - min(1.0, (missing_physical + missing_sensor) / max(1, total))) * 0.10

    return {
        "files": len(files),
        "samples": total,
        "sessions": len(sessions),
        "avg_physical_action_magnitude": round(avg_physical, 5),
        "avg_sensor_action_magnitude": round(avg_sensor, 5),
        "avg_physical_reward": round(reward_sum / max(1, total), 5),
        "strike_positive_ratio": round(strike_positive / max(1, total), 5),
        "sensor_interaction_ratio": round(sensor_interactions / max(1, total), 5),
        "missing_physical_rows": missing_physical,
        "missing_sensor_rows": missing_sensor,
        "estimated_quality_score": round(quality, 4),
        "tags": tags,
        "flight_modes": flight_modes,
        "sensor_modes": sensor_modes,
    }


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__.strip())
        return 2
    path = Path(sys.argv[1])
    files = list(iter_jsonl_files(path))
    if not files:
        print(json.dumps({"error": "no_jsonl_files_found", "path": str(path)}, indent=2))
        return 1
    print(json.dumps(analyze(files), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
