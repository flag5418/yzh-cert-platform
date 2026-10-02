# -*- coding: utf-8 -*-
"""
纸面实验：对 167 份真实标准文档（ISO 13485 医疗器械质量体系）做锚点统计。
数据源：MinIO cert-platform/standard-directory/...（模板行 YZH-STD-ENT）
目的：量化「企业专有信息锚点」占文档总内容的比重，判断机械自动化覆盖率。
"""
import os
import re
import json
from collections import defaultdict

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                    "CS河北雄安尚龙医疗科技有限公司13485体系材料")

# 企业专有实体（来自实际文档）
COMPANY_FULL = "河北雄安尚龙医疗科技有限公司"
COMPANY_SHORT = "雄安尚龙"
CODE_PREFIX = "XASL"
PERSONS = ["尚月永", "王倩", "董晶晶", "尚月兴"]
PERSON_ROLES = {"尚月永": "总经理/法定代表人", "王倩": "管理者代表",
                "董晶晶": "综合部/编制", "尚月兴": "供销部"}

RE_CODE = re.compile(r"XASL-[A-Z]{1,3}(?:-\d{3})?")
RE_DATE = re.compile(r"(20\d{2})\s*[年.\-/]\s*(\d{1,2})\s*(?:[月.\-/]\s*(\d{1,2})\s*日?)?")
RE_TABLE_ROW = re.compile(r"^\|.*\|\s*$")
RE_SEP_ROW = re.compile(r"^\|[\s\-|:]+\|\s*$")


def classify_path(rel):
    p = "/" + rel.replace("\\", "/")
    if "/1质量手册/" in p:
        return "1质量手册"
    if "/2程序文件/" in p:
        return "2程序文件"
    if "/3制度文件" in p:
        return "3制度文件"
    if "/4记录文件/技术类/" in p:
        return "4记录-技术类"
    if "/4记录文件/质量类/" in p:
        return "4记录-质量类"
    if "/4记录文件/生产类/" in p:
        return "4记录-生产类"
    if "/4记录文件/其它/" in p:
        return "4记录-其它"
    if "/4记录文件/" in p:
        return "4记录-根"
    return "0根"


def is_blank_form(text):
    """判断是否为「空白表格」：非空单元格中，除表头标签外几乎没有实际填写内容。"""
    rows = [l for l in text.splitlines()
            if RE_TABLE_ROW.match(l) and not RE_SEP_ROW.match(l)]
    if not rows:
        return None  # 非表格文档
    filled = 0
    total = 0
    for r in rows:
        for c in r.strip().strip("|").split("|"):
            c = c.strip()
            if not c:
                continue
            total += 1
            # 纯占位/标签判定：含 □、( 、：、换行占位、或为纯表头词
            if re.fullmatch(r"[\s\W]*", c):
                continue
            if "□" in c or "(" in c or "：" in c or c.endswith(":"):
                continue
            if re.fullmatch(r"[0-9]+", c):
                continue
            filled += 1
    return {"table_rows": len(rows), "cells": total, "filled_cells": filled,
            "fill_ratio": round(filled / total, 3) if total else 0.0}


def analyze(path, rel):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    lines = text.splitlines()
    chars = len(re.sub(r"\s", "", text))
    codes = RE_CODE.findall(text)
    dates = RE_DATE.findall(text)
    return {
        "path": rel,
        "group": classify_path(rel),
        "name": os.path.basename(rel).replace(".md", ""),
        "chars": chars,
        "lines": len(lines),
        "company_hits": text.count(COMPANY_FULL),
        "company_short_hits": text.count(COMPANY_SHORT),
        "code_hits": len(codes),
        "distinct_codes": len(set(codes)),
        "date_hits": len(dates),
        "person_hits": sum(text.count(p) for p in PERSONS),
        "table": is_blank_form(text),
    }


def main():
    recs = []
    for dirpath, _dirs, files in os.walk(ROOT):
        for fn in files:
            if not fn.endswith(".md"):
                continue
            full = os.path.join(dirpath, fn)
            rel = os.path.relpath(full, ROOT)
            if "/pdf/" in rel.replace("\\", "/"):
                continue
            recs.append(analyze(full, rel))
    recs.sort(key=lambda r: (r["group"], r["name"]))

    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                           "anchors.json"), "w", encoding="utf-8") as f:
        json.dump(recs, f, ensure_ascii=False, indent=1)

    # 分组汇总
    g = defaultdict(lambda: {"n": 0, "chars": 0, "company": 0, "code": 0,
                             "date": 0, "person": 0, "blank": 0, "table": 0,
                             "files_with_company": 0, "files_with_code": 0,
                             "files_with_person": 0, "files_with_date": 0})
    for r in recs:
        d = g[r["group"]]
        d["n"] += 1
        d["chars"] += r["chars"]
        d["company"] += r["company_hits"]
        d["code"] += r["code_hits"]
        d["date"] += r["date_hits"]
        d["person"] += r["person_hits"]
        if r["company_hits"]:
            d["files_with_company"] += 1
        if r["code_hits"]:
            d["files_with_code"] += 1
        if r["person_hits"]:
            d["files_with_person"] += 1
        if r["date_hits"]:
            d["files_with_date"] += 1
        if r["table"]:
            d["table"] += 1
            if r["table"]["fill_ratio"] < 0.15:
                d["blank"] += 1

    print(f"{'分组':<14}{'文件':>4}{'总字符':>9}{'公司名':>6}{'编号':>6}"
          f"{'日期':>6}{'人名':>6}{'表格':>5}{'近空白':>7}")
    tot = defaultdict(int)
    for k in sorted(g):
        d = g[k]
        print(f"{k:<14}{d['n']:>4}{d['chars']:>9}{d['company']:>6}{d['code']:>6}"
              f"{d['date']:>6}{d['person']:>6}{d['table']:>5}{d['blank']:>7}")
        for kk in ("n", "chars", "company", "code", "date", "person", "table", "blank"):
            tot[kk] += d[kk]
    print(f"{'合计':<14}{tot['n']:>4}{tot['chars']:>9}{tot['company']:>6}"
          f"{tot['code']:>6}{tot['date']:>6}{tot['person']:>6}{tot['table']:>5}{tot['blank']:>7}")

    print("\n=== 文件级命中率 ===")
    for k in sorted(g):
        d = g[k]
        n = d["n"]
        print(f"{k:<14} 含公司名 {d['files_with_company']:>3}/{n:<3}"
              f" 含编号 {d['files_with_code']:>3}/{n:<3}"
              f" 含人名 {d['files_with_person']:>3}/{n:<3}"
              f" 含日期 {d['files_with_date']:>3}/{n:<3}")


if __name__ == "__main__":
    main()
