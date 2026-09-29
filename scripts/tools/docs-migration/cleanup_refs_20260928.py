#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
2026-09-28 文档清理：输出产物/ 与三个违规 50-* 目录迁移后的引用批量改写。
用法:
    python3 cleanup_refs_20260928.py --dry-run   # 只报告将改写的文件与命中数
    python3 cleanup_refs_20260928.py --execute   # 实际改写
排除: docs/90-归档/（历史快照不回改）、分类清单-2026-09-28.md（映射记录本身）、
      .git / node_modules / src/old / .kilo / .playwright-mcp / backup 等。
"""
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
ARC = "docs/90-归档/旧版本/归档-2026-09-28-输出产物清理"
D_ANALYSIS = "docs/50-任务/分析报告"
D_PLAN = "docs/50-任务/开发计划"
D_MIG = "docs/50-任务/迁移计划"
D_IMPL = "docs/40-实施"
HIST_QA = "docs/90-归档/历史项目/vol.web代码质量分析"
HIST_PROTO = "docs/90-归档/历史项目/审核员端原型"

# (旧路径片段, 新路径) —— 长串在前，脚本按列表顺序执行替换
_an = ["NC规则设计页面问题诊断与修复方案-V1.md", "YZH架构评估与完善建议-V1.md",
       "yzh.vue.core包化设计与业务边界评估-V1.md", "yzh.vue.core结构合理性与科学性评估及专家端接入方案-V2.md",
       "yzh.vue.core架构分离评估与演进建议-V3.md", "yzh.vue.core架构原型对照与必改问题清单-V4.md",
       "yzh.vue.core核心内核对标与完善建议-V5.md", "专家平台冲刺决策与推进方案-V1.md",
       "业务代码不合规清单-V1.md", "产品定位与核心功能可行性-V1.md", "企业端七步方案评估与可行性-V1.md",
       "企业端业务模型设计评估与决策-V1.md", "后台结构审查与产品形态评估-V1.md",
       "命名规范违规清单与消灭方案-V1.md", "标准目录链路-逻辑缺陷审计-V1.md",
       "多标准一体化认证业务规则与数据模型-V1.md", "标准目录与企业资料-存储与编码规范-V4.md"]
_outsrc = [f"{n}" for n in _an] + [
    "外包供应商调查报告-V1.md", "外包公司调查表-V1.md", "外包公司调查表-V1.docx",
    "专家平台-企业管理功能开发计划-V5.md", "体系认证专家系统建设计划-V4.md",
    "后台管理-文件上传转换链路重构开发计划-V3.md", "前端架构收敛与文档先行计划-V2.md",
    "企业端落地计划与后台完善清单-V1.md", "后台管理模块建设计划-V1.md",
    "审核员端落地与前端治理-执行计划-V1.md", "企业资料管理-OrgCode论证与版本边界-V3.md",
    "企业资料管理-开发建议-V2.md", "企业资料管理-路径与版本方案-决策清单-V1.md",
    "端到端迁移缺口清单-V1.md", "提取模块路径变更影响与TODO清单-V1.md",
    "文档失效评估与归档方案-V1.md"]
_arc = ["专家平台-企业管理功能开发计划-V1.md", "专家平台-企业管理功能开发计划-V2.md",
        "专家平台-企业管理功能开发计划-V3.md", "专家平台-企业管理功能开发计划-V4.md",
        "体系认证专家系统建设计划-V1.md", "体系认证专家系统建设计划-V2.md", "体系认证专家系统建设计划-V3.md",
        "后台管理-文件上传转换链路重构开发计划-V1.md", "后台管理-文件上传转换链路重构开发计划-V2.md",
        "前端架构收敛与文档先行计划-V1.md", "AI协作痛点分析与方案-V1.md", "YZH框架层文档补齐方案-V1.md",
        "YZH框架独立文档化方案-V1.md", "docs总体结构方案-V3.md", "文档审计与修复方案-V1.md",
        "文档管理方案-V2.md", "前后端架构统一-深度分析与方案-V1.md", "历史项目独立层方案-V1.md",
        "代码质量分析报告-vol.web.md", "代码质量分析报告-vol.web-分层版.md"]
_out = []  # (old, new)
for n in _an:
    _out.append((f"输出产物/{n}", f"{D_ANALYSIS}/{n}"))
_out += [(f"输出产物/外包供应商调查报告-V1.md", f"{D_ANALYSIS}/外包调研/外包供应商调查报告-V1.md"),
         ("输出产物/外包公司调查表-V1.md", f"{D_ANALYSIS}/外包调研/外包公司调查表-V1.md"),
         ("输出产物/外包公司调查表-V1.docx", f"{D_ANALYSIS}/外包调研/外包公司调查表-V1.docx")]
for n in ["专家平台-企业管理功能开发计划-V5.md", "体系认证专家系统建设计划-V4.md",
          "后台管理-文件上传转换链路重构开发计划-V3.md", "前端架构收敛与文档先行计划-V2.md",
          "企业端落地计划与后台完善清单-V1.md", "后台管理模块建设计划-V1.md",
          "审核员端落地与前端治理-执行计划-V1.md", "企业资料管理-OrgCode论证与版本边界-V3.md",
          "企业资料管理-开发建议-V2.md", "企业资料管理-路径与版本方案-决策清单-V1.md"]:
    _out.append((f"输出产物/{n}", f"{D_PLAN}/{n}"))
_out.append(("输出产物/端到端迁移缺口清单-V1.md", f"{D_MIG}/端到端迁移缺口清单-V1.md"))
_out.append(("输出产物/提取模块路径变更影响与TODO清单-V1.md", f"{D_IMPL}/提取模块路径变更影响与TODO清单-V1.md"))
_out.append(("输出产物/文档失效评估与归档方案-V1.md", "docs/00-工程体系/文档失效评估与归档方案-V1.md"))
for n in _arc:
    _out.append((f"输出产物/{n}", f"{ARC}/{n}"))
for n in ["代码质量分析报告-vol.web-V1.1.md", "代码质量分析报告-vol.web-分层版-V1.1.md"]:
    _out.append((f"输出产物/{n}", f"{HIST_QA}/{n}"))
for n in ["审核员端-交互原型-V1.html", "审核员端-交互原型-V1.1.html"]:
    _out.append((f"输出产物/{n}", f"{HIST_PROTO}/{n}"))

# docs 内部
_out += [
    ("docs/50-分析报告/后端手写SQL问题分析-V4.md", f"{D_ANALYSIS}/后端手写SQL问题分析-V4.md"),
    ("docs/50-分析报告/后端手写SQL问题分析与修复报告-V4.md", f"{D_ANALYSIS}/后端手写SQL问题分析与修复报告-V4.md"),
    ("docs/50-分析报告/后端手写SQL修复-回归测试指南.md", f"{D_ANALYSIS}/后端手写SQL修复-回归测试指南-V1.md"),
    ("docs/50-分析报告/后端手写SQL修复完成报告.md", f"{D_IMPL}/后端手写SQL修复完成报告-V1.md"),
    ("docs/50-分析报告/后端手写SQL修复-自动测试报告.md", f"{D_IMPL}/后端手写SQL修复-自动测试报告-V1.md"),
    ("docs/迁移脚本/20260919_rename_id_to_Id.sql", "scripts/db/20260919_rename_id_to_Id.sql"),
]
for n in ["NC规则设计迁移方案-V1.md", "ReportDefinition迁移方案-V1.md",
          "yzh.vue.core系统底座化迁移计划-V1.md", "迁移代码架构审核报告-V1.md"]:
    _out.append((f"docs/50-迁移计划/{n}", f"{D_MIG}/{n}"))

# 50-任务 平铺 → 子目录
_flat_plan = ["ISO标准管理模块设计与开发文档-V2.md", "Phase1-逐表列名变更清单-V1.md",
              "Skill节点配置移植方案-V1.md", "YZH架构修复与项目稳定化-TODO清单-V1.md",
              "YZH架构安全改进TODO计划-V1.md", "全链路命名一致性统一-TODO清单-V1.md",
              "全链路命名一致性统一方案-V1.md", "前端Logic基类增强与Composable封装实施计划-V1.md",
              "前端TreeTable基类增强与Composable封装实施计划-V2.md", "前端原子组件与CRUD内核重构TODO清单-V1.md",
              "前端组件Layout命名规范迁移TODO-V1.md", "后端SQL迁移LINQ-TODO清单-V1.md",
              "后端SQL迁移LINQ实施方案-V1.md", "工作流设计器通用组件抽象与重构方案-V1.md",
              "接口权限管理系统实施文档-V1.md", "数据字典管理-开发计划-V1.md",
              "文档提取规则-移植实施计划-V1.md", "机构关联管理-开发计划-V1.md",
              "标准目录管理-移植TODO-V1.md", "标准目录管理-移植实施计划-V1.md",
              "系统参数与审计字段修复实施计划-V1.md", "角色-接口权限实施计划-V1.md",
              "认证机构管理-开发计划-V1.md", "认证阶段定义-开发计划-V1.md",
              "项目文档管理-实施计划-V3.md", "项目文档管理-开发TODO-V1.md",
              "项目文档管理-开发计划-V1.md"]
for n in _flat_plan:
    _out.append((f"docs/50-任务/{n}", f"{D_PLAN}/{n}"))
_out.append(("docs/50-任务/项目文档管理-老项目对比与修复建议-V1.md", f"{D_ANALYSIS}/项目文档管理-老项目对比与修复建议-V1.md"))

# 排序：长串优先
_out.sort(key=lambda p: -len(p[0]))

SKIP_DIRS = {".git", "node_modules", "src/old", ".kilo", ".playwright-mcp", "backup", "temp",
             ".agnes", ".freebuff", ".dotnet-cli", ".idea", ".vscode", ".workbuddy-ai", "docker", "DB"}
SKIP_FILES = {"分类清单-2026-09-28.md", "cleanup_refs_20260928.py"}
EXTS = {".md", ".py", ".sh", ".mjs", ".js", ".json", ".yml", ".yaml", ".ts"}


def should_skip(path, is_dir=False):
    rel = os.path.relpath(path, REPO)
    if rel == ".":
        return False
    for s in SKIP_DIRS:
        if rel == s or rel.startswith(s + os.sep):
            return True
    if rel.startswith("docs/90-归档/"):
        return True
    if is_dir:
        return False
    if os.path.basename(path) in SKIP_FILES:
        return True
    return os.path.splitext(path)[1] not in EXTS


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "--dry-run"
    changed = []
    for root, dirs, files in os.walk(REPO):
        dirs[:] = [d for d in dirs if not should_skip(os.path.join(root, d), is_dir=True)]
        for fn in files:
            path = os.path.join(root, fn)
            if should_skip(path):
                continue
            try:
                with open(path, encoding="utf-8") as f:
                    src = f.read()
            except (UnicodeDecodeError, OSError):
                continue
            out, hits = src, 0
            for old, new in _out:
                # 变体1：完整仓库相对路径
                for key, val in ((old, new),):
                    if key in out:
                        hits += out.count(key)
                        out = out.replace(key, val)
                # 变体2：去掉 "docs/" 前缀（匹配 "./50-分析报告/x"、"50-迁移计划/x" 等相对写法）
                if old.startswith("docs/"):
                    k2, v2 = old[len("docs/"):], new[len("docs/"):]
                else:
                    k2, v2 = "docs/" + old, new  # "docs/输出产物/x" 笔误前缀 → 新全路径
                if k2 in out:
                    hits += out.count(k2)
                    out = out.replace(k2, v2)
            if hits:
                changed.append((os.path.relpath(path, REPO), hits))
                if mode == "--execute":
                    with open(path, "w", encoding="utf-8") as f:
                        f.write(out)
    verb = "已改写" if mode == "--execute" else "将改写"
    print(f"{verb} {len(changed)} 个文件：")
    for rel, n in sorted(changed):
        print(f"  {n:3d}×  {rel}")
    if mode != "--execute":
        print("\n（dry-run：加 --execute 执行）")


if __name__ == "__main__":
    main()
