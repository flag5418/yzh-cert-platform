#!/usr/bin/env python3
"""只读核对：第 4-5 章 7 条条款在「父子回退」修复后的召回效果。

对比两种对号口径（旧=精确匹配 / 新=父子回退），用真实提取产物证明：
  4.4 → 4.4.1 命中；5.1 → 5.1.1 + 5.1.2 命中。
输入：cert_document_extraction.ContentJson（doc_clause_extract 产物）导出的 JSON 文件。
用法：python3 probe_ch45_recall_after_fix.py [/tmp/manual_extract.json]
"""
import json
import sys

TARGET = ["4.1", "4.2", "4.3", "4.4", "5.1", "5.2.1", "5.3"]


def resolve_exact(number: str, wanted: str) -> str | None:
    return number if number == wanted else None


def resolve_descendant(number: str, wanted: str) -> str | None:
    if number == wanted:
        return number
    if number.startswith(wanted + "."):
        return wanted
    return None


def main(path: str) -> None:
    data = json.load(open(path, encoding="utf-8"))
    clauses = data.get("Clauses", [])
    by_number: dict[str, list] = {}
    for c in clauses:
        n = c.get("ClauseNumber", "")
        by_number.setdefault(n, []).append(c)

    print(f"提取产物条款总数：{len(clauses)}")
    print("=" * 76)
    for target in TARGET:
        exact = sorted({n for n in by_number if resolve_exact(n, target)})
        desc = sorted({n for n in by_number if resolve_descendant(n, target)})
        excerpt_total = sum(
            len(e.get("Excerpts", []))
            for n in desc
            for e in by_number[n]
        )
        old = ",".join(exact) if exact else "(空)"
        new = ",".join(desc) if desc else "(空)"
        print(f"{target:6} | 旧(精确): {old:18} | 新(父子): {new:20} | 新口径摘录数: {excerpt_total}")
    print("=" * 76)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "/tmp/manual_extract.json")
