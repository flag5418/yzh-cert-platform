#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
EntityConfig 静态审计（L3 提交期规则）

═══ 为什么需要它 ═══
`EntityConfigHelper.LoadAndParse` 用 `JsonStringEnumConverter` 反序列化配置 JSON。
**任何一个 `Type` 值不在 `ControlType` 枚举里，就会抛异常**，而被 catch 静默吞掉
⇒ 返回 `NewEmptyConfig` ⇒ **整份配置变空（Columns=[]）**，症状是：
  · 页面「有数据行、一列都不显示」
  · `Title` 等于实体类型名（不是 JSON 里写的中文标题）
  · 后端零报错、前端零报错

2026-09-30 实测踩坑：`Progress` 列写了 `"Type": "CustomSlot"`（前端 `entityAdapters.ts`
支持该值，但后端枚举里没有）⇒ 整个 `CertExpertTask` 列表一列都不显示。

本脚本在**提交期**把这类错误拦下来，代价 ~0，收益 = 不再有「配了半天不生效」的排查。

═══ 检查项 ═══
  E1  `Columns[].Type` 必须是 `ControlType` 枚举成员（从 C# 源码解析，不硬编码）
  E2  `SearchFields[].ControlType` 必须在 `input|select|date|cascader` 内
      （越界 → 前端回落 `'text'`，下拉静默变文本框）
  E3  `EnableField` 的值必须是 `IsValid`（铁律九：启用字段零容忍）
  E4  顶层键必须是 `EntityConfig` 的已知属性（拼错的键 = 死配置）
  E5  `Columns[].FieldName` 不得重复
  E6  配置文件所在路径必须已被 `Program.cs` 的 `BusinessEntityConfigPaths` 覆盖

用法：python3 scripts/tools/audit-entity-config.py [--quiet]
退出码：0 = 全部通过；1 = 存在违规
"""

import io
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# ── 配置根目录（★ 新增业务模块必须在此登记，与 Program.cs 的 BusinessEntityConfigPaths 保持一致）──
CFG_ROOTS = [
    os.path.join(ROOT, "src", "certplatform-api", "CertPlatform.Admin", "Assets", "EntityConfigs"),
    os.path.join(ROOT, "src", "certplatform-api", "CertPlatform.Auditor", "Assets", "EntityConfigs"),
]

# 核心目录（YZH.Core.Web 自带 System/*）
CORE_ROOT = os.path.join(ROOT, "src", "yzh-core", "YZH.Core.Web", "Assets", "EntityConfigs")

CONTROL_TYPE_CS = os.path.join(
    ROOT, "src", "yzh-core", "YZH.Core.Stand", "Enums", "ControlType.cs")
ENTITY_CONFIG_CS = os.path.join(
    ROOT, "src", "yzh-core", "YZH.Core.Stand", "Models", "Config", "EntityConfig.cs")

VALID_SEARCH_CONTROL_TYPES = {"input", "select", "date", "cascader"}

# EntityConfig 的已知顶层属性（新增属性时同步此处）
KNOWN_TOP_KEYS = {
    "Title", "FillMode", "FormCols", "Columns", "NewEntity", "Schema",
    "EnableField", "Toolbar", "RowButtons", "SearchFields", "TreeConfig",
    "TreeBehavior", "Association", "Remark", "Extra",
}


def parse_enum_members(path, enum_name):
    """从 C# 源码解析枚举成员名（不硬编码，避免枚举演进后脚本失真）"""
    if not os.path.isfile(path):
        return None
    src = io.open(path, encoding="utf-8", errors="ignore").read()
    m = re.search(r"enum\s+%s\s*\{(.*?)\n\}" % re.escape(enum_name), src, re.S)
    if not m:
        return None
    body = m.group(1)
    # 去掉注释行
    body = re.sub(r"///.*", "", body)
    body = re.sub(r"//.*", "", body)
    members = set()
    for part in body.split(","):
        part = part.strip()
        if not part:
            continue
        name = part.split("=")[0].strip()
        if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
            members.add(name)
    return members


def parse_entity_config_props(path):
    if not os.path.isfile(path):
        return None
    src = io.open(path, encoding="utf-8", errors="ignore").read()
    src = re.sub(r"///.*", "", src)
    src = re.sub(r"//.*", "", src)
    props = set(re.findall(r"public\s+[\w<>\?\[\]\.]+\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get", src))
    return props


def load_json(path):
    try:
        import json
        return json.loads(io.open(path, encoding="utf-8-sig").read()), None
    except Exception as ex:
        return None, str(ex)


def main():
    quiet = "--quiet" in sys.argv

    control_types = parse_enum_members(CONTROL_TYPE_CS, "ControlType")
    if control_types is None:
        print("✗ 无法解析 ControlType 枚举：%s" % CONTROL_TYPE_CS)
        return 1
    cfg_props = parse_entity_config_props(ENTITY_CONFIG_CS) or KNOWN_TOP_KEYS

    roots = [r for r in CFG_ROOTS if os.path.isdir(r)]
    if os.path.isdir(CORE_ROOT):
        roots.append(CORE_ROOT)
    if not roots:
        print("✗ 未找到任何 EntityConfig 根目录")
        return 1

    files = []
    for r in roots:
        for dp, _dn, fn in os.walk(r):
            for f in fn:
                if f.endswith(".json"):
                    files.append(os.path.join(dp, f))
    files.sort()

    errors = []
    notes = []

    for path in files:
        rel = os.path.relpath(path, ROOT)
        cfg, err = load_json(path)
        if err:
            errors.append((rel, "E0", "JSON 解析失败：%s" % err))
            continue
        if not isinstance(cfg, dict):
            errors.append((rel, "E0", "顶层不是对象"))
            continue

        # E4 顶层键
        for k in cfg.keys():
            if k not in cfg_props:
                errors.append((rel, "E4", "未知顶层键 `%s`（EntityConfig 无此属性 ⇒ 死配置）" % k))

        cols = cfg.get("Columns") or []
        if not isinstance(cols, list):
            errors.append((rel, "E1", "`Columns` 不是数组"))
            cols = []

        # E1 列类型
        seen = {}
        for i, c in enumerate(cols):
            if not isinstance(c, dict):
                continue
            fn_ = c.get("FieldName") or "<无 FieldName>"
            t = c.get("Type")
            if t is None:
                continue
            if t not in control_types:
                errors.append((rel, "E1",
                    "Columns[%d] `%s` 的 Type=`%s` 不在 ControlType 枚举内 ⇒ "
                    "**整份配置会静默变空**。合法值：%s"
                    % (i, fn_, t, ", ".join(sorted(control_types)))))
            # E5 重复字段
            if fn_ in seen:
                errors.append((rel, "E5", "FieldName `%s` 重复（第 %d、%d 项）" % (fn_, seen[fn_], i)))
            seen[fn_] = i

        # E2 搜索控件类型
        for i, s in enumerate(cfg.get("SearchFields") or []):
            if not isinstance(s, dict):
                continue
            ct = s.get("ControlType")
            if ct is not None and ct not in VALID_SEARCH_CONTROL_TYPES:
                errors.append((rel, "E2",
                    "SearchFields[%d] `%s` 的 ControlType=`%s` 越界（合法：%s）⇒ 前端静默回落 text"
                    % (i, s.get("Field"), ct, ", ".join(sorted(VALID_SEARCH_CONTROL_TYPES)))))

        # E3 EnableField —— 铁律九：启用/禁用唯一字段是 `IsValid`。
        #    ⚠️ 合法例外：实体若**只继承 BaseEntity**（未实现 IIsValid，如 ValidationRule），
        #       它根本没有 IsValid 列，此时 `IsActive` 才是正确值（业务开关）。
        #       空字符串 = 该表没有启用概念（如日志表），也放过。
        ef = cfg.get("EnableField")
        if ef is not None and ef not in ("IsValid", "IsActive", ""):
            errors.append((rel, "E3", "EnableField=`%s`，只允许 `IsValid` / `IsActive`（见铁律九）" % ef))
        elif ef == "IsActive":
            notes.append((rel, "N1", "EnableField=`IsActive` —— 仅当实体未实现 IIsValid 时才合法，请人工确认"))

        # E6 路径登记
        if os.path.isdir(CORE_ROOT) and path.startswith(CORE_ROOT):
            pass
        else:
            covered = any(path.startswith(r + os.sep) or os.path.dirname(path) == r
                          or r in path for r in CFG_ROOTS)
            if not covered:
                errors.append((rel, "E6", "该配置目录未被 BusinessEntityConfigPaths 覆盖 ⇒ 不会被加载"))

    # ── 输出 ──
    if not quiet:
        print("EntityConfig 审计：%d 个文件 / %d 个配置根目录" % (len(files), len(roots)))
        print("ControlType 合法值：%s" % ", ".join(sorted(control_types)))

    if errors:
        print("")
        print("✗ 发现 %d 处违规：" % len(errors))
        for rel, code, msg in errors:
            print("  [%s] %s" % (code, rel))
            print("        %s" % msg)
        print("")
        print("★ 提醒：E1 违规会让**整份配置静默变空**（页面有数据行但一列都不显示、零报错）。")
        print("  修法二选一：① 改用合法枚举值；② 若该值确实需要，先加到")
        print("  src/yzh-core/YZH.Core.Stand/Enums/ControlType.cs 再改 JSON。")
        return 1

    if notes and not quiet:
        print("")
        print("ℹ %d 处提示（不算违规）：" % len(notes))
        for rel, code, msg in notes:
            print("  [%s] %s —— %s" % (code, rel, msg))

    if not quiet:
        print("✓ 通过：无违规（%d 个文件）" % len(files))
    return 0


if __name__ == "__main__":
    sys.exit(main())
