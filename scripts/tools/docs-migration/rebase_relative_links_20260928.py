#!/usr/bin/env python3
"""rebase_relative_links_20260928.py — 修复迁移导致的相对链接断链。

对指定 md 中每个本地相对链接：按当前文件位置解析不存在时，
逐级上跳（最多 4 级父目录）寻找首个存在的解析结果，回写为正确相对路径。
始终基于原文偏移改写（跳过围栏/行内代码块内的匹配），不会错位。

用法：python3 rebase_relative_links_20260928.py [--dry-run]  （默认 dry-run，--execute 执行）
"""
import io
import os
import re
import sys
from urllib.parse import unquote

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))

SCOPE_DIRS = [
    'docs/50-任务/分析报告',
    'docs/50-任务/开发计划',
    'docs/50-任务/迁移计划',
    'docs/50-任务/迁移工作台',
    'docs/40-实施',
    'docs/00-工程体系',
    'docs/10-YZH架构',
    'docs/20-体系认证',
    'docs/30-项目规则',
]
LINK_RE = re.compile(r'\]\(([^)\s]+)(?:\s+"[^"]*")?\)')


def code_ranges(text):
    """围栏代码块 + 行内代码的 (start, end) 区间。"""
    ranges = []
    for m in re.finditer(r'```[\s\S]*?```', text):
        ranges.append((m.start(), m.end()))
    for m in re.finditer(r'`[^`\n]*`', text):
        rng = (m.start(), m.end())
        if not any(rng[0] >= a and rng[1] <= b for a, b in ranges):
            ranges.append(rng)
    return ranges


def in_ranges(pos, ranges):
    return any(a <= pos < b for a, b in ranges)


def collect_files():
    out = []
    for d in SCOPE_DIRS:
        full = os.path.join(REPO, d)
        if not os.path.isdir(full):
            continue
        for dp, _, fn in os.walk(full):
            for f in fn:
                if f.endswith('.md'):
                    out.append(os.path.join(dp, f))
    return out


def main():
    dry = '--execute' not in sys.argv
    fixed_files = fixed_links = 0
    for abs_f in collect_files():
        raw = io.open(abs_f, encoding='utf-8').read()
        cr = code_ranges(raw)
        changes = []
        for m in LINK_RE.finditer(raw):
            start = m.start(1)
            if in_ranges(start, cr):
                continue
            target = m.group(1).strip()
            if re.match(r'^(https?:|mailto:|tel:|data:|#)', target):
                continue
            base = unquote(target.split('#')[0].split('?')[0])
            if not base or base.startswith('/') or '{{' in base:
                continue
            cur = os.path.normpath(os.path.join(os.path.dirname(abs_f), base))
            if os.path.exists(cur):
                continue
            fixed = None
            prefix = os.path.dirname(abs_f)
            for _ in range(4):
                prefix = os.path.dirname(prefix)
                cand = os.path.normpath(os.path.join(prefix, base))
                if os.path.exists(cand):
                    fixed = os.path.relpath(cand, os.path.dirname(abs_f)).replace(os.sep, '/')
                    break
            if fixed:
                changes.append((start, len(m.group(1)), fixed))
        if changes:
            out = raw
            for start, length, new in sorted(changes, reverse=True):
                out = out[:start] + new + out[start + length:]
                fixed_links += 1
                print(f'  {"DRY" if dry else "FIX"} {os.path.relpath(abs_f, REPO)}: '
                      f'{raw[start:start + length]} -> {new}')
            if out != raw:
                fixed_files += 1
                if not dry:
                    io.open(abs_f, 'w', encoding='utf-8').write(out)
    print(f'[{"DRY-RUN" if dry else "EXECUTE"}] {fixed_files} files / {fixed_links} links')


if __name__ == '__main__':
    main()
