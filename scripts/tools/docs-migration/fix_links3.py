#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
第三遍链接修复：
  1. URL 解码后再解析（处理 %20 空格）
  2. 断链时在全仓库范围查找唯一同名文件（含 docs 之外的 项目全局规则.md 等）
  3. 重写为正确的相对路径（必要时重新 URL 编码）
用法:
    python3 fix_links3.py --dry-run
    python3 fix_links3.py --execute
"""
import os
import re
import sys
from collections import defaultdict
from urllib.parse import unquote, quote

DOCS = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(DOCS)

LINK_RE = re.compile(r'\]\(([^)\s]+?)(#[^)\s]*)?\)')
SKIP_PREFIX = ('http://', 'https://', 'mailto:', 'tel:', 'data:', '//', 'file://', '#')
DOC_EXT = ('.md', '.txt', '.cs', '.html', '.doc', '.docx', '.xlsx', '.sql', '.json', '.png', '.jpg')
EXCLUDE_DIRS = {'.git', 'node_modules', 'bin', 'obj', '.vs', 'dist'}


def list_md(root):
    out = []
    for dp, dn, fn in os.walk(root):
        dn[:] = [d for d in dn if d not in EXCLUDE_DIRS]
        for f in fn:
            if f.endswith('.md'):
                out.append(os.path.join(dp, f))
    return sorted(out)


def index_repo():
    idx = defaultdict(list)
    for dp, dn, fn in os.walk(REPO):
        dn[:] = [d for d in dn if d not in EXCLUDE_DIRS]
        for f in fn:
            if f == '.DS_Store':
                continue
            if f.lower().endswith(DOC_EXT):
                idx[f].append(os.path.relpath(os.path.join(dp, f), REPO))
    return idx


def encode_path(p):
    """路径含空格等字符时做最小化 URL 编码（保留 / . - _）"""
    return quote(p, safe='/.@-_()（）')


def main():
    dry = '--execute' not in sys.argv
    idx = index_repo()
    files = list_md(DOCS)

    fixed_files = fixed_links = 0
    still = []

    for full in files:
        rel_repo = os.path.relpath(full, REPO)
        rel_docs = os.path.relpath(full, DOCS)
        try:
            text = open(full, encoding='utf-8').read()
        except UnicodeDecodeError:
            continue

        new_dir_repo = os.path.dirname(rel_repo)
        repls = []

        for m in LINK_RE.finditer(text):
            raw, anchor = m.group(1), m.group(2) or ''
            if raw.startswith(SKIP_PREFIX):
                continue
            decoded = unquote(raw)
            if not decoded.lower().endswith(DOC_EXT):
                continue

            # 先按 docs 内相对路径解析（原始语义）
            cand_cur = os.path.normpath(os.path.join(new_dir_repo, decoded))
            if os.path.exists(os.path.join(REPO, cand_cur)):
                continue  # 未断

            base = os.path.basename(decoded)
            cands = idx.get(base, [])
            if len(cands) == 1:
                new_rel = os.path.relpath(os.path.join(REPO, cands[0]),
                                          os.path.join(REPO, new_dir_repo)).replace(os.sep, '/')
                if not new_rel.startswith('.'):
                    new_rel = './' + new_rel
                new_val = encode_path(new_rel) + anchor
                if new_val != raw:
                    repls.append((m.start(1), m.end(1), new_val))
            elif len(cands) > 1:
                still.append((rel_docs, raw, f"同名{len(cands)}份"))
            else:
                still.append((rel_docs, raw, "目标不存在"))

        if repls:
            for s, e, v in reversed(repls):
                text = text[:s] + v + text[e:]
            fixed_files += 1
            fixed_links += len(repls)
            if not dry:
                open(full, 'w', encoding='utf-8').write(text)

    print(f"{'[DRY-RUN]' if dry else '[EXECUTE]'} 扫描 {len(files)} 份文档")
    print(f"  修复: {fixed_links} 条链接 / {fixed_files} 份文档")
    print(f"  仍无法修复: {len(still)}")
    if still:
        print("\n  剩余清单:")
        for rel, raw, why in still[:25]:
            print(f"    [{why}] {rel}")
            print(f"        -> {raw}")
    if dry:
        print("\n加 --execute 实际改写。")


if __name__ == '__main__':
    main()
