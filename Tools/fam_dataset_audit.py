#!/usr/bin/env python3
"""Audit MAVERICK FAM flight JSONL without third-party dependencies."""

from __future__ import annotations

import argparse
import json
import math
from collections import Counter, defaultdict
from pathlib import Path


def shannon_entropy(counts: Counter[str]) -> float:
    total = sum(counts.values())
    if total <= 0:
        return 0.0
    value = 0.0
    for count in counts.values():
        p = count / total
        if p > 0:
            value -= p * math.log2(p)
    return value


def audit(path: Path) -> dict:
    frames = []
    with path.open("r", encoding="utf-8") as fh:
        for line_no, line in enumerate(fh, 1):
            line = line.strip()
            if not line:
                continue
            try:
                frames.append(json.loads(line))
            except json.JSONDecodeError as exc:
                raise SystemExit(f"invalid JSON at line {line_no}: {exc}") from exc

    token_counts: Counter[str] = Counter()
    transitions: Counter[str] = Counter()
    leader_available = 0
    control_nonzero = Counter()
    dts = []
    previous_token = None
    previous_session = None

    for frame in frames:
        token = str(frame.get("behaviorToken", "UNKNOWN"))
        session = str(frame.get("sessionId", ""))
        token_counts[token] += 1

        if previous_token is not None and session == previous_session and token != previous_token:
            transitions[f"{previous_token}->{token}"] += 1
        previous_token = token
        previous_session = session

        leader = frame.get("leader") or {}
        if leader.get("available"):
            leader_available += 1

        control = frame.get("control") or {}
        for key in ("pitch", "roll", "yaw", "throttleDelta"):
            try:
                if abs(float(control.get(key, 0.0))) > 1e-4:
                    control_nonzero[key] += 1
            except (TypeError, ValueError):
                pass

        try:
            dt = float(frame.get("dt", 0.0))
            if dt > 0:
                dts.append(dt)
        except (TypeError, ValueError):
            pass

    duration = 0.0
    if len(frames) >= 2:
        try:
            duration = float(frames[-1].get("time", 0.0)) - float(frames[0].get("time", 0.0))
        except (TypeError, ValueError):
            duration = 0.0

    mean_dt = sum(dts) / len(dts) if dts else 0.0
    result = {
        "file": str(path),
        "sample_count": len(frames),
        "duration_seconds": duration,
        "mean_dt": mean_dt,
        "approx_hz": (1.0 / mean_dt) if mean_dt > 0 else 0.0,
        "token_counts": dict(token_counts),
        "token_entropy_bits": shannon_entropy(token_counts),
        "distinct_tokens": len(token_counts),
        "within_session_transition_count": sum(transitions.values()),
        "transitions": dict(transitions),
        "leader_available_fraction": (leader_available / len(frames)) if frames else 0.0,
        "nonzero_control_samples": dict(control_nonzero),
        "quality": {
            "has_multiple_tokens": len(token_counts) >= 2,
            "has_transitions": sum(transitions.values()) > 0,
            "near_4hz": 0.20 <= mean_dt <= 0.30 if mean_dt > 0 else False,
            "enough_samples_for_30s": len(frames) >= 120,
        },
    }
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("jsonl", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()

    result = audit(args.jsonl)
    text = json.dumps(result, ensure_ascii=False, indent=2)
    print(text)
    if args.out:
        args.out.write_text(text + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
