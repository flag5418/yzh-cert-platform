#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""文档整治辅助分析（只读）。报告：
1. 同目录「版本族」(同名不同 -Vn，或去掉数字前缀后同名) → 除最高版外均为被取代；
2. 全库内容级重复（md5 相同）；
3. 40-实施 与 20-体系认证 之间的同名/同内容（= 已提升，可归档）。
用法: python3 scripts/tools/docs-migration/triage_20261013.py <docs根>
"""
import hashlib
import os
import re
import sys
from collections import defaultdict

ROOT = sys.argv[1] if len(sys.argv) > 1 else "docs"
LAYERS = ["00-工程体系", "10-YZH架构", "20-体系认证", "30-项目规则", "40-实施", "50-扩展", "90-归档"]

VER_RE = re.compile(r"^(.*?)-V(\d+)\.md$")
NUM_RE = re.compile(r"^\d+-")


def norm_key(fn):
    """把文件名归一为「概念键」，用于识别版本族/跨目录同名。"""
    base = fn[:-3] if fn.endswith(".md") else fn
    m = VER_RE.match(fn)
    if m:
        base = m.group(1)
    base = NUM_RE.sub("", base)          # 去 数字前缀
    base = re.sub(r"[（(].*?[)）]", "", base)  # 去括号补充
    base = re.sub(r"[-_ ]+", "", base)
    return base


def md5(fp):
    h = hashlib.md5()
    with open(fp, "rb") as fh:
        for chunk in iter(lambda: fh.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


files = []
for layer in LAYERS:
    base = os.path.join(ROOT, layer)
    if not os.path.isdir(base):
        continue
    for dp, dn, fns in os.walk(base):
        for fn in fns:
            if fn.endswith(".md"):
                files.append(os.path.join(dp, fn))

# 1) 版本族
print("=" * 70)
print("① 同目录版本族（除最高版外 = 被取代候选）")
print("=" * 70)
fam = defaultdict(list)
for fp in files:
    fam[(os.path.dirname(fp), norm_key(os.path.basename(fp)))].append(fp)
n_fam = 0
for (d, k), v in sorted(fam.items()):
    if len(v) > 1:
        n_fam += 1
        print(f"\n[{d}]")
        for fp in sorted(v):
            print(f"   {'KEEP ' if fp == sorted(v)[-1] else 'STALE'}  {os.path.basename(fp)}")
print(f"\n版本族总数: {n_fam}")

# 2) 全库内容重复
print("\n" + "=" * 70)
print("② 内容级重复（md5 相同）")
print("=" * 70)
byhash = defaultdict(list)
for fp in files:
    byhash[md5(fp)].append(fp)
nd = 0
for h, v in byhash.items():
    if len(v) > 1:
        nd += 1
        print("\n".join("   " + x for x in sorted(v)))
        print()
print(f"重复组数: {nd}")

# 3) 40-实施 vs 20-体系认证 同名
print("=" * 70)
print("③ 40-实施 与 20-体系认证 同名概念（可能已提升）")
print("=" * 70)
impl = {norm_key(os.path.basename(f)): f for f in files if f.startswith(os.path.join(ROOT, "40-实施")) or f.startswith(os.path.join(ROOT, "40-实施"))}
cert = {norm_key(os.path.basename(f)): f for f in files if "20-体系认证" in f}
common = sorted(set(impl) & set(cert))
for k in common:
    print(f"   {impl[k]}\n   {cert[k]}\n")
print(f"同名概念数: {len(common)}")
