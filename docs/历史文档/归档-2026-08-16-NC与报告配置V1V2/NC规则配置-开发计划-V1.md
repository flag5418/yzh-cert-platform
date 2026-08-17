# NC 规则配置 — 开发计划 V1

> **日期**：2026-08-16
> **关联**：审核规则库与工作流设计器-功能设计-V4-评审报告.md §五 第二阶段
> **目标**：NC 检查规则的全功能 CRUD + 规则配置 UI，形成独立菜单

---

## 一、现状盘点

### 1.1 已有资产

| 层 | 文件 | 状态 |
|----|------|------|
| 数据库表 | `cert_validation_rule` | 已建，字段齐全 |
| 后端实体 | `VOL.Entity/.../Cert/ValidationRule.cs` | 已建，13 个业务字段 |
| 后端 Service | `VOL.Builder/.../Partial/ValidationRuleService.cs` | 已实现 CRUD + Copy + ToggleActive |
| 后端 Controller | `VOL.WebApi/.../ValidationRuleController.cs` | 已实现 6 个 API 端点 |
| 前端页面 | `views/cert/Standard/WorkflowRules/List.vue` | 已有骨架（树+列表+编辑弹窗） |
| 路由 | `/CertPlatform/WorkflowRules/Rules` | 已注册 |

### 1.2 待整改问题

| # | 问题 | 影响 | 整改 |
|---|------|------|------|
| 1 | 前端编辑弹窗引用了 `workflowCode`（绑定 `wf_workflow_definition`），V4 已废弃该表 | 规则保存时 workflowCode 无意义 | 移除 workflowCode 选择器，改为 `ruleJson` JSON 编辑器 |
| 2 | `rule_json` 列已有但前端未展示/编辑 | 无法配置工作流 DAG | 新增 JSON 编辑器（CodeMirror） |
| 3 | 缺 `rpt_report_section.workflow_config` 列 | 报告章节无法存储工作流配置 | DDL 新增（P0，报告模块一起做） |
| 4 | `org_code` 列存在于实体但 V4 已从全局表移除 | 实体映射可能报错 | 确认已移除（`remove_orgcode_from_global_tables.sql` 已执行） |

---

## 二、数据库字段设计

### 2.1 `cert_validation_rule` 表字段（现有，无新增）

| 列名 | 类型 | 可空 | 默认 | 说明 |
|------|------|------|------|------|
| id | bigint | NO | AUTO | 主键 |
| code | varchar(36) | NO | UNI | UUID 业务编码 |
| ~~org_code~~ | varchar(50) | YES | | **已删除**（全局表移除） |
| standard_code | varchar(36) | NO | | 标准编码（如 ISO9001） |
| phase_code | varchar(36) | NO | | 阶段编码（如 first_stage） |
| clause_code | varchar(36) | NO | | 条款编码（如 6.1） |
| workflow_code | varchar(36) | NO | | 工作流编码（V4 后改为 rule_code 自身，不再关联 wf_workflow_definition） |
| rule_json | text | YES | | **工作流 DAG JSON**（图形化设计器导出的 workflow_config） |
| rule_code | varchar(50) | NO | UNI | 规则编码（唯一，如 VR-ISO9001-6.1-001） |
| rule_name | varchar(200) | NO | | 规则中文名（如"资源提供检查"） |
| rule_name_en | varchar(200) | YES | | 规则英文名 |
| severity_if_violated | varchar(20) | NO | | 违规等级：conformant/observation/minor/major |
| nc_description_template | text | YES | | NC 描述模板（如"组织未提供{evidence}来证实{requirement}"） |
| is_active | tinyint(1) | YES | 1 | 是否启用 |
| enable | tinyint(1) | NO | 1 | 框架软删标志 |
| status | varchar(50) | NO | active | 业务状态 |
| remark | varchar(500) | YES | | 备注 |
| + 审计字段 | | | | create_id/creator/create_date/modify_*/delete_* |

### 2.2 DDL 整改

无需新增 DDL。但需确认 `org_code` 列已从表中移除：

```sql
-- 确认 org_code 已删除
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE table_schema=DATABASE() AND table_name='cert_validation_rule' AND column_name='org_code';
-- 预期：0 行
```

### 2.3 实体整改

`ValidationRule.cs` 中已移除 `OrgCode` 属性（跟随 YZHBaseEntity 移除 OrgCode 的全局整改）。需确认 Service 层不再引用 `entity.OrgCode`。

---

## 三、后端 API 设计

### 3.1 现有 API 端点（无需改动）

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `api/validation-rule/page` | 分页查询（支持 orgCode/standardCode/phaseCode 过滤） |
| GET | `api/validation-rule/list` | 列表查询 |
| GET | `api/validation-rule/{ruleCode}` | 详情查询 |
| POST | `api/validation-rule` | 保存（新增/编辑） |
| POST | `api/validation-rule/delete/{id}` | 删除 |
| POST | `api/validation-rule/toggle-active/{id}` | 启停切换 |
| POST | `api/validation-rule/copy/{sourceRuleCode}` | 复制规则 |

### 3.2 待新增 API

| 方法 | 路由 | 说明 | 优先级 |
|------|------|------|--------|
| POST | `api/validation-rule/validate-json` | 校验 ruleJson 格式 | P1（JSON 编辑器集成时） |
| POST | `api/validation-rule/test-run` | 试运行（注入 YZH-STD-ENT） | P2（工作流解释器就位后） |

### 3.3 Service 层整改

`ValidationRuleService.cs` 修改点：

1. **移除 OrgCode 引用**：`GetPageDataAsync` 和 `GetByOrgStandardPhaseAsync` 中的 `orgCode` 过滤参数保留但改为可选（兼容前端树形导航）
2. **SaveAsync 补充 RuleCode 自动生成**：新建时如未传 ruleCode，自动生成 `VR-{standardCode}-{clauseCode}-{seq}`
3. **SaveAsync 补充 Code 自动生成**：新建时 `entity.Code = Guid.NewGuid().ToString("N")`

---

## 四、前端 UI 设计

### 4.1 页面布局（改造现有 List.vue）

```
┌─────────────────────────────────────────────────────────┐
│  审核规则库                                              │
├──────────┬──────────────────────────────────────────────┤
│          │  [当前节点: ISO9001 / 一阶段]  [查询] [重置]  │
│ 树形     ├──────────────────────────────────────────────┤
│ 导航     │  ┌─ NC检查规则列表 ─────────┐  [新建规则]    │
│          │  │                          │                │
│ 机构     │  │ 编码 | 名称 | 条款 | 等级 │ 状态 | 操作    │
│ ├─标准   │  │ VR-..| 资源提供| 6.1 | 严重│ 启用 | 编辑.. │
│ │ ├─阶段 │  │ VR-..| 能力意识| 7.2 | 轻微│ 启用 | 编辑.. │
│ │ │      │  │                          │                │
│ │        │  └──────────────────────────┘                │
│          │                                              │
└──────────┴──────────────────────────────────────────────┘
```

### 4.2 编辑弹窗设计（改造现有弹窗）

```
┌─ 编辑NC规则 ────────────────────────────────────┐
│                                                  │
│  规则编码*: [VR-ISO9001-6.1-001    ]  自动生成   │
│  规则名称*: [资源提供检查                      ]  │
│  英文名称:  [Resource Provision Check          ]  │
│                                                  │
│  ┌─ Row ──────────────────────────────────────┐  │
│  │ 标准编码*: [ISO9001      ]  条款编码*: [6.1]  │  │
│  │ 阶段编码*: [first_stage  ]  违规等级*: [严重▼] │  │
│  └────────────────────────────────────────────┘  │
│                                                  │
│  NC描述模板:                                     │
│  ┌────────────────────────────────────────────┐  │
│  │ 组织未提供{evidence}来证实{requirement}的   │  │
│  │ 实施情况。                                  │  │
│  └────────────────────────────────────────────┘  │
│                                                  │
│  ── 工作流配置 ──                                │
│  ┌─ Tab: JSON编辑器 │ Tab: 图形化设计器 ──┐     │
│  │                                          │     │
│  │ {                                        │     │
│  │   "version": 1,                          │     │
│  │   "nodes": [...],                       │     │
│  │   "edges": [...]                         │     │
│  │ }                                        │     │
│  │                                          │     │
│  │  [校验JSON]                              │     │
│  └──────────────────────────────────────────┘     │
│                                                  │
│  备注: [                                     ]   │
│                                                  │
│                    [取消]  [保存]                 │
└──────────────────────────────────────────────────┘
```

### 4.3 字段-控件对照表

| 字段 | 控件 | 必填 | 宽度 | 说明 |
|------|------|------|------|------|
| ruleCode | el-input | 是 | 50 | 自动生成，可编辑 |
| ruleName | el-input | 是 | 200 | 中文名 |
| ruleNameEn | el-input | 否 | 200 | 英文名 |
| standardCode | el-select | 是 | - | 从 ISOStandard 表加载 |
| phaseCode | el-select | 是 | - | 从 PhaseDefinition 表加载 |
| clauseCode | el-input | 是 | 36 | 条款编码（如 6.1） |
| severityIfViolated | el-select | 是 | - | conformant/observation/minor/major |
| ncDescriptionTemplate | el-input textarea | 否 | 3行 | NC 描述模板，支持 {field_code} 占位符 |
| ruleJson | CodeMirror JSON Editor | 否 | - | 工作流 DAG JSON（Tab 页签切换 JSON/图形化） |
| remark | el-input textarea | 否 | 2行 | 备注 |

### 4.4 列表表格列

| 列 | 字段 | 宽度 | 说明 |
|----|------|------|------|
| 规则编码 | ruleCode | 180 | |
| 规则名称 | ruleName | 200 | |
| 英文名称 | ruleNameEn | 150 | |
| 标准 | standardCode | 100 | |
| 阶段 | phaseCode | 80 | |
| 条款 | clauseCode | 80 | |
| 违规等级 | severityIfViolated | 80 | Tag 标签着色 |
| 状态 | isActive | 70 | 启用/禁用 Tag |
| 操作 | - | 220 | 编辑/复制/删除/启停 |

### 4.5 树形导航

层级：机构 → 标准 → 阶段
- 树节点点击后过滤列表
- 显示规则数量 Tag
- 懒加载子节点

---

## 五、开发步骤

### 步骤 1：数据库确认（0.5h）

- 确认 `org_code` 列已从 `cert_validation_rule` 删除
- 确认 `rule_json` 列存在且类型为 TEXT
- 无需新增 DDL

### 步骤 2：后端 Service 整改（1h）

- `ValidationRuleService.cs`：
  - SaveAsync 补充 Code/RuleCode 自动生成
  - 移除 OrgCode 引用（如果还有的话）
  - GetPageDataAsync 的 orgCode 参数改为可选（空则不过滤）
- `ValidationRuleController.cs`：无需改动

### 步骤 3：前端 List.vue 改造（2h）

- 移除编辑弹窗中的 `workflowCode` 选择器
- 移除 `loadWorkflowList()` 函数和 `workflowList` 引用
- 新增 `ruleJson` 的 textarea（临时，后续替换为 CodeMirror）
- 编辑弹窗字段布局优化（Row/Col 分行）
- 树形导航数据源适配（移除 org_code 过滤）

### 步骤 4：前端路由 + 菜单确认（0.5h）

- 路由已注册：`/CertPlatform/WorkflowRules/Rules`
- 确认菜单表数据库记录：`MenuUrl` 指向正确路由

### 步骤 5：前后端联调（1h）

- 新建规则 → 保存 → 列表刷新
- 编辑规则 → 保存 → 数据更新
- 删除规则 → 确认 → 列表刷新
- 复制规则 → 确认 → 新增副本
- 启停切换 → 状态更新
- 树形导航过滤

### 步骤 6：测试数据录入（0.5h）

录入 3-5 条 NC 规则：
- ISO9001 / 一阶段 / 6.1 资源提供 / 严重不符合
- ISO9001 / 一阶段 / 7.2 能力意识 / 轻微不符合
- ISO9001 / 一阶段 / 8.2 产品和服务要求 / 观察项

---

## 六、验收标准

- [ ] NC 规则列表正常分页显示
- [ ] 树形导航过滤正常
- [ ] 新建规则保存成功（Code/RuleCode 自动生成）
- [ ] 编辑规则保存成功
- [ ] 删除规则确认后成功删除
- [ ] 复制规则生成副本（名称加"（副本）"后缀）
- [ ] 启停切换正常
- [ ] `ruleJson` 字段可编辑保存
- [ ] 编译无错误（`dotnet build` + `npm run build`）
