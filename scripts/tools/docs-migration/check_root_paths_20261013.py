#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""校验权威文件里反引号包裹的 docs/ 路径是否真实存在（只读）。
用法: python3 scripts/tools/docs-migration/check_root_paths_20261013.py
"""
import os
import re

FILES = ["AGENTS.md", "项目全局规则.md", "docs/README.md"]
RE = re.compile(r"`(docs/[^`\s]+?)(?::\d+(?:-\d+)?)?`")

total_bad = 0
for f in FILES:
    txt = open(f, encoding="utf-8").read()
    refs = set(RE.findall(txt))
    bad = [p for p in refs
           if not os.path.exists(p) and not os.path.exists(p.rstrip("/"))
           and not os.path.exists(os.path.join(os.path.dirname(f), p))]
    total_bad += len(bad)
    print("%s: 引用 docs/ 路径 %d 个，不存在 %d" % (f, len(refs), len(bad)))
    for b in sorted(bad):
        print("   MISS", b)
print("合计缺失:", total_bad)
