#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""条款证据发现结果运行时校验器（离线版）。

校验 5 类问题：
1. DocFact 填写状态与 Markdown 实际表格冲突；
2. OriginalQuote 非连续、省略号、低信息、错引样板文字；
3. ClauseItemRef 不在条款允许条目内，或无编号条款未用 main；
4. DoubtType 与关联 finding 的 FillStatus/DocClass 冲突，日期冲突无两个日期或方向反；
5. CanFormEvidence=true 但无 filled 实施记录、缺 required doc class 或 Gap 非空。

只读输入，不修改结果 JSON。
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

ELLIPSIS = ("...", "……", "…")
BOILERPLATE = ("著作权", "版权所有", "copyright", "本文件为受控文件", "未经许可不得复制")
RECORD_CLASSES = {"record", "report"}


def load_json(path: str) -> Any:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def norm(text: str) -> str:
    text = text or ""
    text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
    text = re.sub(r"<br\s*/?>", "", text, flags=re.I)
    text = re.sub(r"[|*#`>]", "", text)
    text = re.sub(r"-{3,}", "", text)
    text = text.replace("-", "")
    text = text.replace("（", "(").replace("）", ")")
    text = text.replace("：", ":").replace("；", ";").replace("，", ",")
    text = re.sub(r"\s+", "", text)
    return text.strip()


def load_clause_texts(paths: list[str]) -> dict[str, str]:
    clauses: dict[str, str] = {}
    for p in paths:
        with open(p, encoding="utf-8") as f:
            for line in f:
                parts = line.rstrip("\n").split("\t")
                if len(parts) >= 3 and parts[0].strip():
                    clauses.setdefault(parts[0].strip(), parts[-1].strip())
    return clauses


def allowed_items(clause_text: str) -> set[str]:
    # 注、示例不是要求项；7.1.6 的 a/b 是“注2”的来源，不得当条款条目。
    body = re.split(r"注\s*[0-9０-９]?\s*[:：]", clause_text or "", maxsplit=1)[0]
    items = set(re.findall(r"(?<![0-9０-９])([a-z])\)", body))
    return items or {"main"}


def parse_dates(text: str) -> list[str]:
    return re.findall(r"20\d{2}\s*[.\-/年]\s*\d{1,2}\s*[.\-/月]\s*\d{1,2}", text or "")


def date_key(s: str) -> tuple[int, int, int]:
    nums = re.findall(r"\d+", s)
    return (int(nums[0]), int(nums[1]), int(nums[2])) if len(nums) >= 3 else (0, 0, 0)


def markdown_tables(md: str) -> list[list[list[str]]]:
    """返回每个表格的数据行；跳过表头和分隔行。"""
    lines = md.splitlines()
    tables: list[list[list[str]]] = []
    i = 0
    while i < len(lines):
        if not lines[i].lstrip().startswith("|"):
            i += 1
            continue
        block = []
        while i < len(lines) and lines[i].lstrip().startswith("|"):
            block.append(lines[i].strip())
            i += 1
        if len(block) < 2:
            continue
        rows = []
        for idx, line in enumerate(block):
            cells = [c.strip() for c in line.strip("|").split("|")]
            if idx == 0:
                continue
            if all(re.fullmatch(r":?-{2,}:?", c or "") for c in cells):
                continue
            rows.append(cells)
        if rows:
            tables.append(rows)
    return tables


def table_has_business_values(md: str) -> bool:
    """判断表格数据行里是否已有“业务值”，用于识别 07记录清单 这类误判。

    不能把表头、预印标签、序号、占位符 /、合计 0 当业务值。
    只在同一行出现至少两个有效值，且其中至少一个有编号/年份/期限特征时成立。
    """
    skip_labels = {
        "序号", "姓名", "性别", "部门", "岗位", "合计", "小计", "核准", "审核",
        "批准", "编制", "制表", "检查员", "备注", "日期", "客户名称", "订单号码",
        "产品名称", "生产通知单", "检验结果判定", "合格", "不合格",
    }

    def value_like(c: str) -> bool:
        c = norm(c)
        if not c or c in {"/", "-", "—", "0", "无"}:
            return False
        if c in skip_labels:
            return False
        if ":" in c or "：" in c:
            return False
        return bool(re.search(r"KSY|QMS|WI-|20\d{2}|年|月|日", c))

    for rows in markdown_tables(md):
        for cells in rows:
            # 跳过明显的页脚/合计/签批行
            first = norm(cells[0]) if cells else ""
            if any(k in first for k in ("合计", "核准", "编制", "审核", "批准", "制表", "检查员")):
                continue
            values = [c for c in cells[1:] if norm(c) and norm(c) not in skip_labels]
            has_code = any(re.search(r"KSY|QMS|WI-", norm(c)) for c in values)
            has_time = any(re.search(r"20\d{2}|年", norm(c)) for c in values)
            if len(values) >= 2 and has_code and has_time:
                return True
    return False


def build_doc_map(meta_paths: list[str]) -> dict[str, str]:
    docs: dict[str, str] = {}
    for p in meta_paths:
        for x in load_json(p):
            name = x.get("file_name")
            path = x.get("path")
            if name and path:
                docs[name] = path
    return docs


def validate_quote(quote: str, md: str, finding: dict[str, Any]) -> list[str]:
    issues = []
    q = (quote or "").strip()
    if not q:
        issues.append("OriginalQuote 为空")
        return issues
    if any(e in q for e in ELLIPSIS):
        issues.append("OriginalQuote 含省略号，不是连续原文")
    qn = norm(q)
    if len(qn) < 10:
        issues.append("OriginalQuote 少于 10 字，信息量不足")
    if any(b.lower() in q.lower() for b in BOILERPLATE):
        if finding.get("FindingType") in {"document_exists", "field_present", "record_exists"}:
            issues.append("OriginalQuote 引用声明/版权/受控标识等样板文字，不能支撑正文结论")
    if q in (md or ""):
        return issues
    if qn and qn in norm(md):
        return issues
    issues.append("OriginalQuote 在原文中无法连续命中（表格重排/跨段拼接）")
    return issues


def validate_clause_item(refs: Any, allowed: set[str], finding_id: str) -> list[str]:
    issues = []
    if not isinstance(refs, list):
        return [f"ClauseItemRef 不是数组：{refs!r}"]
    if not refs:
        return ["ClauseItemRef 为空；无编号条款应填 ['main']"]
    bad = [r for r in refs if r not in allowed]
    if bad:
        issues.append(f"ClauseItemRef 含条款不允许的条目 {bad}；允许 {sorted(allowed)}")
    if "main" in refs and len(refs) > 1:
        issues.append("ClauseItemRef 混用 main 与其他条目")
    return issues


def validate_doubt(doubt: dict[str, Any], findings: dict[str, dict[str, Any]]) -> tuple[list[str], list[str]]:
    """返回 (硬错误, 人工复核提示)。"""
    issues, warns = [], []
    t = doubt.get("DoubtType")
    related = [findings[i] for i in (doubt.get("RelatedFindings") or []) if i in findings]
    if not related:
        issues.append("Doubt 无有效 RelatedFindings")
    if t == "blank_template":
        if any(f.get("FillStatus") != "template_only" for f in related):
            issues.append("blank_template 但关联 finding 不是 template_only")
    elif t == "missing_signature":
        if related and all(f.get("FillStatus") == "template_only" for f in related):
            issues.append("missing_signature 但关联文档整体是空白模板；应判 blank_template")
        if not related:
            issues.append("missing_signature 无关联 finding")
    elif t == "date_conflict":
        dates = parse_dates(doubt.get("Description", ""))
        if len(set(dates)) < 2:
            issues.append("date_conflict 少于两个不同日期")
        elif len(set(dates)) >= 2:
            d1, d2 = sorted({date_key(x) for x in dates})[:2]
            desc = doubt.get("Description", "")
            if "晚于" in desc and d1 < d2:
                # 描述里说“第一个日期晚于第二个”，但实际更早
                issues.append("date_conflict 日期方向与描述相反（说晚于，实际更早）")
            if "早于" in desc and d1 > d2:
                issues.append("date_conflict 日期方向与描述相反（说早于，实际更晚）")
    elif t == "report_only":
        if related and not any(f.get("DocClass") == "report" for f in related):
            issues.append("report_only 但关联 finding 不是 report 类")
        if not related:
            issues.append("report_only 无关联 finding")
    elif t == "inconsistent_person":
        # 人员不一致很难自动判定；只在无关联 finding 时硬报错，其余给人工复核提示
        if not related:
            issues.append("inconsistent_person 无关联 finding")
        else:
            warns.append("inconsistent_person 需人工确认是否为不同角色/职责，而不是人员矛盾")
    elif t not in {
        "blank_template", "missing_signature", "missing_date", "inconsistent_person",
        "date_conflict", "scope_mismatch", "report_only", "no_traceability",
        "cross_doc_conflict", "unreadable",
    }:
        issues.append(f"未知 DoubtType：{t!r}")
    return issues, warns


def validate_assembly(assembly: dict[str, Any], findings: list[dict[str, Any]]) -> list[str]:
    issues = []
    can = assembly.get("CanFormEvidence")
    if can not in {"true", "partial", "false"}:
        issues.append(f"CanFormEvidence 非法：{can!r}")
        return issues
    if can != "true":
        if can == "false" and not assembly.get("Gap"):
            issues.append("CanFormEvidence=false 但 Gap 为空，false 缺依据")
        return issues
    if assembly.get("Gap"):
        issues.append("CanFormEvidence=true 但 Gap 非空")
    required = set(assembly.get("RequiredDocClasses") or [])
    present = {f.get("DocClass") for f in findings if f.get("DocClass")}
    missing = sorted(required - present)
    if missing:
        issues.append(f"CanFormEvidence=true 但缺 required doc class {missing}")
    filled_impl = [
        f for f in findings
        if f.get("DocClass") in RECORD_CLASSES and f.get("FillStatus") == "filled"
    ]
    if not filled_impl:
        issues.append("CanFormEvidence=true 但无 filled 的 record/report 实施记录")
    return issues


def validate_docfact(fact: dict[str, Any], md: str) -> list[str]:
    issues = []
    status = fact.get("FillStatus")
    rows = fact.get("SampleRows") or []
    if status == "template_only":
        if rows:
            issues.append("template_only 但 SampleRows 非空")
        if md and table_has_business_values(md):
            issues.append("template_only 但表格数据行已有业务值（序号列空不等于整表空）")
    return issues


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--results", nargs="+", required=True)
    ap.add_argument("--docfacts", nargs="+", required=True)
    ap.add_argument("--meta", nargs="+", required=True)
    ap.add_argument("--clauses", nargs="+", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    clause_texts = load_clause_texts(args.clauses)
    doc_map = build_doc_map(args.meta)

    results = []
    for p in args.results:
        results.extend(load_json(p))
    results = [r for r in results if r.get("status") == "JSON_OK" and isinstance(r.get("result"), dict)]

    docfacts = []
    for p in args.docfacts:
        for x in load_json(p):
            fact = x.get("doc_fact") or x.get("result")
            if fact:
                docfacts.append({**x, "fact": fact})

    issues: list[dict[str, Any]] = []

    def add(clause: str, obj: str, oid: str, rule: str, severity: str, message: str, evidence: Any = None):
        issues.append({
            "clause": clause,
            "object_type": obj,
            "object_id": oid,
            "rule": rule,
            "severity": severity,
            "message": message,
            "evidence": evidence,
        })

    # 1) 条款结果校验
    for r in results:
        clause = str(r.get("clause"))
        res = r["result"]
        allowed = allowed_items(clause_texts.get(clause, ""))
        findings = res.get("EvidenceFindings") or []
        fmap = {f.get("FindingId"): f for f in findings if f.get("FindingId")}
        docs = [d for d in doc_map if d]

        for f in findings:
            fid = f.get("FindingId", "?")
            for msg in validate_clause_item(f.get("ClauseItemRef"), allowed, fid):
                add(clause, "finding", fid, "ClauseItemValidator", "P1", msg, f.get("ClauseItemRef"))
            doc = f.get("DocName", "")
            path = doc_map.get(doc)
            if not path:
                # 尝试按文件名主干匹配
                stem = Path(doc).stem
                for n, p in doc_map.items():
                    if Path(n).stem == stem:
                        path = p
                        break
            if not path or not Path(path).exists():
                add(clause, "finding", fid, "QuoteValidator", "P1", "找不到对应 Markdown，无法校验引用", doc)
                continue
            md = Path(path).read_text(encoding="utf-8")
            for msg in validate_quote(f.get("OriginalQuote", ""), md, f):
                add(clause, "finding", fid, "QuoteValidator", "P1", msg, f.get("OriginalQuote", "")[:80])

        for d in res.get("Doubts") or []:
            did = d.get("DoubtId", "?")
            hard, warns = validate_doubt(d, fmap)
            for msg in hard:
                add(clause, "doubt", did, "DoubtTypeValidator", "P0", msg, d.get("DoubtType"))
            for msg in warns:
                add(clause, "doubt", did, "DoubtTypeValidator", "P2", msg, d.get("DoubtType"))

        for a in res.get("EvidenceAssembly") or []:
            aid = a.get("AssemblyId", "?")
            for msg in validate_assembly(a, findings):
                add(clause, "assembly", aid, "AssemblyValidator", "P0", msg, a.get("CanFormEvidence"))

    # 2) DocFact 校验
    for x in docfacts:
        clause = str(x.get("clause", ""))
        name = x.get("file_name", "")
        path = x.get("path") or doc_map.get(name)
        if not path:
            stem = Path(name).stem
            for n, p in doc_map.items():
                if Path(n).stem == stem:
                    path = p
                    break
        if not path or not Path(path).exists():
            add(clause, "docfact", name, "DocFactValidator", "P1", "找不到对应 Markdown，无法校验填写状态", name)
            continue
        md = Path(path).read_text(encoding="utf-8")
        for msg in validate_docfact(x["fact"], md):
            add(clause, "docfact", name, "DocFactValidator", "P0", msg, x["fact"].get("FillStatus"))

    counts = Counter(i["rule"] for i in issues)
    by_clause = defaultdict(lambda: Counter())
    for i in issues:
        by_clause[i["clause"]][i["rule"]] += 1

    out = {
        "summary": {
            "clauses": len(results),
            "findings": sum(len(r["result"].get("EvidenceFindings") or []) for r in results),
            "doubts": sum(len(r["result"].get("Doubts") or []) for r in results),
            "assemblies": sum(len(r["result"].get("EvidenceAssembly") or []) for r in results),
            "docfacts": len(docfacts),
            "issues": len(issues),
            "counts_by_rule": dict(counts),
            "counts_by_severity": dict(Counter(i["severity"] for i in issues)),
        },
        "by_clause": {k: dict(v) for k, v in sorted(by_clause.items())},
        "issues": issues,
    }
    Path(args.out).write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(out["summary"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
