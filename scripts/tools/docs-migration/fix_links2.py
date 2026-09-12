#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
第二遍链接修复：修复迁移前就存在、且目标文件在新树中唯一存在的断链。
用法:
    python3 fix_links2.py --dry-run
    python3 fix_links2.py --execute
"""
import os
import re
import sys
from collections import defaultdict

DOCS = os.path.dirname(os.path.abspath(__file__))
LINK_RE = re.compile(r'\]\(([^)\s]+?)(#[^)\s]*)?\)')
SKIP_PREFIX = ('http://', 'https://', 'mailto:', 'tel:', 'data:', '//', 'file://', '#')
# 只处理看起来是文档链接的目标
DOC_EXT = ('.md', '.txt', '.cs', '.html', '.doc', '.docx', '.xlsx', '.sql', '.json', '.png', '.jpg')


def list_md():
    out = []
    for dp, dn, fn in os.walk(DOCS):
        dn[:] = [d for d in dn if d not in ('.git', 'node_modules')]
        for f in fn:
            if f.endswith('.md'):
                out.append(os.path.join(dp, f))
    return sorted(out)


def index_by_basename():
    idx = defaultdict(list)
    for dp, dn, fn in os.walk(DOCS):
        dn[:] = [d for d in dn if d not in ('.git', 'node_modules')]
        for f in fn:
            if f == '.DS_Store':
                continue
            idx[f].append(os.path.relpath(os.path.join(dp, f), DOCS))
    return idx


def main():
    dry = '--execute' not in sys.argv
    idx = index_by_basename()

    files = list_md()
    fixed_files = 0
    fixed_links = 0
    still_broken = []
    ambiguous = []

    for full in files:
        rel = os.path.relpath(full, DOCS)
        try:
            text = open(full, encoding='utf-8').read()
        except UnicodeDecodeError:
            continue
        new_dir = os.path.dirname(rel)
        repls = []

        for m in LINK_RE.finditer(text):
            raw, anchor = m.group(1), m.group(2) or ''
            if raw.startswith(SKIP_PREFIX):
                continue
            if not raw.lower().endswith(DOC_EXT):
                continue
            tgt = os.path.normpath(os.path.join(new_dir, raw))
            if os.path.exists(os.path.join(DOCS, tgt)):
                continue  # 未断

            base = os.path.basename(raw)
            cands = idx.get(base, [])
            if len(cands) == 1:
                new_rel = os.path.relpath(os.path.join(DOCS, cands[0]),
                                          os.path.join(DOCS, new_dir)).replace(os.sep, '/')
                if not new_rel.startswith('.'):
                    new_rel = './' + new_rel
                repls.append((m.start(1), m.end(1), new_rel + anchor))
            elif len(cands) > 1:
                ambiguous.append((rel, raw, cands))
            else:
                still_broken.append((rel, raw))

        if repls:
            for s, e, v in reversed(repls):
                text = text[:s] + v + text[e:]
            fixed_files += 1
            fixed_links += len(repls)
            if not dry:
                open(full, 'w', encoding='utf-8').write(text)

    print(f"{'[DRY-RUN]' if dry else '[EXECUTE]'} 扫描 {len(files)} 份文档")
    print(f"  可修复: {fixed_links} 条链接 / {fixed_files} 份文档")
    print(f"  目标不存在（无法修复）: {len(still_broken)}")
    print(f"  同名多份（需人工判断）: {len(ambiguous)}")

    if ambiguous:
        print("\n  同名多份清单:")
        for rel, raw, c in ambiguous[:12]:
            print(f"    {rel}")
            print(f"      -> {raw}")
            for x in c:
                print(f"         · {x}")
    if still_broken:
        print("\n  目标不存在清单:")
        for rel, raw in still_broken[:15]:
            print(f"    {rel}  ->  {raw}")
    if dry:
        print("\n加 --execute 实际改写。")


if __name__ == '__main__':
    main()
