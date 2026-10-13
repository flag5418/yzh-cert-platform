#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""扫描 docs 活跃层的 markdown 链接，报告断链（指向不存在的目标）。
用法: python3 scripts/tools/docs-migration/check_broken_links_20261013.py [docs根]
只读，不修改任何文件。

跳过：围栏代码块（``` / ~~~）、行内代码（`…`）、纯锚点、外链、
      以及显式的「伪链接」豁免（仓库根相对路径 src/…、包别名 @…）。
"""
import os
import re
import sys
from urllib.parse import unquote

ROOT = sys.argv[1] if len(sys.argv) > 1 else "docs"
ACTIVE = ["00-工程体系", "10-YZH架构", "20-体系认证", "30-项目规则", "40-实施", "50-扩展"]
LINK_RE = re.compile(r"\[[^\]]*\]\(([^)]+)\)")
FENCE_RE = re.compile(r"^[ \t]*(```|~~~)", re.M)
# 行内代码：`...`（单反引号，不含换行）
INLINE_CODE_RE = re.compile(r"`[^`\n]*`")
SKIP_PREFIX = ("http://", "https://", "mailto:", "#", "tel:", "@", "src/", "./src/", "../src/")


def strip_code(text: str) -> str:
    """去掉围栏代码块与行内代码，保留等长占位，避免误报。"""
    # 围栏代码块
    out, in_fence = [], False
    for line in text.splitlines(keepends=True):
        if FENCE_RE.match(line):
            in_fence = not in_fence
            out.append("\n" if line.endswith("\n") else "")
        else:
            out.append(("" if in_fence else line))
    text = "".join(out)
    # 行内代码
    return INLINE_CODE_RE.sub(lambda m: " " * len(m.group(0)), text)


broken = {}
total_links = 0
for layer in ACTIVE:
    base = os.path.join(ROOT, layer)
    if not os.path.isdir(base):
        continue
    for dirpath, dirnames, filenames in os.walk(base):
        if "99-归档" in dirpath:
            continue
        for fn in filenames:
            if not fn.endswith(".md"):
                continue
            fp = os.path.join(dirpath, fn)
            with open(fp, encoding="utf-8", errors="replace") as fh:
                text = strip_code(fh.read())
            for m in LINK_RE.finditer(text):
                target = m.group(1).strip()
                if not target or target.startswith(SKIP_PREFIX):
                    continue
                target = target.split("#", 1)[0].strip()
                if not target:
                    continue
                total_links += 1
                target = unquote(target)
                resolved = os.path.normpath(os.path.join(dirpath, target))
                if not os.path.exists(resolved):
                    broken.setdefault(fp, set()).add(target)

print(f"# 断链报告（活跃层，共扫描有效相对链接 {total_links} 条）\n")
print(f"断链文件数: {len(broken)}\n")
for fp in sorted(broken):
    print(f"## {fp}")
    for t in sorted(broken[fp]):
        print(f"  - {t}")
