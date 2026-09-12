#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
docs 文档体系重构迁移脚本
用法:
    python3 migrate.py --dry-run    # 只打印计划
    python3 migrate.py --execute    # 实际执行（git mv）
"""
import os
import subprocess
import sys

DOCS = os.path.dirname(os.path.abspath(__file__))

# ============================================================
# 映射表: (源路径, 目标目录)
# 源为目录时整目录移动；为文件时单文件移动
# ============================================================
MAPPING = [
    # ---------- A. 00-工程体系 → 归档（已被 10-YZH架构 取代） ----------
    ("00-工程体系/前端架构设计-V1.md", "90-归档/旧版本"),
    ("00-工程体系/树形结构设计-V2.md", "90-归档/旧版本"),
    ("00-工程体系/树形结构设计评审报告-V1.md", "90-归档/旧版本"),

    # ---------- B. 20-架构决策 → 拆解 ----------
    ("20-架构决策/总体设计/总体设计-V3.md", "20-体系认证/01-总体设计"),
    ("20-架构决策/总体设计/数据库表设计-V2.md", "20-体系认证/02-数据库设计"),
    ("20-架构决策/业务流程/CertPlatform业务流程与数据链路-V2.1.md", "20-体系认证/01-总体设计"),
    ("20-架构决策/业务流程/核心工作原理-V1.md", "20-体系认证/01-总体设计"),
    ("20-架构决策/技术选型/技术研究-文档解析开源方案与轻量部署-V1.md", "20-体系认证/01-总体设计/技术选型"),
    ("20-架构决策/权限体系/README.md", "20-体系认证/01-总体设计/权限体系"),
    ("20-架构决策/权限体系/多角色复杂权限体系设计-V1.md", "20-体系认证/01-总体设计/权限体系"),
    ("20-架构决策/权限体系/数据权限体系设计-V1.md", "20-体系认证/01-总体设计/权限体系"),
    ("20-架构决策/权限体系/用户权限统一接口设计-V1.md", "20-体系认证/01-总体设计/权限体系"),
    ("20-架构决策/实施报告/Phase1_实施报告.md", "40-实施"),
    ("20-架构决策/实施报告/YZH-Core架构演进与问题追踪-V1.md", "40-实施"),
    ("20-架构决策/实施报告/YZH-Framework架构设计评审报告-V1.md", "40-实施"),
    ("20-架构决策/实施报告/cert_phase2_implementation_summary.md", "40-实施"),
    ("20-架构决策/架构设计/CertPlatform基类架构设计-V1.0.md", "90-归档/旧版本"),
    ("20-架构决策/架构设计/左树右表统一架构设计-V1.md", "90-归档/旧版本"),
    ("20-架构决策/YZH-架构体系总纲-V1.md", "90-归档/旧版本"),
    ("20-架构决策/README.md", "90-归档/旧版本/20-架构决策-README.md"),

    # ---------- C. 60-AI工程设计 → 拆解 ----------
    ("60-AI工程设计/AI工程规范-V1.md", "30-项目规则"),
    ("60-AI工程设计/AI代码生成检查清单-V1.md", "30-项目规则"),
    ("60-AI工程设计/EntityValidationHandler设计文档.md", "30-项目规则"),
    ("60-AI工程设计/Skill清单-V1.md", "30-项目规则"),
    ("60-AI工程设计/cert-platform-page-development-guide.md", "30-项目规则"),
    ("60-AI工程设计/vue-ts-coding-standards.md", "30-项目规则"),
    ("60-AI工程设计/前后端代码结构统一规则-V1.md", "30-项目规则"),
    ("60-AI工程设计/README.md", "90-归档/旧版本/60-AI工程设计-README.md"),
    # 知识库：业务知识抽出
    ("60-AI工程设计/YZH-知识库/02-边界约束/ISO体系认证NC与报告标准约束-V1.md", "20-体系认证/05-业务知识库"),
    # 知识库：被新版取代
    ("60-AI工程设计/YZH-知识库/01-能力清单/02-YZH增量清单.md", "90-归档/旧版本"),
    ("60-AI工程设计/YZH-知识库/YZH前端框架知识库-V1.0-MVP.md", "90-归档/旧版本"),
    # 知识库：其余整目录 → 30-项目规则/知识库（保留内部结构）
    # rename=True：目标即最终路径，不是父目录
    ("60-AI工程设计/YZH-知识库", "30-项目规则/知识库", True),

    # ---------- D. 80-功能设计 → 20-体系认证/03-详细设计 ----------
    ("80-功能设计/01-系统管理", "20-体系认证/03-详细设计"),
    ("80-功能设计/02-审核员端", "20-体系认证/03-详细设计"),
    ("80-功能设计/03-平台基础", "20-体系认证/03-详细设计"),
    ("80-功能设计/README.md", "90-归档/旧版本/80-功能设计-README.md"),

    # ---------- E. 90-延展规划 → 20-体系认证/04-延展规划 ----------
    ("90-延展规划/40-领域设计", "20-体系认证/04-延展规划"),
    ("90-延展规划/50-规划与优先级", "20-体系认证/04-延展规划"),
    ("90-延展规划/文档合并与脚本规范化实施方案-V2.md", "20-体系认证/04-延展规划"),
    ("90-延展规划/README.md", "90-归档/旧版本/90-延展规划-README.md"),

    # ---------- F. 历史文档 → 90-归档 ----------
    # F1. 案例资料（rename=True：目标即最终路径）
    ("历史文档/案例", "90-归档/案例资料", True),
    # F2. Vol 框架（历史项目）
    ("历史文档/归档-2026-09-09-Vol框架历史文档", "90-归档/历史项目/Vol框架"),
    ("历史文档/08-Vol框架实战速查手册.md", "90-归档/历史项目/Vol框架"),
    ("历史文档/vol-framework-complete-guide.md", "90-归档/历史项目/Vol框架"),
    ("历史文档/vol-framework-troubleshooting.md", "90-归档/历史项目/Vol框架"),
    ("历史文档/vol-skill.md", "90-归档/历史项目/Vol框架"),
    ("历史文档/BASH_README.md", "90-归档/历史项目/Vol框架"),
    # F3. 旧 YZH-Framework（历史项目）
    ("历史文档/YZH-Framework架构设计-V1.0.md", "90-归档/历史项目/旧YZH-Framework"),
    ("历史文档/YZH-V3.0-架构设计文档.md", "90-归档/历史项目/旧YZH-Framework"),
    ("历史文档/YZH-前端架构v3设计-V1.md", "90-归档/历史项目/旧YZH-Framework"),
    # F4. 架构优化建议
    ("历史文档/99-架构优化建议", "90-归档/旧版本"),
    # F5. README 由新版取代
    ("历史文档/README.md", "90-归档/旧版本/历史文档-README.md"),
]

# 历史文档剩余内容 → 90-归档/旧版本（兜底规则）
HIST_FALLBACK_SRC = "历史文档"
HIST_FALLBACK_DST = "90-归档/旧版本"

# 迁移后应清空的目录（仅校验，不删除）
SHOULD_BE_EMPTY = [
    "20-架构决策",
    "60-AI工程设计",
    "80-功能设计",
    "90-延展规划",
    "历史文档",
]


# 迁移前需清掉的空目录（避免 rename 式移动被嵌套）
PRE_CLEAN_EMPTY_DIRS = [
    "30-项目规则/知识库",
    "90-归档/案例资料",
]


def rel(p):
    return os.path.join(DOCS, p)


def exists(p):
    return os.path.exists(rel(p))


def listdir_safe(p):
    d = rel(p)
    if not os.path.isdir(d):
        return []
    return sorted(x for x in os.listdir(d) if x != ".DS_Store")


def build_plan():
    plan = []
    errors = []
    for entry in MAPPING:
        src, dst = entry[0], entry[1]
        rename = entry[2] if len(entry) > 2 else False
        if not exists(src):
            errors.append(f"源不存在: {src}")
            continue
        plan.append((src, dst, rename))

    # 兜底：历史文档剩余内容
    moved = {e[0] for e in MAPPING}
    for name in listdir_safe(HIST_FALLBACK_SRC):
        src = f"{HIST_FALLBACK_SRC}/{name}"
        if src in moved:
            continue
        plan.append((src, HIST_FALLBACK_DST, False))
    return plan, errors


def main():
    dry = "--execute" not in sys.argv
    plan, errors = build_plan()

    if errors:
        print("!! 映射表存在问题:")
        for e in errors:
            print("   ", e)
        if not dry:
            print("\n存在错误，中止执行。")
            return 1

    print(f"{'[DRY-RUN] ' if dry else '[EXECUTE] '}共 {len(plan)} 项迁移\n")
    for src, dst, rename in plan:
        kind = "DIR " if os.path.isdir(rel(src)) else "FILE"
        arrow = f"{dst}" if (rename or dst.endswith(".md")) else f"{dst}/"
        print(f"  {kind}  {src}")
        print(f"        -> {arrow}")

    if dry:
        print("\n以上为计划。加 --execute 实际执行。")
        return 0

    # 清掉占位的空目录，保证 rename 式移动落到正确位置
    for d in PRE_CLEAN_EMPTY_DIRS:
        full = rel(d)
        if os.path.isdir(full) and not [x for x in os.listdir(full) if x != ".DS_Store"]:
            os.rmdir(full)
            print(f"  [清理占位空目录] {d}")

    print("\n开始执行...")
    ok, fail = 0, 0
    for src, dst, rename in plan:
        dst_path = rel(dst)
        if rename:
            os.makedirs(os.path.dirname(dst_path), exist_ok=True)
            target = dst_path
        elif dst.endswith(".md"):
            os.makedirs(os.path.dirname(dst_path), exist_ok=True)
            target = dst_path
        else:
            os.makedirs(dst_path, exist_ok=True)
            target = os.path.join(dst_path, os.path.basename(src))
        try:
            subprocess.run(
                ["git", "mv", rel(src), target],
                cwd=DOCS, check=True,
                stdout=subprocess.DEVNULL, stderr=subprocess.PIPE,
            )
            ok += 1
        except subprocess.CalledProcessError as e:
            try:
                subprocess.run(["mv", rel(src), target], check=True)
                ok += 1
                print(f"  [mv] {src} -> {dst}")
            except subprocess.CalledProcessError:
                fail += 1
                print(f"  !! 失败: {src} -> {dst}  {e.stderr.decode()[:120]}")

    print(f"\n完成: 成功 {ok} / 失败 {fail}")

    print("\n目录清空校验:")
    for d in SHOULD_BE_EMPTY:
        left = listdir_safe(d)
        if not os.path.isdir(rel(d)):
            print(f"  ✓ {d} 已不存在")
        elif not left:
            print(f"  ✓ {d} 已清空（空目录待删）")
        else:
            print(f"  ! {d} 残留 {len(left)} 项: {left[:5]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
