---
status: living
---

# YZH 架构规则 V1

> **本文定位**：YZH 架构的**规则总集**。每条规则给「内容 / 为什么 / 违反症状 / 检查方式 / 权威出处 / 例外」。
> **本文是索引与判据，不是教程**——原理细节看对应章节文档，动手看 `样板页面指南-V1.md`。
> **阅读顺序**：`AGENTS.md`（AI 入口）→ **本文**（规则）→ `01-架构总纲` / `02-后端架构` / `03-前端架构`（原理）→ `样板页面指南-V1.md`（动手）
>
> **为什么需要本文**：架构约束如果只散落在 27 份文档里，等于没有约束。
> 本文是**唯一规则清单**——新增规则必须先加进本文，再加守卫。
>
> **V1.1 修正（2026-09-24，用户纠正）**：原判「`Sxh` 是死字段、建议从 JSON 删除」**是错的** ——
> `Sxh` 是 WPF 原型的一等字段（`DefineColumn.cs:59` 注释「**显示顺序**」），**控制表格列顺序**；
> `Row`/`Col`/`RowSpan`/`ColSpan` 控制**表单 Grid 布局**。二者现状是**契约断裂（G18/G19）**，**应补齐**。
> → 新增 **R4-10**（表格列顺序 = `Sxh` 升序）与 **R4-11**（表单栅格 = WPF Grid 模型）。

---

## 〇、规则总览

**强制等级**：**L1 编译期** > **L2 启动期** > **L3 提交期（守卫）** > **L4 构建期** > **L5 文档期（靠 review）**
**判据**：**没有等级的规则 = 不是规则，是愿望。**

| 编号 | 规则 | 等级 | 机器检查 | 权威 |
|---|---|---|---|---|
| **R1-1** | Id 永不定位/关联/分流，只用 Code | L5 | ⚠️ 待补守卫 | `01-架构总纲 §2.1` · `08-常见错误 E201` |
| **R1-2** | 中间表用复合业务键（`RoleCode+UserCode`） | L5 | ⚠️ 待补守卫 | 同上 |
| **R2-1** | 三层同构（Controller = API 文件 = Pages 文件夹） | L5 | ❌ | `前后端代码结构统一规则-V1` |
| **R2-2** | 前端三层：core / share / host，引用方向单向 | L3 | ✅ R10 | `19-前端分层与宿主接入规范` |
| **R2-3** | 引用方式唯一：`@yzh-core` / `@share` alias | L3 | ⚠️ 待补 R13 | `19-前端分层与宿主接入规范` |
| **R3-1** | 列名 PascalCase 逐字一致（**铁律七**） | L3 | ✅ R2（TreeNode） | `项目全局规则 §16.9` |
| **R3-2** | 全库 `utf8mb4` + `utf8mb4_general_ci`（**铁律八**） | L2 | ✅ 启动自检 | `项目全局规则 §16.10` |
| **R3-3** | **`Enable` 零容忍**：DB 无 `Enable` 列，启禁统一走 `IIsValid.IsValid`（**铁律九**） | L3 | ✅ R7 + DB 验证 SQL | `18 §R3-3` · `命名规范违规清单` |
| **R3-4** | `Yxk` = 允许为空（**不是必填**） | L2 | ✅ 基类校验 | `AGENTS.md §③` |
| **R4-1** | EntityConfig 文件名 = `ConfigName`（默认实体类型名） | L2 | ✅ 启动 warning / `StrictConfigLoad` | `EntityConfigHelper.cs` |
| **R4-2** | **新 Controller 必须 `StrictConfigLoad => true`** | L2 | ✅ | `YzhControllerBase.cs:123` |
| **R4-3** | JSON 主要提供 `Title` + `Columns` | L5 | ❌ | `EntityConfigDto.cs` 注释 |
| **R4-4** | `GroupIndex`：`0`=可编辑，`1+`=特定模式只读，`99`=详情全只读 | L2 | ✅ 基类 | `ColumnConfigDto.cs` |
| **R4-10** | **表格列顺序 = `Sxh` 升序**（不是 JSON 数组顺序；⛔ 禁删 `Sxh`） | L2 | ⚠️ 待补守卫 | `DefineColumn.cs:59` · `04-数据契约` |
| **R4-11** | **表单栅格 = `Row`/`Col`/`RowSpan`/`ColSpan`**（WPF Grid 模型） | L2 | ⚠️ 待补守卫 | `DefineColumn.cs:12-21` · `04-数据契约` |
| **R5-1** | 后端基类按职能选用（**非强制**），合规看行为契约 | L5 | ❌ | `AGENTS.md §②` |
| **R5-2** | `ApiCode` 绑定角色-接口，改控制器名/动作名 = 授权静默断裂 | L5 | ❌ | `11-接口权限自动同步设计` |
| **R5-3** | 判成功只用 `result.Success`（`Ok()` 的 `Code` 是 `null`） | L1 | ✅ 编译期可查 | `08-常见错误 E201` |
| **R5-4** | 统一 `ApiResponse` 信封（例外 E7） | L3 | ✅ R3/R3b | `04-数据契约` |
| **R6-1** | 页面必须用内核（`SingleTableCore` / `TreeTableCore`） | L5 | ❌ | `样板页面指南-V1` |
| **R6-2** | 页面禁手写 `handleAdd`/`handleSubmit` 等 | L3 | ✅ 守卫（部分） | `AGENTS.md §①` |
| **R6-3** | 禁 `axios` / `.vue` 内 `fetch(` | L3 | ✅ R4/R5 | `AGENTS.md §①` |
| **R6-4** | 页面禁内联 `<el-table>`，必须 `YzhTable` | L3 | ✅ R6 | `AGENTS.md §①` |
| **R6-5** | 装配只在 `composables/*`，页面只消费 | L5 | ❌ | `前端原子组件与逻辑内核分层规范` |
| **R6-6** | core 禁硬编码后端地址 | L3 | ✅ R11 | `19-前端分层与宿主接入规范` |

---

## 一、双关键字规则（Id / Code）

### R1-1 规则内容

| 场景 | 允许 | 禁止 |
|---|---|---|
| **WHERE 定位** | `WHERE Code = @code` | `WHERE Id = @id` |
| **表关联** | `ON a.XxxCode = b.Code` | `ON a.XxxId = b.Id` |
| **新增 vs 更新分流** | `GetByCode(entity.Code)` 有→更新、无→新增 | `Id > 0` / `Id == 0` / `Id == null` |
| **存在性判定** | `Exists(Code)` | `Exists(Id)` |
| **前后端传参** | 只传 `Code` | 传 `Id` |
| **前端行操作** | `deleteXxx(row.Code)`、`data.Code ? update : add` | `row.Id` |
| **排序** | ✅ `OrderBy(Id)` 允许 | — |
| **未落库信号** | ✅ `new` 实体 `Id = 0`/`null` 允许 | — |
| **Code 为空却要更新** | 报错 `更新失败：缺少业务键 Code` | ⛔ **禁止回退到 Id** |

### 为什么（这条规则的作用 —— 四条）

**① Id 是存储实现细节，Code 是业务身份。**
`Id` 由数据库自增/序列生成，**换库、数据迁移、多环境同步、重新导入时都会变**。
`Code` 是业务定义的稳定标识（`CB001`、`STD-ISO9001`、`ROLE_ADMIN`）。
→ 用 `Id` 关联，迁移后关联关系**全部断裂**，而且**不报错**（匹配 0 行）。

**② 收敛"新增/更新"的判据，让 AI 生成代码有规律可循。**
若允许 `Id` 分流，同一个实体在不同页面会混用两套判据（有的 `Id > 0`、有的 `GetByCode`）→ **AI 每次生成的写法都不同** → 出问题时无法统一排查，改一处可能影响另一处。
→ **这是"为什么要求所有代码符合架构"的直接答案**：约束的价值不在优雅，在**可预测**。

**③ 前后端传参只有一条链路。**
前端只传 `Code`，后端只按 `Code` 查 → 不可能出现"前端传 `Id`、后端按 `Code` 找"的错位。

**④ 中间表天然用复合业务键。**
`Sys_RoleUser` 没有业务意义的 `Code`，用 `RoleCode + UserCode`；`Sys_RoleMenu` 用 `RoleCode + MenuCode`。
→ 这比"给中间表造一个代理 Id"更稳，且查询语义自解释。

### 违反症状（照着查）

| 症状 | 根因 |
|---|---|
| 树/列表**有数据行但关联列全空**，无报错 | 用 `Id` 关联，跨环境后匹配 0 行 |
| 前端未回传 `Id` 时**新增变成更新**（或反之），静默覆盖数据 | `Id > 0` 分流 |
| 角色/菜单/接口**关联关系批量丢失** | 中间表用 `Id` |
| 同一实体在 A 页面能改、B 页面报"缺少业务键" | 两套判据并存 |

### 检查方式

```bash
# 后端：WHERE Id / Id 分流
grep -rnE "WHERE\s+Id\b|\.Id\s*==\s*0|\.Id\s*>\s*0|Id\s*==\s*null" --include="*.cs" src/yzh-core src/certplatform-api | grep -vE "/bin/|/obj/"
# 前端：row.Id
grep -rnE "row\.Id\b|\bId\b.*delete" --include="*.ts" --include="*.vue" src/certplatform-web
```

**当前状态**：前端 ✅ 已贯彻（实测 0 处 `row.Id`）；后端靠 review，**无守卫**（R1-1 待补）。

### 例外

- `OrderBy(Id)` —— 排序允许
- `new` 实体的 `Id = 0`/`null` —— "未落库"信号，允许
- 框架层内部（`EntityService` 的物理主键维护）—— 允许，但**不得外泄到业务层**

---

## 二、项目结构规则

### R2-1 三层同构

**规则**：后端 `Controllers/{Domain}/{Xxx}Controller.cs` = 前端 `api/{domain}/xxx.ts` = 前端 `pages/{domain}/xxx/`，**三层命名与层级一致**。

**为什么**：找一个功能时不用思考"它在哪一层"——顺着同一个路径走到底。AI 生成新模块时也能按同一规则落位。

**违反症状**：后端有 `Foundation/CertStage`，前端 API 文件却叫 `cert-stage-api.ts` 放在 `api/` 根 → 每次找都要 grep。

### R2-2 前端三层与引用方向（★ 单向）

```
yzh.vue.core   （框架层，跨项目复用；不启动、不感知后端地址）
      ▲
      │ 只被引用，不引用上层
cert-share     （跨端业务共享层：admin + auditor 共用）
      ▲
      │
cert-admin / cert-auditor / cert-enterprise  （宿主，独立可运行）
```

**依赖铁律（违反即返工）**：

| 层 | 禁止 |
|---|---|
| **组件**（`core/components`） | import 任何 Logic / `@/api` / `@share` / `pinia` / `vue-router`（守卫 R1） |
| **内核**（`core/logic`） | import 任何 Vue 组件（只允许 `vue` 响应式 API + `element-plus` 的 `ElMessage`/`ElMessageBox`） |
| **core 新层**（pages/layouts/router/composables/api） | import `@/` 或 `@share/`（守卫 R10） |
| **页面** | 直接 import `@/api/**`（须走 `api` 模块） |

**权威**：`19-前端分层与宿主接入规范-V1.md` · `前端原子组件与逻辑内核分层架构设计规范-V1.md`

### R2-3 引用方式唯一（★ 当前不统一）

**规则**：跨包引用**只用 alias** —— `@yzh-core`（→ `yzh.vue.core/src`）、`@share`（→ `cert-share/src`）。
**禁止**包名引用：`yzh.vue.core/*`、`@certplatform/share`。

**为什么**：两套机制并存时，**改一处不会改另一处**。实测：`@certplatform/share` 在 4 个 `package.json` 里声明为 workspace `file:` 依赖，**代码里 0 引用**——纯死声明，却让人以为"改了 package.json 就生效"。

**当前违规**：`cert-share/src/api/workflow/{job-skill,ai-usage}.ts` 用 `yzh.vue.core/types`（2 处）。
**检查**：`grep -rnE "from ['\"]yzh\.vue\.core|from ['\"]@certplatform" src/certplatform-web/cert/*/src`
**待补守卫**：R13

### R2-4 新增端必须登记守卫

**规则**：新增宿主端时，必须在 `src/certplatform-web/scripts/guards.mjs` 的 `PAGE_ROOTS` / `API_ROOTS` 登记。
**为什么**：**未登记的端处于守卫盲区** —— 写了违规代码但守卫报绿。
**当前盲区（实测 6 处）**：`core/logic`｜`core/utils`｜`core/types`+`adapters`｜`cert-share/src/{components,composables,utils,logic}`｜`cert-admin/src/{store,composables,router,layouts}`｜`package.json`。

---

## 三、字段编写规则

### R3-1 列名 PascalCase 逐字一致（铁律七）

**规则**：`DB 列名` = `C# 属性名` = `TS 字段名` = **PascalCase 逐字一致**。
前端读 `row.RuleName`、`node.Code`、`formData.StandardCode`。

**为什么**：**这条规则是"唯一一条违反后不报错、但症状最难看"的规则**。三处名字只要有一处大小写不同，就**静默取不到值** → 表格渲染成空行、树节点全空、表单提交丢字段，**控制台零报错**。

**违反症状**：**表格有数据行、一列都不显示、零报错**（不是"整页空白"）。

**例外清单**（须注释标注「已登记例外」）：

| ID | 例外 | 说明 |
|---|---|---|
| E1 | `ApiResponse` 信封 camelCase | `success` / `message` / `data` / `code` / `timestamp` |
| E6 | 裸 JSON `{img, uuid}` | 验证码接口 |
| E7 | `{code, data, msg}` **无 `success`** | `StandardDirectory` 系列，须用 `res.code === 200` 且**禁用 `res.success`** |

**检查**：守卫 R2（TreeNode 小写读取）；后端 `BizNamingRules` 启动自检。

### R3-2 字符集与排序规则（铁律八）

**规则**：**全库统一 `utf8mb4` + `utf8mb4_general_ci`，无例外。**
**两个反直觉根因**（必须显式写）：

1. 建表只写 `DEFAULT CHARSET=utf8mb4`（**不带 `COLLATE`**）→ 落 `utf8mb4_0900_ai_ci`
2. `SET NAMES utf8mb4`（**不带 `COLLATE`**）→ 连接 collation 也是 `0900_ai_ci`（**即使 `collation_server` 已是 `general_ci`**）

→ **写 SQL 必须显式带 `COLLATE`**：`DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci` / `SET NAMES utf8mb4 COLLATE utf8mb4_general_ci`

**违反症状**：`ERROR 1267 (Illegal mix of collations)` **只在列 vs 列关联时出现** → **单表测试全绿，只在跨表关联时暴露**。

### R3-3 启禁字段唯一 = `IsValid`（★ 升级为铁律九：`Enable` 零容忍）

**规则**：启用/禁用**唯一字段 = `IsValid`（int，0/1）**，由 **`IIsValid` 接口**统一提供。
- ⛔ **数据库中不允许任何表存在 `Enable` / `enable` 列**（用户 2026-09-24 明确）
- ⛔ 新实体/新列**禁止**声明 `Enable` / `EnableField`
- 业务开关用 `IsActive`
- 软删除用 `IsDeleted`
- **~~例外：`sys_api.Enable`~~** ← **该例外已作废**（用户明确「所有的表都统一用接口的字段」）

**契约（`YZH.Core.Stand/Interfaces/IIsValid.cs`，实测）**：

```csharp
public interface IIsValid
{
    /// <summary>有效标志（1=有效，0=无效）</summary>
    int IsValid { get; set; }
}
```

**为什么**（用户原话）：「这样的设计为了是**约定**，和**架构的可控**」——
`Enable` 这个词在本系统里**已经有三重语义**（见 §四 R4-5），再加一层必然混淆；
更重要的是：**唯一契约才能让「前端按钮 / 后端过滤 / 权限判定」三处自动一致**。

**实测违规（2026-09-24，修正前）**：

| 项 | 数量 |
|---|---|
| `Enable`/`enable` 列（正式表） | **35 个**（25 小写 + 10 大写） |
| 其中与 `IsValid` 并存（语义冲突，直接删） | **26 个** |
| 其中无 `IsValid`（需代码配合/决策） | **9 个**：`sys_api`｜`sys_log`｜`Sys_TableColumn`｜`Sys_TableInfo`｜`Sys_UserDepartment`｜`Sys_WorkFlow*`（4 张） |

**★ 违反症状（照这两条查）**：
1. **一个表里 `enable` 与 `IsValid` 并存** → **列表显示的行 ≠ 能操作的行**，且**两边都不报错**（后端按 `IsValid` 过滤、前端按钮按 `EnableField` 渲染）
2. 实体注释写 `// DB: Name` 而 DB 实际是 `name` → **代码自证清白，DB 不是**（`wf_skill` 即此形态）

**检查**：守卫 **R7**（前端）+ `scripts/db/fix/fix-column-naming-2026-09-24.sql` 文末验证 SQL。
⚠️ **验证 SQL 必须用 `CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin`** ——
`information_schema.COLUMN_NAME` 排序规则大小写不敏感，直接写 `NOT REGEXP '^[A-Z]'` 会**永远返回 0 行**（假阴性）。

**权威**：`AGENTS.md §③`｜`输出产物/命名规范违规清单与消灭方案-V1.md`

### R3-4 `Yxk` = 允许为空（★ 极易读反）

**规则**：`Yxk = true` ⇒ **该列可留空、不校验、前端不显示必填标记**。
**后端校验条件**：`BcFlag && !Yxk`（`YzhControllerBase.cs:906`）
**前端**：`required: !c.Yxk`

**为什么强调**：AI 曾把 `Yxk` 读成"必填"并据此写错校验。
**权威**：`AGENTS.md §③` · `XamlHelper.cs:357`（`!YXK` → 标签棕色）/ `:611`（`!YXK && value == null` → "不能为空"）
**注意**：**DB 无 `Yxk` 列**——它属 EntityConfig 层。

### R3-5 审计字段约定

| 字段 | 类型 | 约定 |
|---|---|---|
| `CreateTime` / `CreateBy` | DateTime / string | 新增时由基类写入 |
| `UpdateTime` / `UpdateBy` | DateTime / string | 更新时由基类写入 |
| `DeleteTime` | DateTime? | 软删除时间（非空 = 已删） |
| `IsValid` | int | 0/1，见 R3-3 |

**EntityConfig 约定**：审计字段 `Sxh ≥ 98`、`GroupIndex = "99"`、`BcFlag = false`、`Enable = false`。

---

## 四、EntityConfig 规则

> **权威源码**：`yzh-core/YZH.Core.Stand/Models/Config/`（`EntityConfigDto` / `ColumnConfigDto` / `TreeTableConfigDto` / `SearchFieldDto` / `ToolbarConfig`）｜`YZH.Core.Stand/Helpers/EntityConfigHelper.cs`｜`YZH.Core.Api/Controllers/YzhControllerBase.cs`

### R4-1 文件位置与命名（★ 查找键 = 配置名）

**两处配置目录**（按顺序搜索）：

| 层 | 目录 | 放什么 |
|---|---|---|
| **核心层** | `src/yzh-core/YZH.Core.Web/Assets/EntityConfigs/` | `System/` 域（框架自带） |
| **业务层** | `src/certplatform-api/CertPlatform.{Admin,Auditor,Enterprise}/Assets/EntityConfigs/` | `Foundation/` `Workflow/` `Cert/` 等业务域 |

**查找规则**（`EntityConfigHelper.LoadFromFile`）：
1. 默认 `configName = typeof(V).Name` → **文件名必须等于它**（**大小写不敏感，但下划线敏感**）
2. 支持 `"System/User"` 形式直接定位
3. 搜索顺序：核心目录 → 业务目录 1 → 业务目录 2 → …
4. 缓存 **1 小时** + `FileSystemWatcher` 监听变更自动清缓存

**★ 违反症状**：文件名与 `ConfigName` 不一致 → **静默空配置** → **页面有数据行、一列都不显示、控制台零报错**（`OnConfigMissing` 只打 warning）。

**★ 自检命令**：
```bash
curl -s http://127.0.0.1:9992/api/<路由>/config | head -5
# Title 等于实体类型名（如 "Sys_User"）⇒ 配置没加载上
```

**当前命名债（实测）**：`System/` 域文件名**前缀不统一**——`Sys_Role.json` / `Sys_Menu.json` / `Sys_MenuForm.json` / `Sys_DictionaryList.json` / `Sys_DictionaryForm.json`（带 `Sys_`）vs `User.json` / `SysLog.json` / `SysApi.json` / `SysConfig.json` / `OrganizationForm.json`（不带）。→ **建议：定一条命名规则并逐个对齐**（见计划 D-12）。

### R4-2 新 Controller 必须开严格模式（★ 机制已有，仅 4 个 Controller 遵守）

**规则**：**所有新 `Controller` 子类显式 `protected override bool StrictConfigLoad => true;`**

**为什么**：`StrictConfigLoad` 默认 `false` → 缺 JSON 时**只打 warning，页面空白且不报错** → 排查极困难（正是 R4-1 那个静默失败）。
`true` 时 `OnConfigMissing` 直接 `throw`，**开发期立刻暴露**。

**权威**：`YzhControllerBase.cs:108`（`virtual bool StrictConfigLoad => false`）· `:123`（注释原文：**"推荐：所有新 Controller 子类显式 override `StrictConfigLoad => true`"**）

**当前贯彻（实测）**：**仅 4 个** override —— `DictionaryController`、`MenuManagementController`、`CertificationBodyController`、`ValidationRuleController`。而继承基类的 Controller 有 **25 个**。
另：`YzhCoreOptions.GlobalStrictConfigLoad`（批量开启）**在代码/配置中 0 使用**。

→ **这是"约定已写、机制已实现、但没贯彻"的典型（根因 C2）。**

**检查**：`grep -rn "StrictConfigLoad" --include="*.cs" src | grep -v "/bin/\|/obj/"`

### R4-3 JSON 只提供 `Title` + `Columns`

**规则**：EntityConfig JSON **主要写 `Title` 与 `Columns`**（可带 `FillMode` / `FormCols`）。
`SearchFields` / `RowButtons` / `Toolbar` / `NewEntity` / `Schema` **由基类智能推断或子类 override 提供**。

**配置优先级（从高到低）**：

| 优先级 | 来源 | 说明 |
|---|---|---|
| 1 | **子类 override 虚方法** | `GetSearchFields()` / `GetRowButtons()` / `GetToolbar()` |
| 2 | **基类默认推断** | `SearchFields` = 前 3 个 `XsFlag=true` 且非 `Other` 类型的字段；`RowButtons` = `Edit=true, Delete=true`；`Toolbar` = `Add=true, Delete=true, Export=false, Import=false` |
| 3 | **JSON 文件静态配置** | **仅 `Columns` 必须** |

**权威**：`EntityConfigDto.cs` 类注释（"JSON 文件静态配置（仅 Columns 必须）"）；`ConfigName` / `TableName` 明确标注 **"历史遗留，新代码不应依赖"**。

**⚠️ 待澄清**：实测 JSON 里普遍写了 `EnableField` / `RowButtons` / `FormCols`。按上述优先级，**JSON 里写这些属于低优先级的历史兼容路径**，是否真生效需逐个验证。→ **规则先定为"新 JSON 只写 `Title` + `Columns`"**（见计划 D-13）。

### R4-4 `ColumnConfigDto` 字段语义（权威全表）

| 字段 | 类型 | 语义 | 备注 |
|---|---|---|---|
| `FieldName` | string | **字段名**（= C# 属性名 = DB 列名） | 铁律七 |
| `DesName` | string | 显示名（**不是 `Name`**） | |
| `Type` | string | 控件类型 | 实测值：`TextBox`/`PasswordBox`/`Switch`/`DateTimePicker`/`Memo`/`ComboBox`/`Decimal`/`Other` |
| `XsFlag` | bool | **是否显示**（表格列 + 表单） | |
| `BcFlag` | bool | **是否保存** | |
| `Yxk` | bool | **允许为空**（见 R3-4） | |
| `Enable` | bool | **该列在表单里是否可编辑** | ⚠️ 与顶层 `EnableField`、`RowButtons.Enable` **三者同名不同义** |
| `Sortable` | bool | 列可排序 | |
| `Width` | int? | 列宽（像素） | 配合 `FillMode = PixFix` |
| `Fixed` | string? | 固定列（`left`/`right`） | |
| `Align` | string? | 对齐（`left`/`center`/`right`） | |
| `DictCode` | string? | 字典编码（如 `stage_category`） | |
| `Format` | string? | 格式化串 | ⚠️ **前端契约缺、适配层不消费** |
| `Row` / `Col` | int | 表单网格坐标 | ⚠️ 实测多为 0（未使用）；主排序依据是 JSON 数组顺序 |
| `RowSpan` / `ColSpan` | int | 跨行/跨列（默认 1） | `ColSpan: 2` 实测用于宽字段（如 `Remark`） |
| `Mrz` | object? | **默认值** | `IsValid` 的 `Mrz` = `"1"` |
| `GroupIndex` | string? | **分组索引**（见下） | |
| `Mask` | bool | 掩码显示（key/secret/password） | ⚠️ **前端契约无此字段** |

**`GroupIndex` 权威语义**（`ColumnConfigDto.cs` 注释原文）：
> `"0"` = 默认可编辑｜`"1"+` = 特定模式只读｜`"99"` = **详情全部只读**

**实测用法**：`Status` / `CreateTime` / `CreateBy` / `UpdateTime` / `UpdateBy` → `GroupIndex = "99"` + `BcFlag = false` + `Enable = false`。
⚠️ **WPF 支持逗号多组**（`GroupIndex.Split(',')`），**Vue 当前只支持单值**。

**`XsFlag` / `BcFlag` 四象限（速查）**：

| `XsFlag` | `BcFlag` | 含义 | 实测例 |
|---|---|---|---|
| ✅ | ✅ | 显示 + 可保存 | `UserName` |
| ✅ | ❌ | 显示但只读 | `OrgName` / `CreateTime` |
| ❌ | ✅ | 不显示但保存（隐藏字段） | `UserPwd` / `Remark` |
| ❌ | ❌ | 完全隐藏 | `OrgCode` / `CreateBy` |

**★ `Sxh` = 表格列显示顺序（不是死字段！）**

`Sxh` 是 **WPF 原型中语义明确的一等字段**（`YZH.Core.Stand/Models/Config/DefineColumn.cs:59-60`）：

```csharp
/// <summary>显示顺序</summary>
public int Sxh { get; set; }
```

| 维度 | 事实 |
|---|---|
| **设计语义** | **表格（`YzhTable`）列的显示顺序，升序排列** |
| **JSON 现状** | **100% 的列都写了**（实测 `System/User.json` 10/10、`Foundation/CertStage.json` 12/12） |
| **`ColumnConfigDto`（C#）** | ⛔ **无 `Sxh` 属性** → 反序列化时**静默丢弃** |
| **`ColumnConfig`（TS）** | ⛔ **无 `Sxh` 字段** |
| **前端消费** | ⛔ `toTableColumns` 只 `.filter(XsFlag).map()`，**无排序** → 退化为 JSON 数组顺序 |
| **设计文档** | ✅ `04-数据契约.md` **本来就写对了**：`Sxh?: number // 排序号（升序）`；`13-框架数据库设计-V1.md` 有 `ColumnSxh` 列 |

→ **结论：这是契约断裂（G18），不是死字段。** 修法（约 0.5h）：给 `ColumnConfigDto` + `ColumnConfig` 补 `Sxh`，`toTableColumns` 加 `.sort((a, b) => (a.Sxh ?? 0) - (b.Sxh ?? 0))`。

> **⚠️ 教训（判死铁律的反面）**：曾把 `Sxh` 判为「死字段」并建议从 JSON 删除 —— **这是错的**。
> 判死必须双问，第①问「**属不属于框架层能力**」当时答错了：它不是能力储备，是**应有能力但接线断了**。
> **判死前必须查「设计文档怎么写的」** —— `04-数据契约.md` 一直写着它的正确语义。

### R4-10 表格列顺序 = `Sxh`（升序）

| 规则 | 内容 |
|---|---|
| **R4-10a** | **表格列的显示顺序由 `Sxh` 升序决定**，**不是** JSON 数组顺序 |
| **R4-10b** | 新增列**必须显式写 `Sxh`**；同一配置内 `Sxh` **不得重复**（重复时按数组顺序兜底） |
| **R4-10c** | 审计字段 `Sxh ≥ 98`（与 R3-5 一致），业务列 `Sxh` 从 1 起 |
| **R4-10d** | 列顺序要改 → **改 `Sxh`，不要挪 JSON 数组位置**（挪数组不生效且易漏） |
| **R4-10e** | ⛔ **禁止删除 `Sxh`**（曾有此错误建议，已作废） |

### R4-11 表单栅格布局 = `Row` / `Col` / `RowSpan` / `ColSpan`（WPF Grid 模型）

`Row` / `Col` / `RowSpan` / `ColSpan` 同样是 WPF 原型的一等字段（`DefineColumn.cs:12/15/18/21`），**控制表单（`YzhForm`）的栅格坐标**，语义等价于 WPF `Grid.SetRow/SetColumn/RowSpan/ColumnSpan`。

| 维度 | 事实 |
|---|---|
| **设计语义** | 表单字段的 **Grid 坐标**：`Row` 行号、`Col` 列号、`RowSpan` 跨行、`ColSpan` 跨列 |
| **JSON 现状** | **每列都写了**（实测 `User.json`：`UserName Row=0,Col=0`；`UserTrueName Row=1,Col=0`；`UserPwd Row=2,Col=0`…） |
| **`ColumnConfigDto`（C#）** | ✅ **有**（`:24-27`） |
| **`ColumnConfig`（TS）** | ✅ **有** |
| **前端消费** | ⛔ **`toFormFields` 不用** —— 统一给 `span = Math.floor(24 / layoutCols)`；`YzhFormField` 也**没有** `Row/Col/RowSpan/ColSpan` 字段 |
| **设计文档** | ✅ `04-数据契约.md` 写对了：`Row/Col/ColSpan | — | CSS Grid 表单布局` |

→ **结论：契约完整但适配层不消费（G19）。** 表单退化为「等宽栅格 + 顺序流」，**跨列/跨行/指定坐标全部失效**。

| 规则 | 内容 |
|---|---|
| **R4-11a** | 表单布局**优先按 `Row`/`Col`/`RowSpan`/`ColSpan` 生成**（WPF Grid 模型）；`FormCols` 仅作**未配置坐标时的兜底** |
| **R4-11b** | `ColSpan = 2`（双列布局下）等价于占满整行；`ColSpan = 1` 为半行 |
| **R4-11c** | 配置坐标时**必须成套写**（`Row` + `Col`），只写一个视为未配置 |
| **R4-11d** | 前端 `YzhFormField` 需补 `Row`/`Col`/`RowSpan`/`ColSpan`，`YzhForm` 按坐标渲染（而非只认 `span`） |
| **R4-11e** | ⛔ **禁止用 `span` 硬编码覆盖 `ColSpan`**（现 `toFormFields` 的做法） |

> **用户原话**：「wpf 设计 grid 布局是非常合理的，我建议 `yzhform` 也可以遵循这个规则，**而且改造起来比较容易**」——✅ 已核实：改造点仅 `toFormFields` 映射 + `YzhFormField` 补 4 字段 + `YzhForm` 渲染，约 1h。

### R4-5 三个 `Enable` 的区分（★ 高频混淆点）

| 出现位置 | 名称 | 语义 |
|---|---|---|
| `EntityConfigDto.EnableField` | 顶层字符串 | **启禁功能作用的字段名**（默认 `"IsValid"`），驱动"启用/禁用"按钮 |
| `ColumnConfigDto.Enable` | 列级 bool | **该列在表单里是否可编辑** |
| `RowButtonConfig.Enable` | 行按钮 bool | **是否显示"启用/禁用"行按钮**（默认 `false`） |

**规则**：写配置时**必须写全限定名**（`EntityConfig.EnableField` / `Column.Enable` / `RowButtons.Enable`），禁止简写"Enable"。

### R4-6 启禁按钮配置位置（单表 vs 左树右表）

| 页面形态 | 看哪里 | 为空时 |
|---|---|---|
| 单表 | `EntityConfig.EnableField` | **整页无启禁** |
| 左树右表 | `TreeTableConfigDto.TableConfig.EnableField`（右表）+ `TreeConfig.EnableField`（左树） | 同上 |
| 左树右表额外 | `TreeConfig.AllowToggle`（`bool?`） | `null`/未设置 = 沿用旧行为 |

**已配启禁的页面（实测 5 个）**：`CertStage`｜`PhaseDefinition`｜`CertificationBody`｜`ISOStandard`｜`ISOClause`。
**⚠️ 机构树不走基类**：`/tree/action/disable` 递归级联、`enable` **不级联**。

### R4-7 `TreeBehaviorConfigDto` 字段（左树右表）

| 字段 | 默认 | 语义 |
|---|---|---|
| `Lazy` | `true` | 懒加载 |
| `AllowEdit` / `AllowAddChild` / `AllowDelete` / `AllowRename` | `true` | 节点动作开关 |
| `AllowDeleteWithChildren` | `false` | 允许删含子节点的节点 |
| `RootParentCode` | `null` | 根父编码 |
| **`NameField` / `CodeField` / `ParentCodeField`** | `"Name"` / **`"Code"`** / `"ParentCode"` | **树三键** —— R1-1 在树配置上的体现 |
| `RelateField` | `""` | 树节点与右表的关联字段 |
| `NoSelectionBehavior` | `"empty"` | 未选中时的右表行为 |
| `MaxLevel` | `0` | 最大层级（0 = 不限） |
| `CustomActions` | `null` | 自定义节点按钮 `{方法名: 显示文字}` |
| `EnableField` / `AllowToggle` | `"IsValid"` / `null` | 启禁（见 R4-6） |

### R4-8 `SearchFieldDto` 字段

| 字段 | 默认 | 语义 |
|---|---|---|
| `Label` / `Field` | — | 标签 / 字段名 |
| `Operator` | `"like"` | 查询操作符 |
| `ControlType` | `"input"` | 控件类型 |
| `Options` | `null` | 下拉选项（`SelectOptionDto{Label,Value}`） |
| `Width` | `180` | 宽度 |

### R4-9 `FillMode` 与 `FormCols`

| 字段 | 值 | 语义 |
|---|---|---|
| `FillMode` | `AutoFix`（默认） | 所有列等比例填满整表（字段少） |
| | `PixFix` | 每列按 `Width` 像素渲染，超出横向滚动（字段多） |
| `FormCols` | `1` / `2` | 表单列数 |
| | `0`（默认） | **自动**：`BcFlag` 字段 ≤10 用 1 列，>10 用 2 列 |

---

## 五、后端接口规则

### R5-1 基类按职能选用（★ 非强制，合规看行为契约）

**规则**：
- 单表 CRUD → `YzhControllerBase<V>`
- 左树右表 → `TreeTableControllerBase<T, V>`
- **职能特殊的控制器直接继承 `ControllerBase` 是允许的**（职能差异，非技术债）

**★ 判断合规看「行为契约」，不看 `:` 后面写了什么**。三条行为契约：

| # | 契约 | 违反症状 |
|---|---|---|
| ① | **未登录返回 401** | 匿名可访问 → 数据泄露 |
| ② | **走 `ApiResponse` 信封** | 前端拆包失败（例外见 E7） |
| ③ | **列名 PascalCase 三处一致** | 表格空行（见 R3-1） |

**当前状态（实测）**：34 个 Controller 中 **25 个**继承基类、**~9 个**直接 `ControllerBase`。
**禁止**：把"基类覆盖率"当合规指标（已明确纠正）。

### R5-2 `ApiCode` 与角色-接口关联（★ 改名前必读）

**规则**：`ApiCode = SHA256("{HTTP方法}|{控制器名}|{动作名小写}")`（`ApiScanner.cs:326`）

| 变更 | `ApiCode` | 后果 |
|---|---|---|
| 改**路由前缀** | 不变 | ✅ 授权不断 |
| 改**控制器名 / 动作名** | **变** | ⛔ 角色-接口关联**静默断裂** → 须重跑 ApiSync 并**重新关联** |
| 新增接口 | 新增 | 需补 `sys_role_api`，否则非超管 **403** |

**路由约定**：`/api/{Domain}/{Controller}/{action}`
**`sys_api` 无 `ControllerName` 列** → 查询用 `Path LIKE '%X%'`。

### R5-3 判成功只用 `result.Success`（★ 静默失败陷阱）

**规则**：`Result<T>.Ok()` 的 `Code` 是 **`null`，不是 200**（`Ok(data) => new() { Data = data }` 只设 `Data`；只有 `Fail` 才设 `Code`）。
→ **判成功只能用 `result.Success`**（`=> Error == null`）。

**实测事故**：`StandardDirectoryService.UpdateConfigAsync/DeleteConfigAsync` 写 `result.Code == 200` → **恒 `false`** → **软删已落库（`IsValid=0` + `DeleteTime`）却回报"删除失败"**。

**检查**：`grep -rnE "\.Code\s*==\s*200" --include="*.cs" src`（全仓应仅剩 E7 相关）

### R5-4 统一 `ApiResponse` 信封

**规则**：`ApiResponse { success, code, message, data, timestamp }`（例外 E1：信封字段 camelCase）。

**例外 E7**：`StandardDirectory` 全系列是 `{code, data, msg}`、**无 `success`** → 断言必须用 `code === 200`，**禁用 `res.success`**。

**守卫**：R3（页面层禁 `.code === 200`）/ R3b（API 模块层禁）。

### R5-5 其他禁止事项

- ⛔ 新架构**禁止**调用旧架构（`src/old/`）的控制器/服务
- ⛔ 禁止修改 `src/old/` 下任何文件
- ✅ `src/yzh-core/`（框架层）**可以且鼓励合理改造**（须遵守 `AGENTS.md §11` 框架层改造准入 5 条）

---

## 六、前端内核规则

### R6-1 内核族与继承关系

| 内核 | 文件 | 用途 |
|---|---|---|
| **`SingleTableCore<V>`** | `core/logic/SingleTableCore.ts` | 单表 CRUD |
| **`TreeTableCore<V>`** | `core/logic/TreeTableCore.ts` | 左树右表（**继承 `SingleTableCore`**） |
| `AssociationTreeCore` | `core/logic/AssociationTreeCore.ts` | 关联树 |
| `CheckTreeCore` | `core/logic/CheckTreeCore.ts` | 勾选授权树（继承 `AssociationTreeCore`） |
| `LinkTableCore` | `core/logic/LinkTableCore.ts` | 关联表 |
| `TreeSide` | `core/logic/TreeSide.ts` | 左树侧（被 `TreeTableCore` 使用） |
| ~~`TreeTableLogic`~~ | `core/logic/TreeTableLogic.ts` | ⛔ **`@deprecated` 过渡别名**，改用 `TreeTableCore` |

**⚠️ 命名不一致（待决策）**：组件是 `YzhTable` / `YzhTreeTable`，内核却是 `SingleTableCore` / `TreeTableCore` —— **前缀不统一**。
→ 建议统一为 `YzhTableCore` / `YzhTreeTableCore`（见计划 D-11）。

**继承边界三条**：
1. **继承只用于「能力递增」** —— `TreeTableCore extends SingleTableCore` ✅；为复用两个方法而继承 ❌
2. **`override` 只用于「替换语义」**；**追加代码用钩子**（`onAfterInit` / `onFormRendered` / `defaultValues`）
3. **禁止三层以上继承链** —— 现状最深 2 层，保持

### R6-2 页面三层

| 文件 | 职责 | 约束 |
|---|---|---|
| `index.vue` | 只用组件 + 装配 | ⛔ 禁手写 `handleAdd` / `handleBatchDelete` / `handleRowAction` / `handleSubmit` |
| `logic.ts` | 继承内核，**只声明差异**（`controllerName` + 少量钩子） | 样板 `system/user/logic.ts` 仅 **19 行** |
| `api` | 走 `yzhApi` | ⛔ 禁 `axios`、禁 `.vue` 内 `fetch(` |

**三个唯一样板**（`docs/10-YZH架构/样板页面指南-V1.md`）：

| 场景 | 样板 |
|---|---|
| 单表 CRUD | `core/src/pages/system/user/` |
| 左树右表 | `core/src/pages/foundation/iso-standard/`（⚠️ **只抄 `logic.ts` 的 `dataLoader` 骨架**，`index.vue` 不抄） |
| 纯树节点 | `core/src/pages/system/role/`（`logic.ts` 仅 16 行、零覆写） |

**⛔ 不要参考 `src/old/**`。**

### R6-3 内核职责边界

| 归内核（非 UI） | 归组件（UI） |
|---|---|
| 端点、契约、生命周期、业务规则 | 列渲染 / formatter / slot |
| 动作分发（`registerHandler` / `dispatch`） | 列设置 / 排序 / 显隐 |
| 数据装载 / 分页参数 | 分页 / 加载态 / 空态 / 错误重试 |
| 表单数据组装 / 提交 | 多选 / 选择恢复 |
| 树加载 / 懒加载 / 索引 | 树搜索渲染 / 展开折叠 |

**★ 内核禁 import 任何 Vue 组件**（只允许 `vue` 响应式 API + `element-plus` 的 `ElMessage`/`ElMessageBox`）。
**★ 装配只在 `composables/*`**（`useSingleTable` / `useTreeTable` / `useCheckTree` / `useLinkTable`），页面只消费。

### R6-4 适配层是唯一"翻译官"

**规则**：`adapters/entityAdapters.ts` 是**唯一同时认识「后端 EntityConfig」与「前端组件契约」的模块**。
纯函数：`toTableColumns` / `toFormFields` / `toToolbarActions` / `toRowActions` / `toSearchFields`。

**为什么**：契约字段一变，只改一处。若让组件直接读 EntityConfig，则每次后端加字段都要改 N 个组件。

**⚠️ 当前缺口（8 项，见 V5 报告）**：`Format` / `Mask` / `Row` / `Col` / `RowSpan` / `ColSpan` / `GroupIndex` 多组 / `SumFlag` —— **契约有字段但适配层不消费 = "撒谎的契约"**。

### R6-5 禁止事项

- ⛔ `view-grid` / `VolBox` / `VolForm` / `VolProvider`
- ⛔ `axios`（守卫 R4）
- ⛔ `.vue` 内直接 `fetch(`（守卫 R5）
- ⛔ 页面内联 `<el-table>`（守卫 R6，须用 `YzhTable`）
- ⛔ `core` 内硬编码后端地址（守卫 R11，地址由宿主 `configureYzhApi` 注入）
- ⛔ `core` 新层 import `@/` 或 `@share/`（守卫 R10）

---

## 七、守卫索引（规则 → 机器检查）

> **权威实现**：`src/certplatform-web/scripts/guards.mjs`。本节只是索引。
> **挂载点**：`build` 首步 + `pre-commit`（**⛔ 严禁挂 `dev`**）。
> **零噪音契约**：通过只输出 1 行；债务仅 `--report` 可见。

### 7.1 已有守卫（10 条，实测 564 文件 / 0 违规）

| ID | 规则 | 扫描根 |
|---|---|---|
| **R1** | 原子组件零领域依赖（禁 `@share` / `@/api` / `logic` / `pinia` / `vue-router`） | `core/components` |
| **R2** | TreeNode 一律 PascalCase（禁 `node.code` 等小写读取） | 页面层 |
| **R3** / **R3b** | 统一 `ApiResponse`（页面层 / API 层禁 `.code === 200`） | 页面层 / API 层 |
| **R4** | 禁 `axios`，必须 `yzhApi` | 页面层 + API 层 + `core/components` |
| **R5** | 禁 `.vue` 内直接 `fetch(` | 页面层 |
| **R6** | 禁内联 `<el-table>`，必须 `YzhTable` | 页面层 |
| **R7** | 业务实体禁声明 `Enable` 列 | 后端 `Shared/Entities` |
| **R10** | core 新层禁宿主反向依赖（禁 `@/` 与 `@share/`） | `core/{pages,layouts,router,composables,api}` |
| **R11** | core 禁硬编码后端地址 | `core/src` 全量 |

**债务基线（实测）**：R6 豁免 **83 处 / 9 文件**；另有 1 个页面直连 `@/api`（`cert-auditor/src/pages/register/index.vue`）。

### 7.2 待补守卫（3 条，见计划 Stage B）

| ID | 规则 | 为什么需要 | 预估基线 |
|---|---|---|---|
| **R12** | 内核层（`core/{logic,utils,types,adapters}`）禁 `axios` / `fetch(` / `@/` / `@share/` | **内核自身当前无任何约束**（盲区 B1/B2/B3） | 需 `--report` 盘点 |
| **R13** | 禁包名引用（统一 `@yzh-core` / `@share`） | R2-3 | **2 处** |
| **R14** | core 内禁引用 `dist/`（源码唯一入口） | 防止绕过源码 | **0** |

**同时扩展 R4/R5 的 roots**：`core/{logic,utils,types,adapters}` + `cert-share/src/{components,composables,utils,logic}` + `cert-admin/src/{store,composables,router,layouts}`。

**★ 规则启用铁律：基线必须为 0。** 基线 ≠ 0 时存量走 `debt` 白名单、随修复逐条删除；**禁止为"先跑起来"调低阈值**（否则报错被存量淹没 → 规则立即失效）。

### 7.3 只能靠文档 + review 的规则（L5）

| 规则 | 为什么机器查不了 | 建议的人工检查点 |
|---|---|---|
| **R1-1 / R1-2** Id/Code 双关键字 | 语义级（`WHERE Id` 语法合法） | 后端 review 每个 Service 的定位/分流 |
| **R2-1** 三层同构 | 命名一致性 | 新增模块时对照后端目录 |
| **R4-3** JSON 只写 `Title`+`Columns` | 语义级 | 新增 EntityConfig 时 |
| **R5-1** 基类按职能选用 | 职能判断 | 新增 Controller 时对照行为契约三条 |
| **R5-2** ApiCode 影响 | 跨系统（DB 关联） | **改控制器名/动作名前必查** |
| **R6-1** 继承边界 | 设计判断 | 新增内核子类时 |
| **R6-5** 装配只在 composables | 设计判断 | 页面出现 `logic` 与 `ref` 混写时 |

---

## 八、规则的演进（怎么加一条新规则）

新增规则**必须**回答四个问题，否则不许加：

| # | 问题 | 不合格的答案 |
|---|---|---|
| ① | **能不能机器检查？** | "靠大家自觉" → 那就不是规则 |
| ② | **基线是不是 0？** | "先加上，存量慢慢改" → 报错会被淹没，规则立即失效 |
| ③ | **权威写在哪？** | "我口头说过" → 下次 AI 又不知道 |
| ④ | **违反症状是什么？** | "不规范" → 排查时无法反查 |

**落地顺序**：**① 写进本文 → ② 补守卫（若可机器检查）→ ③ 跑 `--report` 定基线 → ④ 存量进 `debt` → ⑤ 逐条摘除至 0 → ⑥ 守卫转强制**。

**文档治理配套**：
- 每份文档必须有 `status: living | draft | archived`
- **living 文档改代码前必须先改**（文档先行）
- **一个主题只有一份权威文档**（见 `20-前后端契约权威表-V1.md` 的权威表）
- **living 文档上限 5 份**，超过必须先归档一份

---

## 附：本文与既有文档的关系

| 主题 | 权威文档 |
|---|---|
| **规则总集（本文）** | **`18-YZH架构规则-V1.md`** |
| 前端分层与宿主接入 | `19-前端分层与宿主接入规范-V1.md` |
| 前后端契约字段级权威 | `20-前后端契约权威表-V1.md` |
| 架构理念与继承体系 | `01-架构总纲.md` |
| 后端基类 / EntityService | `02-后端架构.md` |
| 前端内核 / 组件 | `03-前端架构.md` |
| 数据契约（FilterRequest / EntityConfig / TreeConfig） | `04-数据契约.md` |
| 权限体系 | `05-权限体系.md` |
| 新建页面怎么做 | `样板页面指南-V1.md` |
| 常见错误 | `08-常见错误与修复.md` |
| 铁律与项目宪法 | `项目全局规则.md` §16.9 / §16.10 |
| AI 编码入口 | `AGENTS.md` |
