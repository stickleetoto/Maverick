#!/usr/bin/env python3
"""
Split Eagle integrated demonstration JSONL into train/valid/test JSONL files.
No third-party packages required.

Usage:
  python Tools/split_demonstrations.py input.jsonl output_dir --valid 0.1 --test 0.1
"""
from __future__ import annotations

import argparse
import random
from pathlib import Path


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("output_dir")
    ap.add_argument("--valid", type=float, default=0.1)
    ap.add_argument("--test", type=float, default=0.1)
    ap.add_argument("--seed", type=int, default=42)
    args = ap.parse_args()

    src = Path(args.input)
    out = Path(args.output_dir)
    out.mkdir(parents=True, exist_ok=True)
    rows = [line for line in src.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()]
    rng = random.Random(args.seed)
    rng.shuffle(rows)

    n = len(rows)
    n_test = int(n * max(0.0, min(0.5, args.test)))
    n_valid = int(n * max(0.0, min(0.5, args.valid)))
    test = rows[:n_test]
    valid = rows[n_test:n_test+n_valid]
    train = rows[n_test+n_valid:]

    (out / "train.jsonl").write_text("\n".join(train) + ("\n" if train else ""), encoding="utf-8")
    (out / "valid.jsonl").write_text("\n".join(valid) + ("\n" if valid else ""), encoding="utf-8")
    (out / "test.jsonl").write_text("\n".join(test) + ("\n" if test else ""), encoding="utf-8")
    print({"total": n, "train": len(train), "valid": len(valid), "test": len(test), "output_dir": str(out)})
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
