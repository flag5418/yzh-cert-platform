#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""从三份 ACM 二阶段结构化报告抽取「第 4–5 章 → 条款 → 要求 / 审核证据」基线。

用途：作为唯一裁判，后续把系统生成的第 4/5 章证据与 ACM 原文逐条对比。
只读，不写库。输出 JSON。

用法：
    python3 scripts/db/verify/extract_acm_ch45_baseline.py
    python3 scripts/db/verify/extract_acm_ch45_baseline.py --out /tmp/a.json
"""

from __future__ import annotations

import argparse
import json
import re
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

# 企业 → (结构化报告相对路径, 标准口径)
REPORTS = [
    ("科斯亚", "QMS", "docs/90-归档/案例/科斯亚质量体系文件/科斯亚-ACM二阶段-结构化.md"),
    ("双尼", "QES", "docs/90-归档/案例/深圳市双尼环境科技有限公司QES体系资料/双尼-ACM二阶段-结构化.md"),
    ("尚龙", "13485", "docs/90-归档/案例/CS河北雄安尚龙医疗科技有限公司13485体系材料/尚龙-ACM二阶段-结构化.md"),
]

CLAUSE_RE = re.compile(r"^####\s*条款\s+([0-9]+(?:\.[0-9]+)*)")

# 页眉/页脚/噪声行（跨页续行时会被误收，这里跳过）
NOISE_RE = [
    re.compile(r"^<!--"),
    re.compile(r"Clause\s+NC/OFI", re.IGNORECASE),
    re.compile(r"Requirements\s+Audit\s+Evidence", re.IGNORECASE),
    re.compile(r"GM/MR\s+Process\s+Area", re.IGNORECASE),
    re.compile(r"Responsible\s+Person", re.IGNORECASE),
    re.compile(r"^Auditor\s+审核员", re.IGNORECASE),
    re.compile(r"^款\s*$"),
    re.compile(r"^A/0\s*$"),
    re.compile(r"^ACM/QR-"),
]


def is_noise(line: str) -> bool:
    s = line.strip()
    if not s:
        return True
    return any(r.search(s) for r in NOISE_RE)


def is_ch45(clause: str) -> bool:
    return clause == "4" or clause.startswith("4.") or clause == "5" or clause.startswith("5.")


def parse_file(path: Path) -> list[dict]:
    clauses: list[dict] = []
    cur: dict | None = None
    last_col: str | None = None

    for raw in path.read_text(encoding="utf-8").splitlines():
        m = CLAUSE_RE.match(raw)
        if m:
            num = m.group(1)
            if is_ch45(num):
                cur = {"clause": num, "requirement": [], "evidence": [], "notes": []}
                clauses.append(cur)
            else:
                cur = None
            last_col = None
            continue

        if cur is None:
            continue

        # 标记行：显式指定列
        if raw.startswith("- 要求："):
            cur["requirement"].append(raw[len("- 要求："):])
            last_col = "requirement"
            continue
        if raw.startswith("- 证据："):
            cur["evidence"].append(raw[len("- 证据："):])
            last_col = "evidence"
            continue
        if raw.startswith("  - 要求续："):
            cur["requirement"].append(raw[len("  - 要求续："):])
            last_col = "requirement"
            continue
        if raw.startswith("  - 证据续："):
            cur["evidence"].append(raw[len("  - 证据续："):])
            last_col = "evidence"
            continue
        if raw.startswith("- 条款列附加："):
            cur["notes"].append(raw[len("- 条款列附加："):])
            continue

        # 跨页续行：无标记，跟着上一列走（页眉页脚先被 is_noise 滤掉）
        if is_noise(raw):
            continue
        if last_col and raw.strip():
            cur[last_col].append(raw)

    out: list[dict] = []
    for c in clauses:
        req = "".join(c["requirement"]).replace("\u3000", " ").strip()
        ev = "".join(c["evidence"]).replace("\u3000", " ").strip()
        req = re.sub(r"\s+", " ", req)
        ev = re.sub(r"\s+", " ", ev)
        out.append({
            "clause": c["clause"],
            "requirement": req,
            "evidence": ev,
            "notes": c["notes"],
        })
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=str(ROOT / "docs/90-归档/案例/acm_ch45_baseline.json"))
    args = ap.parse_args()

    reports = []
    for enterprise, standard, rel in REPORTS:
        p = ROOT / rel
        if not p.exists():
            print(f"[skip] 不存在：{p}")
            continue
        clauses = parse_file(p)
        reports.append({
            "enterprise": enterprise,
            "standard": standard,
            "source": rel,
            "clause_count": len(clauses),
            "clauses": clauses,
        })
        print(f"[ok] {enterprise}（{standard}）：第 4–5 章 {len(clauses)} 条条款")

    payload = {
        "generated_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "generator": Path(__file__).name,
        "scope": "第 4–5 章（4.x / 5.x）条款的 ACM 审核证据基线",
        "reports": reports,
    }

    out_path = Path(args.out)
    out_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"[done] 写入 {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
