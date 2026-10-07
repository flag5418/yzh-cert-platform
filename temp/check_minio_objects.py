#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""MinIO 对象存在性核对（评审 #3 的实测脚本）。

只读：列 MinIO 全部对象 → 与 DB 里 5 个路径列逐一对账。
输出「有多少 DB 行指向了 MinIO 里不存在的对象」——即评审报的「文件不存在 / 源文件读取失败」。
"""
import os
import sys

for k in ("HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy", "ALL_PROXY", "all_proxy"):
    os.environ.pop(k, None)
os.environ["NO_PROXY"] = "*"
os.environ["no_proxy"] = "*"

from minio import Minio  # noqa: E402

ENDPOINT = "127.0.0.1:9000"
ACCESS = "admin"
SECRET = "Yzh123456."
BUCKET = "cert-platform"

cli = Minio(ENDPOINT, access_key=ACCESS, secret_key=SECRET, secure=False)

if not cli.bucket_exists(BUCKET):
    print(f"!! 桶不存在: {BUCKET}")
    sys.exit(1)

keys = set()
for obj in cli.list_objects(BUCKET, recursive=True):
    keys.add(obj.object_name)
print(f"MinIO 对象总数（bucket={BUCKET}，recursive）: {len(keys)}")

# 顶层前缀分布（前两段）
from collections import Counter  # noqa: E402

top = Counter()
for k in keys:
    parts = k.strip("/").split("/")
    top["/".join(parts[:2])] += 1
print("\n--- 顶层前缀分布（前 20）---")
for p, c in top.most_common(20):
    print(f"{c:6d}  {p}")


def norm(p: str) -> str:
    """★ DB 存的是 `/standard-directory/...`（带前导斜杠），MinIO key 无前导斜杠。

    不归一化就会得出「100% 缺失」的假结论 —— 这是本脚本第一版踩的坑。
    """
    p = (p or "").strip().replace("\\", "/")
    while p.startswith("/"):
        p = p[1:]
    return p


def load(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line:
                continue
            rows.append(line.split("\t"))
    return rows


COLS = [
    "StoragePath",
    "ConvertedStoragePath",
    "PreviewPdfPath",
    "MarkdownPath",
    "EditableStoragePath",
]

rows = load("/tmp/yzh_sd_files.tsv")
print(f"\nDB 行数（cert_standard_directory_file, IsDeleted=0）: {len(rows)}")

print("\n=== 逐列对账（cert_standard_directory_file）===")
print(f"{'列名':<24}{'非空':>6}{'命中':>6}{'缺失':>6}  缺失率")
for i, col in enumerate(COLS, start=1):
    nonempty = hit = miss = 0
    samples = []
    for r in rows:
        v = r[i].strip() if i < len(r) else ""
        if not v:
            continue
        nonempty += 1
        if norm(v) in keys:
            hit += 1
        else:
            miss += 1
            if len(samples) < 3:
                samples.append(v)
    rate = f"{miss / nonempty * 100:.1f}%" if nonempty else "-"
    print(f"{col:<24}{nonempty:>6}{hit:>6}{miss:>6}  {rate}")
    for s in samples:
        print(f"    缺失样例: {s}")

# 空白模板
print("\n=== 空白模板（cert_doc_template）===")
tpl = load("/tmp/yzh_tpl.tsv")
for r in tpl:
    code, sp = r[0], r[1]
    ok = "存在" if norm(sp) in keys else "★缺失"
    print(f"{ok}  {code}  {sp}")

# 未被任何 DB 列引用的对象（孤儿）
refd = set()
for r in rows:
    for i in range(1, len(COLS) + 1):
        if i < len(r) and r[i].strip():
            refd.add(norm(r[i]))
for r in tpl:
    if r[1].strip():
        refd.add(norm(r[1]))
orphan = keys - refd
print(f"\n=== 孤儿对象 ===\nMinIO 有但 DB 无引用: {len(orphan)} / {len(keys)}")
