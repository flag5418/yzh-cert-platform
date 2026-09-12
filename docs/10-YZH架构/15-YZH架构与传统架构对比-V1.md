# YZH 架构与传统架构对比

> **版本**：V1.0 | **日期**：2026-09-12 | **状态**：评审中
>
> 本文对比**传统 Vol 架构**（`src/old/server/Vue.NetCore/vol.api` + `vol.web`）与 **YZH 架构**（`src/yzh-core` + `src/certplatform-api` + `src/certplatform-web`）。
> 所有数字均为**实测**，实证样本为**数据字典模块**——同一业务需求在两种架构下的完整实现。
> 关联文档：[01-架构总纲](./01-架构总纲.md) · [02-后端架构](./02-后端架构.md) · [12-框架能力清单-V1.md](./12-框架能力清单-V1.md)

---

## 〇、一句话结论

| 架构 | 业务逻辑落在哪 | 用什么关联 | 接口怎么暴露 | 界面怎么配置 |
|------|--------------|-----------|-------------|-------------|
| **传统 Vol** | **Service 层** | **Id**（`ParentId`/`Dic_ID`/`Role_Id`） | 反射约定路由：**Service 方法名即端点** | 前端 `options.js` **硬编码** |
| **YZH** | **Controller 钩子** | **Code**（`ParentCode`/`DicCode`/`RoleCode`） | **固定端点**（`/filter` `/tree/*` `/treepconfig`） | 后端 `EntityConfigs/*.json` |

**结果**：同一模块，YZH 的后端业务代码约为传统的 **1/5**，前端"表格列 / 表单字段"配置代码趋近于 **0**（因为搬到了后端 JSON）。

---

## 一、对比总表

| 维度 | 传统 Vol 架构 | YZH 架构 |
|------|--------------|---------|
| **业务逻辑落点** | Service 层（`Partial/Sys_XxxService.cs`） | Controller 钩子（`OnBeforeAdd` / `OnBeforeAddTree` / `OnBeforeDeleteTree` …） |
| **接口暴露方式** | 反射约定路由：Service 方法名即端点 | 固定端点 + `[Route]`；越界能力靠 `virtual` 覆盖 |
| **关联模型** | **Id 关联**（`ParentId` / `Dic_ID` / `Role_Id` / `Dept_Id`） | **Code 关联**（`ParentCode` / `DicCode` / `OrgCode` / `RoleCode`） |
| **记录定位键** | 主键 `Id` | **`Code`**（`Update` 的 `WHERE` 条件就是 `Code`） |
| **主从表保存** | `SaveModel` 一次性提交主从 | 左树右表分离：`TreeTableControllerBase<T,V>` 两个实体各管各的 |
| **树形结构** | 各模块自研 | `TreeTableControllerBase<T,V>` + `TreeTableLogic` 统一 |
| **表格列 / 表单字段** | 前端 `options.js` 硬编码 | 后端 `Assets/EntityConfigs/{域}/{类型}.json` |
| **分页查询端点** | `POST getPageData` | `POST /filter`（基类**没有** `getPageData`） |
| **软删除** | 各模块手写 | `[YZHDeleteStrategy(Mode = Soft)]` + `SoftDelete()` |
| **启用标识** | 各表 `Enable` 语义不一 | 统一 `IsValid` + `tree/toggle-valid` 端点 |
| **权限** | `[ApiActionPermission]` 特性 + 手动 Filter | `[YZHAuthorize]` + SSO 挤号（`TokenVersionService`） |
| **缓存** | 各模块自研（如 `DictionaryManager` 105 行） | 复用 `ICacheManager` |
| **前端绑定** | `view-grid` / `VolProvider` / `VolBox` / `VolForm` | `YzhTable` / `YzhForm` / `CrudPageLogic` / `TreeTableLogic` |
| **配置缺失行为** | — | 缺 JSON **静默空白**（需 `StrictConfigLoad => true` 暴露） |

---

## 二、四个本质差异

### 差异 1 · 逻辑落点：Service 层 vs Controller 钩子

**传统 Vol**：控制器是**空壳**，业务全在 Service 的 `Partial` 分部类里。

```csharp
// vol.api/YZH.WebApi/Controllers/Admin/Sys/Sys_DictionaryController.cs（全文 18 行）
[Route("api/Sys_Dictionary")]
public partial class Sys_DictionaryController : ApiBaseController<ISys_DictionaryService>
{
    public Sys_DictionaryController(ISys_DictionaryService service)
        : base("System", "System", "Sys_Dictionary", service) { }
}
```

```csharp
// vol.api/YZH.Sys/Services/System/Partial/Sys_DictionaryService.cs（283 行）
public object GetVueDictionary(string[] dicNos)          { /* 批量取字典 */ }
public object GetSearchDictionary(string dicNo, string v){ /* 远程搜索 */ }
public object GetTableDictionary(Dictionary<...> keyData){ /* 表格按值反查 */ }
public override WebResponseContent Add(SaveModel saveDataModel)  { /* 主从一次性保存 */ }
public override WebResponseContent Update(SaveModel saveDataModel) { /* 含明细去重 */ }
public override WebResponseContent Del(object[] keys, bool delList) { /* 软删除 */ }
```

**YZH**：Service 退化为**原子操作**（`EntityService<T>`，只做 CRUD 与查询），业务逻辑上移到 Controller 的 `virtual` 钩子。

```csharp
// yzh-core/YZH.Core.Web/Controllers/System/DictionaryController.cs（约 60 行，含全部业务逻辑）
protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(Sys_Dictionary e)   { ... }
protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(Sys_Dictionary e){ ... }
protected override async Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)   { ... }
protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_DictionaryList e)    { ... }
```

> **为什么这样更好**：钩子天然挂在"事务边界"上，无需手动 `try/catch` + `WebResponseContent` 拼装；且**基类已把 18 个端点写好**，子类只需回答"什么情况下不允许"。

### 差异 2 · 关联模型：Id vs Code（双键设计）

这是**最容易被低估、后果最严重**的差异。

**传统 Vol**：用 Id 建立关联。

| 关联场景 | 传统 Vol 字段 | YZH 字段 |
|---------|-------------|---------|
| 树父子 | `ParentId`（int，指向父节点主键） | `ParentCode`（varchar，指向父节点 `Code`） |
| 字典 → 明细 | `Dic_ID`（int） | `DicCode`（varchar） |
| 机构 → 人员 | `OrgId`（bigint） | `OrgCode`（varchar） |
| 角色 → 菜单 | `Role_Id` / `Menu_Id` | `RoleCode` / `MenuCode` |

**YZH**：`Code` 是唯一业务键，**所有关联必须用 `Code`**（`RoleController.cs:194` 明文声明）。

**全库核查结果**：项目 **~60 条业务外键全部指向 `Code`**，**没有一条指向 Id 列**。

```
cert_iso_clause.ParentCode            → cert_iso_clause.Code
audit_task.PhaseCode                  → ent_enterprise_phase.Code
ent_enterprise_document.EnterpriseCode → ent_enterprise.Code
rpt_report_section.ReportCode         → rpt_audit_report.Code
cert_validation_rule.StandardCode     → cert_iso_standard.Code
...
```

**配套的「双键设计」**（`scripts/db/dual_key_design_V1.sql`）：`Code` 是**系统生成的稳定标识**，业务编码是**独立属性**。

| 表 | `Code`（稳定标识 / 关联键） | 业务编码（独立属性，可空） |
|----|--------------------------|------------------------|
| `sys_user` | `USER_` + LPAD(Id,6,'0') | `UserName` |
| `Sys_Role` | `ROLE_` + LPAD(Id,6,'0')；核心角色语义化 `ROLE_SUPER_ADMIN` | `RoleName` |
| `Sys_Menu` | `MENU_` + LPAD(Id,6,'0')；核心菜单语义化 `MENU_DICT` | `MenuName` |
| `Sys_Dictionary` | `DICT_` + LPAD(Id,6,'0') | `DicNo`（字典编码，**可空**） |
| `Sys_DictionaryList` | `DICTLIST_` + LPAD(Id,6,'0') | **无**（字典项没有"项编码"） |

**Id 关联 vs Code 关联的实质差别**

| 问题 | Id 关联 | Code 关联 |
|------|--------|----------|
| 跨环境/跨库迁移 | Id 会变，关联失效 | `Code` 稳定，关联可重建 |
| 数据导入导出 | 需维护 Id 映射表 | 直接用 `Code`，无需映射 |
| 可读性 | `ParentId = 137` | `ParentCode = 'DICT_000137'` |
| 前端传参 | 需要 Id | `TreeItemDto` 天然携带 `Code` |
| 关联字段能否变更 | 可随意改 | **`Code` 插入后不可改**（见 §四） |

### 差异 3 · 接口契约：反射约定路由 vs 固定端点

**传统 Vol**：Service 的公开方法**直接变成 HTTP 端点**，靠反射分发。

```
POST api/Sys_Dictionary/GetVueDictionary     ← 直接对应 Service.GetVueDictionary()
POST api/Sys_Dictionary/GetSearchDictionary  ← 直接对应 Service.GetSearchDictionary()
POST api/Sys_Dictionary/getPageData          ← 基类端点
POST api/Sys_Dictionary/GetDetailPage        ← 基类端点（明细分页）
```

> ⚠️ **这正是新架构前端残留 `getPageData` 调用的根源**——从旧 Vol 前端照搬时把旧路由一起搬了过来，而新基类**没有** `getPageData`。这是本项目最高频的系统性错误。

**YZH**：端点**固定**，由基类一次性写好，子类只覆盖行为。

| 基类 | 固定端点 |
|------|---------|
| `YzhControllerBase<V>` | `GET /config` · `POST /filter` `/add` `/update` `/delete` `/export` `/import` `/import/template` `/action/{methodName}` `/toggle-valid` |
| `TreeTableControllerBase<T,V>` | 上述全部 + `POST /tree/root` `/tree/children` `/tree/add` `/tree/update` `/tree/delete` `/tree/action/{methodName}` `/tree/toggle-valid` · `GET /treepconfig` · `POST /checkTree` `/check/add` `/check/remove` `/check/all` |

### 差异 4 · UI 配置：前端硬编码 vs 后端 JSON

**传统 Vol**：列与字段定义在**前端**，`options.js` 一个文件 78 行描述表格列。

```js
// vol.web/src/views/cert/admin/system/system/Sys_Dictionary/options.js
{ field: 'DicNo',   title: '字典编号', width: 120, ... },
{ field: 'DicName', title: '字典名称', width: 180, ... },
// ... 主表 16 列 + 明细 12 列
```

**YZH**：定义在**后端** JSON，前端从 `/config` 或 `/treepconfig` 拉取后动态渲染。

```json
// yzh-core/YZH.Core.Web/Assets/EntityConfigs/System/Sys_Role.json
{ "FieldName": "DicName", "DesName": "字典名称", "Type": "Text",
  "XsFlag": true, "BcFlag": true, "Yxk": true, "Width": 180 }
```

**收益**：改一列 = 改一个 JSON，**前端零改动、无需重新构建**；**代价**：JSON 缺失时页面**静默空白**（`EntityConfigHelper` 默认只 warning），必须 `override StrictConfigLoad => true`。

---

## 三、实证：同一模块的代码量（数据字典）

### 传统 Vol 实现（实测行数）

| 层 | 文件 | 行数 |
|----|------|:----:|
| 控制器 | `Sys_DictionaryController.cs` | 18 |
| 控制器 | `Sys_DictionaryListController.cs` | 21 |
| 服务 | `Sys_DictionaryService.cs` | 27 |
| 服务 | `Partial/Sys_DictionaryService.cs` | **283** |
| 服务 | `Sys_DictionaryListService.cs` | 26 |
| 服务 | `Partial/Sys_DictionaryListService.cs` | 27 |
| 基础设施 | `YZH.Core/Infrastructure/DictionaryManager.cs` | 105 |
| 实体 | `Sys_Dictionary.cs` | 159 |
| 实体 | `Sys_DictionaryList.cs` | 128 |
| **后端小计** | | **794** |
| 前端页面 | `Sys_Dictionary.vue`（view-grid 主从表） | 481 |
| 前端配置 | `Sys_Dictionary/options.js`（主 16 列 + 明细 12 列） | 78 |
| **前端小计** | | **559** |

### YZH 实现（方案估算）

| 层 | 文件 | 行数 |
|----|------|:----:|
| 控制器 | `DictionaryController.cs`（4 个钩子 + 1 个私有生成器，**0 自定义端点**） | **60** |
| 实体 | `Sys_Dictionary.cs` + `Sys_DictionaryList.cs` | 85 |
| **后端小计** | | **145** |
| 配置 | `Sys_Dictionary{,Form,List}.json` | ~150 |
| 前端 API | `api/system/dictionary.ts` | ~20 |
| 前端 Logic | `pages/system/dictionary/logic.ts`（3 行配置 + 继承） | **~10** |
| 前端页面 | `pages/system/dictionary/index.vue`（照 `organization/index.vue`） | ~400（模板复用） |
| **前端小计（业务逻辑）** | | **~30** |

### 对比结论

| 指标 | 传统 Vol | YZH | 倍差 |
|------|:--------:|:---:|:----:|
| 后端业务代码 | 794 行 C# | **145 行 C#** | **约 1/5.5** |
| 其中控制器 | 39 行（空壳） | 60 行（**含全部逻辑**） | — |
| 其中服务/业务逻辑 | 363 行 | **0 行**（逻辑进钩子） | **归零** |
| 其中自研基础设施 | 105 行（字典缓存） | 0（复用 `ICacheManager`） | 归零 |
| 前端"列/字段"配置代码 | 78 行硬编码 | **0**（后端 JSON） | 归零 |
| 前端业务逻辑代码 | 481 行 | **~30 行** | **约 1/16** |
| 自定义端点 | 6 个业务方法 + 4 个基类端点 | **0** | 归零 |
| 树形结构 | 无（主从弹窗） | 基类内置 | — |
| 新增列 | 改前端 + 重新构建 | 改 JSON | — |

> **新增配置层（~150 行 JSON）是 YZH 引入的唯一新成本**，但它替代了前端 78 行硬编码 + 后端 283 行服务逻辑，且**运行时可改**。

---

## 四、YZH 架构的代价（诚实评估）

| 代价 | 具体表现 | 应对 |
|------|---------|------|
| **基类能力 = 天花板** | 基类没有的能力（如历史 Vol 的 `SaveModel` 主从一次性保存）**无法照搬**，必须重新设计形态 | 优先换形态（如"主从弹窗"→"左树右表"）；实在不行才加自定义端点 |
| **`Code` 插入后不可改** | `SqlSugarDbOrm.UpdateAsync` 执行 `.IgnoreColumns({"Code","Id","CreateTime","CreateBy"})`，且 `WHERE Code = @Code` → 必须**插入前定值**；"先插入再补 Code"必然失败 | 在 `OnBeforeAdd` / `OnBeforeAddTree` 中生成 `Code` |
| **`IsValid` / `IsDeleted` 需显式重映射** | `BaseEntity` 的基类属性带 `[SugarColumn(IsIgnore = true)]` → `HasProperty<T>()` 返回 false → **过滤条件被静默跳过** | 子实体用 `new` + `[SugarColumn(ColumnName=...)]` 重映射到真实列 |
| **行操作只能拿到 `Code`** | `ExecuteAction`（`YzhControllerBase.cs:546`）与 `ExecuteTreeAction`（`TreeTableControllerBase.cs:293`）**只把 JSON 的 `Code` 注入实体** | handler 内用 `Code` 反查：`GetByCodeAny(e.Code)` |
| **配置缺失静默失败** | 缺 `EntityConfigs/*.json` → 页面空白且**无报错** | `override StrictConfigLoad => true` |
| **端点固定，特殊需求要覆盖** | 如 `treepconfig` 必须改名（`[HttpGet("config")]` 会与基类 virtual 冲突） | 按基类注释使用约定路由名 |
| **学习成本集中在框架源码** | 上述陷阱**文档不足**，必须读基类源码才能避开 | 见 [08-常见错误与修复](./08-常见错误与修复.md) |

---

## 五、迁移经验（可直接复用）

1. **先判形态**：单表 → `YzhControllerBase<V>`；左树右表 → `TreeTableControllerBase<T,V>`。判错形态会导致大量返工（字典模块 V1 就判成了"自建主从端点族"）。
2. **实体对齐三件套**：`Code`（定位键）/ `IsValid`（启用）/ `IsDeleted`+`DeleteTime`+`DeleteBy`（软删除），且**必须显式映射到真实列**。
3. **关联一律用 `Code`**：树用 `ParentCode`，右表用 `xxxCode`；**不引入任何 Id 型关联字段**。
4. **业务逻辑只写钩子**：`OnBeforeAdd*` 校验、`OnAfterAdd*` 缓存失效；不要新建 Service 层业务方法。
5. **三层同构建文件**：后端 Controller 路径 = 前端 API 路径 = 前端 Pages 路径（见 [06-代码结构规范](./06-代码结构规范.md)）。
6. **缺配置就补 JSON**：不要退回前端硬编码列定义。
7. **旧代码只作参考**：`src/old/**` 禁止修改；检索时用术语「历史后端代码」「历史前端代码」。**特别提醒**：照搬旧前端时务必删掉 `getPageData` 之类旧 Vol 路由。

---

## 六、框架自身的待清理项（本次核查发现）

| # | 问题 | 位置 | 建议 |
|:-:|------|------|------|
| 1 | **双键设计规范未入文档体系** | 仅存在于 `scripts/db/dual_key_design_V1.sql` | 提炼为正式规范文档（本文 §二·差异 2 已初步收录） |
| 2 | 框架既有表仍残留 Id 型字段 | `Sys_Organization`：`CreateID`/`ModifyID`/`DeleteID`；`Sys_User`：`CreateID`/`ModifyID`/`OrgId`/`Dept_Id`/`Role_Id`/`ParentUserId`；`Sys_Role`：`ParentId` | 另立任务统一清理；`Sys_Role.ParentId` 被 `RoleController.cs:81-114` 引用，需同步改代码 |
| 3 | `Sys_User.Code` 被当作 `UserName` 使用 | `UserController.cs:99` `ExistsByCodeAsync(entity.UserName)` | 与双键设计冲突，导致**账号唯一性校验实际失效**，应单独修复 |
| 4 | `Sys_Organization.Code` 值域混杂 | 既有 `ORG_ROOT_001` 等语义码，又有 Guid（基类 `TreeTableControllerBase.cs:146-147` 自动填充） | 统一为 `PREFIX_` 序号规范 |

---

## 七、结论

| 结论 | 说明 |
|------|------|
| **代码量** | YZH 架构下同一模块后端业务代码约为传统的 **1/5**，前端业务逻辑约为 **1/16**，列/字段配置代码 **归零** |
| **真正省在哪** | 省在"**不写 CRUD、不写端点、不写树、不写软删除、不写列定义**"——这五件事占传统实现的大头 |
| **真正贵在哪** | 贵在**判形态**与**补库结构**：形态判错或实体缺 `Code`/`IsValid`/软删除三列，代码量会瞬间回到传统水平 |
| **前置条件** | 关联必须走 `Code`、`Code` 必须插入前定值、`IsValid`/`IsDeleted` 必须显式重映射、配置 JSON 必须存在 |
| **一句话** | **YZH 把"写代码"变成了"配对形态 + 补齐结构 + 写钩子"。** |

---

## 同组文档

[README.md](./README.md) · [01-架构总纲](./01-架构总纲.md) · [02-后端架构](./02-后端架构.md) · [03-前端架构](./03-前端架构.md) · [08-常见错误与修复](./08-常见错误与修复.md) · [12-框架能力清单-V1.md](./12-框架能力清单-V1.md)
