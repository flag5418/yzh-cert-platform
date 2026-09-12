#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
更新文档中残留的旧目录路径引用（纯文本与链接显示文本）
用法:
    python3 update_refs.py --dry-run
    python3 update_refs.py --execute
"""
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 顺序敏感：更具体的规则必须排在前面
RULES = [
    # 知识库
    ('docs/60-AI工程设计/YZH-知识库/', 'docs/30-项目规则/知识库/'),
    ('docs/60-AI工程设计/YZH-知识库', 'docs/30-项目规则/知识库'),
    # 60-AI工程设计 其余
    ('docs/60-AI工程设计/', 'docs/30-项目规则/'),
    ('docs/60-AI工程设计', 'docs/30-项目规则'),
    # 80-功能设计
    ('docs/80-功能设计/', 'docs/20-体系认证/03-详细设计/'),
    ('docs/80-功能设计', 'docs/20-体系认证/03-详细设计'),
    # 20-架构决策 细分
    ('docs/20-架构决策/总体设计/', 'docs/20-体系认证/01-总体设计/'),
    ('docs/20-架构决策/业务流程/', 'docs/20-体系认证/01-总体设计/'),
    ('docs/20-架构决策/权限体系/', 'docs/20-体系认证/01-总体设计/权限体系/'),
    ('docs/20-架构决策/技术选型/', 'docs/20-体系认证/01-总体设计/技术选型/'),
    ('docs/20-架构决策/实施报告/', 'docs/40-实施/'),
    ('docs/20-架构决策/', 'docs/90-归档/旧版本/'),
    ('docs/20-架构决策', 'docs/90-归档/旧版本'),
    # 90-延展规划
    ('docs/90-延展规划/', 'docs/20-体系认证/04-延展规划/'),
    ('docs/90-延展规划', 'docs/20-体系认证/04-延展规划'),
    # 历史文档 细分（必须早于通用规则）
    ('docs/历史文档/归档-', 'docs/90-归档/旧版本/归档-'),
    ('docs/历史文档/案例/', 'docs/90-归档/案例资料/'),
    ('docs/历史文档/案例', 'docs/90-归档/案例资料'),
    ('docs/历史文档/99-架构优化建议/', 'docs/90-归档/旧版本/99-架构优化建议/'),
    ('docs/历史文档/99-架构优化建议', 'docs/90-归档/旧版本/99-架构优化建议'),
    ('docs/历史文档/', 'docs/90-归档/旧版本/'),
    ('docs/历史文档', 'docs/90-归档/旧版本'),
]

# 处理范围：仓库根 md + src/docker 说明 + docs 活跃层（排除归档）
TARGETS = [
    'AGENTS.md',
    '项目全局规则.md',
    'src/certplatform-web/README.md',
    'docker/anydoc/README.md',
]
DOCS_ACTIVE = ['00-工程体系', '10-YZH架构', '20-体系认证', '30-项目规则', '40-实施', '50-任务']
EXCLUDE_DIRS = {'.git', 'node_modules', 'bin', 'obj', '.vs', 'dist', '输出产物', 'src', 'docker'}

# 这些文件刻意保留「迁移前的原始路径」，不可改写
EXCLUDE_FILES = {
    'docs/10-YZH架构/source/migration-index.md',
}


def collect():
    out = list(TARGETS)
    for d in DOCS_ACTIVE:
        for dp, dn, fn in os.walk(os.path.join(REPO, 'docs', d)):
            dn[:] = [x for x in dn if x not in EXCLUDE_DIRS]
            for f in fn:
                if f.endswith('.md'):
                    out.append(os.path.relpath(os.path.join(dp, f), REPO))
    out.append('docs/README.md')
    return sorted(set(out))


def main():
    dry = '--execute' not in sys.argv
    files = collect()
    hit_files = hit_count = 0
    details = []

    for rel in files:
        if rel in EXCLUDE_FILES:
            continue
        p = os.path.join(REPO, rel)
        if not os.path.isfile(p):
            continue
        try:
            t = open(p, encoding='utf-8').read()
        except UnicodeDecodeError:
            continue
        orig = t
        n_file = 0
        for old, new in RULES:
            c = t.count(old)
            if c:
                t = t.replace(old, new)
                n_file += c
        if t != orig:
            hit_files += 1
            hit_count += n_file
            details.append((rel, n_file))
            if not dry:
                open(p, 'w', encoding='utf-8').write(t)

    print(f"{'[DRY-RUN]' if dry else '[EXECUTE]'} 扫描 {len(files)} 份文件")
    print(f"  命中: {hit_count} 处 / {hit_files} 份文件\n")
    for rel, n in sorted(details, key=lambda x: -x[1]):
        print(f"  {n:>3} 处  {rel}")
    if dry:
        print("\n加 --execute 实际改写。")


if __name__ == '__main__':
    main()
