# YZH 前端框架知识库 V1.0（MVP）

> **落地范围**：M1 基础骨架 + M2 认证机构单表基类 已完成。  
> **对应文档链**：`方案V1.0`（`docs/60-AI工程设计/YZH-前端框架建设方案-V1.0-待审批版.md`）→ 本篇「知识库」（落地细节 + 踩坑） → 使用手册（`components/yzh/README.md`）。

---

## 0. 架构总原则（AI 代码生成必须读本条）

1. **源码隔离**：**严禁修改 `vol.web/src/components/basic/` 下的 Vol 核心源码**（ViewGrid / VolTable / VolForm / Action.js 等）。所有新代码统一放：
   - 类型：`vol.web/src/types/yzh/`
   - 组件 / TS 脚本：`vol.web/src/components/yzh/`
2. **组合优于继承**：YZH 基类是「外层壳」，用 `<view-grid ref=xx v-bind=...>` 直接组合 Vol 核心，不做 `extends/mixins`（避免 Vue 3 碎片问题）。
3. **三端对齐**：`schema.keyField / schema.controllerName / 数据库列名` 必须 PascalCase + 与后端实体完全一致（Phase 2 P2-01 教训）。

---

## 1. YZH 源码文件 → 职责矩阵（AI 排查错时直接用这张表定位）

| 文件 | 职责 | 出错表现 |
|------|------|----------|
| `types/yzh/YZHEntitySchema.ts` | 实体元 Schema + Action 常量 | URL 404 / 主键找错导致增量更新不生效 |
| `types/yzh/YZHLifecycles.ts` | 13+ 生命周期类型 | 业务钩子签名错、参数个数不对 |
| `components/yzh/YZHBaseApiClient.ts` | 泛型 HTTP 客户端（自动拼路由） | 404 / URL 缺末尾斜杠（P2-06 复发） |
| `components/yzh/YZHRowDiff.ts` | 新增插 / 修改换 / 删除移 3 算法（纯 TS） | 新增后位置不对 / 修改后字段没变 / 删除没生效 |
| `components/yzh/YZHEditGuard.ts` | 二次确认 + 必填校验 | 删除无确认 / 必填不提示 |
| `components/yzh/presets/defaultButtons.ts` | 顶部 8 按钮 + 编辑模式开关（删除常显） | 工具栏缺按钮 / 编辑模式点了没显示多选框 / 点删除提示「请先选择一行」 |
| `components/yzh/presets/defaultActionColumn.ts` | 行级「修改/删除」列 | 行按钮点了不触发 / formatter 报错（P2-05 复发）|
| `components/yzh/composables/useYZHEditMode.ts` | 编辑模式 + 多选状态机 | 编辑模式切换复选框不出现 / 选中数不计数 |
| `components/yzh/composables/useYZHIncrementSync.ts` | 增量同步 orchestrator（删除已简化，KISS） | 删完后该行还在（schema.keyField 错） / 新增后列表显示重复 |
| **★ `components/yzh/base/YzhBaseSingleTable.vue`** | ★ 单表基类窗体 MVP（目录/命名按用户 §4 调整） | 整体不显示 / 白屏 / 生命周期没触发 |
| `views/cert/CertificationBody/*.vue` | 首个样板业务页（用于回归验证） | 业务按钮灰 / 跳转不带 CbCode |

---

## 2. 最常见的 5 个报错 & 根治口诀（MVP 首版总结）

### Y1. 点「保存」后数据没变（新增 / 修改都如此）
> 口诀：**先看 incremental，再看 schema.keyField**。
> - ① `incremental-update` 关了 → 走 Vol 原生 search，若 API 正常但没刷新就是 Vol 自身问题；
> - ② 开着增量但没更新 → 90% 是 `schema.keyField` 填错了，导致 `replaceByKey` / `removeByKeys` 找不到匹配行。
> - ③ 还有 10%：后端 Add / Update 返回实体里 `keyField` 为 `0` 或空（自增 ID 后端没填充）。

### Y2. 点「✎ 编辑」后表格没出现多选列
> 口诀：**看 mergedTable.showCheckbox 是否被 Vol 覆盖回 false**。
> - `base/YzhBaseSingleTable.vue` 的 `watch(editMode, ...)` 会把 `mergedTable.value.showCheckbox = editMode.value`；若 Vol 内部 `onInited` 后重写了 `table.showCheckbox`，需要在 `onInited` 中再写一次（后续若真出现，在 `YzhBaseSingleTable.onInited()` 中补一行即可）。

### Y3. 操作列「修改/删除」按钮不显示
> 口诀：**showActionColumn=true？ columns 是否正确被 push 到末尾？**。
> - 排查 `mergedColumns.value.length`：原列数 + 1（操作列）。
> - 若 Vol 重渲染 columns 导致操作列丢失：请在业务页 `options.js` 里把操作列写进末尾，而不是由 YZH 基类追加（兜底兼容路径）。

### Y4. 行级删除后本页空了
> 口诀：**用户 §2 已要求「删除 = 纯移除」，本页空了不跳页**。
> - 新行为：删除只会把选中行从本页列表里去掉，**不会**自动跳到上一页、**不会**补拉 N 条；
> - 如果本页被删空，就保持空页，用户点「⟳刷新」会拉回一页正常数据（KISS，省 token、省复杂度）。

### Y5. 顶部查询条被收起来了（用户要求默认展开）
> 口诀：**`searchMode=fixed` → onInit 里 setFixedSearchForm(true)**。
> - 若你把 `searchMode='togglable'`，查询条会折叠 + 右上角显示小按钮（默认 Vol 行为），不符合你本次 UX 要求。

---

## 3. 与后端 Controller 约定的 Action 名（禁止 AI 自定义）

**前端 `YZH_ACTIONS` 常量**（`types/yzh/YZHEntitySchema.ts`）与后端 `ApiBaseController` 方法名 1:1 对应：

| 前端动作 | 后端方法名 | URL |
|---------|-----------|-----|
| 分页查询 | `GetPageData` | `POST /api/{controllerName}/GetPageData` |
| 新增 | `Add` | `POST /api/{controllerName}/Add` |
| 修改 | `Update` | `POST /api/{controllerName}/Update` |
| 删除 | `Del` | `POST /api/{controllerName}/Del`（body：`{ ids: [...] }`） |
| 导出 | `Export` | `POST /api/{controllerName}/Export`（blob） |
| 导入 | `Import` | `POST /api/{controllerName}/Import`（form-data） |

> ⚠ URL **尾部**规则：基类会保证 `{controllerName}/` 以 `/` 结尾，P2-06 404 不再出现。业务方 **不要在 options.table.url 里手写 URL**（写了也会被基类用 schema 覆盖）。

---

## 4. 增量刷新的三层拦截点（理解后就知道为什么不 reload）

Vol 原生保存 → 成功后默认调 `search()` 全量刷新：
```
saveAfter (Vol)
  └─> YzhBaseSingleTable.saveAfter(action, result)
        │
        ├── ① action='Add'    → incSync.applyInsert(savedEntity, pager)
        │                       └─ YZHRowDiff.insertByOrder(rows, newRow, sortField, sortOrder)
        │
        ├── ② action='Update' → incSync.applyReplace(savedEntity)
        │                       └─ YZHRowDiff.replaceByKey(rows, row, keyField)
        │
        └── ③ action='Delete'（多选删）/ handleRowDelete（行级删）
                                → incSync.applyRemove(ids, pager)
                                   ├─ removeByKeys(rows, ids, keyField)
                                   └─ total -= removed（仅此而已，不跳页、不补拉）
```

`incSync.enabled = false`（即业务页 `:incremental-update=false`）时，以上 3 条支路全部跳过，直接走 Vol 原生 `search()` 兜底。

---

## 5. 4 类基类窗体路线图（后续 M3+）

| 窗体类型 | 组件文件 | 适用业务页（计划） |
|---------|----------|--------------------|
| ✅ 单表 | `base/YzhBaseSingleTable.vue` | 认证机构 / ISOStandard（标准表）/ Enterprise（企业） |
| ⏳ 左树右表 | `base/YzhBaseTreeTable.vue`（待写） | ISOClause（条款树）/ Enterprise 按行业树筛选 / 按机构树筛选标准 |
| ⏳ 主从表 | `base/YzhBaseMasterDetail.vue`（待写） | CertApplication（申请+附件）/ AuditProject（项目 + 5 个阶段 Task） |
| ⏳ 工作流 | `base/YzhBaseWorkflow.vue`（待写） | 审批流程（通过/不通过/意见） |

> **上线顺序**：当前 M1-M2 已经把**最通用的 70%** 底座写好（Schema/Lifecycles/API/增量/编辑模式），后面 3 种基类只是把 YzhBaseSingleTable 作为内部零件组合起来，增量/生命周期不用重写。

---

## 6. 本次实施的文件变更清单（全量统计）

**新增 16 个文件（0 修改 Vol 核心源码）**：
```
vol.web/src/types/yzh/
  ├─ index.ts
  ├─ YZHEntitySchema.ts
  ├─ YZHLifecycles.ts
  └─ YZHPageProps.ts
vol.web/src/components/yzh/
  ├─ README.md                     ← 使用手册（业务方直接看这个）
  ├─ YZHBaseApiClient.ts
  ├─ YZHPageLifecycle.ts
  ├─ YZHRowDiff.ts
  ├─ YZHEditGuard.ts
  ├─ base/                         ★ 基类目录（用户 §4 新增，统一前缀 YzhBaseXxx）
  │   └─ YzhBaseSingleTable.vue
  ├─ composables/useYZHEditMode.ts
  ├─ composables/useYZHIncrementSync.ts
  ├─ presets/defaultButtons.ts
  └─ presets/defaultActionColumn.ts
```

**修改 2 个业务文件**：
```
vol.web/src/views/cert/CertificationBody/
  └─ CertificationBody.vue      ← 从原生 view-grid → YzhBaseSingleTable 包装
  （options.js 保持不变，100% 兼容原生成器输出）
```

---

## 7. 下一步（等你确认 M2 正常再推进）

- [ ] **冒烟测试通过**：认证机构页 ① 顶部 8 按钮（新增/刷新/导入/导出/列/✎编辑/删除/排序）显示正常（删除常显） ② 行级修改/删除按钮存在 ③ 点「✎编辑」出现多选框 ④ 点任意一行后点顶部「删除」也能直接删（单选） ⑤ 新增后按 CreateDate desc 自动插在列表第 1 行（不 reload）⑥ 修改后该行替换 ⑦ 删除后该行从列表移除（不跳页、不补拉）。
- [ ] 输出 M3 左树右表方案 → 写 `base/YzhBaseTreeTable.vue` + 落地到 ISOClause。
- [ ] 把 §0/§3 的硬约束追加进 `docs/60-AI工程设计/vol-skill.md` §12.F。
- [ ] 两前端端同步：`admin`（审核员前端）同名目录拷贝 ts/vue（99% 代码相同，脚本一键同步）。
