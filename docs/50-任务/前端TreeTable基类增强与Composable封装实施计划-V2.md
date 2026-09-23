# 前端 TreeTable 基类增强与 Composable 封装实施计划 V2

> **文档版本**: V2（在 V1 基础上补充「约定基础 / 覆盖机制 / 全量改造清单」并重组修复计划）
> **创建时间**: 2026-09-20
> **状态**: 待审批
> **前置文档**:
> - `docs/50-任务/前端Logic基类增强与Composable封装实施计划-V1.md`（代码级方法设计，V2 不再重复）
> - `docs/10-YZH架构/单页面基类架构设计规范-V1.md`（后端基类规范）
>
> **V2 相对 V1 的三点变化**:
> 1. **把「约定」提到第一性**：先固化后端固定端点 ↔ 前端基类方法的映射表，再谈封装。
> 2. **增加覆盖机制**：说明后端 `virtual/override` 与前端 getter/方法覆写如何对齐，这是"不必改基类就能做业务"的根因。
> 3. **增加全量改造清单**：在重大架构调整前，先梳理出"哪些页面、哪些逻辑必须改"的分级清单，架构落地后按清单逐个解决。

---

## 一、约定优先：为什么这套基类成立

### 1.1 三层约定

整个架构的可复用性建立在三层固定约定之上，缺一层基类都会失效：

| 层 | 约定内容 | 落地位置 |
|----|---------|---------|
| **① 端点约定** | 所有 Controller 走固定 URL + 固定 HTTP 方法 + 固定请求体形状 | `YzhControllerBase<V>` / `TreeTableControllerBase<T,V>` |
| **② 命名约定** | JSON 字段名 = 实体属性名 = 列名（PascalCase）；`Code` 为业务键、不可改、前端不生成 | 后端 `EntitySchemaHelper`、前端架构铁律 |
| **③ 契约约定** | 统一 `ApiResponse{success,data,message}`；分页 `{Items,TotalCount}`；树节点 `TreeItemDto` | `EntityConfigDto` / `PagedData` / `TreeItemDto` |

有了这三层，前端基类才能"闭着眼睛"调接口：URL 不用拼、响应不用猜、字段不用转。

### 1.2 ① 端点约定矩阵（后端固定，业务可继承不可改名）

**单表基类 `YzhControllerBase<V>`**（`[Route("api/[controller]")]`）：

| 语义 | 端点 | HTTP | 请求 | 响应 |
|------|------|------|------|------|
| 页面配置 | `config` | GET | — | `ApiResponse<EntityConfigDto>` |
| 过滤分页 | `filter` | POST | `FilterRequest` | `ApiResponse<PagedResult<V>>` |
| 新增 | `add` | POST | `V` | `ApiResponse<V>` |
| 修改 | `update` | POST | `V` | `ApiResponse<V>` |
| 批量删除 | `delete` | POST | `string[]`（Codes） | `ApiResponse<object>` |
| 行自定义操作 | `action/{methodName}` | POST | `JsonElement`（仅取 Code） | `ApiResponse<object>` |
| 启用/禁用 | `toggle-valid` | POST | `{Code}` | `ApiResponse<{Code,IsValid}>` |
| 导出 | `export` | POST | `ExportRequest` | 文件流 |
| 导入 | `import` | POST | `IFormFile` | `ApiResponse<ImportResult>` |
| 导入模板 | `import/template` | GET | — | 文件流 |

**左树右表基类 `TreeTableControllerBase<T,V> : YzhControllerBase<V>`** 追加：

| 语义 | 端点 | HTTP | 请求 | 响应 |
|------|------|------|------|------|
| 树表配置 | **`treepconfig`** | GET | — | `ApiResponse<TreeTableConfigDto>`（含 TableConfig/TreeConfig/TreeFormConfig） |
| 根节点 | `tree/root` | POST | `{}` | `ApiResponse<TreeItemDto[]>` |
| 子节点 | `tree/children` | POST | `{ParentCode,Level}` | `ApiResponse<TreeItemDto[]>` |
| 树新增 | `tree/add` | POST | `T` | `ApiResponse<TreeItemDto>` |
| 树修改 | `tree/update` | POST | `T` | `ApiResponse<TreeItemDto>` |
| 树删除 | `tree/delete` | POST | `string[]` | `ApiResponse<string>` |
| 树自定义操作 | `tree/action/{methodName}` | POST | `JsonElement`（仅取 Code） | `ApiResponse<object>` |
| 树启用/禁用 | `tree/toggle-valid` | POST | `{Code}` | `ApiResponse<{Code,IsValid}>` |
| 勾选树（选择器） | `checkTree` / `check/add` / `check/remove` / `check/all` | POST | 见选择器模型 | `ApiResponse<...>` |

> **必须记住的两个特殊约定**（前端基类已编码，业务不要绕过）：
> - 树表配置端点叫 `treepconfig` 而不是 `config`（`TreeTableControllerBase` 注释明确：`config` 与基类 virtual 冲突，ASP.NET 会选基类方法）。
> - 删除一律传 **Code 数组**；行/树自定义操作只从 body 取 `Code`。

### 1.3 ② 前端基类 ↔ 端点的机械映射（当前已实现，需保持）

| 前端基类方法 | 端点 | 备注 |
|-------------|------|------|
| `CrudPageLogic.loadConfig` | GET `config` | |
| `CrudPageLogic.dataLoader` / `loadPage` | POST `filter` | 唯一分页查询入口 |
| `CrudPageLogic.add/update/delete` | POST `add`/`update`/`delete` | |
| `CrudPageLogic.executeAction` | POST `action/{m}` | |
| `CrudPageLogic.toggleIsValid` | POST `toggle-valid` | |
| `CrudPageLogic.exportData/importData/downloadImportTemplate` | `export`/`import`/`import/template` | |
| `TreeTableLogic.loadConfig` | GET `treepconfig` | 覆写基类 |
| `TreeTableLogic.loadTreeRoot` | POST `tree/root` | |
| `TreeTableLogic.loadChildren` | POST `tree/children` | |
| `TreeTableLogic.addTreeNode/updateTreeNode/deleteTreeNode` | POST `tree/add`/`update`/`delete` | |
| `TreeTableLogic.executeTreeAction` | POST `tree/action/{m}` | |
| `TreeTableLogic.toggleTreeNodeIsValid` | POST `tree/toggle-valid` | |

**结论**：端点约定层已完整，前端基类只需**继续遵守**，改造重点在"把业务页面里重复的调用下沉"，而不是新增约定。

### 1.4 ③ 覆盖机制：后端 virtual ↔ 前端 getter/方法覆写

继承之所以能"一个基类 + 少量业务差异"，是因为两端都提供**模板方法 + 钩子**：

| 后端虚方法（`YzhControllerBase`） | 前端对应覆写点 |
|-----------------------------------|---------------|
| `GetSearchFields()` / `GetRowButtons()` / `GetToolbar()` | 配置驱动，前端读 `config` 即可，通常不用覆写 |
| `OnBuildingFilter(filters)` | `CrudPageLogic.buildFilters()` / `shouldApplyTreeFilter()` |
| `OnQueried(result)` | `onDataLoaded()` / `postprocessRows()` |
| `OnConfigLoading(config)` | 前端读取后 `onAfterInit()` |
| `OnBeforeAdd/OnAfterAdd` | `onBeforeAdd/onAfterAdd` |
| `OnBeforeUpdate/OnAfterUpdate` | `onBeforeUpdate/onAfterUpdate` |
| `OnBeforeDelete/OnAfterDelete` | `onDelete/onAfterDelete` |
| `ValidateEntity` / `OnCustomValidate` | 后端为主，前端不重复 |
| `BuildExportData` / `ProcessImport` | 前端不介入 |
| `LoadConfig` / `StrictConfigLoad` / `OnConfigMissing` | 配置来源 |

| 后端树虚方法（`TreeTableControllerBase`） | 前端对应覆写点 |
|-------------------------------------------|---------------|
| `MapToTreeItem` | `dtoToNode()` |
| `FillIsLeafBatch` | `TreeNode.IsLeaf` 直接消费 |
| `OnBeforeAddTree/OnAfterAddTree` | `onBeforeAddTree/onAfterAddTree`（新增钩子） |
| `OnBeforeUpdateTree/OnAfterUpdateTree` | `onBeforeUpdateTree/onAfterUpdateTree`（新增钩子） |
| `OnBeforeDeleteTree/OnAfterDeleteTree` | `onBeforeDeleteTree/onAfterDeleteTree`（新增钩子） |
| `FilterCore`（注入 RelateField / NoSelectionBehavior） | `shouldApplyTreeFilter()` / `isVirtualNode()` |

**TS 覆盖语义**：`TreeTableLogic` 的成员是 class 原型上的 getter/方法，业务子类用 `protected get xxx()` 或 `override xxx()` 覆写即可，基类内部通过 `this.xxx()` 动态派发到子类实现（等价于 C# 的 `virtual`）。因此**基类新增能力不需要动业务子类，业务子类的差异化也不需要复制基类代码**——这正是本次改造成立的核心依据。

> 反例（当前 `role/logic.ts`）：业务子类把 `openAddDialog(parent)` 覆写成与基类 `openAddDialog()` 不同的签名，语义被劫持、基类无法统一派发。改造的方向是**用钩子表达差异**（如 `openTreeNodeDialog` + `defaultTreeValues`），而不是改签名覆写。

### 1.5 约定缺口（当前违约点，必须先清零）

| # | 违约点 | 位置 | 影响 |
|---|--------|------|------|
| G1 | 旧响应契约 `res.code === 200` | `api/system/menu.ts`、`pages/system/menu/logic.ts` | 与 `ApiResponse.success` 不一致，无法用基类 |
| G2 | 手写端点而非基类 | `api/system/{menu,organization,user,role,dictionary,api}.ts` | 与基类端点重复/冲突 |
| G3 | 路由前缀不一致 `/api/System/Organization` vs `/api/Organization` | `api/system/organization.ts` | 疑似死代码 |
| G4 | TreeNode 小写读取 | 5 个 TreeTable 页面 | 静默失效（详见 2.7） |
| G5 | `rowButtons`（数组）与 `rowActionButtons`（字典）双轨 | 基类 + 多页面 | 需要统一 |
| G6 | 部分 Controller 未走基类 | 见 2.6 E 类 | 端点/契约不固定，前端无法统一 |

---

## 二、全量改造清单：哪些页面、哪些逻辑必须调整

> 这是本次重新规划的重点。**在架构动工前先锁定清单**，架构落地后按清单逐个销项。

### 2.1 分级方法

| 级别 | 定义 | 处理策略 |
|------|------|---------|
| **A 单表合规** | 已 `extends CrudPageLogic` | 按 V1 增强，非强制 |
| **B 左树右表合规** | 已 `extends TreeTableLogic` | **本次改造主体** |
| **C 树选择/关联** | 勾选分配模式（`checkTree` 约定 或 自研） | 单独抽象，非 CRUD |
| **D 手写未继承** | 有 CRUD 语义但未继承基类 | 迁移到基类 |
| **E 控制器未走基类** | 无 `logic.ts` 且后端 `ControllerBase` | 依赖后端迁移，逐个评估 |
| **F 冗余 API 层** | 手写 api 模块与基类重复 | 清理/标废弃 |

### 2.2 A 类：单表已继承基类（8 页，非强制）

| 页面 | Logic | 说明 |
|------|-------|------|
| system/user | `UserLogic` | |
| system/config | `ConfigLogic` | 有 `rowButtons.reduce` 本地转换 |
| workflow/job-skill | `JobSkillLogic` | |
| workflow/prompt-template | `PromptTemplateLogic` | |
| workflow/nc-config | `NCConfigLogic` | |
| foundation/cert-stage | `CertStageLogic` | |
| foundation/phase-definition | `PhaseDefinitionLogic` | |
| foundation/certification-body | `CertificationBodyLogic` | |

**动作**：随 V1 的 `defaultValues/entityNameField/onAfterInit/useCrudPage` 顺带改造；把 `.vue` 里的 `rowButtons.reduce(...)` 换成基类 `rowActionButtons`。

### 2.3 B 类：左树右表已继承（5 页，本次改造主体）

| 页面 | Logic | controllerName | 控制器 | 主要重复 |
|------|-------|---------------|--------|---------|
| system/role | `RolePageLogic` | `Role` | `RoleController : TreeTableControllerBase<Sys_Role,Sys_Role>` | 节点弹窗覆写基类同签名方法 |
| system/organization | `OrgPageLogic` | `Organization` | `OrganizationController : TreeTableControllerBase<Sys_Organization,Sys_User>` | 行/机构弹窗、dataLoader、ShowDisabled |
| system/dictionary | `DictionaryPageLogic` | `Dictionary` | `DictionaryController : TreeTableControllerBase<Sys_Dictionary,Sys_DictionaryList>` | 行/字典弹窗、fetchItems、数值归一化 |
| foundation/iso-standard | `ISOStandardTreeTableLogic` | `Foundation/ISOStandardTreeTable` | `ISOStandardTreeTableController` | 条款/标准弹窗、dataLoader |
| workflow/skill-manage | `SkillTreeTableLogic` | `Workflow/SkillTreeTable` | `SkillTreeTableController` | 技能/分类弹窗、dataLoader、`__all__` 特判 |

### 2.4 C 类：树选择 / 关联（非 CRUD，单独抽象）

| 页面 | Logic | 机制 | 约定 |
|------|-------|------|------|
| system/role-user | `RoleUserLogic : BaseRoleTreeLogic` | 角色树 + 勾选用户 | `checkTree`/`check/add`/`check/remove`/`check/all` |
| system/role-menu | `RoleMenuLogic : BaseRoleTreeLogic` | 角色树 + 勾选菜单 | 同上（含祖先自动补全） |
| system/role-api | `RoleApiLogic : BaseRoleTreeLogic` | 角色树 + 勾选接口 | 同上（含分组跟随） |
| foundation/cert-org-standard | 无（`index.vue` 内联） | 机构树 + 勾选标准（checkbox 即存） | 自研 `certOrgStandardApi` |
| foundation/cert-org-stage | 无（`index.vue` 内联） | 机构 × 阶段 关联 | 自研 |

**动作**：抽取 `useAssociationTree`（或 `AssociationTreeLogic`），统一"左选右勾 + 增量保存 + 本地缓存"；`cert-org-*` 属于同一语义，可归并。**不建议**强行套 CrudPageLogic。

### 2.5 D 类：手写未继承（必须迁移）

| 页面 | 现状 | 问题 | 动作 |
|------|------|------|------|
| system/menu | `pages/system/menu/logic.ts`（`useMenuLogic` 组合式，非 class）+ `api/system/menu.ts` | G1 旧契约 `res.code===200`；G2 手写端点；后端 `MenuManagementController : YzhControllerBase<Sys_Menu>` 但用自定义 `GET tree`/`tree/all` | 评估后端改 `TreeTableControllerBase<Sys_Menu,...>`，前端改 `MenuTreeLogic extends TreeTableLogic` |

### 2.6 E 类：控制器未走基类、页面无 logic.ts（依赖后端迁移）

| 页面 | 控制器 | 类型 | 评估 |
|------|--------|------|------|
| workflow/directory | `StandardDirectoryController : ControllerBase` | 树/目录 | 可迁 `TreeTableControllerBase` + 前端 `logic.ts` |
| workflow/doc-extraction-rule | `DocExtractionRuleController : ControllerBase` | CRUD | 可迁 `YzhControllerBase` |
| workflow/report-rule | `ReportDefinitionController : ControllerBase` | CRUD | 可迁 `YzhControllerBase` |
| workflow/report-rule-config | 待核对 | CRUD? | 待核对 |
| workflow/ai-usage | `AIUsageController : ControllerBase` | 查询/统计 | 非 CRUD，白名单 |
| workflow/queue | `QueueMonitorController : ControllerBase` | 监控 | 非 CRUD，白名单 |
| foundation/cert-org-standard | `CertOrgStandardController : ControllerBase` | 关联 | 归 C 类 |
| foundation/cert-org-stage | `CertOrgStageController : ControllerBase` | 关联 | 归 C 类 |

**动作**：先做后端迁移（走基类 → 端点/契约固定），再补前端 `logic.ts` 继承基类；非 CRUD 的显式列入"白名单"并说明原因。

### 2.7 必须调整的"逻辑点"逐项清单（B 类，核心销项表）

> 列「问题 → 动作」。这是架构落地后的施工清单。

#### iso-standard

| # | 逻辑点 | 问题 | 动作 |
|---|--------|------|------|
| ISO-1 | `dataLoader` 用 `selectedNode.value.code` | 小写 → 关联过滤/新增 `StandardCode` 恒 undefined | 改 `Code`；或直接删 `dataLoader` 用基类 |
| ISO-2 | 手写 `dataLoader` | 重复 | 删除，用基类 `dataLoader` + `shouldApplyTreeFilter` |
| ISO-3 | `openAddClauseDialog/openEditClauseDialog/submitClauseForm/deleteClause/batchDeleteClauses` | 重复 | → `openRowDialog/submitRowForm/deleteRow/batchDeleteRows` |
| ISO-4 | `openAddClauseDialog` 里 `selectedNode.value.code` | 小写 | 基类 `relatedValue()` 统一 |
| ISO-5 | `std*` 弹窗状态（5 个 ref） | 与基类 `treeDialog*` 重复 | 删除，复用基类 |
| ISO-6 | `openAddStdDialog/openEditStdDialog/submitStdForm/deleteStd` | 重复 | → `openTreeNodeDialog/submitTreeNodeForm/deleteTreeNodeWithConfirm` |
| ISO-7 | `init()` 覆写 | 重复 | → `onAfterInit` |
| ISO-8 | `index.vue` 的 `node.name` | 小写 | 改 `node.Name` |
| ISO-9 | `nodeActions` 覆写去掉 `add-child` | 业务差异（无层级） | 用钩子 `allowAddChild: false` |
| ISO-10 | `refreshTable` / 手动刷新 | 双机制 | 用基类 `onNodeClick` |

#### skill-manage

| # | 逻辑点 | 问题 | 动作 |
|---|--------|------|------|
| SK-1 | `dataLoader` 用 `selectedNode.value.code`、`findCategoryName` 用 `n.code/n.children/n.name` | 小写 | 改 PascalCase |
| SK-2 | `Code === '__all__'` 硬编码 | 特判 | `isVirtualNode` + `afterTreeLoaded` 注入 + `autoSelectFirstNode` |
| SK-3 | 手写 `dataLoader` + `CategoryCodeName` 翻译 | 重复 | 用基类 + `postprocessRows` |
| SK-4 | `openAddSkillDialog/openEditSkillDialog/submitSkillForm/deleteSkill/batchDeleteSkills` | 重复 | → 行泛型 |
| SK-5 | `category*` 状态 + `openAddCategoryDialog/openEditCategoryDialog/submitCategoryForm/deleteCategory` | 重复 | → 树泛型（复用 `treeDialog*`） |
| SK-6 | `rowActionButtons` getter 覆写 | 与基类重复 | 删除，用基类 |
| SK-7 | `init()` 注入 `__all__` + 选中 | 特殊 | → `afterTreeLoaded` + `autoSelectFirstNode` |
| SK-8 | `findCategoryName` 线性递归 | 性能 | 用基类节点索引 `findNode` |

#### organization

| # | 逻辑点 | 问题 | 动作 |
|---|--------|------|------|
| ORG-1 | `showDisabled` ref 重复声明 | 基类已有 | 删除 |
| ORG-2 | 覆写 `loadPageWithTree/loadPageWithoutTree` 注入 ShowDisabled | 基类 `buildFilters` 已自动注入 `ShowDisabled` | 删除覆写 |
| ORG-3 | `apiPostPublic` 暴露 protected `apiPost` | 破坏封装 | 删除 |
| ORG-4 | `perRowActionButtons`（按行二选一） | 业务差异 | 基类 `rowActionButtons` 支持 `(row)=>Record` 函数式 |
| ORG-5 | `formFieldsWithHidden`（OrgCode custom 字段） | 业务差异 | 保留为业务覆写 |
| ORG-6 | `openAddUserDialog`（叶子校验） | 业务校验 | → `openRowDialog` + `canAddUnderNode` + `requireTreeSelectionForAdd` |
| ORG-7 | `openAddOrgDialog/openEditOrgDialog/submitOrgForm/deleteOrg` | 重复 | → 树泛型 |
| ORG-8 | `node.IsLeaf ?? node.isLeaf` 兜底、`node.Extra ?? node.extra` | 契约不干净 | 清理小写兜底（P0 统一后不需要） |

#### dictionary

| # | 逻辑点 | 问题 | 动作 |
|---|--------|------|------|
| DIC-1 | `fetchItems` 手写 filters + reserved set | 重复/易漏 | 用基类 `dataLoader` |
| DIC-2 | `selectedNode.value.code` | 小写 | 改 `Code` |
| DIC-3 | `normalizeNumbers`（Decimal→number） | 业务归一化 | → `normalizeBeforeSubmit` |
| DIC-4 | `pickFormValues` | 通用 | 上移基类 |
| DIC-5 | `openTreeDialog/submitTreeForm/deleteTree/toggleTreeValid` | 重复 | → 树泛型 |
| DIC-6 | `openItemDialog/submitItemForm/deleteItem/batchDeleteItems/toggleItemValid` | 重复 | → 行泛型 |
| DIC-7 | `editingRow` 合并提交 | 通用 | 上移基类 |
| DIC-8 | `searchFields` 覆写（`treepconfig` 不映射 SearchFields） | **后端缺口** | 修 `ConfigDtoConverter`/`ConvertToDto` 映射 SearchFields，或文档标注 |

#### role

| # | 逻辑点 | 问题 | 动作 |
|---|--------|------|------|
| ROL-1 | `node.code/name/extra/parentCode` | 小写 | 改 PascalCase |
| ROL-2 | 覆写 `openAddDialog(parent)` / `openEditDialog(node)` / `submitForm()`，签名与基类不一致 | 劫持基类语义 | → `openTreeNodeDialog/submitTreeNodeForm` |
| ROL-3 | `parentNode/editingNode` 与基类 `treeParentNode/treeEditingNode` 重复 | 重复 | 删除 |
| ROL-4 | `index.vue` `node.name` | 小写 | 改 `node.Name` |

### 2.8 后端侧配套清单（与前端基类强相关）

| # | 位置 | 问题 | 动作 |
|---|------|------|------|
| BE-1 | `ConfigDtoConverter.ConvertToDto`（treepconfig 路径） | `SearchFields` 未映射，导致前端拿不到搜索字段 | 补齐映射 |
| BE-2 | `MenuManagementController` | 单表基类 + 自定义 `GET tree`，前端手写 | 评估迁 `TreeTableControllerBase` |
| BE-3 | E 类 8 个 `ControllerBase` 控制器 | 端点/契约不固定 | 逐个评估迁移或列白名单 |
| BE-4 | 树节点钩子 | `OnBeforeAddTree` 等已有，但前端无对应钩子 | 前端补 `onBeforeAddTree` 系列 |

---

## 三、TreeTable 基类设计（在 V1 基础上补充）

> 行 CRUD 泛型流、树节点 CRUD 泛型流、统一 `dataLoader`、`useTreeTable` 的**代码级设计见 V1 第三～九章**，此处只列 V2 新增/修订的契约与覆盖点。

### 3.1 新增/修订覆盖点总表（模板方法）

| 覆盖点 | 默认实现 | 典型业务覆写 |
|--------|---------|-------------|
| `defaultValues` | `{}` | 各页新增默认值 |
| `defaultTreeValues` | `{}` | 各页树节点新增默认值 |
| `entityNameField` | `'Name'` | iso-standard=`'Title'`、dictionary=`'DicName'` |
| `treeEntityNameField` | `treeConfig.NameField` | organization=`'OrgName'` |
| `requireTreeSelectionForAdd` | `true` | role 无层级可 `false` |
| `canAddUnderNode(node)` | `true` | organization=仅叶子 |
| `relatedValue()` | 选中节点 Code（虚拟节点 null） | — |
| `shouldApplyTreeFilter()` | 有选中且非虚拟 | dictionary 全量模式 |
| `isVirtualNode(node)` | `NodeType==='virtual'` | skill-manage `__all__` |
| `postprocessRows(rows)` | 原样 | skill-manage 翻译分类名 |
| `normalizeBeforeSubmit(payload)` | 原样 | dictionary 数值归一 |
| `afterTreeLoaded()` | 空 | skill-manage 注入"全部" |
| `autoSelectFirstNode` | `false` | skill-manage `true` |
| `onNodeAction` / `rowActionButtons` | 配置驱动 | organization 函数式按钮 |
| `onBeforeAddTree/onAfterAddTree/...` | 空 | 与后端树钩子对齐 |

### 3.2 按钮统一

- `CrudPageLogic.rowActionButtons: Record<string,string> | ((row)=>Record)`（**getter，字典**）。
- `rowButtons` 保留为派生数组（`Object.entries(rowActionButtons)`），供内部派发，避免破坏 A 类页面。
- `TreeTableLogic` 删除自己的 `rowActionButtons` 覆写，改为继承基类。

### 3.3 Composables

- `useCrudPage(LogicClass)`（V1）
- `useTreeTable(LogicClass)`（V1 第九章）
- `useAssociationTree(...)`（V2 新增，C 类专用）

---

## 四、重新规划的修复计划（重排）

> 关键变化：**先固化约定与清单，再做基类，再按清单逐个迁移**；每个阶段独立可交付、可回滚。

| 阶段 | 名称 | 内容 | 依赖 | 工作量 | 风险 |
|------|------|------|------|--------|------|
| **P0** | 约定固化与缺口确认 | 本文档「约定矩阵 + 分级清单」评审定稿；确认 E 类白名单；确认 BE-1～BE-4 | — | 0.5d | — |
| **P1** | 前端契约统一 | 修 TreeNode 小写（B 类 5 页）；统一 `rowActionButtons`；`initFormData/resetObject` 改 `protected` | P0 | 1.5h | 中 |
| **P2** | 基类内核增强 | `CrudPageLogic`（V1 P1）+ `TreeTableLogic` 行/树泛型 + 统一 `dataLoader` + 覆盖点 | P1 | 3h | 中 |
| **P3** | Composable | `useCrudPage` + `useTreeTable` + 导出 | P2 | 1.5h | 低 |
| **P4** | B 类迁移 | role → iso-standard → skill-manage → dictionary → organization（按 2.7 销项） | P3 | 3h | 中 |
| **P5** | D 类迁移 + F 类清理 | menu 迁移；清理 `api/system/*.ts` 冗余模块 | P3 | 2h | 中 |
| **P6** | 大树可扩展性 | 节点索引、懒加载、搜索不拷贝、虚拟滚动开关 | P4 | 2h | 中 |
| **P7** | 后端配套 | BE-1 修 SearchFields 映射；BE-2 menu 控制器；BE-3 E 类逐个评估 | P0 | 2d+ | 中 |
| **P8** | E 类页面迁移 | 后端就绪后补 `logic.ts` 继承基类 | P7 | 2d+ | 中 |
| **P9** | C 类关联抽象 | `useAssociationTree`；归并 cert-org-* | P3 | 1.5h | 低 |
| **P10** | 守卫与文档 | ESLint/CI 规则 + 模板 + 开发流程文档 | P4 | 1h | 低 |

### 4.1 守卫（防止回潮，强烈建议随 P10）

| 守卫 | 规则 | 目的 |
|------|------|------|
| 禁小写节点 | `no-restricted-syntax`：`MemberExpression` 命中 `node.code/name/extra/children/isLeaf/parentCode` | 防 G4 复现 |
| 禁页面直连 API | `no-restricted-imports`：`pages/**` 不得 import `@/api/**`（除声明豁免） | 防 G2 复现 |
| 禁手写 handle | CI grep：`pages/**/*.vue` 不得定义 `handleAdd/handleBatchDelete/handleRowAction/handleSubmit`（白名单除外） | 保证 composable 落地 |
| 禁旧契约 | grep：`res.code === 200` | 防 G1 复现 |

---

## 五、验收清单

### 5.1 分阶段验收

| 阶段 | 验收 |
|------|------|
| P1 | `grep` 无小写节点读取；`rowActionButtons` 单表/树表均可取；编译通过；5 页浏览器回归建立基线 |
| P2 | `role` 接入后：节点增删改、行增删改、联动刷新全部由基类完成，业务 Logic ≤ 30 行 |
| P3 | `.vue` 不再手写 `handle*`；IDE 类型完整 |
| P4 | B 类 5 页按 2.7 逐项销项完成；功能与基线一致 |
| P5 | menu 走基类；冗余 api 模块删除后引用清零 |
| P6 | 千级节点下删除/替换 O(1)；搜索无深拷贝；懒加载生效 |
| P7/P8 | E 类页面端点/契约固定，前端接入基类 |
| P10 | 守卫规则在 CI 生效 |

### 5.2 代码质量

| 项 | 标准 |
|----|------|
| B 类 Logic 行数 | ≤ 90（organization/dictionary 允许 ≤ 120） |
| `.vue` 行数 | ≤ 140 |
| 小写节点读取 | 0 |
| 页面直连 `@/api` | 0（白名单除外） |
| 编译 | `vue-tsc --noEmit` 0 error |

---

## 六、模板

### 6.1 新页面（左树右表）

见 V1 第十五章；V2 仅强调：
- `logic.ts` 只声明 `controllerName` + 覆盖点 getter。
- `.vue` 只做 `useTreeTable(Logic) + <YzhTreeTableLayout> + <YzhTable> + 两个 <el-dialog>`。
- 不出现任何 `node.code`、`res.code`、手写 `handle*`、手写端点。

### 6.2 页面决策树（放哪个基类）

```
页面有 CRUD 语义？
├─ 否 → 非 CRUD 白名单（queue/ai-usage/api 等）
└─ 是
   ├─ 左树右表？ → TreeTableLogic
   ├─ 单表？      → CrudPageLogic
   └─ 左选右勾关联？ → useAssociationTree（C 类）
```

---

## 七、执行建议（最小可验证步）

1. **P0** 评审并冻结本文档的「约定矩阵 + 清单」，确认 E 类白名单。
2. **P1 单独提交**（纯修 bug），5 页浏览器回归建立基线。
3. **P2+P3** 在基类完成泛型化与 composable，**用 `role` 先接入**（页面小、结构清晰）。
4. **P4** 按 2.7 清单逐页迁移，每页一个提交。
5. **P5/P6** 收口手写端点与大树性能。
6. **P7/P8** 后端迁移 E 类，前端跟进。
7. **P9/P10** 关联页抽象 + 守卫/文档。

---

## 八、不在本次范围

| 场景 | 原因 |
|------|------|
| C 类关联页的完整重构 | 语义不同（checkTree），仅做 P9 抽象 |
| 非 CRUD 白名单页面（queue/ai-usage/api）的统一基类 | 无 CRUD 语义 |
| 拖拽排序 / 跨节点移动 | 后端无统一约定 |
| 树节点级权限差异 | 权限体系独立演进 |
