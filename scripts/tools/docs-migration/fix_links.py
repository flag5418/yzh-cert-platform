#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
迁移后链接修复
依据 migrate.py 的映射表，重算所有 .md 中的相对链接。
用法:
    python3 fix_links.py --dry-run
    python3 fix_links.py --execute
"""
import os
import re
import sys

DOCS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DOCS)
from migrate import MAPPING, HIST_FALLBACK_SRC, HIST_FALLBACK_DST  # noqa

LINK_RE = re.compile(r'\]\(([^)\s]+?)(#[^)\s]*)?\)')
SKIP_PREFIX = ('http://', 'https://', 'mailto:', 'tel:', 'data:', '//')


def norm(p):
    return os.path.normpath(p)


def build_move_map():
    """old_path(相对 docs) -> new_path(相对 docs)"""
    m = {}
    for entry in MAPPING:
        src, dst = entry[0], entry[1]
        rename = entry[2] if len(entry) > 2 else False
        if rename or dst.endswith('.md'):
            m[norm(src)] = norm(dst)
        else:
            m[norm(src)] = norm(os.path.join(dst, os.path.basename(src)))

    # 兜底规则：历史文档剩余内容
    hist_dir = os.path.join(DOCS, HIST_FALLBACK_SRC)
    if os.path.isdir(hist_dir):
        pass  # 迁移后已不存在，无需处理
    return m


def list_md():
    out = []
    for dp, dn, fn in os.walk(DOCS):
        dn[:] = [d for d in dn if d not in ('.git', 'node_modules')]
        for f in fn:
            if f.endswith('.md'):
                out.append(os.path.join(dp, f))
    return sorted(out)


def main():
    dry = '--execute' not in sys.argv
    move = build_move_map()
    rev = {v: k for k, v in move.items()}

    files = list_md()
    changed_files = 0
    changed_links = 0
    broken = []

    for full in files:
        rel_new = os.path.relpath(full, DOCS)
        rel_old = rev.get(norm(rel_new), norm(rel_new))
        old_dir = os.path.dirname(rel_old)

        try:
            with open(full, encoding='utf-8') as fh:
                text = fh.read()
        except UnicodeDecodeError:
            continue

        new_dir = os.path.dirname(rel_new)
        replacements = []

        for m in LINK_RE.finditer(text):
            raw, anchor = m.group(1), m.group(2) or ''
            if raw.startswith(SKIP_PREFIX) or raw.startswith('/'):
                continue

            # 用「迁移前的文件位置」解析目标
            abs_old = norm(os.path.join(old_dir, raw))
            if abs_old in move:
                target_new = move[abs_old]
            elif os.path.exists(os.path.join(DOCS, abs_old)):
                target_new = abs_old
            else:
                # 迁移前就是断链，跳过（不修改）
                broken.append((rel_new, raw))
                continue

            new_rel = os.path.relpath(os.path.join(DOCS, target_new), os.path.join(DOCS, new_dir))
            new_rel = new_rel.replace(os.sep, '/')
            if not new_rel.startswith('.'):
                new_rel = './' + new_rel

            if new_rel != raw:
                replacements.append((m.start(1), m.end(1), new_rel + anchor))

        if replacements:
            for s, e, newval in reversed(replacements):
                text = text[:s] + newval + text[e:]
            changed_files += 1
            changed_links += len(replacements)
            if not dry:
                with open(full, 'w', encoding='utf-8') as fh:
                    fh.write(text)

    print(f"{'[DRY-RUN]' if dry else '[EXECUTE]'} 扫描 {len(files)} 份文档")
    print(f"  需改写: {changed_files} 份文档 / {changed_links} 条链接")
    print(f"  迁移前即断链（未改动）: {len(broken)} 条")
    if broken:
        agg = {}
        for f, r in broken:
            agg.setdefault(r, []).append(f)
        print("\n  断链 TOP10（按出现次数）:")
        for r, fs in sorted(agg.items(), key=lambda x: -len(x[1]))[:10]:
            print(f"    {len(fs):>3}x  {r}")
    if dry:
        print("\n加 --execute 实际改写。")


if __name__ == '__main__':
    main()
