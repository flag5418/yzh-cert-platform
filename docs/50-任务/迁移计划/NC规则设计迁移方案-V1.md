# NC 规则设计迁移方案

> 创建时间：2026-09-15 | 更新：2026-09-15（TreeTable 模式分析）
> 关联路由：`/business/nc-config`（菜单 ID 212）
> 状态：**骨架完成，后端未建**

---

## 1. 功能概述

NC（不合格项）规则设计页面用于配置检查项，定义哪些 ISO 条款需要被检查，以及检查规则的名称、启用状态等。数据存储在 `cert_validation_rule` 表。

---

## 2. TreeTable 模式分析

### 2.1 标准 TreeTable 模式（参考 Organization / ISO Standard）

```
后端：TreeTableControllerBase<T, V>
  T = 树实体（实现 ITreeEntity：Code, ParentCode, IsLeaf）
  V = 表实体（右表数据）
  配置：TreeConfig（NameField, RelateField, MaxLevel...）
  API：/tree/root, /tree/children, /tree/add, /filter, /add, /update, /delete

前端：TreeTableLogic + YzhTreeTableLayout + YzhTable
  左树：YzhTree（el-tree 封装，支持搜索/节点操作/懒加载）
  右表：YzhTable（分页/搜索/排序/行操作）
  联动：选中树节点 → 自动注入 RelateField 过滤右表
```

### 2.2 NC Config 的树结构差异

| 维度 | Organization（标准 TreeTable） | NC Config（实际需求） |
|------|-------------------------------|---------------------|
| **树实体** | 单一实体 `Sys_Organization` | **复合树**：组织 → 标准 → 阶段（3 张表） |
| **树层级** | 数据库父子关系（ParentCode） | 逻辑层级（Org→Std 无 FK，Std→Phase 无 FK） |
| **RelateField** | `OrgCode`（单一字段） | `OrgCode` + `StandardCode` + `PhaseCode`（三个字段） |
| **TreeTableControllerBase** | ✅ 直接适用 | ⚠️ 不直接适用（需要单树实体） |

### 2.3 推荐方案：复用 useFileTree + 自定义右表

由于 NC Config 的树是**复合树**（Org→Std→Phase），无法直接使用 `TreeTableControllerBase<T,V>`（要求单树实体 T）。

**推荐**：复用 `useFileTree` composable（与 ReportDef 相同模式），后端提供：
- `/api/ValidationRule/filter` — 分页查询（接收 OrgCode/StandardCode/PhaseCode 过滤）
- `/api/ValidationRule/save` — 新建/编辑
- `/api/ValidationRule/delete` — 删除
- `/api/ValidationRule/toggle-active` — 切换启用
- `/api/ValidationRule/copy` — 深拷贝

前端使用 `YzhTable`（分页表格）+ `useFileTree`（左树），不使用 `YzhTreeTableLayout` 组件。

---

## 3. 架构对比

| 维度 | 旧架构（Vol 框架） | 新架构（YZH.Core） | 差异 |
|------|-------------------|-------------------|------|
| **前端页面** | `NCConfig/index.vue`（1600+ 行，LogicFlow 工作流设计器） | `nc-config/index.vue`（261 行，基础 CRUD 表格） | ⚠️ 旧版是完整工作流设计器，新版仅基础 CRUD |
| **前端 API** | Vol 自动注册 | `nc-config.ts`（手写，调用旧后端路由） | ⚠️ API 指向旧后端 |
| **后端控制器** | `ValidationRuleController.cs`（78 行，Vol 风格） | **未建** | ❌ |
| **后端实体** | `ValidationRule.cs`（54 行，继承 EntityBase） | **未建** | ❌ |
| **后端服务** | `ValidationRuleService.cs`（168 行，含 JOIN 查询、自动编号） | **未建** | ❌ |
| **DB 表** | `cert_validation_rule`（已存在） | 同左 | ✅ 无需迁移 |

---

## 4. 设计决策（V3）

根据 `docs/20-体系认证/03-详细设计/01-系统管理/工作流管理/02-NC规则配置/NC规则配置-开发计划-V3.md`：

- **V3 定位**：5 字段极简配置页（ruleName, ruleNameEn, clauseCode, isActive, remark）
- **严重级别**：由工作流引擎决定，不在配置页设置
- **工作流配置**：由独立的工作流设计器管理，本页不涉及
- **结论**：当前新前端骨架符合 V3 设计，**无需移植旧版工作流设计器**

---

## 5. 当前问题

### 5.1 后端缺失
- 无 `ValidationRuleController`（新架构）
- 无 `ValidationRule` 实体（新架构）
- 前端 API（`/api/ValidationRule/*`）指向旧后端，无法独立运行

### 5.2 前端硬编码
- 机构下拉：硬编码 `CB-001`，应从 API 动态加载
- 标准下拉：硬编码 ISO9001/14001/45001，应从 `cert_iso_standard` 加载
- 阶段下拉：硬编码 `phase1/phase2`，应从 `cert_cert_stage` 加载
- `editForm` 使用 camelCase（`ruleName`），但后端 JSON 序列化为 PascalCase，需统一

### 5.3 布局问题
- 当前为平铺表格 + 筛选下拉，缺少左树右表结构
- 用户要求采用标准 TreeTable 模式（左树右表）

---

## 6. 迁移范围

### Phase 1：后端 CRUD

| 层 | 文件 | 说明 |
|----|------|------|
| **Entity** | `CertPlatform.Shared/Entities/Cert/ValidationRule.cs` | 继承 `BaseEntity`，映射 `cert_validation_rule` |
| **Controller** | `CertPlatform.Admin/Controllers/Cert/ValidationRuleController.cs` | 继承 `YzhControllerBase<ValidationRule>`，自定义 filter 实现 JOIN |

后端 Controller 需要额外实现：
- `Filter` — 重写分页查询，JOIN `cert_iso_clause` 返回 `ClauseNumber`/`ClauseTitle`，支持 OrgCode/StandardCode/PhaseCode 多字段过滤
- `ToggleActive` — 切换 `IsActive` 状态
- `Copy` — 深拷贝规则（生成新 GUID + 新 RuleCode）

### Phase 2：前端改造（useFileTree + YzhTable）

| 改动 | 说明 |
|------|------|
| **布局重构** | 左侧：`useFileTree`（Org→Std→Phase 三级树），右侧：`YzhTable`（规则表格） |
| **API 路由修正** | 所有函数改为调用新后端 `/api/ValidationRule/*` |
| **树→表格联动** | 选中阶段节点后，自动注入 OrgCode/StandardCode/PhaseCode 过滤 |
| **字段命名 PascalCase** | `editForm` 的 reactive 属性和 `YzhForm` 的 `prop` 统一 PascalCase |
| **EntityConfig JSON** | 创建 `Cert/ValidationRule.json` 定义表格列和表单字段 |

### Phase 3（可选）：工作流设计器

旧版 `NCConfig/index.vue` 包含完整的 LogicFlow 工作流设计器（1600+ 行），V3 设计明确不移植。

---

## 7. 前端改造详细设计

### 7.1 布局结构（参考 ReportDef + ISO Standard）

```
┌─────────────────────────────────────────────────────────┐
│  NC 规则设计                                              │
├──────────┬──────────────────────────────────────────────┤
│ 左树      │ 右表                                          │
│ (useFileTree) │                                              │
│          │ ┌──────────────────────────────────────────┐ │
│ 组织      │ │ 筛选：关键词 │ 条款 │ 启用 │ 查询 │ 重置  │ │
│  └ 标准   │ ├──────────────────────────────────────────┤ │
│    └ 阶段 │ │ 工具栏：新建检查项 │ 批量删除 │ 刷新     │ │
│      ├ AP │ │ ┌──────────────────────────────────────┐ │ │
│      ├ CR │ │ │ 中文名称 │ 英文名称 │ 条款 │ 启用 │ 操作│ │ │
│      ├ SP │ │ │          │          │     │     │     │ │ │
│      └ ...│ │ └──────────────────────────────────────┘ │ │
│          │ │ 分页：Total X │ < 1 > │ 20/page           │ │
│          │ └──────────────────────────────────────────┘ │
└──────────┴──────────────────────────────────────────────┘
```

### 7.2 文件结构

```
src/certplatform-web/cert/cert-admin/src/pages/workflow/nc-config/
  ├── index.vue          # 页面布局（左树右表）
  └── logic.ts           # 业务逻辑（TreeTable 模式）

src/certplatform-web/cert/cert-share/src/
  ├── api/workflow/nc-config.ts  # API 函数（改为调用新后端）
  └── composables/useFileTree.ts # 复用（Org→Std→Phase 复合树）

src/certplatform-api/CertPlatform.Shared/Assets/EntityConfigs/Cert/
  └── ValidationRule.json        # 表格列 + 表单字段配置
```

### 7.3 树→表格联动逻辑

```typescript
// 选中阶段节点后，注入三个过滤条件
async function handleNodeClick(node: TreeNode) {
  selectedPhase.value = node
  // YzhTable 刷新时自动注入过滤
  tableRef.value?.refresh()
}

// dataLoader 中构建过滤
function buildFilters(): FilterItem[] {
  const phase = selectedPhase.value
  if (!phase) return []
  return [
    { Field: 'OrgCode', Value: phase.orgCode, Operator: 'eq' },
    { Field: 'StandardCode', Value: phase.stdCode, Operator: 'eq' },
    { Field: 'PhaseCode', Value: phase.phaseDefinitionCode, Operator: 'eq' },
  ]
}
```

---

## 8. DB 表结构参考

```sql
CREATE TABLE cert_validation_rule (
  Id bigint NOT NULL AUTO_INCREMENT,
  Code varchar(36) NOT NULL,           -- UUID（全局唯一）
  OrgCode varchar(50),
  -- 审计字段...
  StandardCode varchar(36) NOT NULL,   -- FK → cert_iso_standard.Code
  PhaseCode varchar(36) NOT NULL,       -- FK → cert_phase_definition.code
  ClauseCode varchar(36) NOT NULL,      -- FK → cert_iso_clause.Code
  WorkflowCode varchar(36),             -- FK → wf_workflow_definition.Code
  RuleCode varchar(50) NOT NULL,        -- 唯一编号（如 NC-ISO9001-001）
  RuleName varchar(200) NOT NULL,       -- 中文名称
  RuleNameEn varchar(200),              -- 英文名称
  SeverityIfViolated enum('major','minor','observation'),
  NcDescriptionTemplate text,
  IsActive tinyint(1) DEFAULT '1',
  Remark varchar(500),
  PRIMARY KEY (Id),
  UNIQUE KEY uk_code (Code),
  UNIQUE KEY uk_rule_code (RuleCode)
);
```

---

## 9. 旧后端核心逻辑（参考）

### 9.1 自动编号生成（ValidationRuleService.SaveAsync）
```csharp
var seq = await _repository.CountAsync(x => x.StandardCode == entity.StandardCode) + 1;
entity.RuleCode = $"NC-{entity.StandardCode}-{seq:D3}";
entity.Code = Guid.NewGuid().ToString();
```

### 9.2 分页查询 JOIN（ValidationRuleService.GetPageDataAsync）
```csharp
var query = from r in rules
            join c in clauses on r.ClauseCode equals c.Code into clauseJoin
            from c in clauseJoin.DefaultIfEmpty()
            select new { r, c.ClauseNumber, c.ClauseTitle };
```

---

## 10. 开发步骤

1. 创建 `ValidationRule.cs` 实体（继承 BaseEntity，映射 cert_validation_rule）
2. 创建 `ValidationRuleController.cs`（继承 YzhControllerBase，重写 Filter 实现 JOIN 查询）
3. 创建 `ValidationRule.json` EntityConfig（表格列 + 表单字段定义）
4. 前端 `nc-config.ts` API 改为调用新后端路由
5. 前端 `index.vue` 重构为左树右表布局（useFileTree + YzhTable）
6. 前端 `logic.ts` 创建业务逻辑类（参考 ISOStandardTreeTableLogic）
7. 树→表格联动：选中阶段节点自动注入三字段过滤
8. 编译验证 + 功能测试

---

## 11. 注意事项

- `cert_validation_rule.ClauseCode` 为 NOT NULL（已由 `phase7_nc_clause_config.sql` 迁移）
- `SeverityIfViolated` 字段为 ENUM 类型，V3 设计决定不在配置页设置
- `WorkflowCode` 可为空（V3 不涉及工作流配置）
- 前端 `editForm` 必须使用 PascalCase 以匹配后端 `PropertyNamingPolicy = null`
- 复合树（Org→Std→Phase）不适用 `TreeTableControllerBase`，使用 `useFileTree` + 自定义 Controller

---

## 12. 验收标准

- [ ] 左树正确加载：组织 → 标准 → 阶段 三级树
- [ ] 右表联动：选中阶段后，表格自动过滤该阶段下的规则
- [ ] 新建检查项：输入名称、选择条款、保存成功
- [ ] 编辑检查项：修改名称/条款、保存成功
- [ ] 删除检查项：确认后删除成功
- [ ] 切换启用：Switch 切换成功
- [ ] 分页查询：按关键词/条款筛选，分页正常
- [ ] 编译通过：`dotnet build` 无错误
