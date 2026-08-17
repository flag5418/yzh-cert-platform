# NC 检查项配置 — 开发计划 V2（极简版）

> **日期**：2026-08-16
> **原则**：极简，只做核心配置；工作流配置等图形化设计器就位后由引擎回写

---

## 一、页面布局

```
┌─────────────────────────────────────────────────────────┐
│  NC检查项配置                                            │
├──────────┬──────────────────────────────────────────────┤
│          │  [当前: CB-001 / ISO9001 / 一阶段]            │
│ 树形     ├──────────────────────────────────────────────┤
│ 导航     │  ┌─ NC检查项列表 ──────────┐  [新建检查项]   │
│          │  │ 中文名称 | 英文 | 启用 | 操作  │          │
│ 机构     │  │ 资源提供 | Res.. | 是 | 编辑删│           │
│ ├─标准   │  │ 能力意识 | Com.. | 是 | 编辑删│           │
│ │ ├─阶段 │  │ 文件记录 | Doc.. | 否 | 编辑删│           │
│ │ │      │  └────────────────────────┘                │
│ ├─标准   │                                              │
│ 机构     │                                              │
└──────────┴──────────────────────────────────────────────┘
```

**左侧树**：机构 → 标准 → 阶段（三级，懒加载）
**右侧列表**：选中节点后，显示该 org+standard+phase 下的所有 NC 检查项

---

## 二、新建/编辑弹窗

```
┌─ 新建NC检查项 ────────────────────────┐
│                                        │
│  中文名称*: [资源提供检查            ]  │
│  英文名称:  [Resource Provision     ]  │
│  是否启用:  [是 ▼]                      │
│  备注:     [                      ]    │
│             [                      ]    │
│                                        │
│         [取消]  [保存]                 │
└────────────────────────────────────────┘
```

**只有 4 个字段**。其余字段（standard_code/phase_code/clause_code/severity/rule_json 等）表中保留、允许为空，当前阶段前端不操作。

---

## 三、数据库表 `cert_validation_rule`

不新增不修改列。当前需要使用的字段：

| 字段 | 用途 | 当前阶段 |
|------|------|---------|
| code | UUID | 后端自动生成 |
| org_code | 机构编码 | **树节点选中时自动带入** |
| standard_code | 标准编码 | **树节点选中时自动带入** |
| phase_code | 阶段编码 | **树节点选中时自动带入** |
| rule_code | 规则编码 | 后端自动生成 |
| rule_name | 中文名称 | **前端编辑** |
| rule_name_en | 英文名称 | **前端编辑** |
| is_active | 是否启用 | **前端编辑** |
| remark | 备注 | **前端编辑** |
| severity_if_violated | 违规等级 | 空着，引擎执行时确定 |
| nc_description_template | NC描述模板 | 空着，后期 |
| workflow_code | 工作流编码 | 空着，引擎回写 |
| rule_json | 工作流DAG | 空着，引擎回写 |

**关键**：`org_code` + `standard_code` + `phase_code` 不是用户手填的，是树节点选中后自动带入的——这就是"冗余但合理"的设计：同一检查项在不同机构/标准/阶段下可以不同。

---

## 四、后端 API

### 现有接口（不改动）

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `api/validation-rule/page` | 分页（支持 orgCode/standardCode/phaseCode 过滤） |
| GET | `api/validation-rule/list` | 列表 |
| POST | `api/validation-rule` | 保存 |
| POST | `api/validation-rule/delete/{id}` | 删除 |

### 需确认的改动

`SaveAsync` 方法需要补充：
- 新建时自动生成 `Code`（UUID）和 `RuleCode`（如 `NC-{orgCode}-{seq}`）
- `OrgCode`/`StandardCode`/`PhaseCode` 由前端从树节点传入

---

## 五、前端改造要点

### 现有 `List.vue` 已有骨架，改造方向：

1. **保留左侧树形导航**（机构→标准→阶段），已经实现懒加载
2. **简化右侧列表**：只显示 `ruleName` / `ruleNameEn` / `isActive` / 操作按钮
3. **简化编辑弹窗**：移除 standardCode/phaseCode/clauseCode/severity/workflowCode/ruleJson 等输入项，只保留 4 个核心字段
4. **移除 `loadWorkflowList()`** 函数和 `workflowList` 引用
5. **树节点选中时**：`currentFilter` 自动带 `orgCode` + `standardCode` + `phaseCode`，新建时自动填入

### 列表表格列

| 列 | 字段 | 宽度 |
|----|------|------|
| 中文名称 | ruleName | 250 |
| 英文名称 | ruleNameEn | 200 |
| 启用 | isActive | 80（Tag） |
| 操作 | - | 120（编辑/删除） |

### 编辑弹窗字段

| 字段 | 控件 | 必填 |
|------|------|------|
| ruleName | el-input | 是 |
| ruleNameEn | el-input | 否 |
| isActive | el-switch | 否（默认是） |
| remark | el-input textarea 2行 | 否 |

---

## 六、开发步骤

1. **后端**（0.5h）：`ValidationRuleService.SaveAsync` 补充 Code/RuleCode 自动生成
2. **前端 List.vue 改造**（1h）：简化列表列、简化弹窗、移除 workflowCode 引用
3. **联调**（0.5h）：新建→列表→编辑→删除
4. **测试数据**（0.5h）：在 ISO9001 一阶段下录入 3 条检查项

---

## 七、验收标准

- [ ] 左侧树形导航正常加载（机构→标准→阶段）
- [ ] 选中节点后右侧列表过滤显示
- [ ] 新建弹窗只有 4 个字段
- [ ] 保存时 orgCode/standardCode/phaseCode 自动从树节点带入
- [ ] 编辑/删除正常
- [ ] 编译无错误
