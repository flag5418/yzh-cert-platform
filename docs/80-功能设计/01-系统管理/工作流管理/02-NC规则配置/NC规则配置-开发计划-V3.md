# NC 检查项配置 — 开发计划 V3（极简版）

> **日期**：2026-08-16
> **版本**：V3 | **状态**：待审核
> **前置文档**：
> - 数据库设计：`docs/20-架构决策/数据库表设计-V2.md` §3.2 A-10/A-03
> - 标准约束：`docs/60-AI工程设计/YZH-知识库/03-边界与约束/ISO体系认证NC与报告标准约束-V1.md`
> - 功能总览：`docs/80-功能设计/01-系统管理/工作流管理/01-核心引擎/审核规则库与工作流设计器-功能设计-V4.md`
>
> **设计原则**：极简，只做核心配置；执行逻辑由工作流引擎承载，不在配置页面绑死
>
> **与 V1/V2 的差异**：
> - V1：全功能 CRUD + 工作流 JSON 编辑器（过度设计）
> - V2：4 字段极简（中文名、英文名、启用、备注），缺少条款关联
> - **V3**：5 字段极简（中文名、英文名、启用、备注 + 关联条款选择），新增 ISO 条款引入机制

---

## 一、设计依据

### 1.1 ISO 标准约束（摘要）

| 约束 | 标准依据 | 设计决策 |
|------|---------|---------|
| NC 必须可追溯到标准条款 | ISO 17021-1 §9.4.4.2 | 检查项创建时**引入关联条款**（从 `cert_iso_clause` 选择） |
| NC 区分严重度（major/minor/observation） | ISO 17021-1 §9.4.4.2 | 严重度由**工作流引擎动态决定**（如"完全缺失=major，部分缺失=minor"），配置页面不填写 |
| NC 应有描述模板 | ISO 17021-1 §9.4.4.2 | 字段保留在表中，允许为空，后期填充 |
| 配置与执行分离 | 设计理念 | 配置页面只维护检查项清单，执行逻辑（提取→比对→判定）由工作流 DAG 承载 |

> 完整标准约束详见：`YZH-知识库/03-边界与约束/ISO体系认证NC与报告标准约束-V1.md`

### 1.2 关键设计决策

| # | 决策 | 理由 |
|---|------|------|
| 1 | 建立独立的 ISO 条款表（`cert_iso_clause`）| 一个 ISO 标准涉及多个条款（如 ISO 9001 有 10 章 40+ 条款），条款是 NC 的审核依据 |
| 2 | NC 检查项创建时引入条款 | ISO 17021 要求每个 NC 可追溯到具体条款，配置阶段就应建立关联 |
| 3 | 严重度不在配置页填写 | 同一条检查项在不同情况下可能产生不同严重度（缺失=major，不完整=minor），由工作流引擎条件输出 |
| 4 | 工作流配置不在配置页操作 | 工作流 DAG 是执行逻辑，由独立的工作流设计器配置，引擎回写 |

---

## 二、ISO 条款表（`cert_iso_clause`）

### 2.1 表结构（已存在，无需新建）

| 字段 | 类型 | 说明 |
|------|------|------|
| id | bigint | PK |
| code | varchar(36) | UUID，表间关联键 |
| standard_code | varchar(36) | 所属标准（关联 `cert_iso_standard.code`） |
| parent_code | varchar(36) | 父条款（树形结构，如 7 的子条款是 7.1） |
| clause_number | varchar(20) | 条款编号（如 6.1、7.1.1） |
| title | varchar(200) | 条款标题（如"资源提供"） |
| description | text | 条款原文或摘要 |
| sort_order | int | 排序 |
| + 审计字段 | | |

### 2.2 条款数据示例（ISO 9001:2015）

```
ISO 9001
├── 4 组织环境
│   ├── 4.1 理解组织及其环境
│   ├── 4.2 理解相关方的需求和期望
│   ├── 4.3 确定管理体系的范围
│   └── 4.4 管理体系及其过程
├── 5 领导作用
│   ├── 5.1 领导作用和承诺
│   ├── 5.2 方针
│   └── 5.3 组织的角色、职责和权限
├── 6 策划
│   ├── 6.1 应对风险和机遇的措施
│   ├── 6.2 质量目标及其实现的策划
│   └── 6.3 变更的策划
├── 7 支持
│   ├── 7.1 资源
│   │   ├── 7.1.1 总则
│   │   ├── 7.1.2 人员
│   │   ├── 7.1.3 基础设施
│   │   ├── 7.1.4 过程运行环境
│   │   ├── 7.1.5 监视和测量资源
│   │   └── 7.1.6 组织的知识
│   ├── 7.2 能力
│   ├── 7.3 意识
│   ├── 7.4 沟通
│   └── 7.5 成文信息
├── 8 运行
│   ...
├── 9 绩效评价
│   ...
└── 10 改进
    ...
```

### 2.3 条款管理方式

- 条款数据通过 SQL 脚本批量导入（`scripts/db/` 下）
- 前端不单独建条款管理页面，条款作为 NC 检查项的引入源
- 条款按 `standard_code` 过滤，按 `parent_code` 构建树形结构

---

## 三、NC 检查项配置页面

### 3.1 页面布局

```
┌─────────────────────────────────────────────────────────┐
│  NC检查项配置                                            │
├──────────┬──────────────────────────────────────────────┤
│          │  [当前: CB-001 / ISO9001 / 一阶段]            │
│ 树形     ├──────────────────────────────────────────────┤
│ 导航     │  ┌─ NC检查项列表 ─────────────────────┐       │
│          │  │ 名称 | 英文 | 条款 | 启用 | 操作  │       │
│ 机构     │  │ 资源提供 | Res.. | 7.1 | 是 | 编辑│       │
│ ├─标准   │  │ 能力意识 | Com.. | 7.2 | 是 | 编辑│       │
│ │ ├─阶段 │  │ 文件记录 | Doc.. | 7.5 | 否 | 编辑│       │
│ │        │  └──────────────────────────────────┘       │
│          │  [新建检查项]                                │
└──────────┴──────────────────────────────────────────────┘
```

**左侧树**：机构 → 标准 → 阶段（三级，懒加载）
**右侧列表**：选中阶段节点后，显示该 org+standard+phase 下的所有 NC 检查项

### 3.2 列表表格列

| 列 | 字段 | 宽度 | 说明 |
|----|------|------|------|
| 中文名称 | ruleName | 200 | |
| 英文名称 | ruleNameEn | 150 | |
| 关联条款 | clauseNumber | 100 | 从 clause_code JOIN cert_iso_clause 显示条款编号 |
| 启用 | isActive | 80 | Tag 标签 |
| 操作 | - | 120 | 编辑 / 删除 |

### 3.3 新建/编辑弹窗

```
┌─ 新建NC检查项 ────────────────────────────┐
│                                            │
│  中文名称*: [资源提供检查            ]      │
│  英文名称:  [Resource Provision     ]      │
│                                            │
│  关联条款*: [7.1 资源提供          ▼]      │
│            （从 cert_iso_clause 树形选择）   │
│                                            │
│  是否启用:  [是 ▼]                          │
│  备注:     [                            ]  │
│                                            │
│         [取消]  [保存]                     │
└────────────────────────────────────────────┘
```

### 3.4 弹窗字段对照

| 字段 | 控件 | 必填 | 宽度 | 说明 |
|------|------|------|------|------|
| ruleName | el-input | 是 | 200 | 中文名称 |
| ruleNameEn | el-input | 否 | 200 | 英文名称 |
| clauseCode | el-tree-select | 是 | — | 从 ISO 条款表树形选择，选中后展示 `条款编号 + 标题` |
| isActive | el-switch | 否 | — | 默认启用 |
| remark | el-input textarea | 否 | 2行 | 备注 |

### 3.5 条款选择控件

- 使用 `el-tree-select`（Element Plus 树形选择器）
- 数据源：调用 API 获取 `cert_iso_clause` 中 `standard_code` = 当前选中标准的条款树
- 显示格式：`条款编号 + 条款标题`（如 `7.1 资源提供`）
- 选中后存储 `clause_code`（关联条款的 code 字段）

### 3.6 隐含自动带入字段

以下字段不在弹窗中展示，由树形导航选中节点后自动带入：

| 字段 | 来源 |
|------|------|
| orgCode | 树形导航选中的机构节点 |
| standardCode | 树形导航选中的标准节点 |
| phaseCode | 树形导航选中的阶段节点 |
| code | 后端自动生成（UUID） |
| ruleCode | 后端自动生成（如 `NC-{standardCode}-{clauseNumber}-{seq}`） |

### 3.7 表中保留但当前不操作的字段

| 字段 | 原因 |
|------|------|
| severityIfViolated | 严重度由工作流引擎动态决定，配置页不填写 |
| ncDescriptionTemplate | 后期填充，当前允许为空 |
| workflowCode | 工作流引擎回写，配置页不操作 |
| ruleJson | 工作流 DAG JSON，引擎回写 |

---

## 四、数据库变更

### 4.1 现有表确认

`cert_validation_rule` 表无需新增列，现有字段完全满足：

| 字段 | 类型 | 当前状态 | 用途 |
|------|------|---------|------|
| clause_code | varchar(36) | 已存在，允许 NULL | 当前改为 NOT NULL（检查项必须关联条款） |

### 4.2 DDL 脚本

```sql
-- 1. clause_code 改为 NOT NULL（检查项必须关联条款）
-- 注意：先确认现有数据中 clause_code 不为空，否则先更新
UPDATE cert_validation_rule SET clause_code = 'unknown' WHERE clause_code IS NULL OR clause_code = '';
ALTER TABLE cert_validation_rule MODIFY COLUMN clause_code VARCHAR(36) NOT NULL COMMENT '关联条款编码(cert_iso_clause.code)';

-- 2. 录入 ISO 9001:2015 标准条款数据（见 scripts/db/ 下 SQL 脚本）
-- 此脚本批量插入 cert_iso_clause 表的 ISO 9001 全部条款
```

### 4.3 实体变更

`ValidationRule.cs` 当前 `ClauseCode` 已有 `[Required]` 特性，无需修改。

---

## 五、后端 API

### 5.1 现有 API（需改造）

| 方法 | 路由 | 当前状态 | 改造 |
|------|------|---------|------|
| POST | `api/validation-rule/page` | 已有 | 返回数据中 JOIN 显示 clause_number（条款编号） |
| POST | `api/validation-rule` | 已有 | SaveAsync 补充 Code/RuleCode 自动生成 |
| POST | `api/validation-rule/delete/{id}` | 已有 | 无需改动 |

### 5.2 新增 API

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `api/iso-clause/tree?standardCode={code}` | 获取指定标准的条款树形数据（供弹窗选择） |

### 5.3 Service 改造

`ValidationRuleService.cs`：

1. **GetPageDataAsync**：查询时 LEFT JOIN `cert_iso_clause`，返回 `clauseNumber`（条款编号）和 `clauseTitle`（条款标题）用于列表展示
2. **SaveAsync**：
   - 新建时自动生成 `Code = Guid.NewGuid().ToString("N")`
   - 新建时自动生成 `RuleCode`（格式：`NC-{standardCode}-{clauseNumber}-{seq}`）
   - `OrgCode`/`StandardCode`/`PhaseCode` 从前端传入（树形导航带入）

---

## 六、前端改造要点

### 6.1 现有 `List.vue` 改造

| # | 改造项 | 说明 |
|---|--------|------|
| 1 | 简化列表列 | 移除 ruleCode/standardCode/phaseCode/workflowCode/severityIfViolated 列，只保留 5 列 |
| 2 | 列表新增条款列 | `clauseNumber` 从后端 JOIN 返回的数据中读取 |
| 3 | 简化编辑弹窗 | 移除 standardCode/phaseCode/clauseCode(手填)/workflowCode/severityIfViolated/ncDescriptionTemplate/ruleJson 等输入项 |
| 4 | 新增条款选择控件 | 用 `el-tree-select` 替代原来的 `clauseCode` 手填 input |
| 5 | 移除 `loadWorkflowList()` | 不再加载工作流列表 |
| 6 | 移除工作流跳转按钮 | 列表操作列移除"工作流"按钮 |
| 7 | 移除复制功能 | 列表操作列移除"复制"按钮（极简模式不需要） |

### 6.2 条款选择组件

```vue
<el-tree-select
  v-model="editForm.clauseCode"
  :data="clauseTreeData"
  :props="{ label: 'label', value: 'code', children: 'children' }"
  filterable
  check-strictly
  placeholder="选择关联条款"
  style="width: 100%"
/>
```

### 6.3 API 调用

```js
// 加载条款树
async function loadClauseTree() {
  if (!currentFilter.standardCode) return
  const res = await proxy.http.get(
    `api/iso-clause/tree?standardCode=${currentFilter.standardCode}`, null, false
  )
  if (res?.status) {
    clauseTreeData.value = res.data || []
  }
}
```

---

## 七、开发步骤

### 步骤 1：数据库（0.5h）

- 执行 DDL：`clause_code` 改为 NOT NULL
- 执行 SQL：批量导入 ISO 9001:2015 标准条款数据到 `cert_iso_clause`

### 步骤 2：后端（1h）

- 新增 API 端点：`GET api/iso-clause/tree`
- 改造 `ValidationRuleService.GetPageDataAsync`：JOIN 条款表返回 clauseNumber
- 改造 `ValidationRuleService.SaveAsync`：补充 Code/RuleCode 自动生成

### 步骤 3：前端 List.vue 改造（1.5h）

- 简化列表列（5 列）
- 简化编辑弹窗（5 字段 + 条款选择器）
- 移除 workflowList 加载和工作流跳转
- 新增条款树形选择控件

### 步骤 4：联调 + 测试数据（1h）

- 新建 → 列表 → 编辑 → 删除全流程
- 录入 3-5 条检查项：
  - ISO9001/一阶段/7.1 资源提供/资源提供检查
  - ISO9001/一阶段/7.2 能力/能力意识检查
  - ISO9001/一阶段/7.5 成文信息/文件记录检查

---

## 八、验收标准

- [ ] 左侧树形导航正常加载（机构→标准→阶段）
- [ ] 选中阶段节点后右侧列表过滤显示
- [ ] 列表正确显示条款编号（JOIN 查询）
- [ ] 新建弹窗只有 5 个字段（中文名、英文名、条款选择、启用、备注）
- [ ] 条款选择器正确加载当前标准的条款树
- [ ] 保存时 orgCode/standardCode/phaseCode 从树节点自动带入
- [ ] 保存时 code/ruleCode 后端自动生成
- [ ] 编辑/删除正常
- [ ] 编译无错误（`dotnet build` + `npm run build`）

---

## 九、与报告配置模块的关系

```
cert_iso_clause（ISO 条款表）
  │
  ├──→ cert_validation_rule.clause_code（NC 检查项关联条款）
  │
  └──→ rpt_report_section.clause_code（报告章节关联条款）

两条线共用条款表，但配置独立：
- NC 检查项：审核过程中发现不符合 → 判定 NC
- 报告章节：审核完成后 → 组装报告内容
```
