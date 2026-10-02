# -*- coding: utf-8 -*-
"""
纸面实验 · 第二轮：把锚点按「性质」再切一刀，得到可机械替换 vs 需生成的比例。
"""
import os
import re
import json
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "CS河北雄安尚龙医疗科技有限公司13485体系材料")

COMPANY_FULL = "河北雄安尚龙医疗科技有限公司"
PERSONS = ["尚月永", "王倩", "董晶晶", "尚月兴"]
RE_CODE = re.compile(r"XASL-[A-Z]{1,3}(?:-\d{3})?")
RE_DATE = re.compile(r"20\d{2}\s*[年.\-/]\s*\d{1,2}\s*(?:[月.\-/]\s*\d{1,2}\s*日?)?")
RE_SIGN = re.compile(r"(编制|编写|审核|批准|审批|签发|会签)")
RE_TABLE_ROW = re.compile(r"^\|.*\|\s*$")
RE_SEP_ROW = re.compile(r"^\|[\s\-|:]+\|\s*$")


def group_of(rel):
    p = "/" + rel.replace("\\", "/")
    for key, name in (("/1质量手册/", "1质量手册"), ("/2程序文件/", "2程序文件"),
                      ("/3制度文件", "3制度文件"),
                      ("/4记录文件/技术类/", "4记录-技术类"),
                      ("/4记录文件/质量类/", "4记录-质量类"),
                      ("/4记录文件/生产类/", "4记录-生产类"),
                      ("/4记录文件/其它/", "4记录-其它"),
                      ("/4记录文件/", "4记录-根")):
        if key in p:
            return name
    return "0根"


def blank_form_stats(text):
    rows = [l for l in text.splitlines()
            if RE_TABLE_ROW.match(l) and not RE_SEP_ROW.match(l)]
    if not rows:
        return None
    cells = 0
    for r in rows:
        for c in r.strip().strip("|").split("|"):
            if c.strip():
                cells += 1
    return {"rows": len(rows), "cells": cells}


def analyze(path, rel):
    text = open(path, encoding="utf-8").read()
    lines = text.splitlines()
    body_chars = len(re.sub(r"\s", "", text))

    sign_lines, body_lines = [], []
    for l in lines:
        (sign_lines if RE_SIGN.search(l) else body_lines).append(l)
    sign_text, body_text = "\n".join(sign_lines), "\n".join(body_lines)

    def hits(t):
        return {
            "company": t.count(COMPANY_FULL),
            "code": len(RE_CODE.findall(t)),
            "date": len(RE_DATE.findall(t)),
            "person": sum(t.count(p) for p in PERSONS),
        }

    hb, hs = hits(body_text), hits(sign_text)
    return {
        "path": rel, "group": group_of(rel),
        "name": os.path.basename(rel)[:-3],
        "chars": body_chars,
        "body": hb, "sign": hs,
        "body_anchor_total": sum(hb.values()),
        "sign_anchor_total": sum(hs.values()),
        "table": blank_form_stats(text),
        "has_sign": bool(sign_lines),
    }


def main():
    recs = []
    for dp, _d, fs in os.walk(ROOT):
        for fn in fs:
            if not fn.endswith(".md"):
                continue
            full = os.path.join(dp, fn)
            rel = os.path.relpath(full, ROOT)
            if "/pdf/" in "/" + rel.replace("\\", "/"):
                continue
            recs.append(analyze(full, rel))
    recs.sort(key=lambda r: (r["group"], r["name"]))
    json.dump(recs, open(os.path.join(HERE, "anchors2.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)

    g = defaultdict(lambda: defaultdict(int))
    for r in recs:
        d = g[r["group"]]
        d["n"] += 1
        d["chars"] += r["chars"]
        for k in ("company", "code", "date", "person"):
            d["b_" + k] += r["body"][k]
            d["s_" + k] += r["sign"][k]
        d["body_anchor"] += r["body_anchor_total"]
        d["sign_anchor"] += r["sign_anchor_total"]
        if r["has_sign"]:
            d["files_sign"] += 1
        if r["table"]:
            d["files_table"] += 1
            d["table_cells"] += r["table"]["cells"]

    hdr = (f"{'分组':<14}{'文件':>4}{'字符':>8}|"
           f"{'正文-公司名':>9}{'正文-编号':>9}{'正文-日期':>9}{'正文-人名':>9}"
           f"{'正文合计':>9}|{'签批-日期':>9}{'签批-人名':>9}{'签批合计':>9}")
    print(hdr)
    print("-" * len(hdr.encode("gbk", errors="ignore")))
    T = defaultdict(int)
    for k in sorted(g):
        d = g[k]
        print(f"{k:<14}{d['n']:>4}{d['chars']:>8}|"
              f"{d['b_company']:>9}{d['b_code']:>9}{d['b_date']:>9}{d['b_person']:>9}"
              f"{d['body_anchor']:>9}|{d['s_date']:>9}{d['s_person']:>9}{d['sign_anchor']:>9}")
        for kk, vv in d.items():
            T[kk] += vv
    print("-" * 80)
    print(f"{'合计':<14}{T['n']:>4}{T['chars']:>8}|"
          f"{T['b_company']:>9}{T['b_code']:>9}{T['b_date']:>9}{T['b_person']:>9}"
          f"{T['body_anchor']:>9}|{T['s_date']:>9}{T['s_person']:>9}{T['sign_anchor']:>9}")
    print()
    print(f"锚点总计            = {T['body_anchor'] + T['sign_anchor']}")
    print(f"  其中 正文锚点      = {T['body_anchor']}")
    print(f"  其中 签批栏锚点    = {T['sign_anchor']}")
    print(f"含签批栏的文件       = {T['files_sign']}/{T['n']}")
    print(f"含表格的文件         = {T['files_table']}/{T['n']}")
    print(f"表格单元格总数       = {T['table_cells']}")
    print(f"总字符              = {T['chars']}")
    print(f"锚点密度            = {(T['body_anchor']+T['sign_anchor'])/T['chars']*1000:.2f} 处/千字")

    # 每文件锚点数分布
    print("\n=== 单文件锚点数分布 ===")
    tot = sorted((r["body_anchor_total"] + r["sign_anchor_total"]) for r in recs)
    import statistics
    print(f"min={tot[0]} p25={tot[len(tot)//4]} 中位数={statistics.median(tot)} "
          f"p75={tot[len(tot)*3//4]} max={tot[-1]}")
    zero = [r for r in recs if r["body_anchor_total"] + r["sign_anchor_total"] == 0]
    print(f"零锚点文件（完全无需替换）= {len(zero)}/{len(recs)}")
    for r in zero:
        print("   ·", r["group"], r["name"])

    # 正文锚点 TOP15
    print("\n=== 正文锚点最多的 15 个文件 ===")
    for r in sorted(recs, key=lambda x: -x["body_anchor_total"])[:15]:
        b = r["body"]
        print(f"{r['body_anchor_total']:>5}  [{r['group']}] {r['name'][:44]:<46}"
              f" 公司{b['company']} 编号{b['code']} 日期{b['date']} 人{b['person']}")


if __name__ == "__main__":
    main()
