#!/usr/bin/env python3
"""
Pure-Python behavior-cloning trainer for Eagle Physical AI v0.2.
No numpy required.

Input:  JSONL files from PhysicalAIDemonstrationRecorder.
Output: linear_policy.json loadable by BehaviorCloningLinearPolicy.modelFilePath.

Example:
  python Tools/train_linear_policy.py --input demo1.jsonl demo2.jsonl --output linear_policy.json --epochs 5
"""
from __future__ import annotations

import argparse
import json
import math
import random
from pathlib import Path
from typing import Iterable, List, Tuple

OBS_COUNT = 32
ACT_COUNT = 6
OBS_LABELS = [
    "speed_norm", "forward_speed_norm", "altitude_norm", "vertical_speed_norm",
    "throttle", "pitch_input", "roll_input", "yaw_input", "stall_risk",
    "is_stalling", "is_crashed", "pitch_angle_norm", "roll_angle_norm",
    "cas_request_active", "target_distance_norm", "target_bearing_x", "target_elevation_y",
    "target_in_front_dot", "strike_allowed", "friendly_risk", "geometry_score",
    "strike_cooldown_ready", "successful_strikes_norm", "aborted_strikes_norm",
    "friendly_fire_norm", "target_alive", "altitude_above_target_norm", "target_priority_norm",
    "low_altitude_danger", "validation_denied", "target_left_right_sign", "target_up_down_sign",
]
ACT_LABELS = ["pitch", "roll", "yaw", "throttle", "strike", "abort"]


def sigmoid(x: float) -> float:
    x = max(-40.0, min(40.0, x))
    return 1.0 / (1.0 + math.exp(-x))


def clamp(v: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, v))


def normalize_len(values: List[float], n: int) -> List[float]:
    out = [0.0] * n
    for i, v in enumerate(values[:n]):
        out[i] = clamp(float(v), -1.0, 1.0)
    return out


def normalize_action(values: List[float]) -> List[float]:
    out = [0.0] * ACT_COUNT
    for i, v in enumerate(values[:ACT_COUNT]):
        if i <= 2:
            out[i] = clamp(float(v), -1.0, 1.0)
        else:
            out[i] = clamp(float(v), 0.0, 1.0)
    return out


def iter_samples(paths: Iterable[Path]) -> Iterable[Tuple[List[float], List[float]]]:
    for path in paths:
        with path.open("r", encoding="utf-8") as f:
            for line_no, line in enumerate(f, 1):
                line = line.strip()
                if not line:
                    continue
                try:
                    row = json.loads(line)
                    obs = normalize_len(row.get("observation") or [], OBS_COUNT)
                    act = normalize_action(row.get("action") or [])
                    yield obs, act
                except Exception as exc:
                    print(f"[WARN] skip {path}:{line_no}: {exc}")


class LinearPolicy:
    def __init__(self, seed: int = 42):
        rng = random.Random(seed)
        self.weights = [(rng.random() * 2.0 - 1.0) * 0.015 for _ in range(OBS_COUNT * ACT_COUNT)]
        self.bias = [0.0 for _ in range(ACT_COUNT)]
        self.bias[3] = 0.2

    def predict(self, obs: List[float]) -> List[float]:
        out = []
        for a in range(ACT_COUNT):
            offset = a * OBS_COUNT
            z = self.bias[a]
            for i in range(OBS_COUNT):
                z += self.weights[offset + i] * obs[i]
            out.append(math.tanh(z) if a <= 2 else sigmoid(z))
        return out

    def train_one(self, obs: List[float], target: List[float], lr: float, l2: float) -> float:
        loss = 0.0
        for a in range(ACT_COUNT):
            offset = a * OBS_COUNT
            z = self.bias[a]
            for i in range(OBS_COUNT):
                z += self.weights[offset + i] * obs[i]
            if a <= 2:
                y = math.tanh(z)
                deriv = 1.0 - y * y
            else:
                y = sigmoid(z)
                deriv = y * (1.0 - y)
            err = y - target[a]
            loss += err * err
            grad_z = 2.0 * err * deriv
            for i in range(OBS_COUNT):
                wi = offset + i
                grad = grad_z * obs[i] + l2 * self.weights[wi]
                self.weights[wi] -= lr * grad
            self.bias[a] -= lr * grad_z
        return loss / ACT_COUNT

    def to_json(self) -> dict:
        return {
            "observationCount": OBS_COUNT,
            "actionCount": ACT_COUNT,
            "weights": self.weights,
            "bias": self.bias,
            "observationLabels": OBS_LABELS,
            "actionLabels": ACT_LABELS,
            "note": "Trained by Tools/train_linear_policy.py for Eagle Physical AI v0.2",
        }


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", nargs="+", required=True, help="JSONL demo files")
    ap.add_argument("--output", required=True, help="Output linear_policy.json")
    ap.add_argument("--epochs", type=int, default=5)
    ap.add_argument("--lr", type=float, default=0.015)
    ap.add_argument("--l2", type=float, default=0.0001)
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--shuffle", action="store_true")
    args = ap.parse_args()

    paths = [Path(p) for p in args.input]
    samples = list(iter_samples(paths))
    if not samples:
        raise SystemExit("No samples found. Record demos first with PhysicalAIDemonstrationRecorder.")

    rng = random.Random(args.seed)
    policy = LinearPolicy(args.seed)

    print(f"Loaded {len(samples)} samples")
    for epoch in range(max(1, args.epochs)):
        if args.shuffle:
            rng.shuffle(samples)
        total = 0.0
        for obs, act in samples:
            total += policy.train_one(obs, act, args.lr, args.l2)
        print(f"epoch {epoch+1}/{args.epochs} loss={total/len(samples):.6f}")

    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(policy.to_json(), ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Saved {out}")


if __name__ == "__main__":
    main()
