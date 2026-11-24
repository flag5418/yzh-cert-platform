#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""按「章」探测召回与证据可用量（只读）。

用途：一个章节一个章节地吃透 —— 先看这一章现有属性够不够、文档能不能被召回、
拿到的证据行有多少，再决定要不要新增属性或文档字段。

不做任何写操作。输出可直接贴进设计文档当"实测"。

用法：
    python3 scripts/db/verify/probe_chapter_recall.py --chapter 4
    python3 scripts/db/verify/probe_chapter_recall.py --chapter 5 --standard iso9001
    python3 scripts/db/verify/probe_chapter_recall.py --chapter 4 --json > ch4.json

依赖：本机 docker + 运行中的 yzh-mysql（与其它 scripts/db 脚本一致）。

2026-11-23 修订（v2）：
  1. 修 StandardCode 双口径：cert_iso_clause / cert_document_extraction 用 GUID，
     cert_clause_attr 用短码（iso9001）。
  2. R1 同时读 PrimaryClauses 与旧格式 Clauses[].ClauseNumber —— 手册（59 条款）
     只在旧格式里，不读就整本手册召不回。
  3. R3 加体裁过滤（封面/目录/汇编封面），并单列被排除的份数，不静默丢弃。
  4. 证据行数改用 Records[].RowCount 真实行数（不是 Record 个数）。
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from collections import Counter
from typing import Any

CONTAINER = os.environ.get("YZH_MYSQL_CONTAINER", "yzh-mysql")
PASSWORD = os.environ.get("YZH_MYSQL_PASSWORD", "CHANGE_ME")
DATABASE = os.environ.get("YZH_MYSQL_DB", "yzh_cert_platform")

# ── 过程码映射（候选，待定稿）：文档侧 ProcessArea 自由文本 → 条款侧 Process 码 ──
PROCESS_MAP = {
    "GM/MR": "management",
    "行政人事": "hr",
    "生产过程": "production",
    "检验过程": "inspection",
    "销售过程": "sales",
    "仓储/设备": "warehouse",
    "采购过程": "purchase",
    "未标注": None,
}

# ── 体裁过滤：这些不是"证据文档"，是壳 ──
GENRE_EXCLUDE = ("封面", "目录")


def sql(query: str) -> list[list[str]]:
    cmd = [
        "docker", "exec", "-i", CONTAINER,
        "mysql", "-uroot", f"-p{PASSWORD}",
        "--default-character-set=utf8mb4", "-N", "-B", DATABASE,
        "-e", query,
    ]
    out = subprocess.run(cmd, capture_output=True, text=True)
    if out.returncode != 0:
        raise RuntimeError(out.stderr.strip()[:800])
    return [line.split("\t") for line in out.stdout.splitlines() if line.strip() != ""]


def q(s: str) -> str:
    return s.replace("\\", "\\\\").replace("'", "''")


def load_standard(short: str) -> tuple[str, str]:
    rows = sql(f"SELECT Code FROM cert_iso_standard WHERE StandardCode='{q(short)}' AND IsDeleted=0 LIMIT 1;")
    if not rows:
        raise SystemExit(f"标准不存在：{short}")
    return rows[0][0], short


def load_clauses(std_guid: str, std_short: str, chapter: int) -> list[dict[str, Any]]:
    rows = sql(f"""
        SELECT c.ClauseNumber, COALESCE(c.Title,''), c.Code,
               (SELECT COUNT(*) FROM cert_iso_clause k
                 WHERE k.ParentCode=c.Code AND k.IsDeleted=0) AS kids
        FROM cert_iso_clause c
        WHERE c.StandardCode='{q(std_guid)}' AND c.IsDeleted=0
          AND c.ClauseNumber REGEXP '^{chapter}(\\\\.[0-9]+)*$'
        ORDER BY c.Sort, c.ClauseNumber;
    """)
    leaves = []
    for num, title, code, kids in rows:
        if kids != "0":
            continue
        leaves.append({"clause": num, "title": title, "code": code, "attrs": {}})

    if not leaves:
        return []

    nums = ",".join(f"'{q(x['clause'])}'" for x in leaves)
    # ★ 属性表用短码，不是 GUID
    arows = sql(f"""
        SELECT ClauseNumber, AttrCode, COALESCE(AttrValue,'')
        FROM cert_clause_attr
        WHERE IsDeleted=0 AND StandardCode='{q(std_short)}' AND ClauseNumber IN ({nums})
        ORDER BY ClauseNumber, Sort;
    """)
    by_num = {x["clause"]: x for x in leaves}
    for num, attr, val in arows:
        if num in by_num:
            by_num[num]["attrs"][attr] = val
    return leaves


def load_plan_depts(clauses: list[str]) -> dict[str, set[str]]:
    if not clauses:
        return {}
    nums = ",".join(f"'{q(c)}'" for c in clauses)
    rows = sql(f"""
        SELECT ClauseNumbers, COALESCE(DeptNames,'')
        FROM cert_audit_plan_detail
        WHERE IsDeleted=0 AND ClauseNumbers IN ({nums});
    """)
    out: dict[str, set[str]] = {}
    for num, depts in rows:
        out.setdefault(num, set()).update(x.strip() for x in depts.split(",") if x.strip())
    return out


def load_docs(std_guid: str) -> list[dict[str, Any]]:
    rows = sql(f"""
        SELECT e.Code,
          ANY_VALUE(COALESCE(JSON_UNQUOTE(JSON_EXTRACT(e.ContentJson,'$.DocMeta.FileName')),'')),
          ANY_VALUE(COALESCE(JSON_UNQUOTE(JSON_EXTRACT(e.ContentJson,'$.ModeOutput.DocType')),'')),
          ANY_VALUE(COALESCE(JSON_UNQUOTE(JSON_EXTRACT(e.ContentJson,'$.ModeOutput.OwnerDept')),'')),
          ANY_VALUE(COALESCE(JSON_UNQUOTE(JSON_EXTRACT(e.ContentJson,'$.ModeOutput.ProcessArea')),'')),
          ANY_VALUE(COALESCE(JSON_UNQUOTE(JSON_EXTRACT(e.ContentJson,'$.ModeOutput.PdcaPhase')),'')),
          ANY_VALUE(COALESCE(JSON_UNQUOTE(JSON_EXTRACT(e.ContentJson,'$.ModeOutput.PrimaryClauses')),'[]')),
          ANY_VALUE(COALESCE(JSON_LENGTH(JSON_EXTRACT(e.ContentJson,'$.ModeOutput.Records')),0)),
          ANY_VALUE(COALESCE(JSON_LENGTH(JSON_EXTRACT(e.ContentJson,'$.ModeOutput.Entries')),0)),
          ANY_VALUE(COALESCE(JSON_LENGTH(JSON_EXTRACT(e.ContentJson,'$.Clauses')),0)),
          ANY_VALUE(COALESCE(JSON_LENGTH(JSON_EXTRACT(e.ContentJson,'$.ModeOutput.DocSections')),0)),
          COALESCE(SUM(j.rc),0)
        FROM cert_document_extraction e
        LEFT JOIN JSON_TABLE(e.ContentJson,'$.ModeOutput.Records[*]' COLUMNS (rc INT PATH '$.RowCount')) j ON TRUE
        WHERE e.IsDeleted=0 AND e.StandardCode='{q(std_guid)}'
        GROUP BY e.Code;
    """)
    docs = []
    for r in rows:
        code, fn, dt, dept, proc, pdca, pc, nrec, nent, ncl, nsec, nrows = r
        try:
            primary = json.loads(pc) if pc else []
        except Exception:
            primary = []
        docs.append({
            "code": code, "file": fn, "doctype": dt or "(旧格式)",
            "dept": dept, "process_area": proc, "pdca": pdca,
            "primary": [str(x) for x in primary] if isinstance(primary, list) else [],
            "records": int(nrec), "entries": int(nent),
            "clauses": int(ncl), "sections": int(nsec),
            "rows": int(nrows or 0),
        })
    return docs


def load_legacy_clause_map(std_guid: str) -> dict[str, list[str]]:
    """旧格式 Clauses[].ClauseNumber —— 手册等 12 份只在这里有条款号。"""
    rows = sql(f"""
        SELECT COALESCE(JSON_UNQUOTE(JSON_EXTRACT(e.ContentJson,'$.DocMeta.FileName')),''),
               COALESCE(j.cn,'')
        FROM cert_document_extraction e
        JOIN JSON_TABLE(e.ContentJson,'$.Clauses[*]' COLUMNS (cn VARCHAR(64) PATH '$.ClauseNumber')) j ON TRUE
        WHERE e.IsDeleted=0 AND e.StandardCode='{q(std_guid)}';
    """)
    out: dict[str, list[str]] = {}
    for fn, cn in rows:
        if fn and cn:
            out.setdefault(fn, []).append(cn)
    return out


def routes_for(attrs: dict[str, str]) -> list[str]:
    family = attrs.get("EvidenceFamily", "")
    form = attrs.get("EvidenceForm", "")
    role = attrs.get("Role", "")
    out: list[str] = []
    if family in ("f1", "f1f2"):
        out.append("R1")
    if family in ("f2", "f1f2"):
        out.append("R2")
    out.append("R3")
    if role == "r5":
        out.append("R4")
    if form == "site":
        out.append("R5")
    return out


def shape_of(doc: dict[str, Any]) -> str:
    if doc["records"]:
        return f"Records[{doc['records']}].Rows[{doc['rows']}]"
    if doc["entries"]:
        return f"Entries[{doc['entries']}]"
    if doc["clauses"]:
        return f"Clauses[{doc['clauses']}].Excerpts"
    if doc["sections"]:
        return f"DocSections[{doc['sections']}]"
    return "仅元数据"


def evidence_rows(doc: dict[str, Any]) -> int:
    """可用证据行数：Records 用真实行数，Entries/Clauses 按条目数。"""
    return doc["rows"] + doc["entries"] + doc["clauses"] + doc["sections"]


def is_shell(doc: dict[str, Any]) -> bool:
    return any(k in doc["file"] for k in GENRE_EXCLUDE)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--standard", default="iso9001")
    ap.add_argument("--chapter", type=int, default=4)
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args()

    std_guid, std_short = load_standard(args.standard)
    clauses = load_clauses(std_guid, std_short, args.chapter)
    if not clauses:
        raise SystemExit(f"第 {args.chapter} 章没有叶子条款")

    plan = load_plan_depts([c["clause"] for c in clauses])
    docs = load_docs(std_guid)
    legacy = load_legacy_clause_map(std_guid)

    for d in docs:
        d["clause_numbers"] = set(d["primary"]) | set(legacy.get(d["file"], []))

    result = []
    for c in clauses:
        attrs = c["attrs"]
        proc_codes = set()
        try:
            proc_codes = set(json.loads(attrs.get("Process", "[]")))
        except Exception:
            pass
        depts = plan.get(c["clause"], set())

        r1 = [d for d in docs if c["clause"] in d["clause_numbers"]]
        r2 = [d for d in docs if d["process_area"] and PROCESS_MAP.get(d["process_area"]) in proc_codes]
        r3_all = [d for d in docs if d["dept"] and d["dept"] in depts]
        r3 = [d for d in r3_all if not is_shell(d)]
        r3_dropped = [d["file"] for d in r3_all if is_shell(d)]

        union = {d["code"]: d for d in (r1 + r2 + r3)}
        result.append({
            "clause": c["clause"],
            "title": c["title"],
            "attrs": attrs,
            "routes": routes_for(attrs),
            "plan_depts": sorted(depts),
            "R1": [d["file"] for d in r1],
            "R2": [d["file"] for d in r2],
            "R3": [d["file"] for d in r3],
            "R3_dropped": r3_dropped,
            "union_count": len(union),
            "evidence_rows": sum(evidence_rows(d) for d in union.values()),
            "shapes": sorted({shape_of(d) for d in union.values()}),
            "empty_evidence_docs": [d["file"] for d in union.values() if evidence_rows(d) == 0],
        })

    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0

    print(f"标准 {args.standard} · 第 {args.chapter} 章 · 叶子条款 {len(clauses)} 条\n")
    for r in result:
        a = r["attrs"]
        print(f"── {r['clause']} {r['title']}")
        print(f"   属性  Process={a.get('Process','-')} Family={a.get('EvidenceFamily','-')} "
              f"Form={a.get('EvidenceForm','-')} Role={a.get('Role','-')} Obligation={a.get('Obligation','-')}")
        print(f"   路线  {'+'.join(r['routes'])}   计划部门={','.join(r['plan_depts']) or '(未分配)'}")
        print(f"   R1 条款号命中 {len(r['R1'])} 份 {r['R1'][:4]}")
        print(f"   R2 过程命中   {len(r['R2'])} 份 {r['R2'][:4]}")
        print(f"   R3 部门命中   {len(r['R3'])} 份 (体裁排除 {len(r['R3_dropped'])} 份) {r['R3'][:4]}")
        print(f"   并集 {r['union_count']} 份 / 证据行 {r['evidence_rows']} / 产出结构 {', '.join(r['shapes']) or '-'}")
        if r["empty_evidence_docs"]:
            print(f"   ⚠ 并集中 0 证据行 {len(r['empty_evidence_docs'])} 份 {r['empty_evidence_docs'][:4]}")
        print()

    tot = sum(r["union_count"] for r in result)
    empty = [r["clause"] for r in result if r["union_count"] == 0]
    no_ev = [r["clause"] for r in result if r["evidence_rows"] == 0]
    print(f"合计：{len(result)} 条款，并集命中 {tot} 份，零召回 {len(empty)} 条 {empty}，零证据行 {len(no_ev)} 条 {no_ev}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
