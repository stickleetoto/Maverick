#!/usr/bin/env python3
"""
Tiny dependency-free linear behavior cloning trainer for SensorAIAction.
It reads JSONL files produced by IntegratedAIDemonstrationRecorder and writes a Unity JsonUtility-compatible model.
"""
import argparse
import glob
import json
import math
import os
import random

OBS_COUNT = 28
ACTION_COUNT = 8

OBS_LABELS = [
    "radar_on", "radar_mode_norm", "radar_track_count_norm", "radar_has_selected",
    "radar_selected_range_norm", "radar_selected_bearing_norm", "radar_selected_quality",
    "radar_selected_id_confidence", "radar_has_lock", "radar_locked_range_norm",
    "radar_locked_quality", "pod_on", "pod_mode_norm", "pod_has_track", "pod_track_quality",
    "pod_id_confidence", "pod_has_los", "pod_fov_norm", "fusion_has_target",
    "fusion_confidence", "fusion_friendly_risk", "cas_request_active", "active_request_priority",
    "strike_cooldown_ready", "target_designated", "target_hostile", "selected_target_in_front",
    "sensor_ready_to_strike"
]
ACTION_LABELS = [
    "radar_mode_step", "radar_next_track", "radar_lock", "radar_unlock",
    "pod_mode_step", "pod_slave_to_radar", "pod_designate", "pod_clear_track"
]


def sigmoid(x):
    x = max(-40.0, min(40.0, x))
    return 1.0 / (1.0 + math.exp(-x))


def tanh(x):
    return math.tanh(max(-20.0, min(20.0, x)))


def iter_samples(patterns):
    for pattern in patterns:
        for path in glob.glob(pattern):
            with open(path, "r", encoding="utf-8") as f:
                for line_no, line in enumerate(f, 1):
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        row = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    obs = row.get("sensorObservation")
                    act = row.get("sensorAction")
                    if not isinstance(obs, list) or not isinstance(act, list):
                        continue
                    if len(obs) != OBS_COUNT or len(act) != ACTION_COUNT:
                        continue
                    # Keep non-empty sensor actions; no-op rows dominate otherwise.
                    if max(abs(float(x)) for x in act) < 0.01:
                        continue
                    yield [float(x) for x in obs], [float(x) for x in act]


def train(samples, epochs, lr, l2, seed):
    rng = random.Random(seed)
    weights = [(rng.random() * 2.0 - 1.0) * 0.015 for _ in range(OBS_COUNT * ACTION_COUNT)]
    bias = [0.0 for _ in range(ACTION_COUNT)]
    data = list(samples)
    if not data:
        raise SystemExit("No valid sensor samples found. Record integrated demonstrations first.")

    for epoch in range(epochs):
        rng.shuffle(data)
        total = 0.0
        for obs, target in data:
            for a in range(ACTION_COUNT):
                offset = a * OBS_COUNT
                z = bias[a]
                for i, x in enumerate(obs):
                    z += weights[offset + i] * x
                tanh_head = a in (0, 4)
                y = tanh(z) if tanh_head else sigmoid(z)
                err = y - target[a]
                total += err * err
                deriv = (1.0 - y * y) if tanh_head else y * (1.0 - y)
                grad_z = 2.0 * err * deriv
                for i, x in enumerate(obs):
                    wi = offset + i
                    weights[wi] -= lr * (grad_z * x + l2 * weights[wi])
                bias[a] -= lr * grad_z
        print(f"epoch={epoch+1}/{epochs} loss={total / max(1, len(data) * ACTION_COUNT):.6f} samples={len(data)}")
    return weights, bias, len(data)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("inputs", nargs="+", help="JSONL files or glob patterns")
    ap.add_argument("--out", default="sensor_linear_policy.json")
    ap.add_argument("--epochs", type=int, default=8)
    ap.add_argument("--lr", type=float, default=0.02)
    ap.add_argument("--l2", type=float, default=0.0001)
    ap.add_argument("--seed", type=int, default=71)
    args = ap.parse_args()

    weights, bias, n = train(iter_samples(args.inputs), args.epochs, args.lr, args.l2, args.seed)
    model = {
        "observationCount": OBS_COUNT,
        "actionCount": ACTION_COUNT,
        "weights": weights,
        "bias": bias,
        "observationLabels": OBS_LABELS,
        "actionLabels": ACTION_LABELS,
        "note": f"Sensor policy trained from {n} non-empty sensor action samples"
    }
    os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(model, f, indent=2)
    print("wrote", args.out)


if __name__ == "__main__":
    main()
