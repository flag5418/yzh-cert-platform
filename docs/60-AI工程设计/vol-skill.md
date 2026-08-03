
## Vol框架AI Skill业务开发
---

<ai-skill-download />
`1. 使用方式：以cursor为例,同时打开下载的vol-skill.md文件与前后端项目,添加描述：`
`2. 按vol-skill.md清单，在【表.vue】文件中编辑弹出框【字段】后加一个选择按钮，从弹出框选择数据，弹出框使用voltable表格配置，表格的数据从【xxx】表获取，选择数据后回写到表单`
## AI 执行契约（最高优先级，冲突时以本节为准）

```
收到需求
  → §12.Z 关键词检索
  → 判定端：仅后台 / 仅前端 / 前后端
  → 判定模式：存在 Edit.vue？→ 新页面编辑(§12.K) : 标准 view-grid({表}.vue)
  → 写文件：Partial/{表}Service.cs | {表}.vue | Edit.vue | 新建 SelectXxx.vue
  → 禁止：.jsx 业务 | options.js 手改 | 非 Partial Service/Controller | ViewGrid 源码
```

| 需求信号 | AI 动作 | Skill | 在线文档（仅 .html） |
|----------|---------|-------|----------------------|
| 查不到数据、保存写库、审核后写关联表 | Partial Service，Func 赋值 + `base.xxx()` | §12.A.0–A | .../cs/service/guid.html |
| searchBefore/addBefore、按钮、弹框流程 | `{表}.vue` Props Hook | §12.C/D | .../view-grid/methods/{名}.html |
| 字段 onChange/回车/明细列计算 | `{表}.vue` onInit/onInited + getFormOption/columns | §12.E | .../view-grid/components.html · event/ |
| 弹框选数、复杂 UI 效果 | `{表}.vue` + 子组件，参照示例 | §12.J | .../web/... |
| 独立页新建编辑 | `Edit.vue` + vol-edit Hook | §12.K | .../edit/ |
| 自定义 API | Partial 三件套 | §12.J.10 | .../cs/dev/api.html · **JsonNormal** → case.html |
| **数据库访问 / 分页查询 / vol-table 自定义接口** | `Partial/{表}Service.cs` **EF + repository** | **§12.B.0** | .../cs/dev/db.html |

**数据库默认规则：** 一律 **EF LINQ + `repository`**（见 §12.B.0）；**禁止**默认使用 [EFsql](http://v3.volcore.xyz/docs/cs/dev/db/EFsql.html) / Dapper 原生 SQL，除非 EF 无法实现。**所有分页 MUST `TakePage(Page, Rows)`**；vol-table / selectTable 自定义查询 MUST `options.ConvertQueryFilter<{实体}>()`。

**文档源：** 优先本 Skill §十二代码 → 不足时 **WebFetch** `http://v3.volcore.xyz/docs/{路径}.html`。Skill 内 `doc.web/docs/` 路径仅作 URL 映射，**不要**假设用户仓库有 doc.web。

**JSON 返回：** 全局 `Json()` 为小驼峰；**自定义 Partial 接口返回业务数据 MUST `return JsonNormal(...)`**（与实体/columns.field 原大小写一致）。标准 `getPageData` 已在 ApiBaseController 内 JsonNormal。见 §12.B / [case.md](http://v3.volcore.xyz/docs/cs/dev/case.html)。

---

| 层级 | 技术 |
|------|------|
| 后端 | .NET 8、ASP.NET Core WebAPI、Entity Framework Core、Autofac |
| 前端 | Vue 3 Composition API、`script setup lang="jsx"`、Element Plus、TypeScript |
| 核心组件 | `view-grid`（列表+表单+明细）、`vol-form`、`vol-table`、`vol-box` |
| HTTP | `proxy.http.get/post`（前端）、`ApiBaseController` 标准路由（后端） |

---

## 零、AI 工作流

** 在线文档：`http://v3.volcore.xyz`。

```
开发者描述业务需求（§十一）
    ↓
① 本 Skill §12.Z 检索 → 定位 §12.A–J 代码模板（优先）
    ↓
② 模板不够/边界不清 → 访问 v3.volcore.xyz 对应页（§十三 URL 映射表）
    ↓
③ 读取用户项目 api + web.vite 中实际 {表}.vue / Partial Service
    ↓
④ 只改业务文件：Partial/{表}Service.cs、{表}.vue 或 Edit.vue、新建 SelectXxx.vue 等（**禁止改 .jsx**）
    ↓
⑤ 勿改 options.js、非 Partial 的生成 Controller/Service
```

| 步骤 | 数据源 | 说明 |
|------|--------|------|
| 优先 | **本 Skill §十二** | 含可执行代码模板 + 全量索引，覆盖常见业务 |
| 补充 | **v3.volcore.xyz** | 与离线 doc.web 同源；AI 用 WebFetch/浏览查具体章节、截图说明、边界 case |
| 必须 | **用户项目源码** | `web.vite/src/views/...`、`api/.../Partial/` |
| 不依赖 | 本地 doc.web、Demo_Order 示例 | 用 Skill + v3.volcore.xyz + 用户项目源码即可 |

**在线文档规则（AI）：**
- URL：`http://v3.volcore.xyz/docs/{相对路径}.html`
- 顺序：§12 模板 → 在线文档补细节 → 读用户 `{表}.vue` / Partial Service 对齐命名

---

## 文档体系与代码归属（AI 路由必读）

官方在线文档：**http://v3.volcore.xyz**（与离线 `doc.web/docs/` 同路径）。  
四类文档**职责不同**，AI 必须先判断需求属于哪一类，再查对应章节。

### 文档四分法

| 文档分区 | 在线入口 | 讲什么 | 不是什么 |
|----------|----------|--------|----------|
| **后台开发** `docs/cs/` | [/docs/cs/](http://v3.volcore.xyz/docs/cs/) | **数据库操作**；生成表 **`Service.cs` 业务处理**（查询/新建/修改/删除/审批/导入导出等 **操作前、操作后** 钩子） | 不是前端 UI |
| **生成页面文档** `docs/view-grid/` | [/docs/view-grid/](http://v3.volcore.xyz/docs/view-grid/) | **`view-grid` 容器**的属性、Hook、Slots；内嵌 **vol-form / vol-table** 的字段事件见 `components.md`、`event/` | 不是 vol-form/vol-table 完整 API，也不是 web 示例集 |
| **前端开发** `docs/web/` | [/docs/web/](http://v3.volcore.xyz/docs/web/) | 生成页之上的 **常用功能示例**（弹框选数、自定义按钮、联动、table 样式…Recipes） | 不是 view-grid 完整 API 列表 |
| **新页面编辑** `docs/edit/` | [/docs/edit/](http://v3.volcore.xyz/docs/edit/) | 生成器选 **「新页面编辑」** 时：新建/编辑在 **独立页面** `Edit.vue` 打开，而非列表弹框 | 不是标准 `{表}.vue` 弹框模式 |

**组件示例**（独立使用）：`docs/form/`、`docs/table/`、`docs/box/` — vol-form / vol-table **完整 API**；生成页内嵌用法以 **view-grid/components.md + event/** 为准。

### view-grid 与 vol-form、vol-table（生成页必知）

view-grid **不重复实现**表单/表格，而是内嵌子组件并透传配置：

| 内嵌 | ref | 配置来源 | 字段/列事件绑定 |
|------|-----|----------|-----------------|
| 查询 vol-form | `searchForm` | `searchFormFields/Options` | `gridRef.getSearchFormOption('字段').onChange` |
| 主表 vol-table | `table` | `columns` | `columns[].click/formatter` + Hook `:rowClick` |
| 编辑 vol-form | `form` | `editFormFields/Options` | `gridRef.getFormOption('字段').onKeyPress/onChange/blur` |
| 明细 vol-table | `detail` | `detailOptions.columns` | `detailOptions.columns[].onChange/onKeyPress/formatter` |

**三层扩展（由外到内）：** ① ViewGrid Hook（`:searchBefore`、`:addBefore`…）→ ② `onInit` 改 gridRef 属性 → ③ 字段/列事件（§12.E）。  
**文档：** http://v3.volcore.xyz/docs/view-grid/components.html · event 目录 · form/table 仅查组件独有 API。

### 后台：Service 数据库前/后处理

**写代码位置：** `api/.../Services/.../Partial/{表}Service.cs`（及 Partial Controller 自定义 API）。

**实现方式（框架约定）：** 业务扩展点定义在 `ApplicationServiceBase.cs`（`Func`/`Action` 属性）；`ServiceBase.cs` 在 `GetPageData`/`Add`/`Update`/`Del` 等 virtual 方法内 **Invoke**。Partial 中 **重写对应方法 → 给委托属性赋 Lambda → `return base.xxx(...)`**，不要复制框架 CRUD。详见 **§12.A.0** 与 [guid.md](http://v3.volcore.xyz/docs/cs/service/guid.html)。

| 阶段 | 典型 Func 属性 | 含义 |
|------|----------------|------|
| **查询前** | `QueryRelativeList`、`QueryRelativeExpression`、`QuerySql` | 改查询条件 / LINQ / 原生 SQL |
| **查询后** | `GetPageDataOnExecuted`、`SummaryExpress` | 改结果行 / 表格合计 |
| **保存前（原始数据）** | `AddOnExecute`、`UpdateOnExecute` | 操作 `SaveModel`，实体转换前 |
| **保存前（实体）** | `AddOnExecuting`、`UpdateOnExecuting`、`DelOnExecuting`、`ImportOnExecuting`… | 写库 **之前**校验、赋默认值 |
| **保存后（同事务）** | `AddOnExecuted`、`UpdateOnExecuted`、`DelOnExecuted`、`ImportOnExecuted`… | 写关联表；`Error` **回滚** |
| **文档** | `docs/cs/service/` | guid 总览 + search/add/update… 完整示例 |

### 前端模式 A：标准生成页（列表 + 弹框编辑）

**写代码位置：** `web.vite/src/views/{类库}/{文件夹}/{表}/{表}.vue`（及同目录子组件 `SelectXxx.vue` 等）。

- 使用 **`<view-grid>`** 组件  
- 业务通过 **Props 传入 Hook**（`:onInit`、`:searchBefore`、`:addBefore`…）  
- 查文档：**view-grid**（属性/方法）+ **web**（实现某效果的示例）

### 前端模式 B：新页面编辑（独立页新建/编辑）

**生效条件：** 代码生成器 **编辑模式 = 新页面编辑**。

**写代码位置：** `views/{类库}/{文件夹}/{表}/Edit.vue`（或 `{表}/edit.vue`，以生成结果为准）。

- 新建/编辑在 **新路由页面** 打开，不是 view-grid 弹框  
- Hook 名与 view-grid **类似但独立一套**（`loadFormBefore`、`loadFormAfter`、`loadTableBefore`…）  
- 查文档：**docs/edit/**；Skill **§12.K**

### 禁止：`{表}.jsx` 不写业务代码

| 文件 | 说明 |
|------|------|
| `extension/.../{表}.jsx` 或生成遗留 **`.jsx`** | **仅兼容旧版项目**，新版本业务 **一律不写此处** |
| AI **MUST NOT** | 在 `.jsx` 中新增或修改业务逻辑 |
| 正确做法 | 标准页 → `{表}.vue`；新页面编辑 → `Edit.vue`；后端 → `Partial/{表}Service.cs` |

若项目 `{表}.vue` 仍 `import extend from '...jsx'`，保留空 jsx 或仅作兼容引用，**新 Hook 写在 .vue**。

### AI 需求路由表

| 开发者描述 | 改哪里 | 查哪类文档 | Skill 章节 |
|------------|--------|------------|------------|
| 只能查自己的单、审核后写库存 | Partial Service | cs/service | §12.A |
| 保存前校验、明细默认值 | Partial Service + 或前端 Hook | cs/service + view-grid | §12.A + §12.C |
| 表单/明细字段 onChange、回车、实时计算 | `{表}.vue` onInit | view-grid **event/** + components | §12.E |
| view-grid 属性/方法/Hook | `{表}.vue` | view-grid methods/properties | §12.C/D |
| 弹框选客户、select 联动 | `{表}.vue` + 子组件 | **web** 示例为主 | §12.J |
| 新页面编辑页保存前校验 | `Edit.vue` | **edit** | §12.K |
| 自定义 API | Partial 三件套 | cs/dev/api | §12.J.10 + **JsonNormal** |
| vol-table / selectTable 自定义分页接口 | Partial Service + Controller | cs/dev/db · selectTable | **§12.B.0** + §12.J.3/J.10 |
| 数据库增删改查（非生成页 Hook） | `Partial/{表}Service.cs` repository | cs/dev/db | **§12.B.0** |

---

## 一、项目结构

### 1.0 业务代码写哪里（速查）

| 场景 | 业务写这里 | 禁止写这里 |
|------|------------|------------|
| 后台 DB 前/后处理 | `Partial/{表}Service.cs` | 非 Partial 的 Service.cs |
| 标准生成页（列表+弹框） | `{表}.vue`、同目录 `*.vue` 子组件 | `options.js`（会被覆盖）、**`.jsx`** |
| 新页面编辑 | `{表}/Edit.vue` | **`.jsx`** |
| 前端静态列/按钮配置 | 在 `{表}.vue` 或 `Edit.vue` 的 `onInit` 改 options | 直接改 `options.js` 文件 |

### 1.1 前端 (web.vite)

```
web.vite/src/views/{类库}/{文件夹}/{表名}/
│   ├── {表名}.vue              ← 【标准模式】view-grid 业务 Hook（★写这里）
│   ├── {表名}/options.js       ← 生成器配置（会被覆盖；动态改写在 onInit）
│   ├── {表名}/Edit.vue         ← 【新页面编辑模式】独立页新建/编辑（★写这里）
│   └── {表名}/*.vue            ← 子组件 SelectXxx、gridFooter 等
extension/{模块}/{表名}/
│   └── {表名}.jsx              ← ⚠ 旧版兼容，【禁止写业务】
components/basic/ViewGrid/      ← 框架核心，勿改
```

**完整代码模板：** 见 **§十二**；URL 映射见 **§十三**。

### 1.2 后端 (api)

```
api/
├── VolPro.WebApi/
│   ├── Controllers/{Module}/{表}Controller.cs           ← 自动生成（会被覆盖）
│   ├── Controllers/{Module}/Partial/{表}Controller.cs   ← 自定义 API（不会覆盖）
│   └── Template/                                        ← 代码生成模板
├── VolPro.{Module}/
│   ├── Services/{folder}/{表}Service.cs                 ← 自动生成（会被覆盖）
│   ├── Services/{folder}/Partial/{表}Service.cs         ← 业务逻辑（不会覆盖）★
│   ├── IServices/{folder}/Partial/I{表}Service.cs       ← 接口声明（不会覆盖）
│   └── Repositories/{folder}/{表}Repository.cs          ← 一般不改
├── VolPro.Core/
│   ├── BaseProvider/ServiceBase.cs                      ← virtual 业务方法
│   ├── BaseProvider/ApplicationServiceBase.cs           ← 钩子委托
│   ├── Filters/ServiceFunFilter.cs                      ← 钩子签名参考
│   └── Controllers/Basic/ApiBaseController.cs           ← 标准 REST 端点
└── VolPro.Entity/DomainModels/                          ← 实体
```

---

## 二、核心原则（AI 必须遵守）

### 2.1 文件与 Partial 规则

| 文件 | 代码生成后 | AI 是否写业务 |
|------|-----------|---------------|
| `{表}.vue`、`Edit.vue`、子组件 `.vue` | 不覆盖 | **是** |
| `Partial/` 下 Service/Controller/IService | 不覆盖 | **是** |
| **`{表}.jsx`** | 不覆盖 | **否（禁止写业务，仅旧项目兼容）** |
| `options.js`、非 Partial Service/Controller | **会覆盖** | 否（在 onInit 动态改） |

### 2.1.1 旧版 .jsx 说明

- 早期项目用 `extension/.../{表}.jsx` 的 `methods` 写 Hook；**新版本已全部迁移到 `{表}.vue`**。  
- AI 遇到 jsx  import 时：**不要**往 jsx 追加逻辑；在对应 **`.vue` 或 `Edit.vue`** 实现同等 Hook。

### 2.2 扩展三件套（自定义 API 必做）

1. `IServices/.../Partial/I{表}Service.cs` — 声明方法  
2. `Services/.../Partial/{表}Service.cs` — 实现方法  
3. `Controllers/.../Partial/{表}Controller.cs` — 暴露端点  

路由规则：主 Controller 有 `[Route("api/{表名}")]`，Partial 中用 `[HttpPost("ActionName")]`，最终 URL = `api/{表名}/{ActionName}`。

### 2.3 前后端职责划分

| 需求 | 后端 Partial Service（DB 前/后） | 前端 |
|------|----------------------------------|------|
| 查询过滤、合计、SQL | GetPageData + Query* / OnExecuted | `{表}.vue` searchBefore |
| 保存校验、写关联表 | Add/Update **OnExecuting / OnExecuted** | addBefore / updateBefore |
| 审核后写库存 | Audit **OnExecuted** | auditBefore |
| UI 按钮、弹框、布局 | — | `{表}.vue` onInit / slots；示例查 **web** |
| view-grid 属性/方法 | — | 查 **view-grid** 文档 |
| 独立页新建编辑 | 同左 Service 钩子 | **`Edit.vue`**（新页面编辑模式） |

---

## 三、代码生成器默认能力

生成一张表后，框架**已内置**以下能力（无需重写即可用，业务在 Partial/Hook 中扩展）：

### 3.1 后端默认 API（ApiBaseController）

| HTTP | 路由 | Service 方法 | 用途 |
|------|------|-------------|------|
| POST | `api/{表}/getPageData` | GetPageData | 主表分页查询 |
| POST | `api/{表}/getPageDataAsync` | GetPageDataAsync | 异步查询 |
| POST | `api/{表}/GetDetailPage` | GetDetailPage | 明细表分页 |
| POST | `api/{表}/Add` | Add | 新建（含主子表） |
| POST | `api/{表}/Update` | Update | 编辑（含明细增删改） |
| POST | `api/{表}/Del` | Del | 删除（可级联明细） |
| POST | `api/{表}/Import` | Import | Excel 导入 |
| GET | `api/{表}/DownLoadTemplate` | DownLoadTemplate | 下载导入模板 |
| POST | `api/{表}/Export` | Export | Excel 导出 |
| POST | `api/{表}/Upload` | Upload | 文件上传 |
| POST | `api/{表}/Audit` | Audit | 审核/审批 |
| POST | `api/{表}/antiAudit` | AntiAudit | 反审 |
| POST | `api/{表}/cancelAudit` | CancelAudit | 撤销审批 |
| POST | `api/{表}/urgentAudit` | UrgentAudit | 催办 |

勾选「异步接口」后，对应 `*Async` 路由生效。

### 3.2 前端 ViewGrid 默认能力

- 查询表单 + 快捷查询 + 高级查询  
- 主表分页、排序、多选、导出、导入、打印、审批按钮  
- 编辑弹框（新建/编辑/复制/连续添加）  
- **主从表**（一个明细）：`detail` 配置  
- **一对多明细**：`details[]` 多 tabs  
- **三级明细**：`subDetails`  
- 行内编辑、拖拽排序、单元格合并、tableV2 高性能模式  
- 字典自动绑定（`loadKey` + 后台字典接口）  
- 按钮权限（`filterPermission`）

---

## 四、后端 Service 业务扩展（完整技能）

**位置：** `Services/{folder}/Partial/{表}Service.cs`  
**基类：** `ServiceBase<TEntity, I{T}Repository>`  
**钩子定义参考：** `VolPro.Core/Filters/ServiceFunFilter.cs`

### 4.1 两种写法

**A. 构造函数注册（固定逻辑）：**
```csharp
public Demo_OrderService(...) : base(dbRepository) {
    QueryRelativeExpression = q => q.Where(x => x.Enable == 1);
    AddOnExecuting = (order, list) => { /* 校验 */ return webResponse.OK(); };
}
```

**B. override 内临时赋值（按请求动态）：**
```csharp
public override WebResponseContent Add(SaveModel saveDataModel) {
    AddOnExecuting = (order, list) => { /* ... */ return webResponse.OK(); };
    return base.Add(saveDataModel);
}
```

### 4.2 查询 GetPageData

| 成员 | 类型 | 时机与用途 |
|------|------|-----------|
| `QueryRelativeList` | `Action<List<SearchParameters>>` | 查询前改/删条件项；可取某字段后设 `item.Value=null` 让框架忽略 |
| `QueryRelativeExpression` | `Func<IQueryable<T>, IQueryable<T>>` | LINQ 过滤（权限、状态、Include 明细） |
| `QuerySql` | `string` | 原生 SQL（select 必须返回实体全部列，防注入） |
| `OrderByExpression` | 多字段排序 | `{ x.CreateDate, QueryOrderBy.Desc }` |
| `SummaryExpress` | `Func<IQueryable<T>, object>` | 主表底部合计行 |
| `GetPageDataOnExecuted` | `Action<PageGridData<T>>` | 结果返回前改 rows |
| `options.Value` | object | 接收前端 searchBefore 传入的额外参数 |
| `IsMultiTenancy` | bool | 多租户过滤开关 |

**override：** `GetPageData(PageDataOptions options)` / `GetPageDataAsync`

**明细：**
- `GetDetailPage(Async)` — 明细分页、弹窗选单  
- `GetDetailSummary<Detail>` — 明细合计  
- `DetailQuery<Detail>` — 明细 IQueryable 过滤  

**OR 查询：** 前端 wheres 项设 `Fields` 数组，多字段 OR。

### 4.3 新建 Add

**流水线：** SaveModel → AddOnExecute → 转实体 → **AddOnExecuting** → [事务入库+明细] → **AddOnExecuted** → 审计+流程

| 钩子 | 参数 | 说明 |
|------|------|------|
| `AddOnExecute` | SaveModel | 原始 JSON 校验前 |
| `AddOnExecuting` | `(T main, object detail)` | **入库前**；主从：`detail as List<TDetail>` |
| `AddOnExecuted` | 同上 | **入库后**，同事务，可 return Error 回滚 |
| `AddWorkFlowExecuting` | T | 进审批流程前，return false 阻止 |
| `AddWorkFlowExecuted` | `(T, List<int> userIds)` | 流程写入后 |

**一对多 SaveModel：** `Details[]` 每项含 `Table`、`Data`、`DelKeys`。

### 4.4 修改 Update

| 钩子 | 参数 |
|------|------|
| `UpdateOnExecute` | SaveModel |
| `UpdateOnExecuting` | `(T main, object addList, object updateList, List<object> delKeys)` |
| `UpdateOnExecuted` | 同上 |

addList/updateList 强转为 `List<TDetail>`。

### 4.5 删除 Del

| 钩子 | 说明 |
|------|------|
| `DelOnExecuting` | 删除前校验（审核状态等） |
| `DelOnExecuted` | 删除后清理，同事务 |

参数：`object[] keys`。`Del(keys, delList: true)` 默认级联删明细。

### 4.6 审批 / 反审 / 流程

| 方法/钩子 | 说明 |
|-----------|------|
| `Audit` / `AuditAsync` | 审核入口 |
| `AuditOnExecuting` / `AuditOnExecuted` | 简单审核（非多级流程） |
| `AuditWorkFlowExecuting` | `(T, AuditStatus, bool isLast)` 流程节点审核前 |
| `AuditWorkFlowExecuted` | `(T, AuditStatus, List<int> nextUserIds, bool isLast)` 流程节点审核后 |
| `AntiAudit` + `AntiAuditOnExecuting/Executed` | 反审及业务回滚 |
| `CancelAudit` / `UrgentAudit` | 撤销 / 催办 |
| `SubmitWorkFlowAudit` | 批量提交进流程 |

**获取流程信息（Service 内）：** `GetTableWorkflow()`、`GetTableCurrentFlowStep()`、`GetTablePreFlowStep()`

### 4.7 导入 / 导出 / 上传

| 操作 | 钩子/配置 |
|------|----------|
| 导出 | `ExportOnExecuting`、`ExportColumns`、`ExportCustomValue`、`Limit`（最大行数） |
| 导入模板 | `DownLoadTemplateColumns` |
| 导入 | `ImportOnExecuting`、`ImportOnExecuted`、`ImportOnReadCellValue`、`ImportStartRowIndex`、`ExcelHeaderMap`、`ImportIgnoreSelectValidationColumns` |
| 上传 | `UploadFolder`、`IsRoot` |

### 4.8 打印

不在 ServiceBase 内。通过 `PrintContainer`（Startup 注册）+ `PrintFilter` 子类重写 `Query`/`QueryDetail`。前端 Hook：`printBefore`、`printModelClose`。

### 4.9 主从 vs 一对多

| 模式 | Entity | SaveModel | AddOnExecuting 明细 |
|------|--------|-----------|---------------------|
| 主从（一对一明细） | `DetailTable = new[] { typeof(TDetail) }` | `DetailData` + `DelKeys` | `object as List<TDetail>` |
| 一对多 | `DetailTable = new[] { typeof(D1), typeof(D2) }` | `Details[]` | Update 分 add/update/del |

### 4.10 Partial Controller 模板

```csharp
namespace VolPro.MES.Controllers
{
    public partial class Stock_in_headController
    {
        private readonly IStock_in_headService _service;

        [ActivatorUtilitiesConstructor]
        public Stock_in_headController(IStock_in_headService service, IHttpContextAccessor httpContextAccessor)
            : base(service)
        {
            _service = service;
        }

        [HttpPost("GetPoItem")]
        public async Task<object> GetPoItem([FromBody] Dictionary<string, object> data)
        {
            int paramsType = Convert.ToInt32(data["paramsType"]);
            string paramsValue = data["paramsValue"]?.ToString();
            return await _service.GetPoItem(paramsType, paramsValue);
        }

        [HttpGet("GetStockInInfo")]
        public List<Stock_in_head> GetStockInInfo() => _service.GetStockInInfo();
    }
}
```

### 4.11 Partial Service 模板

```csharp
public partial class Stock_in_headService
{
    public async Task<object> GetPoItem(int paramsType, string paramsValue)
    {
        return await Task.FromResult(repository.Find(x => x.field == paramsValue));
    }
}
```

### 4.12 后台常用扩展场景（非生成页 Hook）

| 场景 | 做法 |
|------|------|
| 跨表写数据 | ctor 注入 `IOtherRepository`，在 AddOnExecuted 中写 |
| 自定义分页 API | §12.B.0.4：`ConvertQueryFilter` + `TakePage` + Partial 三件套 |
| vol-table 自定义 url 查询 | §12.B.0.4 + §12.J.10 B |
| 接口免登录 | Controller Action 加 `[AllowAnonymous]` |
| 重写权限 | Partial Controller 或 `interfaceauth` 模式 |
| EF 事务 | `repository.DbContextBeginTransaction(() => {})` |
| 当前用户 | `UserContext.Current.UserId` |
| 单据号 | 框架编码规则服务 |
| 租户过滤 | `QueryRelativeExpression` + 租户字段 |

---

## 五、前端 ViewGrid 扩展（完整技能）

**Hook 权威列表：** `web.vite/src/components/basic/ViewGrid/ViewGridFilter.js`  
**页面写法：** 在 `{表}.vue` 定义函数，通过 `:onInit="onInit"` 等 props 传入 `<view-grid>`。

### 5.1 标准页面模板

```vue
<template>
  <view-grid ref="grid"
    :columns="columns" :detail="detail" :details="details"
    :editFormFields="editFormFields" :editFormOptions="editFormOptions"
    :searchFormFields="searchFormFields" :searchFormOptions="searchFormOptions"
    :table="table" :extend="extend"
    :onInit="onInit" :onInited="onInited"
    :searchBefore="searchBefore" :searchAfter="searchAfter"
    :addBefore="addBefore" :updateBefore="updateBefore"
    :modelOpenBefore="modelOpenBefore" :modelOpenAfter="modelOpenAfter"
    :rowClick="rowClick">
    <template #gridFooter><!-- 自定义 --></template>
  </view-grid>
</template>
<script setup lang="jsx">
import viewOptions from './{表}/options.js'
import { ref, reactive, getCurrentInstance } from 'vue'
const grid = ref(null)
const { proxy } = getCurrentInstance()
const { table, editFormFields, editFormOptions, searchFormFields, searchFormOptions, columns, detail, details } = reactive(viewOptions())
let gridRef  // onInit 中赋值 gridRef = $vm
</script>
```

### 5.2 生命周期 Hook

| Hook | 时机 | 典型用途 |
|------|------|----------|
| `onInit($vm)` | 最早 | 获 gridRef；改 buttons/columns/url；配 detail；绑字典 data |
| `onInited()` | 数据对象初始化后 | height、detailOptions.columns、beginEdit |
| `dicInited(dic)` | 字典加载完 | 级联刷新 |
| `showAdvancedSearchBefore()` | 高级查询前 | return false 阻止 |

```javascript
const onInit = async ($vm) => {
  gridRef = $vm
  detail.value = { enabled: true, loadKey: 'Order_Id', url: 'api/Demo_Order/getDetailPage' }
}
const onInited = async () => {
  gridRef.height = gridRef.height - 280
  gridRef.detailOptions.columns.forEach(c => {
    if (c.field === 'Qty') c.require = true
  })
}
```

### 5.3 查询 Hook

| Hook | 参数 | 说明 |
|------|------|------|
| `searchBefore(param)` | wheres/page/sort/url/value | return false 取消；`param.wheres.push({name,value,displayType})` |
| `searchAfter(param, result)` | 结果 | 后处理 rows |
| `searchDetailBefore(param, callBack, table, item)` | 明细 | 弹框/列表明细加载前 |
| `searchDetailAfter(param, data)` | — | 明细加载后 |
| `searchSubDetailBefore/After` | 三级明细 | — |
| `resetSearchFormAfter()` | — | 查询重置后 |

**传参到后端：** `searchBefore` 中设 `param.value = { key: val }`，后端 `options.Value` 读取。

### 5.4 新建/编辑/保存 Hook

| Hook | 说明 |
|------|------|
| `modelOpenBefore(row)` / `modelOpenBeforeAsync` | 弹框打开前 |
| `modelOpenAfter(row, currentAction, isCopyClick)` | 弹框打开后，设默认值/焦点 |
| `saveConfirm(callback, formData, isAdd)` | 保存前确认，需调 callback() 继续 |
| `addBefore(formData, isCopyClick)` / `addBeforeAsync` | 新建保存前，return false 阻止 |
| `addAfter(result, isCopyClick)` | 新建保存后 |
| `updateBefore(formData)` / `updateBeforeAsync` | 编辑保存前 |
| `updateAfter(result)` | 编辑保存后 |
| `submitBefore/After(formData, isAdd, isCopyClick)` | 统一提交（async） |
| `onModelClose(iconClick)` | return false 阻止关闭 |
| `continueAddAfter(formFields, formData, res)` | 连续新建后 |
| `copyDataBefore(rows)` | 复制前 |
| `resetAddFormBefore/After` / `resetUpdateFormBefore/After` | 表单重置 |
| `resetEditFormBefore/After(row, isAdd, isCopyClick)` | 编辑表单重置 |

### 5.5 删除 Hook

| Hook | 说明 |
|------|------|
| `delBefore(ids, rows)` / `delBeforeAsync` | 主表删除前 |
| `delAfter(result)` | 主表删除后 |
| `getDelMessage(rows)` | 自定义删除提示文案 |
| `delRowBefore/After(rows, table, item, index)` | 弹框明细行删除（仅前端 table） |
| `delDetailRow(rows, table)` | 同上（旧） |

### 5.6 审批/流程/打印 Hook

| Hook | 说明 |
|------|------|
| `auditBefore(ids, rows, callback)` / `auditAfter` | 审批前/后 |
| `auditModelOpenBefore(rows, isAnti, view)` | 审批弹框前 |
| `boxAuditOptionOpenBefore(row)` | 审批选项弹框 |
| `getAuditTable(rows, isAnti, view)` | 指定审批表名 |
| `flowLoadAfter(form, result)` | 流程信息加载后 |
| `cancelAuditBefore/After` / `urgentAuditAfter` / `antiAfter` | 撤销/催办/反审 |
| `printBefore(rows)` / `printModelClose()` | 打印 |

### 5.7 导入/导出 Hook

| Hook | 说明 |
|------|------|
| `importBefore(formData, callback)` | FormData 可加额外字段；异步则 callback() |
| `importAfter(data)` | 导入后刷新 |
| `importDetailBefore(formData, callback)` / `importDetailAfter` | 明细导入 |
| `exportBefore(param)` / `exportAfter(res, param)` | 导出 |
| `getFileName(isDetail)` | 自定义导出文件名 |
| `reloadDicSource()` | 重载字典 |

### 5.8 表格交互 Hook

| Hook | 说明 |
|------|------|
| `rowClick` / `rowDbClick` | 行点击/双击 |
| `rowChange` / `selectionChange` | 复选框 |
| `selectable(row, index)` | 是否可选 |
| `beginEdit` / `endEditBefore` | 行内编辑 |
| `spanMethod` / `detailSpanMethod` | 单元格合并 |
| `sortEnd` / `detailSortEnd` | 拖拽排序 |
| `tableAddRowBefore` / `getDefaultRow` | 行内编辑默认行 |
| `headerDragend` / `detailHeaderDragend` | 列宽拖动 |
| `tabClick(name)` | 表单分组 |
| `fullscreen(full)` | 全屏 |

### 5.9 明细表 Hook

| Hook | 说明 |
|------|------|
| `detailAddRowBefore(table, item)` | 添加行，return `{}` 或 `[{}]` 默认值 |
| `addDetailRow(table, item, index)` | 二/三级表默认行 |
| `detailRowClick/Change/Selectable` | 明细行事件 |
| `detailTabsClick(table)` | 一对多 tabs |
| `detailOptions.beginEdit/endEditBefore/endEditAfter` | 明细编辑控制（onInited 中配置） |
| `checkEdit(row, column, index)` | 动态是否可编辑 |

### 5.10 字段级事件（editFormOptions / columns）

```javascript
// 编辑表单字段
const opt = gridRef.getFormOption('字段名')
opt.onChange = (val) => { }
opt.onKeyPress = ($event) => { }
opt.blur = () => { }
opt.focus = () => { }

// 查询表单
gridRef.getSearchFormOption('字段名')

// 明细列 formatter 实时计算
c.formatter = (row) => row.A + row.B
```

### 5.11 Slots 插槽

| Slot | 位置 |
|------|------|
| `gridHeader` / `gridBody` / `gridFooter` | 列表页上/中/下 |
| `btnLeft` / `btnRight` | 工具栏按钮区 |
| `modelHeader` / `modelBody` / `modelFooter` | 编辑弹框 |
| `detailContent` | 明细表按钮左侧 |
| `modelBtn` / `modelRight` | 弹框按钮 |
| `importContent` / `auditContent` / `auditButton` / `printContent` | 导入/审批/打印 |

### 5.12 ViewGrid 实例方法（gridRef）

| 方法 | 说明 |
|------|------|
| `search()` / `refresh()` | 刷新主表 |
| `getSelectRows()` / `getSelected()` | 主表选中行 |
| `getDetailSelectRows(table)` | 明细选中 |
| `getTable(table)` / `getTableRef(table)` | vol-table 实例 |
| `getFormOption(field)` / `getSearchFormOption(field)` | 字段配置 |
| `getSearchParameters()` | 当前查询参数 |
| `add()` / `edit(row)` / `del(row)` | CRUD |
| `clearSelection()` / `toggleRowSelection(row)` | 选择 |
| `setFixedSearchForm(visiable)` | 固定查询栏 |
| `updateDetailTableSummaryTotal(fields, table)` | 明细合计 |
| `searchFocus(field)` / `editFocus(field)` | 焦点 |
| `initDicKeys()` | 刷新字典 |
| `filterPermission(table, action)` | 按钮权限 |

**组件通信：**
```javascript
// 子组件 → ViewGrid
proxy.$emit('parentCall', p => p.search())
// 父 → 子 refs
proxy.$refs.gridHeader / gridFooter / modelBody
```

### 5.13 常用前端模式

**字典/下拉绑定：**
```javascript
const fieldOption = editFormOptions.flat().find(o => o.field === 'stock_in_no')
fieldOption.data = [{ key: '1', value: '选项1' }]
fieldOption.dataKey = 'key'
fieldOption.dataShowKey = 'value'
```

**字段 extra 搜索按钮：**
```javascript
fieldOption.extra = {
  icon: 'el-icon-search', text: '搜索',
  click: async () => { await handleSearch() }
}
```

**HTTP：**
```javascript
await proxy.http.get('api/Controller/Action', {}, true)
await proxy.http.post('api/Controller/Action', { param: value }, true)
// 第三参数 true = 原始 JSON 响应
```

**vol-box 弹窗：**
```vue
<vol-box v-model="visible" title="选单" :width="900" :lazy="true">
  <!-- 内容 -->
  <template #footer>
    <el-button type="primary" @click="confirm">确定</el-button>
  </template>
</vol-box>
```

**明细表操作：**
```javascript
const detailTable = gridRef.getTable()
detailTable.rowData.push({ Field1: val })
gridRef.detailOptions.columns.forEach(c => {
  if (c.field === 'qty') c.min = 1
})
```

**自定义按钮：**
```javascript
gridRef.buttons.splice(1, 0, {
  name: '自定义', icon: 'el-icon-document', type: 'primary', plain: true,
  onClick: () => { const rows = gridRef.getSelectRows() }
})
// 列按钮：columns[].render + click
// 明细按钮：detailOptions.buttons
// 弹框按钮：boxButtons
```

### 5.14 关键属性（onInit/onInited 中修改）

**主表：** `tableV2`, `columnIndex`, `ck`, `buttons`, `splitButtons`, `boxButtons`, `load`, `height`, `pagination`, `boxOptions`, `editFormFields/Options`, `searchFormFields/Options`, `single`, `summary`, `doubleEdit`, `fixedSearchForm`, `queryFields`, `url`, `hasDetail`, `continueAdd`, `rowKey`, `sortable`, `showTableAudit`, `subDetails`, `dyScript`

**detailOptions：** `columns`, `height`, `buttons`, `url`, `load`, `edit`, `doubleEdit`, `pagination`, `beginEdit`, `summary`, `tableV2`

---

## 六、独立组件 API（非 ViewGrid 场景）

### 6.1 vol-form

**Props：** `formFields`, `formRules`（二维数组）, `loadKey`, `labelWidth`, `eventNext`

**字段 type：** input, textarea, decimal, number, date/datetime, select/selectList/selectTable/selectBox/cascader/treeSelect, switch, radio, checkbox, img/file/excel, editor, password

**方法：** `validate()`, `reset()`, `focus(field)`, `initDicKeys()`, `clearValidate()`

**字段事件：** onKeyPress, onChange, blur, focus, uploadBefore/After, validator, render

### 6.2 vol-table

**Props：** columns, url/tableData, tableV2, ck, pagination, height, beginEdit/endEditBefore, spanMethod, reserveSelection, rowKey

**Emit：** loadBefore, loadAfter, selectionChange, rowClick, rowDbClick, paginationChange

**方法：** load(), addRow(), delRow(), getSelected(), setEdit(), updateSummary(), validate(), focus()

**columns：** field, title, edit.type, bind, formatter, render, click, summary, checkEdit, onChange

### 6.3 vol-box

**Props：** v-model, title, width, height, padding, lazy, onModelClose  
**Slot：** #footer

---

## 七、前端业务场景速查（生成页常用）

| 场景 | 主要 Hook/配置 |
|------|---------------|
| 查询默认值 | searchFormFields 赋值；searchBefore 改 wheres |
| 隐藏按钮 | onInit 中 filter buttons/boxButtons；filterPermission |
| 弹框选数据回填 | modelOpenAfter + vol-box/selectTable |
| select 联动 | getFormOption('A').onChange 改 B 的 data |
| 明细实时计算 | columns.formatter 或 onChange |
| 动态隐藏字段 | getFormOption('x').hidden = true |
| 只读/必填 | formReadonly 模式；column.require / readonly |
| 多 tab 页面 | tabs 路由工具 |
| 页面多开 | tables 配置多菜单 |
| 大批量表格 | tableV2: true |
| 列表下展示子表 | #gridFooter + 子组件 |
| 导入带参数 | importBefore append FormData |
| 导出自定义名 | getFileName |
| 保存前确认 | saveConfirm |
| 调用其他页编辑 | openEdit 模式 |

---

## 八、问题排查（AI 诊断清单）

| 现象 | 检查项 |
|------|--------|
| 下拉无数据 | onInit 是否设 data/dataKey/dataShowKey；字典 loadKey；API 格式 |
| POST 404 | Partial Controller 是否 `[HttpPost]`；URL `api/{表}/{Action}`；方法名大小写 |
| 表格有数据但列空白 / field 为 undefined | 自定义 API 是否 **`return JsonNormal`**（勿用 `Json`）；columns.field 与实体属性大小写一致 |
| 「未找到请求地址」 | http.js 报错 → 路由/HTTP 方法不匹配 |
| CS1513 缺少 } | Partial 文件 namespace/class 花括号 |
| CS0535 未实现接口 | IService 声明了方法但 Service 未实现 |
| 保存后数据未写入 | 是否 return webResponse.Error；OnExecuted 是否抛异常 |
| 明细未保存 | 一对多是否用 Details[]；Update 是否处理 add/update/del |
| options 修改丢失 | 是否写在 options.js（会被覆盖）→ 改 onInit |
| 审核后业务未执行 | AuditOnExecuted / AuditWorkFlowExecuted 是否注册 |
| SSR/文档站脚本 | VuePress client.ts 用 onMounted 加载第三方脚本 |

---

## 九、AI 开发工作流

### 9.1 新增自定义 API

1. `Partial/I{表}Service.cs` 声明  
2. `Partial/{表}Service.cs` 实现（可用 repository.Find/Queryable）  
3. `Partial/{表}Controller.cs` 暴露 `[HttpPost("Name")]`，**返回业务数据用 `return JsonNormal(...)`**（勿用 `Json`，否则字段变小驼峰与前端 columns 对不上）  
4. 前端 `proxy.http.post('api/{表}/Name', data, true)`

### 9.2 扩展生成页业务

1. 只改 `{表}.vue`（及子组件），不改 ViewGrid 源码  
2. onInit：buttons、detail、字典、url  
3. onInited：列属性、高度、detailOptions  
4. 按需挂 Hook：searchBefore、addBefore、modelOpenAfter 等  
5. 复杂 UI 用 slots + vol-box  

### 9.3 扩展后台 CRUD

1. 只改 `Partial/{表}Service.cs`  
2. 优先钩子，必要时 override + base.Xxx()  
3. 事务内逻辑放 OnExecuted，勿重复开事务  
4. 跨表注入其他 Repository  

---

## 十、完整技能检查清单

### A. 框架基础
- [ ] 区分会被覆盖 vs 不会覆盖的文件
- [ ] Partial 三件套（IService + Service + Controller）
- [ ] 自定义 API 返回业务数据用 **JsonNormal**（勿 Json 小驼峰）
- [ ] 标准 API 路由规则 api/{表}/{Action}
- [ ] proxy.http 第三参数 true
- [ ] gridRef = $vm 获取 ViewGrid 实例

### B. 后端查询
- [ ] QueryRelativeList / QueryRelativeExpression
- [ ] QuerySql / OrderByExpression / SummaryExpress
- [ ] GetPageDataOnExecuted / options.Value
- [ ] GetDetailPage / GetDetailSummary
- [ ] OR 查询 Fields

### C. 后端 CRUD
- [ ] AddOnExecuting / AddOnExecuted
- [ ] UpdateOnExecuting（add/update/del 明细）
- [ ] DelOnExecuting / DelOnExecuted
- [ ] 主从明细 List 强转
- [ ] 一对多 Details[]

### D. 后端审批/导入导出
- [ ] AuditOnExecuted / AuditWorkFlowExecuting
- [ ] AntiAuditOnExecuted
- [ ] ImportOnExecuting / ExportOnExecuting
- [ ] DownLoadTemplateColumns / ExcelHeaderMap

### E. 前端生命周期
- [ ] onInit / onInited / dicInited
- [ ] searchBefore（wheres + param.value）
- [ ] modelOpenBefore/After
- [ ] addBefore/updateBefore/addAfter/updateAfter
- [ ] delBefore / saveConfirm / onModelClose

### F. 前端明细/表格
- [ ] detail / details / subDetails 配置
- [ ] detailAddRowBefore / searchDetailBefore
- [ ] beginEdit / spanMethod / sortEnd
- [ ] importBefore / exportBefore / printBefore
- [ ] auditBefore / flowLoadAfter

### G. 前端 UI 扩展
- [ ] slots（gridFooter/modelBody/detailContent）
- [ ] 自定义 buttons/boxButtons/columns 列按钮
- [ ] vol-box 选单弹窗
- [ ] getFormOption 字段事件
- [ ] parentCall 组件通信

### H. 独立组件
- [ ] vol-form formRules + validate
- [ ] vol-table loadBefore + 行内编辑
- [ ] vol-box 弹窗

---

## 十一、用户自然语言需求 → AI 解析

AI 从用户消息中提取下表字段；用户不必写框架术语。

```
【表名】{TableName}
【模块路径】{module}/{folder}（如 mes/stock）
【编辑模式】标准弹框 / 新页面编辑（Edit.vue）— 不确定则看项目是否存在 Edit.vue
【前端/后端/都要】
【业务描述】例：入库单明细从采购单弹框多选；审核通过后写库存；仅查当前用户数据
【规则】触发时机、字段映射、校验、单选/多选
【数据来源】（可选）选哪张表 / API 名
```

AI 收到后按 **§零 工作流** 处理。

---

## 十二、全量业务逻辑实现手册

> **AI MUST：** 先 **§12.Z 索引** → **§12 代码模板** 生成业务代码。  
> **AI SHOULD：** 模板不足或需确认官方写法时，查 **v3.volcore.xyz**（§十三 URL 表，与离线 doc.web 同路径）。  
> **AI MUST：** 读取并修改用户项目中的 `{表}.vue` 或 **`Edit.vue`**（新页面编辑）、`Partial/{表}Service.cs`；**禁止**在 `.jsx` 写业务；勿假设存在 Demo_Order。

### 12.0 章节目录

| 章节 | 内容 |
|------|------|
| §12.A | 后台 Service **Func 委托**业务（ApplicationServiceBase + base.xxx 调用） |
| §12.B | **数据库访问 EF（§12.B.0 重点）** + cs/dev 常用功能 |
| §12.C | ViewGrid 全部 Hook（生命周期/CRUD/审批/导入导出/表格/明细） |
| §12.D | ViewGrid 公共方法、属性、Slots、主从一对多 |
| §12.E | 字段级事件（主表表单/明细表） |
| §12.F | 前端 general 通用功能 |
| §12.G | 前端 web/table 查询与表格 |
| §12.H | 前端 web/form 编辑表单 |
| §12.I | 独立组件 vol-form / vol-table / vol-box |
| §12.J | 弹出框选数据、自定义按钮、API 等 **J.1–J.13**（含对应关系总表 + 完整代码） |
| §12.K | **新页面编辑** `Edit.vue` + `vol-edit` Hook（独立页新建/编辑） |
| §12.Z | **全量操作索引**（开发者说法 → 章节号） |

---

## §12.A 后台 Service 业务扩展（Partial/{表}Service.cs）

> **文档类型：** `docs/cs/service/` — 与 `ApplicationServiceBase.cs` Func 定义一一对应。  
> **在线总览：** http://v3.volcore.xyz/docs/cs/service/guid.html  
> **源码：** `VolPro.Core/BaseProvider/ApplicationServiceBase.cs`（委托声明）、`ServiceBase.cs`（Invoke 时机）

### A.0 Func 委托机制（AI 必须遵守）

**统一写法：**

```csharp
public override WebResponseContent Add(SaveModel saveDataModel)
{
    var res = new WebResponseContent();
    AddOnExecuting = (TEntity main, object list) => {
        var details = list as List<TDetail>;
        return res.OK(); // res.Error("msg") 中止
    };
    AddOnExecuted = (TEntity main, object list) => res.OK(); // 同事务，Error 回滚
    return base.Add(saveDataModel); // MUST：框架在此 Invoke 上述 Func
}
```

**规则：**
1. Func 在 **`return base.xxx(...)` 之前** 赋值到 `this` 上。
2. `WebResponseContent.Error` 或 `Status=false` → 中止后续步骤；`*OnExecuted` 内 Error → **事务回滚**。
3. 异步接口（生成器勾选）：重写 `AddAsync` 等，可另赋 `AddOnExecutingAsync`；与同步版 **链式都执行**。
4. **禁止** 在 Partial 中重写整个 Save 逻辑；仅通过 Func 扩展。完整自定义 API 用 Partial Controller（§12.J.10）。

**⚠ YZH 硬约束（踩坑 P2-02，2026-08-03）：Partial 构造函数必须显式注入 `[ActivatorUtilitiesConstructor]`**

Autofac + ServiceBase 多层继承在 Partial 类声明自定义构造函数时，若未加 `[ActivatorUtilitiesConstructor]` 特性会回退到无参构造，导致 `repository`/`unitWork` 全部 `null`，`GetPageData` 报 `NullReferenceException`。

**CertPlatform 域所有 Partial Service 必须使用以下构造函数模板，缺一不可：**

```csharp
using Microsoft.Extensions.DependencyInjection;   // ActivatorUtilitiesConstructor
using VOL.Core.BaseProvider;
using VOL.Core.Utilities;

namespace VOL.Builder.Services.CertPlatform
{
    public partial class CertCertificationBodyService  // 其他服务同理
    {
        // ①【必须】告诉 Autofac 用这个构造函数注入
        [ActivatorUtilitiesConstructor]
        public CertCertificationBodyService(
            IRepository<CertificationBody> repository,   // ② 强类型仓储
            IUnitWork unitWork
        ) : base(repository, unitWork)                  // ③ 必须传 base 构造
        {
            this.repository = repository;               // ④ 兜底赋值，防反射偶尔丢失
        }

        // ⑤ GetPageData 兜底：若反射仍拿不到 repository，手动 Autofac 解析
        public override PageGridData<CertificationBody> GetPageData(PageDataOptions options)
        {
            if (this.repository == null)
            {
                this.repository =
                    AutofacContainerModule.GetService<IRepository<CertificationBody>>();
            }
            return base.GetPageData(options);
        }
    }
}
```

同时 **`VOL.Core/EFDbContext/VOLContext.cs` 实体准入扫描需三层校验**（踩坑 P2-03）：
```csharp
1. typeof(BaseEntity).IsAssignableFrom(type)          // 继承链深度扫描
2. !type.IsDefined(typeof(NotMappedAttribute))         // 过滤 NotMapped DTO/Option 类
3. type.GetProperty("DBServer") != null                // 过滤 object 属性容器
```
任何 FilterOptions / DTO / ViewModel 类都必须第一行加 `[NotMapped]`。

**执行顺序速查：**

| 操作 | 顺序 |
|------|------|
| **GetPageData** | QueryRelativeList → QuerySql/租户 → QueryRelativeExpression → 分页 → SummaryExpress → GetPageDataOnExecuted |
| **Add** | AddOnExecute → 实体转换校验 → AddOnExecuting(Async) → 事务 Insert → AddOnExecuted(Async) → AddWorkFlow* |
| **Update** | UpdateOnExecute → 转换校验 → UpdateOnExecuting(Async) → 事务 Update 主从 → UpdateOnExecuted(Async) |
| **Del** | DelOnExecuting(Async) → 事务 Delete → DelOnExecuted(Async) |

**Func 与子文档：**

| Func 属性 | 文档 |
|-----------|------|
| QueryRelativeList / QueryRelativeExpression / SummaryExpress / GetPageDataOnExecuted | search.md |
| AddOnExecute / AddOnExecuting / AddOnExecuted / AddWorkFlow* | add.md |
| UpdateOnExecute / UpdateOnExecuting / UpdateOnExecuted | update.md |
| DelOnExecuting / DelOnExecuted | del.md |
| Audit* / AntiAudit* | audit.md / antiAudit.md |
| Import* / Export* | importData.md / exportData.md |

### A.1 主表查询 GetPageData

```csharp
public override PageGridData<{T}> GetPageData(PageDataOptions options)
{
    object extraValue = options.Value; // 前端 searchBefore 的 param.value

    QueryRelativeList = (List<SearchParameters> parameters) => {
        foreach (var item in parameters) {
            if (item.Name == "{字段}") { var v = item.Value; item.Value = null; }
        }
    };

    QueryRelativeExpression = (IQueryable<{T}> q) => {
        // q = q.Where(x => x.CreateID == UserContext.Current.UserId);
        // q = q.Include(x => x.{明细});
        return q;
    };

    // QuerySql = @"select 全部列 from {表} where ..."; // 原生 SQL

    OrderByExpression = x => new Dictionary<object, QueryOrderBy>() {
        { x.CreateDate, QueryOrderBy.Desc }
    };

    SummaryExpress = (IQueryable<{T}> q) => q.GroupBy(x => 1).Select(g => new {
        TotalQty = g.Sum(x => x.Qty)
    }).FirstOrDefault();

    GetPageDataOnExecuted = (PageGridData<{T}> grid) => {
        // 改 grid.rows
    };

    return base.GetPageData(options);
}
```

### A.2 明细表查询 GetDetailPage / 明细合计

```csharp
public override object GetDetailPage(PageDataOptions pageData)
{
    // 一对多：pageData.TableName 区分明细表
    // return base.GetDetailPage(pageData);
    return base.GetDetailPage(pageData);
}

protected override object GetDetailSummary<{TDetail}>(IQueryable<{TDetail}> queryable)
{
    return queryable.GroupBy(x => 1).Select(g => new { Total = g.Sum(x => x.Amount) }).FirstOrDefault();
}
```

### A.3 OR 多字段查询

前端 searchBefore：
```javascript
param.wheres.push({ name: 'Keyword', value: 'xxx', displayType: 'like', fields: ['Field1','Field2','Field3'] })
```

### A.4 新建 Add

```csharp
public override WebResponseContent Add(SaveModel saveDataModel)
{
    var res = new WebResponseContent();
    // 可选：实体转换前操作原始 SaveModel
    AddOnExecute = (SaveModel m) => res.OK();

    AddOnExecuting = ({T} main, object list) => {
        var details = list as List<{TDetail}>;
        // 校验、赋默认值（写库前）
        return res.OK();
    };
    AddOnExecuted = ({T} main, object list) => {
        // 同事务写关联表；return res.Error("msg") 回滚
        return res.OK();
    };
    AddWorkFlowExecuting = (main) => true;
    AddWorkFlowExecuted = (main, userIds) => { };
    return base.Add(saveDataModel);
}
```

### A.5 修改 Update

```csharp
public override WebResponseContent Update(SaveModel saveDataModel)
{
    var res = new WebResponseContent();
    // 非前端提交字段：saveDataModel.MainData["字段"]="值" 或 UpdateOnExecute
    UpdateOnExecute = (SaveModel m) => res.OK();

    UpdateOnExecuting = ({T} main, object addList, object updateList, List<object> delKeys) => {
        var add = addList as List<{TDetail}>;
        var upd = updateList as List<{TDetail}>;
        // 一对多：MultipleTableEntity.GetAddList(typeof(TDetail2), null)
        return res.OK();
    };
    UpdateOnExecuted = ({T} main, object addList, object updateList, List<object> delKeys) => res.OK();
    return base.Update(saveDataModel);
}
```

### A.6 删除 Del

```csharp
public override WebResponseContent Del(object[] keys, bool delList = true)
{
    var res = new WebResponseContent();
    DelOnExecuting = (_keys) => {
        // 已审核不可删
        return res.OK();
    };
    DelOnExecuted = (_keys) => res.OK();
    return base.Del(keys, delList);
}
```

### A.7 审批 Audit

```csharp
public override WebResponseContent Audit(object[] keys, int? auditStatus, string auditReason)
{
    var res = new WebResponseContent();
    AuditWorkFlowExecuting = ({T} order, AuditStatus status, bool lastAudit) => res.OK();
    AuditWorkFlowExecuted = ({T} order, AuditStatus status, List<int> nextUserIds, bool lastAudit) => {
        if (lastAudit) { /* 流程结束写业务 */ }
        return res.OK();
    };
    AuditOnExecuting = (List<{T}> list) => res.OK();
    AuditOnExecuted = (List<{T}> list) => res.OK();
    return base.Audit(keys, auditStatus, auditReason);
}
```

**流程信息：** `order.GetTableWorkflow()` / `GetTableCurrentFlowStep()` / `GetTablePreFlowStep()`

### A.8 反审 AntiAudit

```csharp
public override WebResponseContent AntiAudit(AntiData antiData)
{
    var res = new WebResponseContent();
    AntiAuditOnExecuting = (entity) => res.OK();
    AntiAuditOnExecuted = (entity) => { /* 回滚库存等 */ return res.OK(); };
    return base.AntiAudit(antiData);
}
```

### A.9 导入 Import / 模板 / 导出 Export

```csharp
public override WebResponseContent Import(List<IFormFile> files)
{
    ImportOnExecuting = (List<{T}> rows) => { /* 校验 */ return new WebResponseContent().OK(); };
    ImportOnExecuted = (List<{T}> rows) => new WebResponseContent().OK();
    ImportStartRowIndex = 1;
    // ExcelHeaderMap, ImportOnReadCellValue
    return base.Import(files);
}

public override WebResponseContent Export(PageDataOptions pageData)
{
    ExportColumns = x => new { x.Field1, x.Field2 };
    ExportOnExecuting = (List<{T}> rows, List<string> ignore) => new WebResponseContent().OK();
    Limit = 10000;
    return base.Export(pageData);
}
```

### A.10 上传 Upload

```csharp
public override WebResponseContent Upload(List<IFormFile> files)
{
    UploadFolder = "Upload/Tables/{表}/";
    IsRoot = true;
    return base.Upload(files);
}
```

### A.11 打印 Print（Startup + PrintFilter）

```csharp
// Startup: PrintContainer.Use<{T}, {TDetail}>("模板名", mainFields, detailFields);
public class {T}PrintFilter : PrintFilter<{T}> {
    public override IQueryable<{T}> Query(PrintQuery query) => base.Query(query);
}
```

### A.12 自定义 API 三件套

见 §12.J.10（IService 声明 + Service 实现 + Controller `[HttpPost("Action")]`）。

---

## §12.B 后台 cs/dev 常用操作

### B.0 数据库访问（EF，默认必用 · 重点）

> **在线文档：** [db.html — EF 操作数据库](http://v3.volcore.xyz/docs/cs/dev/db.html)  
> **非必要勿用：** [EFsql.html — EF 执行原生 SQL](http://v3.volcore.xyz/docs/cs/dev/db/EFsql.html)（`repository.QueryList` / Dapper 参数化 SQL）。AI **默认只用 EF LINQ**，仅当 LINQ 无法表达复杂 SQL 时才考虑原生 SQL，且须说明理由。

#### B.0.1 写代码位置与访问方式

| 规则 | 说明 |
|------|------|
| **写哪里** | `Partial/{表}Service.cs`（业务查询/写库）；Controller 仅转发或简单组合 |
| **用什么** | 当前表 `repository`（`ServiceBase` 注入）；**其他表** ctor 注入 `I{其他表}Repository` |
| **禁止** | 在 Controller 直接写复杂 LINQ（应放 Service）；非 Partial Service；默认走 EFSql/Dapper |
| **NoTracking** | EF 默认 NoTracking；**更新 MUST** `repository.Update(model, cols, true)` + `SaveChanges()` |

```csharp
// 跨表：Partial Service 构造方法注入
public partial class Sys_WorkFlowTableService
{
    private readonly ISys_WorkFlowTableRepository _repository;
    private readonly ISys_WorkFlowTableStepRepository _stepRepository;
    [ActivatorUtilitiesConstructor]
    public Sys_WorkFlowTableService(
        ISys_WorkFlowTableRepository dbRepository,
        IHttpContextAccessor httpContextAccessor,
        ISys_WorkFlowTableStepRepository stepRepository)
        : base(dbRepository)
    {
        _repository = dbRepository;
        _stepRepository = stepRepository; // 操作其他表
    }
}
```

#### B.0.2 EF 查询（repository LINQ）

```csharp
using Microsoft.EntityFrameworkCore;
using VolPro.Core.Extensions; // TakePage、WhereIF

// 单表
var list = repository.FindAsIQueryable(x => x.Enable == 1).ToList();
var one = await repository.FindAsIQueryable(x => x.Id == id).FirstOrDefaultAsync();
var data = repository.FindAsIQueryable(x => true)
    .Select(s => new { s.Field1, s.Field2 }).ToList();

// 模糊 / in
repository.FindAsIQueryable(x => x.Name.Contains(kw)).ToList();
repository.FindAsIQueryable(x => x.Name.StartsWith(kw)).ToList();
repository.FindAsIQueryable(x => x.Name.EndsWith(kw)).ToList();
var ids = new List<int> { 1, 2, 3 };
repository.FindAsIQueryable(x => ids.Contains(x.Id)).ToList();

// 动态条件 WhereIF
repository.WhereIF(!string.IsNullOrEmpty(kw), x => x.Name.Contains(kw))
    .WhereIF(status > 0, x => x.Status == status)
    .ToList();

// 主子表 Include（生成器已配置明细 navigation）
var list = repository.FindAsIQueryable(x => true).Include(x => x.明细表).ToList();

// 是否存在
var exists = repository.Exists(x => x.Code == code);
var existsAsync = await repository.ExistsAsync(x => x.Code == code);
```

#### B.0.3 分页 — 一律 TakePage

**MUST：** 所有 IQueryable 分页使用 **`TakePage(page, rows)`**（框架扩展，内部处理 Skip/Take），**禁止**手写 `Skip((page-1)*rows).Take(rows)`。

```csharp
// 普通分页
var page = 1; var rows = 30;
var list = repository.FindAsIQueryable(x => 条件).TakePage(page, rows).ToList();

// 带排序 + 匿名投影
var query = repository.FindAsIQueryable(x => x.Enable == 1);
var total = await query.CountAsync();
var data = await query.OrderByDescending(x => x.CreateDate)
    .TakePage(options.Page, options.Rows)
    .Select(s => new { s.Id, s.Name }).ToListAsync();
```

#### B.0.4 vol-table / selectTable 自定义分页接口（ConvertQueryFilter）

**场景：** 独立 `vol-table`、`selectTable` 的 `item.url` / `url` 指向**自定义接口**（非标准 `getPageData`）。前端 POST **`PageDataOptions`**（含 `Page`、`Rows`、`Wheres`/`Filter`、`Sort`、`Value` 等），与 view-grid 查询格式一致。

**后台 MUST：**

1. 参数 `[FromBody] PageDataOptions options`
2. **`options.ConvertQueryFilter<{实体}>()`** — 将前端 `wheres` 自动转为 LINQ（`Sys_User` 换为实际查询实体 `{T}`）
3. 业务过滤 `.Where(x => ...)`
4. **`CountAsync()`** 得 total
5. 排序 + **`TakePage(options.Page, options.Rows)`** + `Select` 投影
6. 返回 **`{ total, rows }`**；Controller **`return JsonNormal(...)`**（字段与前端 `columns.field` 大小写一致）

```csharp
// Partial/I{表}Service.cs
Task<object> SearchSysUser(PageDataOptions options);

// Partial/{表}Service.cs — Sys_User 换为要查询的实体
public async Task<object> SearchSysUser(PageDataOptions options)
{
    var query = options.ConvertQueryFilter<Sys_User>().Where(x => x.Enable == 1);
    var total = await query.CountAsync();
    var rows = await query.OrderByDescending(x => x.User_Id)
        .TakePage(options.Page, options.Rows)
        .Select(s => new
        {
            s.User_Id,
            s.UserName,
            s.UserTrueName,
            s.Gender,
            s.PhoneNo,
            s.Email
        }).ToListAsync();
    return new { total, rows };
}

// Partial/{表}Controller.cs
[HttpPost("SearchSysUser")]
public async Task<IActionResult> SearchSysUser([FromBody] PageDataOptions options)
{
    return JsonNormal(await _service.SearchSysUser(options));
}
```

**前端 wheres 来源：** vol-table `loadBefore` 的 `param.wheres`；selectTable 列设 `search: true` 时框架自动写入 wheres；或 `loadBefore` 设 `param.value` 由后台在 `ConvertQueryFilter` 之外再 `Where` 处理。

**selectTable 推荐：** 自定义 `api/{表}/SearchXxx` + 上模板，优于直接 `getPageData`（权限/字段可控）。见 §12.J.3。

#### B.0.5 新建 / 更新 / 删除

```csharp
// 新建
var model = new 表Model();
model.SetCreateDefaultVal(); // 可选：创建人、创建时间
repository.Add(model);
repository.SaveChanges(); // 保存后主键自动回填

// 批量新建
repository.AddRange(list);
repository.SaveChanges();

// 更新（主键必须有值）
model.SetModifyDefaultVal();
repository.Update(model, x => new { x.Field1, x.Field2 });
repository.SaveChanges();

// 批量更新
repository.UpdateRange(list, x => new { x.Field1 }, false);
repository.SaveChanges();

// 按条件更新/删除（不立即提交时第三参数 false）
repository.Update(model, x => new { x.Status }, false);
repository.Delete(x => 条件, false);

// 删除单条 / 批量主键
repository.Delete(model);
repository.DeleteWithKeys(new object[] { id1, id2 });
repository.SaveChanges();
```

#### B.0.6 事务与其他

```csharp
// EF 事务
repository.DbContextBeginTransaction(() =>
{
    repository.Add(main);
    _otherRepository.Add(detail);
    repository.SaveChanges();
    return webResponse.OK();
});

// 调试 SQL
Console.WriteLine(queryable.ToQueryString());

// 原生 EF Set（少用）
repository.DbContext.Set<Sys_User>();
```

#### B.0.7 与生成页 GetPageData 的分工

| 场景 | 做法 |
|------|------|
| 标准生成页列表 | **不重写** GetPageData；用 `QueryRelativeExpression` / `QueryRelativeList`（§12.A.1） |
| vol-table / selectTable / 弹框内嵌表自定义 url | §12.B.0.4：`ConvertQueryFilter` + `TakePage` + `{ total, rows }` |
| 非分页列表 API | `repository.FindAsIQueryable(...).Take(N)` 或 ToList |
| 复杂 SQL 非 EF 可表达 | 最后手段：EFsql / Dapper（文档 EFSql），须注释说明 |

---

### B.1 其他 cs/dev 速查

| 操作 | 实现要点 |
|------|----------|
| **调试** | 断点 Partial Service；`Console.WriteLine(queryable.ToQueryString())` |
| **appsettings** | `AppSetting.GetSettingString(key)` / 注入 `IOptions` |
| **API 参数** | POST `[FromBody] Dictionary<string,object>`；GET 查询字符串 |
| **JSON 返回大小写** | 自定义接口 **`return JsonNormal(data)`**；全局 `Json()` 为小驼峰；标准 getPageData 已 JsonNormal |
| **接口免登录** | `[AllowAnonymous]` on Partial Controller Action |
| **重写接口权限** | Partial Controller 重写方法或框架权限过滤器配置 |
| **序列化** | Newtonsoft / System.Text.Json 按项目配置 |
| **字典** | `DictionaryManager.GetDictionary("dicNo")` |
| **EF 查询/分页** | §12.B.0 · `FindAsIQueryable` / `WhereIF` / **`TakePage`** |
| **EF 条件更新删除** | §12.B.0.5 · `Update` / `Delete` / `SaveChanges` |
| **EF 事务** | `repository.DbContextBeginTransaction(() => {})` |
| **EF 多表** | `Include` / `Join` in QueryRelativeExpression 或 ctor 注入多 Repository |
| **EF 原生 SQL** | **非默认**；见 EFSql 文档，优先 LINQ |
| **Dapper / SqlSugar** | **非默认**；跨库或特殊场景 |
| **HttpContext** | 注入 `IHttpContextAccessor` |
| **当前用户** | `UserContext.Current.UserId` / `UserName` |
| **用户权限** | 角色/菜单 API |
| **单据号** | 规则编码服务 |
| **定时任务** | 后台 Task 接口配置 |
| **DI 注入** | ctor 注入 `I{X}Repository` / `I{X}Service` |
| **Repository 实例** | `{T}Repository.Instance` 或 ctor 注入 |
| **视图生成代码** | 代码生成器配置视图 |
| **缓存** | Memory / Redis 封装 |
| **Model 校验** | DataAnnotations / FluentValidation |
| **日志** | `Logger.Info(LoggerType.Info, message)` |
| **租户过滤** | QueryRelativeExpression 加租户字段 |
| **SqlBulkInsert** | 批量扩展方法 |
| **工具类** | `VolPro.Core.Utilities` / 项目 util |

---

## §12.C ViewGrid 全部 Hook（{表}.vue 绑定 :hookName="fn"）

> **文档类型：** `docs/view-grid/` — **view-grid 组件**对标准生成页的属性/方法/Slots；**不是** web 示例集。  
> **适用文件：** 标准模式 `{表}.vue`；新页面编辑的列表页仍用 view-grid，编辑页见 **§12.K**。

**通用规则：** 返回 `false` 取消操作；async 钩子可返回 Promise；`gridRef = $vm` 在 onInit。

| Hook | 参数 | 返回值/作用 | 代码模板 |
|------|------|-------------|----------|
| onInit | $vm | — | `gridRef=$vm` |
| onInited | — | — | 改 height/columns/detailOptions |
| showAdvancedSearchBefore | — | bool | `return true` |
| dicInited | dic | — | 级联 initDicKeys |
| searchBefore | param | bool | `param.wheres.push({name,value,displayType}); param.value={}; return true` |
| searchAfter | param,result | bool | 后处理 result.rows |
| searchDetailBefore | param,callback,table,item | bool | 改 param.wheres |
| searchDetailAfter | param,data | bool | — |
| searchSubDetailBefore/After | rows,table,item | bool | 三级明细 |
| resetSearchFormAfter | — | — | 重置后 |
| modelOpenBefore | row | bool | 打开弹框前 |
| modelOpenBeforeAsync | row | Promise | 异步取数 |
| modelOpenAfter | row,action,isCopy | — | 默认值/焦点 editFocus |
| saveConfirm | callback,formData,isAdd | — | `proxy.$confirm().then(()=>callback(true))` |
| addBefore | formData,isCopy | bool | 校验 return false 阻止 |
| addBeforeAsync | formData,isCopy | `Promise&lt;bool&gt;` | — |
| addAfter | result,isCopy | bool | 刷新 search() |
| updateBefore | formData | bool | — |
| updateBeforeAsync | formData | `Promise&lt;bool&gt;` | — |
| updateAfter | result | bool | — |
| submitBefore | formData,isAdd,isCopy | `Promise&lt;bool&gt;` | 统一提交前 |
| submitAfter | result,formData,isAdd,isCopy | Promise | 提交后 |
| delBefore | ids,rows | bool | 删除前 |
| delBeforeAsync | ids,rows | `Promise&lt;bool&gt;` | — |
| delAfter | result | bool | — |
| getDelMessage | rows | string | 自定义提示 |
| delRowBefore/After | rows,table,item,index | bool | 弹框明细行 |
| delDetailRow | rows,table | bool | 旧明细删行 |
| auditBefore | ids,rows,callback | bool | 审批前 |
| auditAfter | result,rows | bool | — |
| auditModelOpenBefore | rows,isAnti,view | bool | — |
| boxAuditOptionOpenBefore | row | — | — |
| getAuditTable | rows,isAnti,view | string | 返回表名 |
| flowLoadAfter | form,result | — | 流程加载后 |
| cancelAuditBefore/After | rows,isCancel / result,rows,isCancel | bool | 撤销 |
| urgentAuditAfter | result,rows | bool | 催办后 |
| antiAfter | result,param,rows,file | bool | 反审后 |
| importBefore | formData,callback | bool | `formData.append('k',v)` |
| importAfter | data | bool | search() |
| importDetailBefore | formData,callback | bool | 明细导入 |
| importDetailAfter | rows | bool | — |
| exportBefore | param | bool | — |
| exportAfter | res,param | — | — |
| getFileName | isDetail | string | `'导出_'+Date.now()` |
| reloadDicSource | — | — | — |
| onModelClose | iconClick | bool | return false 阻止关闭 |
| printBefore | rows | bool | — |
| printModelClose | — | — | — |
| selectable | row,index | bool | 能否勾选 |
| rowClick/rowDbClick | {row,column,event} | — | — |
| rowChange/selectionChange | rows | — | — |
| beginEdit/endEditBefore | row,column,index | bool | 行内编辑 |
| spanMethod/detailSpanMethod | merge args, rows | — | Element 合并 |
| sortEnd/detailSortEnd | indices,rows | — | 拖拽排序 |
| tableAddRowBefore/getDefaultRow | row,index | bool/object | 默认行 |
| detailAddRowBefore | table,item | object/array | return `{字段:默认值}` |
| addDetailRow | table,item,index | — | 二三级默认行 |
| detailRowClick/Change/Selectable | ... | — | 明细行事件 |
| detailTabsClick | table | — | 一对多 tabs |
| checkEdit | row,column,index | bool | 动态可编辑 |
| tabClick | name | — | 表单分组 |
| copyDataBefore | rows | bool | 复制 |
| continueAddAfter | formFields,formData,res | — | 连续新建 |
| resetAdd/Update/EditForm Before/After | ... | bool | 表单重置 |
| headerDragend/detailHeaderDragend | width event,table | — | 列宽 |
| fullscreen | full | bool | 全屏 |
| mounted | — | — | 少用 |

**saveConfirm 完整示例：**
```javascript
const saveConfirm = (callback, formData, isAdd) => {
  proxy.$confirm('确认保存?', '提示', { type: 'warning' })
    .then(() => callback(true))
}
```

**importDetailBatch（大批量明细导入防卡顿）：** 分批 push 到 rowData，用 `setTimeout` 或 requestAnimationFrame 分帧。

---

## §12.D ViewGrid 公共方法 / 属性 / Slots / 主从

### D.1 公共方法（gridRef）

| 方法 | 用法 |
|------|------|
| search()/refresh() | 刷新主表 |
| getSelectRows()/getSelected() | 选中行 |
| getDetailSelectRows(table) | 明细选中 |
| getTable(table)/getTableRef(table) | 明细 vol-table |
| getElementTableRef() | Element table |
| getFormOption(field)/getSearchFormOption(field) | 字段配置 |
| getSearchParameters() | 当前 wheres |
| add()/edit(row)/del(row) | 调框架 CRUD |
| clearSelection()/toggleRowSelection(row) | 选择 |
| setFixedSearchForm(true) | 固定查询 |
| updateDetailTableSummaryTotal(fields,table) | 明细合计 |
| searchFocus(field)/editFocus(field,callback) | 焦点 |
| initDicKeys() | 刷新字典 |
| filterPermission(table,action) | 权限 |
| rowData() | 主表全部行 |
| getCurrentAction() | Add/Update 等 |

### D.2 常用属性（onInit/onInited）

```javascript
gridRef.tableV2 = true          // 高性能表格
gridRef.single = true           // 单选
gridRef.ck = false              // 隐藏 checkbox
gridRef.fixedSearchForm = true  // 默认展开查询
gridRef.continueAdd = true      // 连续添加
gridRef.doubleEdit = true       // 双击编辑
gridRef.url = 'api/{表}/'       // 接口前缀【⚠ 必须以 / 结尾，缺尾斜杠会导致 404 → urlgetPageData，踩坑 P2-06】
gridRef.pagination = { size: 30, sizes: [30,60,100] }
gridRef.boxOptions.width = 1000
gridRef.queryFields = ['Field1','Field2'] // 多快捷查询
gridRef.dyScript = `{ ... }`    // 动态脚本扩展
```

### D.3 Slots

`#gridHeader` `#gridBody` `#gridFooter` `#btnLeft` `#btnRight` `#modelHeader` `#modelBody` `#modelFooter` `#detailContent` `#modelBtn` `#modelRight` `#importContent` `#auditContent` `#auditButton` `#printContent`

> **⚠ 强约束（YZH-Framework 特定，踩坑 P2-04，2026-08-03）**  
> Vol 框架的 `ViewGridProvider.jsx` 对插槽内容做了 `parentNode` 手动遍历，**与 Vue 3 Fragment 多根节点 v-if 卸载时机冲突**会导致
> `TypeError: Cannot read properties of null (reading 'parentNode')`。
>
> **所有插槽内容必须包裹在单一根节点 `<div>` 中**，即使只有一个子元素也要包：
>
> ```vue
> <!-- 正确：单根 -->
> <template #gridHeader>
>   <div>
>     <el-alert v-if="desc" ... />
>   </div>
> </template>
>
> <template #btnLeft>
>   <div>
>     <el-button ... />
>   </div>
> </template>
>
> <!-- 错误：Fragment 多根（哪怕只有一个 el-alert） -->
> <template #gridHeader>
>   <el-alert v-if="desc" ... />
> </template>
> ```
>
> 详见 YZH 知识库踩坑记录：`docs/60-AI工程设计/YZH-知识库/05-踩坑记录/2026-08-03_Phase2联调全栈问题修复记录.md` P2-04。

### D.4 主从 / 一对多 / 三级

```javascript
// 主从（单明细）
detail.value = { enabled: true, loadKey: '{主键}', url: 'api/{表}/getDetailPage' }

// 一对多
details.value = [
  { cnName: '明细1', table: '{Detail1}', columns: [...], url: '...' },
  { cnName: '明细2', table: '{Detail2}', ... }
]

// 三级 subDetails 在 onInit 配置 subDetails 数组
```

---

## §12.E 字段级事件（view-grid 内嵌 vol-form / vol-table）

> **机制：** view-grid 把 `editFormOptions`、`columns`、`detailOptions.columns` 传给内嵌 vol-form/vol-table；事件写在 **options 项**上，通过 `getFormOption` / 遍历 columns 绑定。  
> **文档：** http://v3.volcore.xyz/docs/view-grid/components.html · `event/forminput`、`event/detailforminput` 等。

### E.0 绑定位置对照

| UI 区域 | 内嵌组件 | 绑定方式 | 典型事件 |
|---------|----------|----------|----------|
| 查询区 | vol-form | `getSearchFormOption('字段')` | onChange |
| 编辑弹框主表 | vol-form | `getFormOption('字段')` | onKeyPress、onChange、blur、focus |
| 主表列表 | vol-table | `gridRef.columns` 列配置 | formatter、click；行事件用 Hook rowClick |
| 编辑弹框明细 | vol-table | `detailOptions.columns` | onKeyPress、onChange、blur、formatter、checkEdit |

在 `{表}.vue` 的 **`onInit`（主表表单/列）** 或 **`onInited`（明细列，字典已加载）** 中绑定。

### E.1 主表编辑表单

```javascript
const bindFormEvent = (field, events) => {
  const opt = gridRef.getFormOption(field)
  Object.assign(opt, events)
}
bindFormEvent('Qty', {
  onKeyPress: ($e) => { if ($e.keyCode === 13) { /*回车*/ } },
  onChange: (val) => { editFormFields.Amount = val * editFormFields.Price },
  blur: () => {},
  focus: () => {}
})
```

### E.2 查询表单：getSearchFormOption('字段').onChange

### E.3 明细 columns

```javascript
c.onKeyPress / c.onChange / c.blur / c.formatter = (row) => {}
c.checkEdit = (row, column, index) => row.Status !== 1
```

> **⚠ 强约束（踩坑 P2-05，2026-08-03）**  
> `columns.formatter` **必须是函数，不能是 `true`/字符串/其他非函数值**。  
> 错误配置 `formatter: true` 会导致 VolTable.vue 渲染时抛出：
> `TypeError: column.formatter is not a function`，随后 Vue 3 二次抛：
> `TypeError: Cannot destructure property 'type' of 'vnode' as it is null.`
>
> **正确写法（示例）**：
> ```javascript
> c.formatter = (row, col, idx) => row.Status === 1 ? '<span class="tag-success">已通过</span>' : '<span class="tag-warning">待审</span>'
> // 或者
> const dateCol = gridRef.columns.find(c => c.field === 'CreateDate')
> dateCol.formatter = (row) => dayjs(row.CreateDate).format('YYYY-MM-DD HH:mm')
> ```
>
> **排查**：发现 VolTable 渲染红屏时先 `grep 'formatter: true'` 全局搜索 options.js 与 onInit 配置。
> 详见踩坑记录 P2-05。

### E.4 事件对照

| 事件 | 适用控件 |
|------|----------|
| onKeyPress | input/textarea/decimal |
| onChange | select/date/cascader |
| blur/focus | 输入框 |
| formatter | 明细实时计算显示 |

---

## §12.F 前端 general 通用功能

> **文档类型：** `docs/web/general/` — **常用功能示例**（怎么做弹框选数、自定义按钮等）；Hook 签名以 **view-grid** 或 **edit** 为准。

| 功能 | 实现 |
|------|------|
| **HTTP 请求** | `proxy.http.post('api/{表}/{Action}', params, true)` GET 同理 |
| **文件下载** | http.post 返回 blob；或 window.open(url) |
| **自定义按钮** | §12.J.5 |
| **隐藏按钮** | `gridRef.buttons.find(x=>x.name==='新建').hidden=true` |
| **权限按钮** | `filterPermission(table,'Add')` |
| **vol-box 多弹框** | 多个 vol-box + v-model |
| **弹出框选主表数据** | §12.J.1 |
| **弹出框选明细** | §12.J.2 |
| **selectTable** | §12.J.3 |
| **事件处理** | §12.E |
| **列表显示明细** | §12.J.8 gridFooter + rowClick |
| **viewDetail 一对多显示** | onInited 配置 detail 在列表页展开 |
| **日期计算** | dayjs/moment 在 onChange |
| **tabs 跳转** | `proxy.$tabs.open(...)` 或路由 |
| **页面多开 tables** | 菜单多开配置 |
| **saveConfirm** | §12.C saveConfirm |
| **openEdit 调其他页弹框** | 引入 `{其他表}.vue` ref 调 `$refs.grid.edit(row)` / `add()` |
| **pagecache 禁缓存** | 路由 meta + keep-alive 配置 |
| **全局 cache** | localStorage / 框架 storage |
| **tableV2** | `gridRef.tableV2=true` |
| **slot 扩展** | §12.D.3 |
| **debug** | console + Vue DevTools |

**openEdit 模板：**
```vue
<OtherTable ref="otherRef" style="display:none" />
<script>
import OtherTable from '@/views/{模块}/{其他表}/{其他表}.vue'
const otherRef = ref()
// 按钮：otherRef.value.$refs.grid.edit({ Id: row.Id })
</script>
```

---

## §12.G 前端 web/table 查询与表格配置

**统一位置：** `onInit` / `onInited` 改 `columns`、`searchFormOptions`、`detailOptions`、`gridRef.*`

| 功能 | 代码要点 |
|------|----------|
| **查询自定义按钮 btn** | detailOptions/buttons/boxButtons + columns.render |
| **dySearch 动态查询** | searchBefore 动态 wheres；子查询 param.value |
| **dropBtn 下拉按钮组** | buttons 配置 children |
| **sort 默认排序** | searchBefore `param.sort={Field:'CreateDate',Order:'desc'}` |
| **ck 禁止勾选** | selectable 返回 false |
| **fixed 固定列** | columns `fixed:'left'/'right'` |
| **showFilter 显示全部查询** | setFixedSearchForm(true) |
| **defaultVal 查询默认值** | onInit `searchFormFields.{字段}=值` |
| **singleDate 单日期** | searchFormOptions type/date 配置 |
| **card 卡片切换** | gridRef 卡片模式属性 |
| **searchSelectChange 查询联动** | getSearchFormOption onChange |
| **removeQuery 移除快捷查询** | queryFields 过滤 |
| **queryFields 多快捷字段** | gridRef.queryFields=[...] |
| **customCell 自定义显示** | columns.render |
| **formatter 格式化** | columns.formatter |
| **cellEvent 单元格点击** | columns.click |
| **rowClick 行点击** | :rowClick |
| **tips 超出换行** | columns `showOverflowTooltip`/textInline |
| **customTips 自定义提示** | render + tooltip |
| **treeTable 树形** | rowKey + parentId 数据 |
| **headFilter 表头筛选** | columns 筛选配置 |
| **headEvent 自定义表头** | columns title render |
| **tagColor 数据源颜色** | columns bind 字典颜色 |
| **switch 单选** | gridRef.single=true |
| **editTable 在线编辑** | doubleEdit/clickEdit + beginEdit |
| **tableSwitch 显示 switch** | columns edit type switch |
| **head 多级表头** | columns children 嵌套 |
| **summary 合计** | columns summary:true + 后端 SummaryExpress |
| **hideColumn 动态隐藏列** | onInited columns.hidden=true |
| **page/hidePage 分页** | pagination / paginationHide |
| **rows 获取行** | getSelectRows/getTable().rowData |
| **rowToggle 点击选中** | rowClick 内 toggleRowSelection |
| **showRowNum 行号** | columnIndex |
| **qrcode 二维码** | columns render QR |
| **progress 进度条** | columns render el-progress |
| **merge/mergeField 合并** | spanMethod |
| **rowColor 行颜色** | columns cellStyle 或 rowStyle |
| **fxColumn 固定对齐** | align/fixed |
| **detailBtn 明细按钮** | detailOptions.buttons |
| **order 明细序号** | column type index 自定义 |
| **detailSlot 明细 slot** | detailContent slot |
| **detailEvent 明细 select** | columns onChange |
| **detailUpload 明细上传** | columns edit type img/file |
| **detailSelectChange 明细联动** | columns onChange 改其他列 data |
| **detailCalc 明细计算** | formatter/onChange |
| **dyEdit 动态可编辑** | checkEdit / beginEdit |
| **detailFilter 明细查询** | searchDetailBefore |

---

## §12.H 前端 web/form 编辑表单

| 功能 | 代码要点 |
|------|----------|
| **onKeyPress 回车** | getFormOption onKeyPress |
| **customValidator 自定义验证** | item.validator=(rule,val,callback)=>{} |
| **customForm 多字段显示** | formRules 多列 + render |
| **multipleUpload 上传** | type img/file + uploadBefore/After |
| **editSelectChange 联动** | §12.J.4 |
| **cascaderLastSelect 级联末级** | cascader 配置 emitPath false |
| **width/labelWidth/inputWidth** | formOptions colSize/width |
| **cutomBtn/cutomLabel 自定义按钮标签** | item.extra / labelRender |
| **tops 自定义提示** | item.placeholder / extra |
| **dataCascader 级联数据源** | type cascader bind |
| **right/group 左右/分组** | formRules 二维分组 |
| **subdetails 三级明细** | subDetails 左右结构 |
| **continueAdd 连续添加** | gridRef.continueAdd + continueAddAfter |
| **focus 焦点** | editFocus / item.focus |
| **formCalc 实时计算** | onChange 改其他字段 |
| **modelEvent 弹框关闭** | onModelClose |
| **dateRange 日期范围** | min/max onInit 动态设置 |
| **hideSelectItem 隐藏下拉项** | onChange 过滤 item.data |
| **hideFormItem 动态隐藏** | getFormOption('x').hidden=true |
| **formReadonly 只读必填** | readonly/required 动态 |
| **formSelectData 弹框选** | §12.J.1 |
| **detailSelectData 明细弹框选** | §12.J.2 |
| **tree 数组转树** | 工具函数转 cascader/tree |
| **excelImport 导入参数** | importBefore append |

---

## §12.I 独立组件（非 ViewGrid 页面）

### I.1 vol-form

```vue
<vol-form ref="formRef" :formFields="formFields" :formRules="formRules" :loadKey="true" />
<script setup>
const formRules = ref([[{ field:'Name', title:'名称', type:'input', required:true }]])
const submit = async () => {
  const valid = await formRef.value.validate()
  if (!valid) return
  proxy.http.post('api/...', formFields, true)
}
</script>
```

### I.2 vol-table

**标准 url：** `api/{表}/getPageData`（框架标准，无需自定义后台）。

**自定义 url（弹框选数、独立页表格）：** 后台 MUST §12.B.0.4 — `PageDataOptions` + `ConvertQueryFilter<{T}>()` + **`TakePage`** + `return JsonNormal(new { total, rows })`。

```vue
<vol-table ref="tableRef" :columns="columns" url="api/{表}/SearchXxx"
  @loadBefore="loadBefore" :ck="true" :pagination="true" />
<script>
// param 即 PageDataOptions：Page, Rows, wheres, Sort, Value...
const loadBefore = (param, cb) => {
  param.wheres.push({ name: 'Enable', value: '1', displayType: 'equal' })
  cb(true)
}
tableRef.value.load(null, true)
tableRef.value.getSelected()
tableRef.value.addRow({ ... })
</script>
```

### I.3 vol-box — 见 §12.J.11

---

## §12.J 弹出框 / 按钮 / API（完整模板）

> **如何找实现：** ① 本节 **J.x 代码** → ② 不够则 **v3.volcore.xyz**（下表 URL，与离线 doc.web 同路径）。

### J.x 对应关系总表

| 编号 | 业务功能 | Skill 代码 | v3.volcore.xyz |
|------|----------|------------|-----------------|
| **J.1** | 主表弹框选数据 | §12.J.1 | http://v3.volcore.xyz/docs/web/general/selectData.html |
| **J.2** | 明细弹框批量选 | §12.J.2 | http://v3.volcore.xyz/docs/web/general/selectDetailData.html |
| **J.3** | selectTable | §12.J.3 | http://v3.volcore.xyz/docs/web/general/selectTable.html |
| **J.4** | select 联动 | §12.J.4 | http://v3.volcore.xyz/docs/web/form/editSelectChange.html |
| **J.5** | 自定义按钮 | §12.J.5 | http://v3.volcore.xyz/docs/web/general/customBtn.html |
| **J.6** | 查询前后端传参 | §12.J.6 | http://v3.volcore.xyz/docs/cs/service/search.html |
| **J.7** | 明细实时计算 | §12.J.7 | http://v3.volcore.xyz/docs/web/table/detailCalc.html |
| **J.8** | gridFooter 子表 | §12.J.8 | http://v3.volcore.xyz/docs/web/general/viewDetail.html |
| **J.9** | 导入带参数 | §12.J.9 | http://v3.volcore.xyz/docs/web/form/excelImport.html |
| **J.10** | 自定义 API | §12.J.10 | http://v3.volcore.xyz/docs/cs/dev/api.html |
| **J.11** | vol-box | §12.J.11 | http://v3.volcore.xyz/docs/web/general/box.html |
| **J.12** | parentCall | §12.J.12 | http://v3.volcore.xyz/docs/view-grid/common/com.html |
| **J.13** | 按钮权限 | §12.J.13 | http://v3.volcore.xyz/docs/web/general/authBtn.html |

**AI 查找顺序：** 需求 → §12.Z → **§12.J.x** →（可选）**上表 URL** → 用户项目源码。

---

### J.1 主表弹框选数据（嵌入 ViewGrid）

**场景：** 编辑表单字段旁「选择」，vol-box 内嵌另一张表的 `{选择表}.vue`。

**新建** `{主表}/{主表}Select{选择表}.vue`：

```vue
<template>
  <vol-box :lazy="true" v-model="model" title="选择{标题}" :width="1200" :padding="0">
    <sourceGrid ref="sourceRef" />
    <template #footer>
      <el-button type="primary" @click="onSelect">确认</el-button>
    </template>
  </vol-box>
</template>
<script setup>
import sourceGrid from '@/views/{模块}/{选择表}/{选择表}.vue'
import { ref, getCurrentInstance, nextTick } from 'vue'
const emit = defineEmits(['onSelect'])
const { proxy } = getCurrentInstance()
const model = ref(false), sourceRef = ref()
const open = () => {
  model.value = true
  nextTick(() => sourceRef.value?.$refs?.grid?.search())
}
const onSelect = () => {
  const rows = sourceRef.value.$refs.grid.getSelectRows()
  if (!rows.length) return proxy.$message.error('请选择数据')
  model.value = false
  emit('onSelect', rows)
}
defineExpose({ open })
</script>
```

**改 `{主表}.vue`：**

```vue
<SelectXxx ref="selectRef" @onSelect="onSelectXxx" />
<script setup lang="jsx">
import SelectXxx from './{主表}/{主表}Select{选择表}.vue'
const selectRef = ref()
const onInit = ($vm) => {
  gridRef = $vm
  const opt = gridRef.getFormOption('{字段}')
  opt.readonly = true
  opt.extra = { icon: 'el-icon-search', text: '选择', click: () => selectRef.value.open() }
}
const onSelectXxx = (rows) => {
  editFormFields.{字段A} = rows[0].{源字段A}
}
</script>
```

---

### J.2 明细表弹框批量选数据

**场景：** 明细工具栏按钮，vol-box + vol-table 或多选嵌入页。

```javascript
// onInited
gridRef.detailOptions.buttons.unshift({
  name: '选择{名称}', icon: 'el-icon-plus',
  onClick: () => selectRef.value.openDetail()
})
const onSelectDetail = (rows) => {
  const table = gridRef.getTable()
  rows.forEach(r => table.rowData.push({
    {明细字段}: r.{源字段}
  }))
}
```

**Select 组件内：** vol-table + `url` + `loadBefore` 设 wheres，确认时 `getSelected()` → emit。完整结构同 J.1，第二段 vol-box 见框架 `SelectData.vue` 的 `openDetail` 模式。

---

### J.3 selectTable（字段下拉搜 table 选一条）

**后台：** 优先自定义接口（§12.B.0.4），`ConvertQueryFilter<{实体}>` + `TakePage`；`Sys_User` 换为实际表实体。

```javascript
const onInit = ($vm) => {
  gridRef = $vm
  const item = gridRef.getFormOption('{字段}')
  item.type = 'selectTable'
  // 推荐自定义 Search 接口，不用 getPageData（权限/字段更可控）
  item.url = 'api/{当前表}/Search{实体}'
  item.columns = [
    { field: '{列1}', title: '列1', width: 100, search: true }, // search:true 才参与 wheres 过滤
    { field: '{列2}', title: '列2', width: 120 },
  ]
  item.onSelect = (rows) => {
    editFormFields.{字段} = rows[0].{源字段}
  }
  item.loadBefore = (param, callback) => {
    // 方式1：输入框关键字 → 后台用 options.Value 或 wheres 处理
    param.value = editFormFields.{字段}
    // 方式2：param.wheres.push({ name, value, displayType: 'like' })
    callback(true)
  }
}
```

```csharp
// Partial/{表}Service.cs — 查询 Sys_User 时实体为 Sys_User，其他表同理替换
public async Task<object> SearchSysUser(PageDataOptions options)
{
    if (options.Page <= 0) options.Page = 1;
    if (options.Rows <= 0) options.Rows = 30;
    var query = options.ConvertQueryFilter<Sys_User>().Where(x => x.Enable == 1);
    var total = await query.CountAsync();
    var rows = await query.OrderByDescending(x => x.User_Id)
        .TakePage(options.Page, options.Rows)
        .Select(s => new { s.User_Id, s.UserName, s.UserTrueName }).ToListAsync();
    return new { total, rows };
}
// Controller: [HttpPost("SearchSysUser")] return JsonNormal(await _service.SearchSysUser(options));
```

---

### J.4 表单 select 联动

```javascript
gridRef.getFormOption('{主字段}').onChange = (val) => {
  editFormFields.{从字段} = null
  proxy.http.post('api/{表}/Get{联动}', { parentId: val }, true).then(res => {
    gridRef.getFormOption('{从字段}').data = res
  })
}
```

---

### J.5 自定义按钮

| 位置 | 代码 |
|------|------|
| 查询栏 | `gridRef.buttons.splice(3,0,{ name, icon, type:'primary', onClick })` |
| 明细栏 | `gridRef.detailOptions.buttons.unshift({ name, onClick })` |
| 弹框 | `gridRef.boxButtons.push({ name, onClick })` |
| 表格列 | `columns.find(...).render = (h,{row}) => el-button link ...` |
| 隐藏系统按钮 | `gridRef.buttons.find(x=>x.name==='新建').hidden = true` |

---

### J.6 前后端传参

```javascript
// 前端 searchBefore
const searchBefore = (param) => {
  param.value = { status: 1, warehouseId: 'xxx' }
  param.wheres.push({ name: 'OrderDate', value: '2024-01-01', displayType: 'thanorequal' })
  return true
}
```

```csharp
// 后端 GetPageData 内
object extra = options.Value;
QueryRelativeExpression = q => q.Where(x => x.Status == 1);
```

---

### J.7 明细实时计算

```javascript
const onInited = () => {
  gridRef.detailOptions.columns.forEach(c => {
    if (c.field === 'Amount') {
      c.formatter = (row) => {
        row.Amount = (row.Qty || 0) * (row.Price || 0)
        return row.Amount
      }
    }
  })
}
```

---

### J.8 列表页 gridFooter 展示子表

```vue
<view-grid ... :rowClick="rowClick">
  <template #gridFooter><DetailPanel ref="footerRef" /></template>
</view-grid>
<script>
const rowClick = ({ row }) => footerRef.value.load(row.{主键})
</script>
```

---

### J.9 导入带自定义参数

```javascript
const importBefore = (formData, callback) => {
  formData.append('warehouseId', editFormFields.WarehouseId)
  return true
}
```

---

### J.10 自定义 API 三件套

> **JSON：** Partial Controller **MUST** `return JsonNormal(...)` 返回业务数据（列表/实体/ `{ total, rows }`），保持与实体属性、前端 `columns.field` 相同大小写。勿用 `return Json(...)`。  
> **数据库：** 默认 **EF + repository**（§12.B.0）；**禁止**默认 EFSql/Dapper。

**A. 非分页 / 下拉 / 联动（Dictionary 或简单参数）：**

```csharp
// Partial/I{表}Service.cs
Task<WebResponseContent> GetItems(string keyword);
// Partial/{表}Service.cs
public Task<WebResponseContent> GetItems(string kw) {
  var data = repository.FindAsIQueryable(x => x.Name.Contains(kw)).Take(100).ToList();
  return Task.FromResult(new WebResponseContent().OK(null, data));
}
// Partial/{表}Controller.cs
[HttpPost("GetItems")]
public async Task<IActionResult> GetItems([FromBody] Dictionary<string,object> d)
{
    var res = await _service.GetItems(d["keyword"]?.ToString() ?? "");
    return JsonNormal(new { status = res.Status, message = res.Message, data = res.Data });
}
```

**B. vol-table / selectTable 分页（PageDataOptions + ConvertQueryFilter + TakePage）：**

```csharp
// Partial/I{表}Service.cs
Task<object> SearchForTable(PageDataOptions options);

// Partial/{表}Service.cs
public async Task<object> SearchForTable(PageDataOptions options)
{
    if (options.Page <= 0) options.Page = 1;
    if (options.Rows <= 0) options.Rows = 30;

    // {T} = 实际查询实体；前端 wheres 的 name 对应实体属性名
    var query = options.ConvertQueryFilter<{T}>().Where(x => x.Enable == 1);
    var total = await query.CountAsync();
    var rows = await query.OrderByDescending(x => x.{主键})
        .TakePage(options.Page, options.Rows)
        .Select(s => new { s.{字段1}, s.{字段2} }).ToListAsync();
    return new { total, rows };
}

// Partial/{表}Controller.cs
[HttpPost("SearchForTable")]
public async Task<IActionResult> SearchForTable([FromBody] PageDataOptions options)
{
    return JsonNormal(await _service.SearchForTable(options));
}
```

```javascript
proxy.http.post('api/{表}/GetItems', { keyword: 'xx' }, true)
```

---

### J.11 vol-box 通用弹窗

```vue
<vol-box v-model="visible" title="标题" :width="900" :height="500" :lazy="true" :padding="10">
  <!-- 内容 -->
  <template #footer>
    <el-button type="primary" @click="ok">确定</el-button>
    <el-button @click="visible = false">取消</el-button>
  </template>
</vol-box>
```

---

### J.12 子组件 parentCall 通信

```javascript
// 子组件内调用父 ViewGrid
proxy.$emit('parentCall', ($parent) => {
  $parent.search()
  $parent.getSelectRows()
})
// 父组件：proxy.$refs.gridFooter / gridHeader / modelBody
```

---

### J.13 按钮权限

```javascript
const onInit = ($vm) => {
  gridRef = $vm
  if (gridRef.filterPermission(gridRef.table.name, 'Add')) {
    gridRef.buttons.splice(1, 0, { name: '扩展', onClick: () => {} })
  }
}
```

---

## §12.K 新页面编辑（Edit.vue + vol-edit）

> **生效条件：** 代码生成器 **编辑模式 = 新页面编辑**。  
> **文档：** http://v3.volcore.xyz/docs/edit/  
> **与标准页区别：** 列表仍在 `{表}.vue`（view-grid）；**新建/编辑**跳转到 **`Edit.vue` 独立路由页**，不用弹框 `modelOpen*`。

### K.1 文件与路由

```
views/{类库}/{文件夹}/{表}/
├── {表}.vue          ← 列表页（view-grid，点新建/编辑 router 跳转）
└── Edit.vue          ← ★ 新页面编辑业务 Hook 写这里
```

- 路由 `query.id` 有值 = 编辑；无值 = 新建  
- `defineOptions({ name: "{表}_edit" })` 须与路由 name 一致  
- 仍从 `./options.js` 取 `editFormFields`、`detail` 等；**动态改配置在 Edit.vue 的 onInit**，勿手改 options.js

### K.2 最小结构

```vue
<template>
  <vol-edit
    ref="edit"
    :keyField="key"
    :tableName="tableName"
    :tableCNName="tableCNName"
    :formFields="editFormFields"
    :formOptions="editFormOptions"
    :detail="detail"
    :details="details"
    :onInit="onInit"
    :loadFormBefore="loadFormBefore"
    :addBefore="addBefore"
    :updateBefore="updateBefore"
    :loadTableBefore="loadTableBefore"
  />
</template>
<script setup lang="jsx">
defineOptions({ name: '{表}_edit' })
import editOptions from './options.js'
import { ref, reactive, getCurrentInstance } from 'vue'
import { useRoute } from 'vue-router'

const { proxy } = getCurrentInstance()
const route = useRoute()
const isAdd = !route.query.id
const edit = ref(null)
const { key, tableName, tableCNName, editFormFields, editFormOptions, detail, details } =
  reactive(editOptions())

const onInit = async ($vm) => {
  // 改按钮、明细列、分组 group 等（同 view-grid onInit 思路）
}

const loadFormBefore = (params, callback) => {
  // 拉主表行前；callback(false) 中止
  callback(true)
}

const addBefore = async (formData, _cb, isCopyClick) => {
  // 新建保存前；return false 中止
  return true
}

const updateBefore = async (formData) => {
  return true
}

const loadTableBefore = (params, callback, table, item) => {
  callback(true)
}
</script>
```

### K.3 Hook 对照（Edit.vue vs view-grid）

| 场景 | view-grid `{表}.vue` | 新页面 `Edit.vue` |
|------|---------------------|-------------------|
| 初始化 | `onInit` / `onInited` | 同左 |
| 加载主表 | `searchBefore` | **`loadFormBefore`** / `loadFormAfter` / `searchBefore` |
| 保存总闸 | `saveConfirm` | **`submitBefore`** |
| 新建/修改保存 | `addBefore` / `updateBefore` | 同左（含 Async 并行版） |
| 明细加载 | `searchDetailBefore` | **`loadTableBefore`** + `searchDetailBefore` |
| 打开弹框默认值 | `modelOpenAfter` | **`modelOpenAfter`**（无列表弹框时语义为数据就绪后） |
| 列表查询 | `searchBefore` | 在 **`{表}.vue`** 列表页处理 |

**ref 常用方法：** `edit.value.search()` 刷新主从、`edit.value.getTable()` 操作明细表、`edit.value.save()` 主动保存。

### K.4 后端

新页面编辑 **仍调用同一套** `api/{表}/getPageData|Add|Update|Del|Audit…`；数据库前/后处理仍在 **`Partial/{表}Service.cs`**（§12.A），与前端模式无关。

### K.5 在线文档

| 内容 | URL |
|------|-----|
| 新页面编辑总览 | http://v3.volcore.xyz/docs/edit/ |
| 各 Hook 说明 | 同上 README 内 Methods 表 |

---

## §12.Z 全量操作索引（开发者描述 → 章节）

> AI：收到需求后**先在本表检索关键词**，再打开对应章节代码。

### Z.1 后台 Service（ApplicationServiceBase Func）

| 开发者说法 | 章节 / Func |
|------------|-------------|
| Func 怎么写 / base.Add 之前赋值 | §12.A.0 |
| 主表查询/过滤/合计/SQL | §12.A.1 · QueryRelativeList / QueryRelativeExpression |
| 查询结果再加工 | GetPageDataOnExecuted |
| 明细表查询/明细合计 | §12.A.2 |
| OR 查询 | §12.A.3 |
| 新建/AddOnExecute/保存前/保存后/进流程 | §12.A.4 · AddOnExecute / AddOnExecuting / AddOnExecuted |
| 修改/UpdateOnExecute/明细增删改 | §12.A.5 · UpdateOnExecuting / UpdateOnExecuted |
| 删除/删除前校验 | §12.A.6 · DelOnExecuting / DelOnExecuted |
| 审批/流程节点/审批后 | §12.A.7 |
| 反审/回滚 | §12.A.8 |
| 导入/导出/模板/Excel | §12.A.9 |
| 文件上传 | §12.A.10 |
| 打印 | §12.A.11 |
| 自定义后台接口 | §12.A.12 / §12.J.10 |
| **数据库 EF 增删改查** | **§12.B.0** · repository LINQ |
| **分页 / TakePage** | **§12.B.0.3** |
| **vol-table 自定义查询 / ConvertQueryFilter** | **§12.B.0.4** · §12.J.10 B · §12.I.2 |
| selectTable 下拉搜表 | §12.J.3 + **§12.B.0.4** |

### Z.1b 新页面编辑（Edit.vue）

| 开发者说法 | 章节 |
|------------|------|
| 新页面编辑 / 独立页新建编辑 / vol-edit | §12.K |
| Edit 页保存前校验 | §12.K.3 addBefore / updateBefore / submitBefore |
| Edit 页加载主表前过滤 | §12.K.3 loadFormBefore |
| Edit 页明细加载 | §12.K.3 loadTableBefore / searchDetailBefore |
| Edit 页自定义按钮/插槽 | §12.K.2 + docs/edit slots |

### Z.2 后台 cs/dev 常用

**数据库EF§12.B.0** | TakePage§12.B.0.3 | ConvertQueryFilter§12.B.0.4 | vol-table分页§12.B.0.4/J.10 | 调试§12.B.1 | 配置文件§12.B.1 | API参数§12.B.1 | 大小写§12.B.1 | **JsonNormal**§12.B.1/J.10 · case.md | 免登录§12.B.1 | 重写权限§12.B.1 | 序列化§12.B.1 | 字典§12.B.1 | EF增删改§12.B.0.5 | EF事务§12.B.0.6 | 多表Include§12.B.0 | **EFsql非默认** | Dapper非默认§12.B.1 | HttpContext§12.B.1 | 当前用户§12.B.1 | 权限§12.B.1 | 单据号§12.B.1 | 定时任务§12.B.1 | DI§12.B.1 | Repository§12.B.0.1 | 视图代码§12.B.1 | 缓存§12.B.1 | 校验§12.B.1 | 日志§12.B.1 | 租户§12.B.1 | BulkInsert§12.B.1 | 工具§12.B.1

### Z.3 ViewGrid Hook（按生命周期）

初始化 onInit/onInited/dicInited§12.C | 查询 searchBefore/After/searchDetail*§12.C | 弹框 modelOpen*/onModelClose§12.C | 保存 saveConfirm/add*/update*/submit*§12.C | 删除 del*§12.C | 审批 audit*/flow*/cancel*/anti*§12.C | 导入导出 import*/export*/getFileName§12.C | 表格 row*/selectable/beginEdit/span/sort§12.C | 明细 detail*§12.C | 打印 print*§12.C | 复制 copyDataBefore§12.C | 连续添加 continueAddAfter§12.C | 全屏 fullscreen§12.C

### Z.4 ViewGrid 公共方法与属性

search/refresh/getSelectRows/getTable/getFormOption/add/edit/del§12.D.1 | tableV2/single/continueAdd/dyScript§12.D.2 | slots§12.D.3 | 主从/一对多/三级§12.D.4

### Z.5 字段事件

表单 onKeyPress/onChange/blur/focus§12.E.1 | 查询表单§12.E.2 | 明细 columns§12.E.3

### Z.6 general 通用

HTTP/下载§12.F | 自定义/隐藏按钮§12.F/J.5 | 权限§12.F/J.13 | 多 vol-box§12.F | 选主表/选明细§12.J.1/J.2 | selectTable§12.J.3 | 列表下明细§12.J.8 | tabs/多开§12.F | saveConfirm§12.C | openEdit§12.F | 缓存§12.F | tableV2§12.F | slot§12.D.3

### Z.7 web/table 全部

自定义按钮 btn§12.G | 动态查询 dySearch§12.G | 下拉按钮 dropBtn§12.G | 排序 sort§12.G | 禁止勾选 ck§12.G | 固定列 fixed§12.G | 显示全部查询 showFilter§12.G | 查询默认值 defaultVal§12.G | 单日期 singleDate§12.G | 卡片 card§12.G | 查询 select 联动 searchSelectChange§12.G | 移除快捷 removeQuery§12.G | 多快捷 queryFields§12.G | 自定义单元格 customCell§12.G | 格式化 formatter§12.G | 单元格事件 cellEvent§12.G | 行点击 rowClick§12.G | 超出提示 tips/customTips§12.G | 树表 treeTable§12.G | 表头筛选 headFilter§12.G | 表头事件 headEvent§12.G | 颜色 tagColor/rowColor§12.G | 单选 switch§12.G | 在线编辑 editTable§12.G | tableSwitch§12.G | 多级表头 head§12.G | 合计 summary§12.G | 隐藏列 hideColumn§12.G | 分页 page/hidePage§12.G | 获取行 rows§12.G | 点击选中 rowToggle§12.G | 行号 showRowNum§12.G | 二维码 qrcode§12.G | 进度 progress§12.G | 合并 merge/mergeField§12.G | 对齐 fxColumn§12.G | 明细按钮 detailBtn§12.G | 明细序号 order§12.G | 明细 slot detailSlot§12.G | 明细事件 detailEvent§12.G | 明细上传 detailUpload§12.G | 明细联动 detailSelectChange§12.G | 明细计算 detailCalc§12.G | 动态编辑 dyEdit§12.G | 明细条件 detailFilter§12.G

### Z.8 web/form 全部

回车 onKeyPress§12.H | 验证 customValidator§12.H | 自定义显示 customForm§12.H | 上传 multipleUpload§12.H | select 联动 editSelectChange§12.H | 级联末级 cascaderLastSelect§12.H | 宽度 width/labelWidth/inputWidth§12.H | 自定义按钮标签 cutomBtn/cutomLabel§12.H | 提示 tops§12.H | 级联 dataCascader§12.H | 左右/分组 right/group§12.H | 三级明细 subdetails§12.H | 连续添加 continueAdd§12.H | 焦点 focus§12.H | 计算 formCalc§12.H | 弹框关闭 modelEvent§12.H | 日期范围 dateRange§12.H | 隐藏选项 hideSelectItem§12.H | 隐藏字段 hideFormItem§12.H | 只读 formReadonly§12.H | 弹框选 formSelectData/detailSelectData§12.J | 树 tree§12.H | 导入 excelImport§12.H

### Z.9 独立组件

vol-form validate/reset§12.I.1 | vol-table load/编辑/选择§12.I.2 | vol-box§12.I.3

### Z.10 ViewGrid 文档 methods 文件名索引

onInit§12.C | onInited§12.C | searchBefore§12.C | searchAfter§12.C | searchDetailBefore/After§12.C | searchSubDetailBefore/After§12.C | modelOpenBefore/Async/After§12.C | addBefore/Async/After§12.C | updateBefore/Async/After§12.C | delBefore/Async/After§12.C | delRowBefore/After§12.C | saveConfirm§12.C | auditBefore/After§12.C | importBefore/After§12.C | importDetailBefore/After/Batch§12.C | exportBefore/After§12.C | printBefore/printModelClose§12.C | rowClick/rowDbClick§12.C | selectionChange/rowChange§12.C | beginEdit/endEditBefore§12.C | spanMethod/detailSpanMethod§12.C | sortEnd/detailSortEnd§12.C | detailAddRowBefore§12.C | detailTabsClick§12.C | flowLoadAfter§12.C | dicInited§12.C | onModelClose§12.C | continueAddAfter§12.C | copyDataBefore§12.C | getDelMessage/getFileName§12.C | checkEdit§12.C | tableAddRowBefore§12.C | resetSearchFormAfter§12.C | tabClick§12.C | selectable§12.C | auditModelOpenBefore§12.C | boxAuditOptionOpenBefore§12.C | getAuditTable§12.C

### Z.11 ViewGrid common 索引

search§12.D.1 | refresh§12.D.1 | getSelectRows/getRowData§12.D.1 | getDetailRowData/detailRowData§12.D.1 | searchDetail§12.D.1 | add/edit/del§12.D.1 | clearSelection/toggleRowSelection§12.D.1 | getFormOption/searchParameters§12.D.1 | setFixedSearchForm§12.D.1 | initDicKeys§12.D.1 | updateSummary/updateDetailSummary§12.D.1 | tableRef§12.D.1 | editRowIndex§12.D.1 | com/parentCall§12.J.12 | dyScript§12.D.2

### Z.12 ViewGrid event 索引

forminput/formfocus/formblur/formchange/formcalc§12.E | detailforminput/detailformblur/detailformchange/detailformcalc§12.E

---

**§12  END** — 与 §四–§七 互补：§四–§七 为能力总览，§十二 为可执行代码与全量索引。


## 十三、v3.volcore.xyz 在线文档（无本地 doc.web 时的官方补充）

**与离线 `doc.web/docs/` 同路径**，仅域名不同。AI 在 Skill 模板不够时 **SHOULD 访问** 下列页面。

### 13.1 文档入口与四分法

| 分区 | 性质 | URL |
|------|------|-----|
| 后台 cs/service | Service **数据库操作前/后**（Partial Service 钩子） | http://v3.volcore.xyz/docs/cs/service/guid.html |
| 后台 cs/dev | 跨表基础设施（API、EF、用户、权限…） | http://v3.volcore.xyz/docs/cs/dev/ |
| **view-grid** | 生成页容器 + Hook + 内嵌 form/table 事件 | http://v3.volcore.xyz/docs/view-grid/ |
| **view-grid/components** | vol-form/vol-table **依赖关系与事件三层模型** | http://v3.volcore.xyz/docs/view-grid/components.html |
| **web** | 常用功能 **示例**（非 view-grid API 手册） | http://v3.volcore.xyz/docs/web/ |
| **edit** | **新页面编辑** `Edit.vue` / `vol-edit` Hook | http://v3.volcore.xyz/docs/edit/ |
| vol-form / vol-table / vol-box | 独立组件 API | http://v3.volcore.xyz/docs/form/ 等 |

| 其他 | URL |
|------|-----|
| 站点首页 | http://v3.volcore.xyz/ |
| 后台开发总览 | http://v3.volcore.xyz/docs/cs/ |

**URL 拼接：** `http://v3.volcore.xyz/docs/` + `{doc.web 相对路径}`，扩展名一般为 `.html`。

**AI 查文档顺序：** 判断需求类型（见 §文档体系）→ Skill §十二 → 上表对应分区。

### 13.2 后台 Service 业务（§12.A / ApplicationServiceBase Func 对照）

| 业务 | v3.volcore.xyz |
|------|-----------------|
| **Func 机制总览（必读）** | http://v3.volcore.xyz/docs/cs/service/guid.html |
| **JsonNormal 返回原大小写** | http://v3.volcore.xyz/docs/cs/dev/case.html |
| 主表查询/合计/SQL | http://v3.volcore.xyz/docs/cs/service/search.html |
| 新建 | http://v3.volcore.xyz/docs/cs/service/add.html |
| 修改 | http://v3.volcore.xyz/docs/cs/service/update.html |
| 删除 | http://v3.volcore.xyz/docs/cs/service/del.html |
| 审批 | http://v3.volcore.xyz/docs/cs/service/audit.html |
| 反审 | http://v3.volcore.xyz/docs/cs/service/antiAudit.html |
| 导入/模板 | http://v3.volcore.xyz/docs/cs/service/importData.html |
| 导出 | http://v3.volcore.xyz/docs/cs/service/exportData.html |
| 上传 | http://v3.volcore.xyz/docs/cs/service/upload.html |
| 明细查询/合计 | http://v3.volcore.xyz/docs/cs/service/detail.html |
| OR 查询 | http://v3.volcore.xyz/docs/cs/service/or.html |
| 流程信息 | http://v3.volcore.xyz/docs/cs/service/flow.html |

### 13.3 ViewGrid 生成页（§12.C/D/E 对照）

| 类型 | URL |
|------|-----|
| **组件关系（vol-form/vol-table）** | http://v3.volcore.xyz/docs/view-grid/components.html |
| 属性 properties | http://v3.volcore.xyz/docs/view-grid/properties.html |
| 插槽 slots | http://v3.volcore.xyz/docs/view-grid/slots.html |
| 主从/一对多 | http://v3.volcore.xyz/docs/view-grid/detailTable.html |
| 方法目录 | http://v3.volcore.xyz/docs/view-grid/ops.html |
| 单方法 | `http://v3.volcore.xyz/docs/view-grid/methods/{方法名}.html` 如 searchBefore、addBefore |
| 公共方法 common | `http://v3.volcore.xyz/docs/view-grid/common/{方法名}.html` |
| 字段事件 event | `http://v3.volcore.xyz/docs/view-grid/event/{事件名}.html` |

### 13.4 前端 web 示例（§12.F/G/H 对照）

| 类型 | URL |
|------|-----|
| general 目录 | http://v3.volcore.xyz/docs/web/general/ |
| table 目录 | http://v3.volcore.xyz/docs/web/table/ |
| form 目录 | http://v3.volcore.xyz/docs/web/form/ |
| 单篇 | `http://v3.volcore.xyz/docs/web/{general\|table\|form}/{文件名}.html` |

### 13.4b 新页面编辑（§12.K 对照）

| 类型 | URL |
|------|-----|
| edit 总览与 Hook 表 | http://v3.volcore.xyz/docs/edit/ |

### 13.5 后台常用 dev（§12.B 对照）

| 类型 | URL |
|------|-----|
| dev 目录 | http://v3.volcore.xyz/docs/cs/dev/ |
| API 参数 | http://v3.volcore.xyz/docs/cs/dev/api.html |
| 重写权限 | http://v3.volcore.xyz/docs/cs/dev/interfaceauth.html |
| **EF/数据库（默认必用）** | http://v3.volcore.xyz/docs/cs/dev/db.html |
| EF 原生 SQL（非默认，勿优先） | http://v3.volcore.xyz/docs/cs/dev/db/EFsql.html |
| selectTable 自定义查询 | http://v3.volcore.xyz/docs/web/general/selectTable.html |
| 当前用户 | http://v3.volcore.xyz/docs/cs/dev/user.html |

### 13.6 本 Skill 不覆盖

配置类任务（生成器操作、菜单、流程设计器、部署等）→ 不在本 Skill 范围；表级业务仍用 §12。

**AI 勿混淆：**
- `web/` = 效果**示例**，Hook 签名以 view-grid/edit 为准
- `view-grid/` = 生成页；字段事件见 components + event/
- `.jsx` = **MUST NOT** 写业务

---

## 十四、YZH 单表基类 MVP 落地 23 项踩坑清单（V1~V11，2026-08-03 联调）

> **背景**：`components/yzh/base/YzhBaseSingleTable.vue` 在 `cert_certification_body` 首次打通前后端链路时，因 Vol 框架 4 层 Provider 与 YZH 自己 state 双源不同步、栅格隐式约定、字典重复、wheres 格式错误等「隐式坑点连续爆发 23 项。本节把所有「之前反复踩过的坑 + 修复代码 + 预防规则全部沉淀为可复用的宪法级规则，**下一轮任何 Cert 7 个页面推广时 1:1 套，不许再反复修同样的错**。

### 14.0 为什么反复了 10+ 轮才修好的 4 条根本原因（AI/开发者必须记住）
1. **Vol 框架是「4 层深度 Provider 脚手架**：ViewGridProvider → ViewGridEvent → VolFormProvider → VolTableProvider，每一层都有自己的初始化钩子和内部 state。**隐式约定 90% 未写文档**，单点 patch 一个问题会暴露下一层另一个问题。
2. **之前是 Vol 内部 state + YZH 自己 state 双源不同步**：勾选框/行点击/编辑模式多选各自维护一套，**不同步必炸。
3. **之前顺序错误的修复顺序反了**：应该先把 7 个交互（增删改查+刷新+导入/导出/列配置 **基类里一次性全部接上 Vol 原生**，再改业务字段；之前是反馈一个改一个，每次都缺其他 6 个没接，每次都爆下一轮。
4. **用户反馈是「现象」不是「根因」**：如「搜索输入没效果」背后实际藏 3 个独立根因（缺 type→v-model 不回写、searchFormFields 字段没被 searchBefore 拆解进 wheres、Vol 内部 VolFormProvider initDicKeys 没去重），之前每次只修 1 个，反复暴露下一轮继续。

### 14.1 P0 - 致命级（页面直接白屏 / 完全不能用，共 8 项
| 编号 | 现象 | 根因 | 修复位置 | 修复手段（宪法级）| 预防规则 |
|---|---|---|---|---|---|
| P0-01 | `locateNonHydratedAsyncRoot: Cannot read properties of null (reading 'component')` 白屏 | 在 JS 层改 dataConfig.buttons=[]，导致 Vol initButtonsAuthFields 内部报错，instance.parent 变成 null | [YzhBaseSingleTable.vue L409-L418](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/components/yzh/base/YzhBaseSingleTable.vue#L409-L418) `_freezeVolButtons` 什么都不做 + CSS 隐藏 UI 层 `:deep(.view-header > .btn-group) { display:none !important }` | **禁止任何场景**在 JS 层删除/清空 Vol 内部 dataConfig 任何数据结构；UI 隐藏一定只改 CSS |
| P0-02 | Vite 500 编译失败 → Vue Router `Failed to fetch dynamically imported module | .vue lang=jsx 文件写 TS 风格 `(x as any)` 语法，@babel/parser 抛 Unexpected token，Vite dynamic import 失败 | 所有 .vue 删除所有 TS 类型断言 `as` / `as T` 语法，保持纯 JSX | 写 .vue 代码时，要泛型一律用 typeof / instanceof / JSDoc 类型注释 |
| P0-03 | 勾选了复选框，点删除仍提示「请选择一行」| 两套 state：Vol 原生勾选框存在 `gridVM.getSelected()`，YZH 自己的 `useYZHEditMode` 自己维护 `selectedRows`，两套不同步 | [useYZHEditMode.ts#L52-L115](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/components/yzh/composables/useYZHEditMode.ts#L52-L115) 重写 selectedKeys/selectedRowObjects/hasSelection：**优先读 Vol 原生 getSelected()**，其次 editMode.selectedRows，最后 singleSelectedRow；用 registerVolSelectionGetter 在 onInit/onInited/onMounted 3 次注册** | selection 永远只认 Vol 原生勾选框为「唯一真实数据源；YZH 自己 state 只做 fallback，永远反例** |
| P0-04 | 搜索关键词输入字符但 Vol 读不到值（用户说输入框能打字但没效果） | ① searchFormOptions.Name **缺 `type:'input'`→Vol fallback 成普通 div 不绑 v-model；② 输入了字符但 set 不到 searchFormFields 回写；③ searchBefore 没把 searchFormFields 拼到 param.wheres | ① options.js searchFormOptions 所有可输入字段显式写 type: 'input' / 'select' / 'textarea'，② opts.searchFormFields 普通 JS 对象浅拷贝（不能 Proxy/reactive readonly），③ searchBefore 把 Name 拆 3 字段 LIKE 到 param.wheres | 所有表单字段可输入**必须显式写 type 同构 |
| P0-05 | wheres 查询条件传给后端后端完全没读到 | VolTableLoadData.js L29 / L61 规定：**所有条件必须放 `param.wheres: Array<{name,value}>`，后端 ServiceBase 只 JSON.parse(wheres_str)；之前写 param.Name=xx 直接写 param 顶层，后端忽略导致搜索看起来传了实际不生效 | [YzhBaseSingleTable.vue L645-L706](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/components/yzh/base/YzhBaseSingleTable.vue#L645-L706) searchBefore 统一把所有 searchFormFields 全部拼到 wheres | 查询条件**一个一个字段** 拼到 `param.wheres` 数组**，永远不能写 param 顶层字段 |
| P0-06 | 404 `/api/api/CertCertificationBody/` 双前缀 | table.url 写成 `/api/CertCertificationBody/`，Vol getUrl = '/' + 'api' + table.url，自动拼 `/api` 前缀 | mergedTable.url 构造时 `/${props.schema.controllerName}/`，URL 必须相对路径 `/xxx/` **不写 /api 前缀 | 前端所有 table.url **禁止写 /api 开头，一律 `/ControllerName/` |
| P0-07 | 弹窗 v-show 不显示隐藏字段列少了 Code 存库失败 | Vol v-show 条件判断必须要写 `{ field: 'Code', type:'input', hidden:true }`；只写 hidden:true 不写 type 不渲染隐藏 input，Vol 会把 Code 当无类型字段跳过，保存时后端缺字段报错 | options.js editFormOptions 第一行必须先放隐藏字段：`{ field:'Code', type:'input', hidden:true }` **所有隐藏字段必须显式写 type:'input' |
| P0-08 | 点遮罩录入一半丢失编辑内容丢了 | Vol dialog 默认 width 70% 视口 + closeOnClickModal=true，点外面直接关 | opts.boxOptions `{ width:960, top:'8vh', closeOnClickModal:false }`， `<view-grid :boxOptions="opts.boxOptions"` | 所有业务弹窗固定宽 960，禁止遮罩关弹窗

### 14.2 P1 - 严重 UX 问题（能用但体验非常不友好，共 10 项）
| 编号 | 现象 | 根因 | 修复位置 | 修复手段 |
|---|---|---|---|---|
| P1-01 | 弹窗 3 列 + 备注只占半行，用户说不够严谨 + 70% 跳格显示不友好 | ① editFormOptions colSize 每行合计按 12 满格重算，备注 colSize=12 只占半格；② ②缺 colSize 的字段被 VolFormProvider L71-78 平均分配→导致 | [CertificationBody/options.js#L36-L105](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/views/cert/CertificationBody/options.js#L36-L105) 严格 2 列 4 行 24 栅格（每行 2×colSize 12=24，备注 colSize 24 整行，备注整行 rows=5 + maxlen 1000；基类 scoped style 对 span=24 textarea 强制 width:100% min-height:140px | 所有 editFormOptions 必须写 colSize 的和：每行严格 =24，备注 textarea colSize 24 整行 |
| P1-02 | 「状态」下拉里每一项重复 2 遍（整改中/注销/暂停/正常各出现 2 次）| VolFormProvider initDataSource L141 `if (!x.data.length \|\| resetData)` 在 onInited + modelOpenBeforeAsync + resetData=true 3 次反复 push 同一 dicKey → GetVueDictionary 返回 N 份同字典→bindData 同一项出现 2 次 | 基类 `_dedupSelectData(items)` 按 value+label Set 去重（x.data.splice 原地替换）；在 onInit / onInited / onMounted 3 次 phase 都对 searchFormOptions + editFormOptions flat 数组去重 | 所有含 dataKey 的 select 列**在基类 3 次 phase 都去重 1 次 |
| P1-03 | 搜索输入/选择后需要点其他地方才刷新 | Vol quickSearchKeyPress 只处理第一个 input 的 keyCode 13，完全没监听 blur/change；Element Plus el-select change 事件不冒泡 | 基类 `_ensureSearchAutoTrigger` 对 fixedSearchBox + customSearchRef 容器所有 input/select/textarea 统一绑：blur/change/keydown Enter；el-select 容器额外 mouseup/mouseleave 延迟 220/320ms 调 `gridVM.search(null,true) `| 搜索区交互：失焦/值变/Enter → 立即 search，永远不依赖用户点搜索按钮或其他地方 |
| P1-04 | 「查看标准」放工具栏未选中时灰色死按钮占槽位导致视觉误导 | toolbarLeft 插槽强制 disabled 灰色按钮占槽位，用户不知道什么时候能点 | [CertificationBody.vue#L14-L29](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/views/cert/CertificationBody/CertificationBody.vue#L14-L29) 改成 `v-if="selectedRow"` 仅选中行时显示 plain 「查看关联标准」，未选中时整个 slot 为空 | 业务工具栏：条件按钮**用 v-if 按需显示，不用 disabled 灰掉占位置** |
| P1-05 | 批量删除要「先进入编辑模式」才能删除（用户说已经取消编辑了为什么还要点编辑）| 之前 hasSelection 只认 editMode.selectedRows，不认勾选框 | useYZHEditMode 重写 hasSelection：勾选框勾了/点行/编辑模式多选 → 任一满足即可删 | batchDelete 永远与 editMode 解耦，勾选即删 |
| P1-06 | 刷新/导入/导出/列配置 4 按钮点了没反应提示占位，显示「列配置可点右上角 | 之前只接了 tbl.importClick/exportClick（Vol 部分版本没暴露），列配置写了占位 $message.info | 基类 onYzhToolbarClick 重写：① 刷新 → gridVM.refresh + search(null,true) 兜底；② 导入/导出 → 优先 tbl.importClick/exportClick，**兜底动态 import ViewGridEventButton.jsx 的 importData/exportData(proxy,props,dataConfig,false) 原始函数**；③ 列配置 → `customColumnRef.show(columns, orginColumnFields, tableName)` 与 ViewGrid.vue L726 参数完全一致 | 4 个按钮接 Vol 原生函数兜底，失败时提示「用表格右上角」而不是完全占位 info |
| P1-07 | 备注挤一块显示挤在弹窗左半格很难看 | 备注 colSize 还是 12（半格）+ rows=3 高度不够，Vol colSize 24 + scoped style `.el-dialog__body .el-form-item textarea` 强制 100% 宽 + min-height 140px，**已修见 P1-01，这里重复但不同角度 | textarea 列必须 colSize 24 + 整行 |
| P1-08 | 关键词多字段模糊（Name/ShortName/CbCode 任一 LIKE）| Vol 默认 searchFormFields.Name=xx 当相等查询 Name=xx 导致只能查全称，查不到简称/CNAS 编号 | searchBefore 里 `_appendOrLikeForKeyword` 对 searchKeywordFields 数组（默认 Name/ShortName/CbCode）逐个拼 wheres LIKE + `{ name:字段, value: kw, operator:'like'}`，**把 Name 置 undefined 防止 Vol 默认相等重复拼一层 | 关键词搜索默认 3 字段 OR LIKE，永远别写单字段 = |
| P1-09 | Status/其他 select 下拉值重复 2 次（同上 P1-02 同一根因 search 角度）| initDicKeys 3 次 + dicKeys 不 dedupe → 同 dicKey 请求多次 | _dedupSelectData 对 searchFormOptions + editFormOptions 两个数组都去重 | 两个表单（编辑 + 搜索）的 data 都去重 |
| P1-10 | 删除提示还是「请进入编辑模式多选」误导用户 | 文案从「请选择一行（点击行选中或进入编辑模式多选）」改成「请先勾选要删除的行（使用左侧复选框或行选择）」 | onYzhToolbarClick batchDelete 分支改文案 | 删除提示永远不出现编辑模式 4 个字，强调复选框。

### 14.3 P2 - 架构级预防规则（不炸但反复导致小问题，共 5 项）
| 编号 | 规则内容 | 为什么是宪法级 | 对应文件/代码 |
|---|---|---|---|
| P2-01 |  opts.searchFormFields / editFormFields / editFormOptions / searchFormOptions **必须全部是普通 JS 对象浅拷贝，不能用 reactive(readonly Proxy)/structuredClone/深拷贝**，否则 Vol 内部 `item.data = []` 赋值静默失败→v-model setter 不回写→「输入框能打字符但 Vol 读不到值」 | Vue 3 reactive Proxy 的 writable:false 导致内部 item.data=[] 静默失败；structuredClone 会把函数/SFC 组件丢掉 → 报 Vue runtime-core locateNonHydratedAsyncRoot | [YzhBaseSingleTable.vue L182-L199](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/components/yzh/base/YzhBaseSingleTable.vue#L182-L199) opts 构造：Object.assign({}, _defaults, rawOpts, { editFormFields: Object.assign({}, ...}) + editFormOptions: row.map(x→Object.assign({}, x) ... }
| P2-02 | **所有可输入字段（search + edit）必须同构显式写 type: 'input' / 'select' / 'textarea' | 缺 type→Vol 根据 type 判断分支** 走错误分支渲染：不绑 v-model→输入框/下拉 v-model setter 无法写回（P0-04/P0-07 | CertificationBody options.js editFormOptions + searchFormOptions 显式全字段 |
| P2-03 | **禁止手改 Vol 核心源码**：ViewGridProvider/VolTable/VolForm 的 .jsx/.js 全部只读** 0 修改** | 一改就连锁炸：定位隐式依赖 init 4 层 provider 内部 state 结构不同步→ P0-01 null.component | 只读诊断定位根因→只改基类 + Preset + Options + 业务页 4 个目录 |
| P2-04 | **view-grid 所有插槽必须 <div> 单根包裹** | Vol ViewGridProvider 内部对插槽做 parentNode 手动遍历，Vue 3 Fragment 多根节点 v-if 空 空 Fragment 先卸载，后 parentNode 访问 null→P2-04 parentNode null TypeError | 见 P2-04 fix 所有业务页所有插槽一律先敲<div> 再写业务 |
| P2-05 | **推广到 7 个 Cert 页面前，必须在基类 YzhBaseSingleTable.vue 一次性把 23 坑都跑完，再 1:1 套**，不能单点 patch 一个页就推广，会再爆 10+ 轮同样错误 | 单点 patch 一个页每次修 1 个坑就推广 → 下页爆同样 6 个没接的按钮/selection 不同步→再修再爆→反复 N 轮 | 推广前先跑一遍 14.0 检查清单再推广 |

---

### 14.4 V12 用户真实 Chrome 操作阶段 4 项显式反馈 + 5 个现象级复现 → 即时修复记录（2026-08-03 18:30 用户侧最新反馈）

> **用户原话总览**：「先将当前踩坑的地方更新到文档吧，现在还有一大堆错误，我觉得我都需要给你做个错误清单了，这个简单页面至少还有10几个错误」+ 截图展示 5 个复现场景（Q1~Q4）。
> 核心要求：①文档沉淀 + ②直接给用户一份打勾 Checklist，不用用户自己写错误列表。

| 用户反馈编号 | 用户原话 / 截图复现场景 | 对应 23 项编号 | 根因（一句话） | 是否已落地 | 验证方式 |
|---|---|---|---|---|---|
| **Q1** | 「顶部的查看标准为什么要放到这里」——截图展示未勾选行时仍显示 disabled 灰色「查看标准」死按钮占槽位 | **P1-04** | 之前 toolbarLeft 一直渲染按钮，v-if 没写，导致未选中时灰色死按钮误导用户视觉 | ✅ 已修 | 未勾选行 → toolbarLeft 空；勾选 1 行 → 立即出现绿色 plain「查看关联标准」 |
| **Q2-Phenomenon A** | 「我明明选择了一行还是提示要选择一行机构」——截图：勾了复选框 + 蓝色选中行，但点删除仍红色提示「请选择一行」 | **P0-03** | Vol 原生勾选框 state（gridVM.getSelected）与 YZH 自己的 editMode.selectedRows 两套不同步；hasSelection 只认后者 | ✅ 已修 | 勾复选框 1 行 → 顶部删除 → 正常二次确认，不出红色提示；勾选全选 → 批量删除正常选多行 |
| **Q2-Phenomenon B** | 「我已经取消编辑了为什么还要点编辑才能删除」——用户退出 ✎编辑模式后勾复选框，仍提示要进编辑模式 | **P1-05** | 删除分支前置判断 editMode，把「复选框勾选」和「编辑模式多选」错误地绑定成一回事 | ✅ 已修 | 点 ✎ → 再点一次退出编辑（编辑模式关闭）→ 勾行复选框 → 顶部删除 → 不出现「编辑模式」4 字，直接弹二次确认 |
| **Q2-Phenomenon C** | 「勾选了行了也不能删除」——勾了行但删除按钮无反应 | **P0-03 + P1-10** | 上同；且删除提示文案仍写「进入编辑模式多选」进一步误导用户 | ✅ 已修 | 删除提示文案：「请先勾选要删除的行（使用左侧复选框或行选择）」——永远不再出现「编辑模式」4 个字 |
| **Q3-Phenomenon A（刷新）** | 「导入、导出、列配置、刷新好像都未实现」——点 🔄 刷新没反应 | **P1-06 刷新分支** | 只写了占位提示 `$message.info`，没真实调 gridVM.refresh + search(null,true) 3 层兜底 | ✅ 已修 | 在第 3 页 → 点 🔄 → 表格从第 1 页重查，Network 看到新的 getPageData POST |
| **Q3-Phenomenon B（导入）** | 点 📥 导入没反应 | **P1-06 导入分支（❓ 待真实 Network 验证）** | tbl.importClick 多数 Vol 版本没 expose；没兜底动态 import ViewGridEventButton.importData | ✅ 已修（待真实浏览器） | 点 📥 导入 → 4 层兜底：volUploadRef.show → volUpload.show → tbl.importClick → ViewGridEventButton.importData；任一命中应弹出「选择 Excel + 下载模板」的 Vol 原生导入弹窗 |
| **Q3-Phenomenon C（导出）** | 点 📤 导出没反应 | **P1-06 导出分支（❓ 待真实 Network 验证 blob 返回）** | tbl.exportClick 部分 Vol 版本没 expose；没兜底动态 import ViewGridEventButton.exportData | ✅ 已修（待真实浏览器 + 后端 .NET 起） | 先筛「暂停业务」→ 点 📤 导出 → 浏览器下载 xlsx；Network POST `/api/CertCertificationBody/export` 返回 blob，下载的行 = 过滤后的行数 |
| **Q3-Phenomenon D（列配置）** | 点 🛠 列设置只弹占位 info 提示「列配置可点右上角图标」 | **P1-06 列配置分支** | 之前只写 $message.info 占位，完全没接 Vol 内部 customColumnRef.show() 原始 API | ✅ 已修 | 点 🛠 列设置 → Vol 列配置面板弹出（每列 checkbox + 「重置默认」+「确认」两个按钮）；参数与 ViewGrid.vue L726 完全一致：show(columns, orginColumnFields, tableName) |
| **Q4 文档要求** | 「我觉得我都需要给你做个错误清单了，这个简单页面至少还有10几个错误」 | §十四 23 项清单 + §十五 23 项推广 Checklist + 踩坑记录.md §8 | 之前文档只写了技术修复面，没有给用户一份可直接打勾、按操作顺序的 Checklist，用户不知道怎么提问题 | ✅ 已写 2 份文档 · 3 份清单 | ① vol-skill.md §十五 23 项推广 Checklist（开发者/AI 侧）；② 踩坑记录.md §8 🎯 12 项用户侧测试 Checklist；③ §8.1 🚩 15+ 项即时打勾清单（本章 §14.4 对应落地） |

**📌 V12 阶段修复落地的 3 份打勾 Checklist 定位索引**：
| 文档 | 给谁用 | 清单内容 | 打勾数量 |
|---|---|---|---|
| `vol-skill.md §十五` | AI / 开发者（推广到 6 个 Cert 页面前逐条对照） | 23 项 P0/P1/P2 技术修复 Checklist | 23 项 |
| `2026-08-03_Phase2联调.md §8` | 用户真实浏览器操作测试（前 12 步 + 2 步 Network ❓） | 🎯 12 步用户操作顺序 Checklist + 2 步 Network 抓包验证 | 14 项 |
| 同文件 §8.1 末尾 | 用户即时反馈新问题时直接勾选，不用自己写列表 | 🚩 15+ 项用户原话对应打勾清单 | 15+ 项 |

---

## 十五、下一轮推广到 6 个 Cert 页面检查清单（23 项 Checklist）
推广到 ISOStandard / ISOClause / Enterprise / CertApplication / AuditTask + M3 左树右表 ISOClauseTreeTable 前，**先在每个页面按下面 23 项逐条打勾，不许漏：
- [ ] P0-01 白屏：CSS 隐藏 btn-group，JS 不改 dataConfig.buttons
- [ ] P0-02 Vite 编译：删除所有 .vue 里所有 TS `as any` 语法
- [ ] P0-03 selection 同步：hasSelection 优先 Vol 原生 getSelected()
- [ ] P0-04 搜索可输入：searchFormOptions 全字段显式 type: input/select
- [ ] P0-05 wheres 数组：searchBefore 所有字段拼 param.wheres，不写 param 顶层
- [ ] P0-06 URL 单前缀 /api 相对 /ControllerName/
- [ ] P0-07 隐藏字段 Code 显式 type:input hidden:true 放第一
- [ ] P0-08 boxOptions 960 + closeOnClickModal=false
- [ ] P1-01 弹窗严格 2 列 24 栅格备注 colSize 24 整行
- [ ] P1-02 下拉去重：_dedupSelectData 3 次 phase
- [ ] P1-03 搜索自动触发：blur/change/Enter/mouseup 立即 search(null,true)
- [ ] P1-04 业务按钮 v-if 按需显示不占槽位
- [ ] P1-05 删除与 editMode 解耦
- [ ] P1-06 刷新/导入/导出/列配置 4 按钮接 Vol 原生兜底
- [ ] P1-07 备注 textarea min-height 140 + rows=5
- [ ] P1-08 关键词 3 字段 OR LIKE
- [ ] P1-09 两个表单（search+edit）下拉都去重
- [ ] P1-10 删除提示文案正确
- [ ] P2-01 opts 普通 JS 浅拷贝无 Proxy
- [ ] P2-02 全字段显式 type 同构
- [ ] P2-03 0 改 Vol 核心源码
- [ ] P2-04 插槽全 <div> 单根
- [ ] P2-05 跑完 23 项 Checklist 再推广下一个页面

*VolPro 业务逻辑 AI 开发技能 · 文档四分法（cs / view-grid / web / edit）· Skill + v3.volcore.xyz 双源 ·*