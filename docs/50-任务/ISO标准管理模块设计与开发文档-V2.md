# ISO 标准管理模块 — 设计与开发文档

> **版本**：V2 | **日期**：2026-09-12 | **状态**：📋 设计阶段（开发前）
> **功能范围**：ISO 标准 + ISO 标准条款
> **架构基准**：YZH.Core 框架（参照 Organization 机构-人员模块）

---

## 一、设计原则

> **核心约束：所有开发必须基于 YZH 架构，禁止脱离框架的硬编码实现。**

### 1.1 参照模板

| 模块 | 文件路径 | 作用 |
|------|---------|------|
| **机构-人员（标准模板）** | `system/organization/` | 左树右表标准实现 |
| **后端 Controller** | `YZH.Core.Web/Controllers/Foundation/ISOStandardController.cs` | 继承 `YzhControllerBase<T>` 或 `TreeTableControllerBase<T,V>` |
| **前端 Logic** | `yzh.vue.core/src/logic/TreeTableLogic.ts` | 基类：自动 columns/formFields/CRUD |
| **前端 Vue** | `system/organization/index.vue` | `<YzhTreeTable>` + `<YzhTable>` + `<YzhForm>` |
| **数据契约** | `docs/10-YZH架构/04-数据契约.md` | 前后端通信规范 |

### 1.2 架构决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 页面模式 | **左列表 + 右表格** | ISO 标准不分级（无需树），但条款依赖标准选择 |
| 后端基类 | `YzhControllerBase<ISOStandard>` + `YzhControllerBase<ISOClause>` | 两个独立单表 CRUD，前端做联动 |
| 前端左面板 | `<YzhTable>`（可空置选择） | 标准列表，支持搜索/分页/排序 |
| 前端右面板 | `<YzhTable>` | 条款列表，选中标准后加载 |
| Logic 基类 | `CrudPageLogic<any>` | 单表 CRUD 基类（比 TreeTableLogic 更轻量） |
| 配置驱动 | EntityConfig JSON | 列/字段/搜索栏从 JSON 派生，无硬编码 |
| 视图路由 | `[ViewName("v_iso_standard")]` | 查询走视图（含字典翻译），增删改走物理表 |

---

## 二、后端设计

### 2.1 控制器（Controller）

```
ISOStandardController : YzhControllerBase<ISOStandard>
├── 路由: api/Foundation/ISOStandard
├── 继承能力: /config /filter /add /update /delete /toggle-valid
├── LoadConfig(): 从 Foundation/ISOStandard.json 加载
└── OnBeforeAdd/OnBeforeUpdate: Code 唯一性校验

ISOClauseController : YzhControllerBase<ISOClause>
├── 路由: api/Foundation/ISOClause
├── 继承能力: /config /filter /add /update /delete /toggle-valid
└── LoadConfig(): 从 Foundation/ISOClause.json 加载
```

### 2.2 实体配置（EntityConfig JSON）

**文件：** `YZH.Core.Web/Assets/EntityConfigs/Foundation/ISOStandard.json`

```json
{
  "Title": "ISO 标准管理",
  "FillMode": "AutoFix",
  "FormCols": 2,
  "EnableField": "Enable",
  "Columns": [
    {
      "FieldName": "StandardCode",
      "DesName": "标准编号",
      "Type": "TextBox",
      "XsFlag": true,
      "BcFlag": true,
      "Yxk": false,
      "Width": 160,
      "Sxh": 1,
      "Placeholder": "如：ISO 13485:2016",
      "Row": 0, "Col": 0
    },
    {
      "FieldName": "StandardName",
      "DesName": "标准名称",
      "Type": "TextBox",
      "XsFlag": true,
      "BcFlag": true,
      "Yxk": false,
      "Width": 280,
      "Sxh": 2,
      "Placeholder": "如：医疗器械质量管理体系",
      "Row": 0, "Col": 1
    },
    {
      "FieldName": "VersionYear",
      "DesName": "版本年份",
      "Type": "NumberBox",
      "XsFlag": true,
      "BcFlag": true,
      "Yxk": true,
      "Width": 100,
      "Mrz": "2026",
      "Sxh": 3,
      "Row": 1, "Col": 0
    },
    {
      "FieldName": "Category",
      "DesName": "类别",
      "Type": "DropDownList",
      "XsFlag": true,
      "BcFlag": true,
      "Yxk": true,
      "Width": 120,
      "DataKey": "iso_category",
      "Sxh": 4,
      "Row": 1, "Col": 1
    },
    {
      "FieldName": "CategoryName",
      "DesName": "类别（翻译）",
      "Type": "Other",
      "XsFlag": true,
      "BcFlag": false,
      "Sxh": 5
    },
    {
      "FieldName": "Description",
      "DesName": "描述",
      "Type": "Memo",
      "XsFlag": true,
      "BcFlag": true,
      "Yxk": true,
      "Width": 400,
      "Row": 2, "Col": 0,
      "ColSpan": 2,
      "Sxh": 6
    },
    {
      "FieldName": "Enable",
      "DesName": "启用状态",
      "Type": "Switch",
      "XsFlag": true,
      "BcFlag": false,
      "Sxh": 7
    }
  ]
}
```

**关键配置说明：**
- `EnableField: "Enable"` → 框架自动渲染"显示已禁用"开关
- `DataKey: "iso_category"` → 框架自动从字典接口获取下拉选项
- `CategoryName`（`BcFlag: false`）→ 仅表格显示，不参与表单编辑

### 2.3 后端修复项

| 编号 | 问题 | 修复方案 | 文件 |
|------|------|---------|------|
| B1 | 表名解析错误 `isostanding` | `EntityService.GetQueryTableName()` 增加 `[Table]` 特性回退 | `YZH.Core.Api/Services/EntityService.cs` |
| B2 | 实体未加 `[SugarTable]` | 添加 `[SugarTable("cert_iso_standard")]` | `ISOStandard.cs` / `ISOClause.cs` |
| B3 | 编译阻断 `PhaseDefinition.cs` | 修复 `BaseEntity` 命名空间引用 | `PhaseDefinition.cs` |

### 2.4 数据库待执行

| 编号 | 操作 | 文件/说明 |
|------|------|---------|
| D1 | 创建 `v_iso_standard` 视图 | 含 CategoryName/StatusName 字典翻译 |
| D2 | 确认字典表 `iso_category` 数据存在 | `Sys_Dictionary` / `Sys_DictionaryList` |

---

## 三、前端设计

### 3.1 页面结构

```
┌────────────────────────────────────────────────────────────────┐
│ ISO 标准管理                                                    │
├─────────────────────┬──────────────────────────────────────────┤
│  左：ISO 标准列表    │  右：ISO 条款列表                         │
│  ┌───────────────┐  │  ┌───────────────────────────────────┐   │
│  │ 工具栏：新增/刷新│  │  │ 工具栏：新增/刷新/批量删除            │   │
│  ├───────────────┤  │  ├───────────────────────────────────┤   │
│  │ <YzhTable>    │  │  │ <YzhTable>                        │   │
│  │ 自动分页      │  │  │ 选中标准后加载                      │   │
│  │ 自动搜索栏    │  │  │ 自动分页/搜索/排序                  │   │
│  │ 自动列配置    │  │  │ 自动列配置                          │   │
│  └───────────────┘  │  └───────────────────────────────────┘   │
├─────────────────────┴──────────────────────────────────────────┤
│  弹窗：标准新增/编辑（<YzhForm>，字段从 config.formFields 派生）   │
│  弹窗：条款新增/编辑（<YzhForm>，字段从 config.formFields 派生）   │
└────────────────────────────────────────────────────────────────┘
```

### 3.2 文件结构

```
src/certplatform-web/cert/cert-admin/src/pages/foundation/
├── iso-standard/
│   ├── index.vue     ← 使用 <YzhTable> + <YzhTable>（配置驱动）
│   └── logic.ts      ← ISOStandardLogic extends CrudPageLogic<any>
└── iso-clause/
    ├── index.vue     ← 使用 <YzhTable>（配置驱动，可选独立入口）
    └── logic.ts      ← ISOClauseLogic extends CrudPageLogic<any>
```

### 3.3 Logic 设计

**文件：** `iso-standard/logic.ts`

```typescript
/**
 * ISOStandardLogic — 标准管理（配置驱动）
 * 继承 CrudPageLogic 获得自动 CRUD 能力
 */
export class ISOStandardLogic extends CrudPageLogic<any> {
  controllerName = 'Foundation/ISOStandard'
  
  // config / columns / formFields / loadPage / add / update / delete
  // 全部由基类自动提供，无需手写
}
```

**文件：** `iso-clause/logic.ts`

```typescript
/**
 * ISOClauseLogic — 条款管理（配置驱动）
 */
export class ISOClauseLogic extends CrudPageLogic<any> {
  controllerName = 'Foundation/ISOClause'
}
```

### 3.4 Vue 组件设计

**关键：使用框架组件，禁止硬编码列/字段**

| 组件 | 用途 | 禁止的写法 |
|------|------|-----------|
| `<YzhTable>` | 配置驱动表格 | `<el-table-column prop="X" />` |
| `<YzhForm>` | 配置驱动表单 | `<el-form-item label="X">` |
| `logic.columns` | 列定义（从 config 自动派生） | 手写 Column 数组 |
| `logic.formFields` | 表单字段（从 config 自动派生） | 手写 FormField 数组 |
| `logic.searchFields` | 搜索字段（从 config 自动派生） | 手写搜索表单 |

### 3.5 前端修复项

| 编号 | 问题 | 修复方案 |
|------|------|---------|
| F1 | `<el-table>` + 硬编码 `<el-table-column>` | 改用 `<YzhTable :columns="logic.columns">` |
| F2 | `<el-form>` + 硬编码 `<el-form-item>` | 改用 `<YzhForm :fields="logic.formFields">` |
| F3 | Logic 不继承框架基类 | `extends CrudPageLogic<any>` |
| F4 | 手写 API 调用 URL | `this.apiPost('/filter')`（自动拼接 controllerName） |
| F5 | 手写 CRUD 状态管理 | 基类自动提供 rows/loading/pagination/dialog |
| F6 | 列/字段不在 config 中定义 | 在 `ISOStandard.json` 中完善 Columns 配置 |

---

## 四、开发 Todo 清单

### Phase 0：前置修复（阻断项）

- [ ] **0.1** 修复 `PhaseDefinition.cs` 编译错误（BaseEntity 命名空间）
- [ ] **0.2** 后端 `dotnet build` 通过
- [ ] **0.3** 后端服务启动成功，`/api/Foundation/ISOStandard/filter` 正常返回

### Phase 1：后端完善

- [ ] **1.1** `ISOStandard.cs` 增加 `[SugarTable("cert_iso_standard")]` 特性
- [ ] **1.2** `ISOClause.cs` 增加 `[SugarTable("cert_iso_clause")]` 特性
- [ ] **1.3** 创建 `ISOStandard.json` EntityConfig（按 2.2 节定义）
- [ ] **1.4** 创建 `ISOClause.json` EntityConfig
- [ ] **1.5** 验证 `/config` 端点返回正确 JSON
- [ ] **1.6** 执行 `v_iso_standard` 视图 SQL
- [ ] **1.7** 确认字典 `iso_category` 数据存在

### Phase 2：前端重构（按 YZH 架构）

- [ ] **2.1** `iso-standard/logic.ts` → `extends CrudPageLogic<any>`，删除手写状态/CRUD
- [ ] **2.2** `iso-standard/index.vue` → 使用 `<YzhTable>` + `<YzhForm>`
- [ ] **2.3** `iso-clause/logic.ts` → `extends CrudPageLogic<any>`（如需要独立入口）
- [ ] **2.4** `iso-clause/index.vue` → 使用 `<YzhTable>` + `<YzhForm>`
- [ ] **2.5** 删除所有硬编码 `<el-table-column>` 和 `<el-form-item>`
- [ ] **2.6** 实现"左标准选择 → 右条款加载"联动（通过 standards 选中事件触发 clauses.loadPage）

### Phase 3：验证测试

- [ ] **3.1** 页面打开自动加载标准列表
- [ ] **3.2** 点击标准行，右侧加载对应条款
- [ ] **3.3** 新增/编辑标准后，表格自动刷新（局部更新）
- [ ] **3.4** 删除标准后，表格自动移除行
- [ ] **3.5** 搜索/排序/分页功能正常
- [ ] **3.6** 字典字段（Category）正确显示中文

---

## 五、验证标准（Definition of Done）

### 5.1 后端验证

| 验证项 | 命令/操作 | 期望结果 |
|--------|----------|---------|
| 编译通过 | `dotnet build` | 0 Error |
| 服务启动 | 访问 `/api/Foundation/ISOStandard/config` | 返回 EntityConfig JSON |
| 分页查询 | POST `/filter` → `{"page":1,"pageSize":10}` | 返回 `{Items:[], Total:N}` |
| 标准编号唯一 | POST `/add` 重复 StandardCode | 返回错误"已存在" |

### 5.2 前端验证

| 验证项 | 操作 | 期望结果 |
|--------|------|---------|
| 配置驱动列 | 修改 JSON 中 `XsFlag` | 表格列自动增减（无需改 Vue） |
| 配置驱动表单 | 修改 JSON 中 `BcFlag` | 表单字段自动增减 |
| 无硬编码 | grep `<el-table-column` | 0 结果 |
| Logic 继承 | `logic instanceof CrudPageLogic` | true |
| 联动加载 | 点击左侧标准 | 右侧自动加载条款 |

### 5.3 架构合规性验证

| 验证项 | 检查方式 |
|--------|---------|
| 无硬编码 API URL | grep `yzhApi.post('/api/` 在 foundation 目录应为 0 |
| 使用框架组件 | `index.vue` 包含 `<YzhTable>` 和 `<YzhForm>` |
| Logic 继承基类 | `logic.ts` 包含 `extends CrudPageLogic` |
| JSON 配置完整 | `ISOStandard.json` 包含 ≥7 个 Column |

---

## 六、风险与依赖

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| EntityConfig JSON 格式错误 | 前端无法渲染 | 参考 `System/User.json` 模板 |
| 字典数据缺失 | Category 下拉无选项 | Phase 1.7 确认字典存在 |
| 视图字段不匹配 | CategoryName 为 null | 视图 SQL 需与实体字段对齐 |
| `extern alias` 类型冲突 | 运行时类型错误 | Phase 0.3 端点测试验证 |

---

## 七、参考文档

- [YZH 后端架构](../../10-YZH架构/02-后端架构.md)
- [数据契约](../../10-YZH架构/04-数据契约.md)
- [机构-人员模块](../../../src/certplatform-web/cert/cert-admin/src/pages/system/organization/)
- [CrudPageLogic 源码](../../../src/certplatform-web/yzh.vue.core/src/logic/CrudPageLogic.ts)
- [TreeTableLogic 源码](../../../src/certplatform-web/yzh.vue.core/src/logic/TreeTableLogic.ts)

---

> ⚠️ **本文档为设计稿，执行前需评审确认。严禁跳过设计直接编码。**
