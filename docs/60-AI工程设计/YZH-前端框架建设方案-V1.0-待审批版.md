# YZH 前端框架建设方案 V1.0（待审批版）

> **状态**：方案设计阶段，**待用户确认后再执行代码改造**。
> **目标**：基于 Vol 框架核心组件（ViewGrid / VolTable / VolForm）做**二次封装**，形成符合「体系认证平台」业务习惯的 4 套基类窗体与一套泛型 TS 基类。
> **硬约束（不可违背）**：**严禁修改 `components/basic/` 下的 Vol 核心源码**；只能在 `components/yzh/` 目录下通过「组合/包裹/引用」方式扩展。

---

## 一、Vol 核心能力调研结论（已完成）

已深度调研 Vol 核心组件源码，结论如下（决定了本方案的封装边界）：

| 模块 | 文件 | 可复用 / 可劫持点 | 不能改的点 |
|------|------|------------------|-----------|
| **查询加载** | `VolTable/VolTableLoadData.js` L6-L120 | `loadBeforeAsync` / `onLoadBefore` / `onLoadAfter` 三个钩子已支持；URL 在 L72 `props.url + getPageData` | 数据请求必须走此逻辑，否则丢分页/权限/字段过滤 |
| **CRUD 动作** | `ViewGrid/Action.js` L1-L13 | `Add / Update / Del / getPageData / Import / Export` 6 个硬编码 Action | Action 名与后端 `ApiBaseController` 对齐，不宜改 |
| **ViewGrid 暴露 API** | `ViewGridExposeMethods.jsx` L11-L159 | `search / refresh / add / edit / del / getFormOption / getTable / getSelectRows / setFixedSearchForm` 16 个方法已暴露为 ref | 暴露 API 足够，无需侵入 Vol 源码 |
| **ViewGrid 插槽** | `ViewGrid.vue` L7-L74 | `gridHeader / btnLeft / btnRight / gridBody / gridFooter / modelBody` 已提供；但 **P2-04 踩坑**：必须用 `<div>` 单根包裹 | 已在 Skill 文档约束 |
| **VolTable 列配置** | `VolTable.vue` L145 / L590 | `columns.formatter / columns.click / v-if` 渲染链已打通；**P2-05 踩坑**：formatter 必须是函数 | 已在 Skill 文档与 VolTable 本身双重校验 |
| **编辑弹框** | `ViewGridProvider.jsx` 弹框打开逻辑 | `addBefore / updateBefore / modelOpenBefore / modelOpenAfter` 已支持 | 弹框样式用 VolBox，不要重写 |

**结论**：ViewGrid + VolTable 已经具备 90% 的 UI / 生命周期 / CRUD 能力；YZH 框架只需要做 **「薄封装 + 规范层 + 额外体验增强」**，不做「底层重写」。

---

## 二、YZH 前端框架的 5 层架构图

```
业务页 < cert/CertificationBody/CertificationBody.vue >
    │
    ▼ 【第 1 层：业务页 Vue】仅写业务特有 Hook & 插槽
【第 2 层：4 个 YZH 基类窗体组件】── 组合 ViewGrid 并注入规范
    YZHSingleTable.vue / YZHTreeTable.vue / YZHMasterDetail.vue / YZHWorkflowPage.vue
    │
    ▼ 内部 new 出一个 YZH 实例 【第 3 层：TS 泛型基类】
【第 3 层：TS 泛型基类 typescript】
    YZHBasePage<TPrimaryKey, TEntity> / YZHBaseApiClient<T>
    │  ├─ 定义 13+ 生命周期钩子（LoadBefore/SaveBefore…）
    │  ├─ 定义 8 项 Props 规范（controllerName / table / columns / form…）
    │  └─ 定义 行级 CRUD 增量更新算法（新增插行 / 修改替换 / 删除移除，不 reload）
    │
    ▼ 通过 api/http 调用 【第 4 层：自动路由】
【第 4 层：自动后端路由】
    controllerName + Action（见 Action.js）→  POST /api/{controllerName}/getPageData ...
    │
    ▼ 写回 Vol Table ref（不调用 search() reload）
【第 5 层：Vol ViewGrid / VolTable / VolForm】── 不改源码，纯复用
```

---

## 三、目录结构（新建独立的 YZH 域，严禁改 vol 源码）

**所有新增代码都放在 `components/yzh/` 和 `types/yzh/` 下**（两前端端同名同步）：

```
vol.web/src/
├── components/
│   ├── basic/                   ← 原 Vol 核心，禁止任何修改
│   │   ├── ViewGrid/…
│   │   ├── VolTable/…
│   │   └── VolForm/…
│   └── yzh/                     ← ★ YZH 框架域（完全新建）
│       ├── README.md            ← YZH 组件使用手册
│       ├── YZHBasePage.ts       ← ★ TS 泛型基类（核心）
│       ├── YZHBaseApiClient.ts  ← ★ TS 通用 API 客户端（自动拼路由）
│       ├── YZHPageLifecycle.ts  ← 13 项生命周期接口定义
│       ├── YZHRowDiff.ts        ← ★ 行级增量更新算法（新增插/修改替换/删除移除）
│       ├── YZHEditGuard.ts      ← 保存 / 删除前表单校验
│       ├── components/          ← ★ 4 套基类窗体（Vue 组件）
│       │   ├── YZHSingleTable.vue      ← 单表窗体（本次先落地：认证机构）
│       │   ├── YZHTreeTable.vue        ← 左树右表（如 ISO 标准树 + 条款 Grid）
│       │   ├── YZHMasterDetail.vue     ← 主从表（如申请 + 明细）
│       │   └── YZHWorkflowPage.vue     ← 工作流页（如审核流程）
│       ├── composables/         ← Vue 3 Composition hooks
│       │   ├── useYZHSingleTable.ts    ← 单表页组合 hook
│       │   ├── useYZHEditMode.ts       ← 编辑 / 多选 / 批量删除 状态机
│       │   ├── useYZHTreeFilter.ts     ← 左树右表：树选中 → 注入 wheres
│       │   └── useYZHIncrementSync.ts  ← ★ 增量同步（不 reload 的核心）
│       └── presets/             ← 预设按钮与列模板
│           ├── defaultButtons.ts        ← 新增 / 刷新 / 导入 / 导出 / 列 / 编辑 / 删除配置 / 排序
│           ├── defaultActionColumn.ts   ← ★ 操作列（行级 修改 / 删除 按钮）
│           └── defaultQuickSearch.ts    ← 顶部查询条（取消 gridHeader 标题，保留查询）
└── types/
    └── yzh/                     ← YZH 类型定义（共享）
        ├── index.ts
        ├── YZHPageProps.ts      ← 组件 Props 类型
        ├── YZHLifecycles.ts     ← 生命周期钩子类型
        └── YZHEntitySchema.ts   ← 实体元信息（主键 / 排序字段 / 标签色）
```

> **两前端端对齐原则**：后台管理 `vol.web` 与审核员前端 `admin` 保持相同目录结构，组件与 TS 代码**逐行一致**（必要时做脚本同步）。

---

## 四、TS 泛型基类设计（YZHBasePage.ts，不依赖 Vue，纯逻辑可测）

### 4.1 两个核心类

```typescript
// types/yzh/YZHEntitySchema.ts
export interface IYZHEntitySchema<TKey, TEntity> {
  /** 实体主键字段名，通常是 'Id' / 'Code' */
  keyField: keyof TEntity & string;
  /** 主键类型，用于 diff 比对 */
  keyType: 'guid' | 'number' | 'string';
  /** 默认排序字段（前端新增行要按此位置插入）*/
  defaultSortField: keyof TEntity & string;
  /** 默认排序方向 */
  defaultSortOrder: 'asc' | 'desc';
  /** 控制器名：自动拼路由 /api/{controllerName}/xxx */
  controllerName: string;
  /** 字段 → 字典编号（用于 tag 色自动渲染） */
  statusTagColors?: Partial<Record<keyof TEntity, string>>;
}

// components/yzh/YZHBaseApiClient.ts
export class YZHBaseApiClient<TKey, TEntity> {
  constructor(private readonly schema: IYZHEntitySchema<TKey, TEntity>) {}

  /** 自动拼：/api/{controllerName}/getPageData */
  getPageData = (param: PageDataOptions) =>
    proxy.http.post<PageGridData<TEntity>>(`/api/${this.schema.controllerName}/GetPageData`, param);

  add    = (saveModel: SaveModel<TEntity>) => proxy.http.post(`/api/${this.schema.controllerName}/Add`,    saveModel);
  update = (saveModel: SaveModel<TEntity>) => proxy.http.post(`/api/${this.schema.controllerName}/Update`, saveModel);
  del    = (ids: TKey[])                  => proxy.http.post(`/api/${this.schema.controllerName}/Del`,    { ids });

  import = (formData: FormData) => proxy.http.post(`/api/${this.schema.controllerName}/Import`, formData);
  export = (param: any)         => proxy.http.post(`/api/${this.schema.controllerName}/Export`, param, true); //blob
}
```

### 4.2 13+ 项业务生命周期（对齐后端 Partial Service）

**前端 YZH 页声明的钩子，会在 Vol 对应 Hook 里有序触发**：

| 生命周期名 | 触发时机 | 返回值 | 对应后端 Hook |
|-----------|----------|--------|--------------|
| `onLoadBefore(param)` | 每次查询（含首次加载）发送前 | `boolean \| Promise<boolean>`；false 取消查询 | Partial Service `QueryRelativeList` |
| `onLoadAfter(rows, data)` | 查询数据回来后 | `TEntity[]` 允许修改最终行 | `GetPageDataOnExecuted` |
| `onAddBefore(formData)` | 新建弹框打开前 / 保存前两次 | `boolean`；false 阻断 | `AddOnExecute` |
| `onAddSaveBefore(main, list)` | 新建保存前（可赋值默认字段） | `boolean` | `AddOnExecuting` |
| `onAddSaveAfter(main, list, result)` | 新建保存后（同事务成功） | `void` | `AddOnExecuted` |
| `onUpdateBefore(row, formData)` | 编辑弹框前 | `boolean` | `UpdateOnExecute` |
| `onUpdateSaveBefore(main, list)` | 编辑保存前 | `boolean` | `UpdateOnExecuting` |
| `onUpdateSaveAfter(main, list, result)` | 编辑保存后 | `void` | `UpdateOnExecuted` |
| `onDeleteBefore(rows, ids)` | 行删 / 批量删前 | `boolean`；false 阻断 + 提示 | `DelOnExecuting` |
| `onDeleteAfter(ids)` | 删除成功后 | `void` | `DelOnExecuted` |
| `onImportBefore(formData)` / `onImportAfter` | Excel 导入前后 | `boolean` | `ImportOnExecuting/Executed` |
| `onExportBefore(param)` / `onExportAfter` | 导出前后 | `boolean` | 自定义 Export 重写 |

> 类型文件 `components/yzh/YZHPageLifecycle.ts` 声明为 `interface IYZHPageLifecycle<TKey, TEntity>`。

---

## 五、4 种基类窗体（Vue 组件）设计

### 5.0 所有窗体的通用设计规范（统一 UX，本次你提的核心要求都在这里）

#### ✅ 顶部布局（所有 YZH 基类通用，取消 gridHeader 标题 + 重排工具条）

```
┌────────────────────────────────────────────────────────────────────────────┐
│ [ 快速查询表单 vol-form 一行 （1~4 字段）]      [查询 v] [重置]             │  ← 查询区（取代原 gridHeader）
│                                                                            │
│ [▶ 自定义扩展工具位  slot #toolbarLeft]                                     │  ← 业务扩展（认证机构：查看标准）
│                                                                            │
│ [+新建] [⟳刷新] [⇪导入] [⇩导出] [☰列] [✎编辑] [✕删除] [⇅排序]  [slot #toolbarRight] │  ← 操作按钮（presets）
│ 说明：                                                                     │
│  • ✎编辑 = 切换「编辑模式」。点击后：表格出现多选框；[✕删除] 按钮出现；      │  ← 需求 1 已满足
│    再次点击 ✎编辑 = 退出编辑模式，隐藏多选 + 批量按钮。
│  • 行级操作列 ★★★：**每行都有「修改 / 删除」按钮**，默认显示，无需进编辑模式。 │  ← 需求 2 已满足
└────────────────────────────────────────────────────────────────────────────┘
```

> **按钮实现方案**：preset `defaultButtons.ts` 生成 8 个按钮配置，注入 `ViewGrid` 的 `#btnLeft` 插槽（**必须用 `<div>` 包裹，避免 P2-04**）。行级「修改/删除」：preset `defaultActionColumn.ts` 在 columns 末尾 push 一列 `操作`，用 `render` + 2 个按钮；点击时直接调 `gridRef.edit(row)` / `handleRowDelete(row)`。

#### ✅ CRUD 增量刷新（不 reload 的核心算法 —— YZHRowDiff.ts）

**你提的 3 条硬体验要求，全部在这里用「行级 patch」实现，不再调用 `gridRef.search()`：**

| 动作 | 旧 Vol 行为 | ★ YZH 新行为（推荐默认，可配置切回 search） |
|------|------------|-------------------------------------------|
| **新增保存成功** | `gridRef.search()` → 全表 reload，分页重置 | **YZHRowDiff.insertByOrder(rows, newRow, sortField, sortOrder)**<br>• 取后端返回实体（带自动生成 Code / CreateDate）<br>• 按当前前端排序条件，找到正确下标 splice 插入一行<br>• **分页判断**：若当前页已满，自动跳转到「最后一页」或新增页 |
| **修改保存成功** | `gridRef.search()` → 全表 reload | **YZHRowDiff.replaceByKey(rows, updatedRow, keyField)**<br>• 找到同主键行直接替换，其他行不动<br>• 触发该行短暂高亮动画（`.yzh-row-flash`）提示成功 |
| **行删除 / 批量删除** | `gridRef.search()` → 全表 reload | **YZHRowDiff.removeByKeys(rows, deletedKeys, keyField)**<br>• 本地 `splice` 删除后重新计算 `total = total - deletedKeys.length` <br>• **边界**：若本页删空且非第 1 页，自动 `page - 1` 并补拉上一页最后 N 行（避免空白页） |

> 性能对比：从 1 次完整分页查询（含 LINQ + 权限过滤 + SQL 执行）→ 0 次查询，列表响应从 300~1200ms → <50ms。

### 5.1 YZHSingleTable.vue（单表，** MVP 先做这一个，落地到认证机构**）

**Props（精简到 5 个）**：
```typescript
interface Props<TKey, TEntity> {
  /** 实体元信息：controllerName + 主键 + 排序字段 */
  schema: IYZHEntitySchema<TKey, TEntity>;
  /** options.js 生成的 4 件套：table / columns / (search|edit)FormFields / Options */
  options: () => {
    table, columns, detail, details, extend,
    editFormFields, editFormOptions, searchFormFields, searchFormOptions
  };
  /** 13 个生命周期钩子（可选，任何一个都可不传） */
  lifecycles?: Partial<IYZHPageLifecycle<TKey, TEntity>>;
  /** 是否启用「增量刷新」，默认 true（你提的核心要求） */
  incrementalUpdate?: boolean;
  /** 启用哪些默认按钮（默认全开） */
  buttons?: Partial<Record<ButtonType, boolean>>;
}
```

**落地页：认证机构 → `views/cert/CertificationBody/CertificationBody.vue`** 只需要：
```vue
<template>
  <YZHSingleTable :schema="schema" :options="viewOptions" :lifecycles="lifecycles">
    <template #toolbarLeft>
      <el-button type="success" size="small" @click="viewClauses" :disabled="!selectedRow">查看标准</el-button>
    </template>
  </YZHSingleTable>
</template>
<script setup lang="ts">
import YZHSingleTable from '@/components/yzh/components/YZHSingleTable.vue'
import viewOptions from './options.js'
import type { IYZHEntitySchema } from '@/types/yzh/YZHEntitySchema'
import type { CertificationBody } from '@/types/entities/CertificationBody'

const schema: IYZHEntitySchema<string, CertificationBody> = {
  keyField: 'Code', keyType: 'guid',
  defaultSortField: 'CreateDate', defaultSortOrder: 'desc',
  controllerName: 'CertCertificationBody',  // 自动拼 /api/CertCertificationBody/xxx
  statusTagColors: { Status: 'org_status' },
}
const selectedRow = ref<CertificationBody>()
const lifecycles = {
  onLoadAfter: (rows) => console.log('rows transformed', rows),
  onDeleteBefore: (rows) => proxy.$confirm(`确认删除 ${rows.length} 条机构？`).then(() => true).catch(() => false),
}
const viewClauses = () => router.push({ path: '/cert/ISOStandard', query: { CbCode: selectedRow.value.CbCode } })
</script>
```

### 5.2 YZHTreeTable.vue（左树右表）

**Props 继承 YZHSingleTable**，额外 3 项：
```typescript
interface TreeTableExtra<TTree> {
  treeData: TTree[] | (() => Promise<TTree[]>);  // 左树数据：如 ISO 标准树
  treeProps: { label: string, children: string };
  /** 左侧选中节点 -> 转成 wheres 注入 getPageData */
  treeNodeToWheres: (node: TTree) => SearchParameters[];
}
```

**适用页**：条款管理（左树 = 标准章节 / 右表 = 条款列表）、机构下标准管理（左树 = 机构 / 右表 = 标准）。

### 5.3 YZHMasterDetail.vue（主从表）

组合 YZHSingleTable（主） + N 个 VolTable 明细（复用 ViewGrid 的 detail / details props），增量算法扩展到「明细行 patch」。适用页：申请（主）+ 申请附件（明细）、审核项目（主）+ 5 个阶段任务（明细）。

### 5.4 YZHWorkflowPage.vue（工作流页）

在 YZHSingleTable 基础上增加 5 个动作：提交 / 撤回 / 审批通过 / 审批不通过 / 流程意见弹框，绑定 Vol 工作流组件 `workflow/workflow.vue`，生命周期加 `onAuditBefore / After`。适用页：申请审批、认证决定。

---

## 六、落地步骤 & 里程碑（分 4 期，一期一审）

| 阶段 | 产出 | 是否需要审批后再动代码 |
|------|------|------------------------|
| **M1：基础骨架**（2~3 天） | ① 目录结构 + 6 个 ts 类型文件<br>② YZHBaseApiClient + YZHBasePage 骨架<br>③ 2 个 presets（默认按钮 + 默认操作列）<br>④ YZHRowDiff.ts 增量 3 算法 & 单测（纯 ts，无 UI 依赖） | ✅ M1 前需要你先审批本方案 |
| **M2：单表 MVP 落地**（1.5 天） | ① YZHSingleTable.vue 写好（含 8 按钮 / 查询条 / 操作列 / 编辑模式多选）<br>② `CertificationBody.vue` 重写：从直接用 ViewGrid → 改为 YZHSingleTable 包装 <br>③ 实际联调通：新增 / 修改 / 删除走**增量不 reload** + 多选批量删除正常 | ✅ M2 完成后给你演示再继续 |
| **M3：左树右表落地**（1 天） | ① YZHTreeTable.vue + useYZHTreeFilter hook<br>② 落地到 2 个页：**ISOClause（条款）**、**Enterprise（企业可按行业树）** | ✅ M3 方案需要你先确认 M2 OK |
| **M4：知识库 & 文档**（0.5 天） | ① `components/yzh/README.md` 使用手册（组件级）<br>② `docs/60-AI工程设计/YZH-前端框架知识库-V1.0.md`（架构级 + 排错手册）<br>③ 更新 vol-skill.md 引用 YZH 基类模板 | 同步 M3 后进行 |

---

## 七、风险 & 回退方案

| 风险 | 级别 | 规避 |
|------|------|------|
| 增量刷新与后端默认值 / 自动生成字段（如 Code、字典翻译）不一致 | 中 | 增量算法以**后端 Add/Update 返回实体**为准（目前 Vol Add 返回 main.entity，可直接替换）；不一致时可 `incrementalUpdate = false` 全局回退 search |
| YZH 基类包装后丢失 Vol 原生 Hook 透传（如 onInit） | 低 | 组件内所有原生 Hook 都 `$$restProps` 透传给 ViewGrid（v-bind="$attrs" + emits） |
| 两前端端代码不同步 | 低 | 先只在 vol.web 落地，跑通后用脚本 copy 到 admin 端同名目录 |
| 你后续要求的「泛型 TS 基类」在 JS 项目中兼容性 | 低 | `.ts` 文件提供类型；`.vue` 仍用 `lang="jsx"`，不强制业务页写 TS（类型仅 IDE 提示） |

---

## 八、需要你先确认的 3 个关键问题 ❓

1. **✎ 编辑按钮交互的最终形态**：你描述的是「点击编辑 → 出现多选框 + 删除按钮；再点编辑取消」—— 这个状态要不要和「行级修改/删除」按钮分开显示？即：**行级按钮**任何时候都可见；而**多选框**只在点击「✎编辑」后出现？（我当前方案里是这么设计的，确认下）
2. **增量刷新边界策略**：删除当前页最后一条时，**默认自动跳上一页并补上最后 N 行**（而不是停在空白页），是否符合预期？
3. **落地优先级**：你要求先做「认证机构」单表，我已把它放在 M2。M2 过程中我**不会动其他 6 个页面**，等你看认证机构 OK 了再批量推广到 ISOStandard / Enterprise / AuditTask，对吧？

> **📌 下一步**：请你回复「同意本方案 + 对上面 3 个问题的回答」，我拿到确认后立刻开始 M1（基础骨架）+ M2（认证机构单表 MVP）的编码，完成后再跑文档与知识库输出。