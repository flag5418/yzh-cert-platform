---
status: living
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_dc9ece8c992711f1a98a525400f8a581
    ReservedCode1: 6cXy5QC9kBwQ1Qa+9ECvLPfdiIX8UwkfKyqJRKCnru6xKPdmfw8PffDC9CnpxAXPSbNo0KQCFs7RTgrkbRIKJiksv3B+djqisOeezj5I3KRPULmc+fW21aIOKU1cobejxVclFy75NribYDx7nqbMwBK2K1DsTDCGAwuzyZi/Omy9daiSjCnkeJ7O6U8=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_dc9ece8c992711f1a98a525400f8a581
    ReservedCode2: 6cXy5QC9kBwQ1Qa+9ECvLPfdiIX8UwkfKyqJRKCnru6xKPdmfw8PffDC9CnpxAXPSbNo0KQCFs7RTgrkbRIKJiksv3B+djqisOeezj5I3KRPULmc+fW21aIOKU1cobejxVclFy75NribYDx7nqbMwBK2K1DsTDCGAwuzyZi/Omy9daiSjCnkeJ7O6U8=
---

# AGENTS.md — AI 编码助手强制入口

> 本文件是 AI 编程助手（Cursor / Claude Code / Copilot / Aider 等）的自动加载入口。
> **编码任务（生成/修改任何代码）必须启用本文件**，启用要求见 `项目全局规则.md` §8.0。
> **本文件是全项目唯一权威源**（2026-09-24 起）。原先 `docs/30-项目规则/知识库/AGENTS.md` 的副本已删除，
> 其独有内容已迁至知识库正确位置（术语表见 `08-术语表/`，踩坑记录见 `05-踩坑记录/`）。⛔ 不得再建副本。

## ⚠️ AI 编码前必读 3 条（违反 = 返工）

> AI 绕过 YZH 架构的**头号原因不是"不听话"，而是文档给了错的地址**。
> 以下 3 条是纠偏后的正确地址，**编码前逐条执行**。

**① 前端新建/修改页面 → 先读 `docs/10-YZH架构/样板页面指南-V1.md`，照抄指定样板。**

- 单表 CRUD 唯一模板：`src/certplatform-web/yzh.vue.core/src/pages/system/user/`（67 行，零手写 CRUD）
  - ✅ **P8 已收口（2026-09-24）**：system 11 页面 + 登录/布局/首页应用壳已全部位于 `src/certplatform-web/yzh.vue.core/src/{pages,layouts,router,api/system}`（计划：`docs/50-任务/迁移计划/yzh.vue.core系统底座化迁移计划-V1.md`）。宿主（cert-admin 等）只组装路由，**不要再往 cert-admin 抄 system 页面**。
- 左树右表唯一模板：`.../foundation/iso-standard/`（⚠️ **只抄 `logic.ts` 的 `dataLoader` 骨架**，`index.vue` 不抄）
- 纯树节点模板：`src/certplatform-web/yzh.vue.core/src/pages/system/role/`（`logic.ts` 仅 16 行、零覆写）
- ⚠️ **`pages/system/user/` 与 `pages/system/role/` 无路由可达是正常的**（2026-09-24 用户裁决：功能已被「机构-人员管理」`system/organization`、「角色-人员管理」`system/role-user` 取代，两条路由已删除）。**它们是照抄样板，目录必须保留** —— 照抄 = 复制文件，**不需要打开页面**。⛔ 不要因为「样板页在、路由不在」就把路由加回来（守卫 **R12** 会拦）。
- ⛔ **不要**参考 `src/old/**`（历史项目，禁止参考、禁止修改）
- ⛔ 禁止 `view-grid` / `VolBox` / `VolForm` / `VolProvider`；禁止 `axios`；`.vue` 内禁止直接 `fetch(`
- ⛔ 禁止手写 `handleAdd` / `handleBatchDelete` / `handleRowAction` / `handleSubmit` —— 由 `useSingleTable` 内核派发

**② 后端新建/修改控制器 → 必须继承基类，禁止裸 `ControllerBase`。**

- 单表：`YzhControllerBase<{实体}>`；左树右表：`TreeTableControllerBase<T, V>`
- 现状：10 个裸 `ControllerBase` 承载 90/96 端点（93.75%）—— **它们是坏榜样，不要学**
- 改控制器名/动作名 → `ApiCode` 变化 → 角色-接口关联**静默断裂**（须重跑 ApiSync 并重关联）

**③ 字段名 = PascalCase 逐字一致（DB列名 = C#属性名 = TS字段名）。**

- 前端读 `row.RuleName`、`node.Code`、`formData.StandardCode`
- 写成 `row.ruleName` / `node.code` → **渲染成空行且无任何报错**（最难查的一类 bug）
- 例外（须注释标注「已登记例外」）：`ApiResponse` 信封 camelCase（E1）、裸 JSON `{img,uuid}`（E6）、`{code,data,message}` **无 `success`**（E7，须用 `res.code === 200` 且**禁用 `res.success`**）
- **★ 铁律九：`Enable` 零容忍。启用/禁用唯一字段 = `IsValid`（int，0/1），由 `IIsValid` 接口统一提供**：**数据库中不允许任何表存在 `Enable`/`enable` 列**；新实体/新列**禁止**声明 `Enable` / `EnableField`；业务开关用 `IsActive`；软删除用 `IsDeleted`。
  - ⛔ **原「例外：`sys_api.Enable`」已作废**（2026-09-24 用户明确：「所有的表都统一用接口的字段」）→ `sys_api` 需迁移为 `IsValid`（改 `SysApi` 实体 + `ApiSyncService` + `RoleApiController.cs:135` + 前端 `pages/system/api/`）
  - ⛔ **`EnableField` 配置项的「值」必须是 `'IsValid'`**（名字保留，值必须纠正）
  - **违反症状**：① 表内 `enable` 与 `IsValid` **并存** → **列表显示的行 ≠ 能操作的行**，两边都不报错 ② 实体注释写 `// DB: Name` 而 DB 实际是 `name`（代码自证清白，DB 不是）
  - 守卫：`guards.mjs` **R7**（前端）+ `scripts/db/fix/fix-column-naming-2026-09-24.sql` 文末 **DB 验证 SQL**
  - ⚠️ **验证 SQL 必须用 `CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin`** —— `information_schema.COLUMN_NAME` 排序规则大小写不敏感，直接 `NOT REGEXP '^[A-Z]'` 会**永远返回 0 行**（假阴性，会让人误以为已清零）
  - 详见 `docs/50-任务/分析报告/命名规范违规清单与消灭方案-V1.md`

**④ 双关键字准则 A（Id/Code · 2026-09-24 · 违反 = 返工）**

- `Id` **永不**进 WHERE / 关联 / `Id>0`·`Id==0` 的 add·update 分流 / 存在性判定；`OrderBy(Id)` 与 `new` 实体 Id=`0`/`null`（未落库信号）**允许**。
- 定位 / 删除 / 更新 / 前后端传参 **只用 `Code`**；中间表：`Sys_RoleUser`→`RoleCode+UserCode`，`Sys_RoleMenu`→`RoleCode+MenuCode`。
- 新增 vs 更新 **唯一合法**：`GetByCode(entity.Code)` 有→更新、无→新增；Code 空却要更新 → `更新失败：缺少业务键 Code`（禁止回退 Id）。
- 前端：`data.Code ? update : add`，`deleteXxx(row.Code)`。权威：`docs/10-YZH架构/01-架构总纲.md` §2.1 · E201。

**⑤ 样式唯一写法 = 令牌 + 兜底（2026-10-03 · 违反 = 返工）**

- 前端 `<style>` 里**颜色 / 字号 / 间距一律 `var(--yzh-*, 兜底值)`**，⛔ 禁止裸 hex、禁止 `font-size/padding/margin` 写字面量 px。
- 映射表唯一权威：`docs/10-YZH架构/25-样式规范与硬编码治理-V1.md` §四（如 `#409eff`→`--yzh-color-primary`、`13px`→`--yzh-font-size-sm`、`16px`→`--yzh-space-4`）
- **覆盖 Element Plus 只能 `:deep()` + 局部 `--el-*`**，`!important` 仅限 `:deep()` 内（stylelint 已放行）
- 豁免（允许保留 hex）：LogicFlow 画布 / ECharts / `tokens.css`·`main.css` 令牌定义 / 登录页品牌渐变
- 守卫：`guards.mjs` **R18**（裸 hex + 裸 px，基线 `scripts/style-baseline.json` **只准减不准增**）+ **R19**（stylelint 结构性错误，基线 0）

**改完必跑**：`cd src/certplatform-web && node scripts/guards.mjs`（0 违规，**含 R12 路由↔菜单一致性 + R18/R19 样式**）→ 对应端 `npm run build`（含 `vue-tsc`）。

> **动了路由或菜单 → 额外跑一次**：`./scripts/db/verify/sync_menu_urls.sh` 刷新菜单快照，否则 R12 基于过期数据（快照缺失时 R12 会跳过并告警）。
> R12 双向判据：菜单 `Url` 无对应路由 = **报错**（点击必白屏）；路由无对应菜单 = **报错**（孤儿路由，须补菜单 / 删路由 / 登记 `ORPHAN_ALLOW`）。

## ⚠️ 核心目的（最高优先级）

> **当前项目核心目的：完善新架构，用新架构重新构造原项目的接口和业务功能。**

| 层级 | 路径 | 状态 | 说明 |
|------|------|------|------|
| **新后端** | `src/certplatform-api/` | ✅ 唯一开发目标 | `CertPlatform.Shared/` + `Admin/` + `Auditor/` + `Enterprise/`，启动入口 `src/yzh-core/YZH.Core.Web` |
| **新前端** | `src/certplatform-web/` | ✅ 唯一开发目标 | `yzh.vue.core/` + `cert/cert-share/` + `cert/cert-admin/` + `cert/cert-auditor/` + `cert/cert-enterprise/` |
| **历史后端代码** | `src/old/server/Vue.NetCore/vol.api/` | ⛔ **禁止修改** | 旧 Vol 框架后端，仅作业务逻辑/接口设计/数据库结构参考 |
| **历史前端代码** | `src/old/server/Vue.NetCore/vol.web/` | ⛔ **禁止修改** | 旧 Vol 框架前端，仅作页面结构/UI 参考 |
| **旧审核员前端** | `src/old/auditor/` | ⛔ **禁止修改** | 未使用的旧审核员前端代码，已归档 |

**强制约束**：
- ❌ 禁止修改 `src/old/` 下任何文件
- ❌ 禁止在新架构中调用旧架构的控制器/服务
- ✅ 新架构开发可参考旧架构的业务逻辑（使用"历史后端代码"/"历史前端代码"术语检索）
- ✅ 所有接口/Bug 修复在新架构中实现

## 快速指针（编码前必读链路）

- **项目宪法**：`项目全局规则.md` — 项目概述/技术栈锁定/文档目录结构/AI 检索协议/端口规划/快速开始/禁止事项
- **项目结构与启动指南**：`docs/00-工程体系/项目结构与启动指南-V1.md` — 新旧架构路径/启动方式/端口规划/AI 检索协议
- **文档导航**：`docs/00-工程体系/README.md` — 全项目文档索引（00/10/20/50/60/80/90 + 历史文档）
- **★ YZH 架构唯一入口**：`docs/10-YZH架构/README.md`（← **V1 强制规范**：编码前必读，包含架构总纲/后端基类/前端基类/数据契约/权限体系/代码结构/开发流程/常见错误）
- **★ 样板页面指南**：`docs/10-YZH架构/样板页面指南-V1.md`（← **新建页面唯一入口**：指定 3 个唯一样板 + "照抄时必须改的 5 处" + 明确"不要参考"清单）
- **YZH 架构分章速查**：
  - 核心理念 + 继承体系 → `docs/10-YZH架构/01-架构总纲.md`
  - 后端基类 + EntityService → `docs/10-YZH架构/02-后端架构.md`
  - 前端基类 + 组件 → `docs/10-YZH架构/03-前端架构.md`
  - 数据契约（FilterRequest/EntityConfig/TreeConfig）→ `docs/10-YZH架构/04-数据契约.md`
  - 权限体系（SSO/角色/三层权限）→ `docs/10-YZH架构/05-权限体系.md`
  - 代码结构（三层同构/命名/路由）→ `docs/10-YZH架构/06-代码结构规范.md`
  - 开发流程（新增页面步骤/钩子速查/常见场景）→ `docs/10-YZH架构/07-开发流程.md`
  - 常见错误 → `docs/10-YZH架构/08-常见错误与修复.md`
  - **接口返回语义**（`success` 唯一判据 / `err` 错误出口 / 业务失败 HTTP 200 / 前端 L1–L4 读取顺序）→ `docs/10-YZH架构/22-接口返回规范-V1.md`
  - **信封统一改造计划**（阶段 F0/P0–P3 + 决策 D1–D6 + 验收矩阵；**改返回相关代码先读它**）→ `docs/10-YZH架构/23-前后端信封统一改造计划-V1.md`
  - **契约字段级对照**（逐字段大小写 + 例外 E1–E7）→ `docs/10-YZH架构/20-前后端契约权威表-V1.md`
- **知识底座**：`docs/30-项目规则/知识库/README.md` — Vol 能力清单 / YZH 增量 / 边界约束 / 代码模板 / 踩坑记录 / 速查手册
- **术语表**：`docs/30-项目规则/知识库/08-术语表/术语表-V1.md` — 认证行业 + 审核过程 + 系统功能 + 技术术语
- **Skill 清单**：`docs/30-项目规则/Skill清单-V1.md` — 全部 Skill 的编码/输入输出/绑定模式/实现类/编写规范
- **编码规范**：C# 编码规范与 Vue/TS 编码规范已归档至 `docs/90-归档/历史项目/Vol框架/归档-2026-09-09-Vol框架历史文档/`（Vol 专属，新架构请参考 `docs/10-YZH架构/02-后端架构.md` 与 `docs/10-YZH架构/03-前端架构.md`）
- **脚本规范**：`scripts/README.md`（backend/db/frontend/storage/generate/tools 子目录）

## 项目速览

- **项目**：映智汇认证审核管理系统（yzh-cert-platform），ISO 体系认证全流程（建档→任务分派→预审→复核→报告→NC）
- **核心目的**：完善 YZH.Core 新架构，重构原 Vol 框架的接口和业务功能
- **技术栈（新架构）**：.NET 8 + YZH.Core（本地源码 `src/yzh-core/`，4 个项目 Stand/DataBase/Api/Web）+ certplatform-api（`src/certplatform-api/`，3 个角色子模块 Admin/Auditor/Enterprise）/ Vue 3 + TypeScript + Vite + Element Plus / MySQL 8.0 / Redis 7 / MinIO / Docker Compose
- **技术栈（旧架构，仅参考）**：.NET 8 + Vol（后端）/ Vue 3 + Element Plus（保留不修改）
- **端口**：后端 9992 / 后台管理 9990 / 审核员前端 9991 / MySQL 3307 / Redis 6380 / MinIO 9000+9001
- **开发模式**：独立开发，多 AI 协作机制不适用（项目全局规则 §十三）
- **前端架构（V4 2026-09 起）**：彻底抛弃 view-grid/VolProvider/VolBox/VolForm，全部使用自研 `YzhTable` + `YzhForm` + `YzhApiClient` + 手写 API
- **新前端结构（2026-09-05 起）**：`src/certplatform-web/` 目录，包含 `yzh.vue.core/`（核心组件库）、`cert/cert-share/`（业务共享层）、`cert/cert-admin/`（管理员端，端口 9990）、`cert/cert-auditor/`（审核员端，端口 9991）
- **新后端结构（2026-09 起）**：`src/certplatform-api/` 目录，包含 `CertPlatform.Shared/`（共享层）、`Admin/`、`Auditor/`、`Enterprise/`，启动入口 `src/yzh-core/YZH.Core.Web`（Program.cs 调用 `UseYzhCore`）
- **YZH.Core 本地源码**：`src/yzh-core/` 目录，包含 `YZH.Core.Stand/`、`YZH.Core.DataBase/`、`YZH.Core.Api/`、`YZH.Core.Web/` 四个项目，是架构层，不直接对外提供服务，通过项目引用被 certplatform-api 使用

## 编码强制约定

1. **文档即宪法**：生成任何代码前，先查阅 `docs/` 中对应业务域的设计文档（见快速指针链路）；发现文档与实现不一致 → 更新文档，而非迁就代码。
2. **知识库前置**：编码前查 `YZH-知识库/` 以下条目，避免重复踩坑：
   - `10-架构迁移指南-V1.md`（**V4 新架构首选**）
   - `08-Vol框架实战速查手册.md` / `09-常见错误对照表.md`（**仅历史参考，新页面不再使用**）
   - `03-边界与约束.md` / `06-YZH与Vol边界定义.md`（不能碰的、不能改的）
   - `01-Vol能力清单.md` / `02-YZH增量清单.md`（能力索引）
   - `04-代码模板/`、`05-踩坑记录/`（直接引用/查重）
   - `07-标准页面开发流程.md`（**已废弃，请按 `docs/10-YZH架构/样板页面指南-V1.md` 的唯一样板 `yzh.vue.core/src/pages/system/user/` 作为模板**）
3. **后端**：业务代码写 `src/certplatform-api/`；业务实体继承 `BaseEntity`，Controller **按职能选用** `YzhControllerBase<V>` / `TreeTableControllerBase<T,V>`（**非强制**——职能特殊的控制器直接继承 `ControllerBase` 是允许的）；钩子通过覆盖 virtual 方法实现。
   - ★ **`src/yzh-core/`（框架层）可以且鼓励合理改造** —— YZH 架构正在持续完善中，**不存在"禁止修改"这条规则**。改造须遵守第 11 条「框架层改造准入」。
   - ⛔ **真正禁止修改的是 `src/old/`**（历史 Vol 项目，已冻结，仅作参考）。
4. **前端（新）**：所有新页面必须使用 V4 自研组件：
   - 核心组件库：`@yzh-core/components/*`（yzh.vue.core）
   - 业务共享层：`@share/*`（share）
   - 管理员端：`src/certplatform-web/cert/cert-admin/`（端口 9990，Element Plus）
   - 审核员端：`src/certplatform-web/cert/cert-auditor/`（端口 9991，**Element Plus**）
   - API 客户端：`yzhApi`（来自 yzh.vue.core）
   - **禁止** 新页面使用 view-grid、VolBox、VolForm、VolProvider、extension 自动生成的 .jsx
   - **注意**：旧 vol.web 保留历史版本（`src/old/server/Vue.NetCore/vol.web/`），不删除，新代码写入 certplatform-web/
5. **数据库**：MySQL 8.0 @ 3307（yzh-mysql）/ Redis @ 6380（yzh-redis）；SQL 脚本遵循 `项目全局规则.md` §十一（脚本放 scripts/db/，禁止散落）。
   ★ **字符集/排序规则全库统一 `utf8mb4` + `utf8mb4_general_ci`**（`项目全局规则.md` §16.10 铁律八）：
   建表**必须显式**写 `COLLATE=utf8mb4_general_ci`；含 `CREATE VIEW` 的脚本**必须**开头写 `SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;`。
   ⛔ 只写 `DEFAULT CHARSET=utf8mb4`（不带 COLLATE）会落到 `utf8mb4_0900_ai_ci`（**不是**库默认值）；⛔ 禁用 `utf8mb3`。
   违反后果：跨表「列 vs 列」关联报 `ERROR 1267 Illegal mix of collations`，且**单表测试全绿**、只在关联时暴露。
6. **命名规范**：文档命名强制 `-V1` 后缀（见 `00-工程体系/文档生命周期管理规范-V1.md`）；脚本按 scripts/ 子目录归类。
7. **启停规范**：后端启停一律走 `scripts/` 脚本（backend/ 子目录），禁止手动 `kill` / 裸 `dotnet run &`（见项目全局规则 §十五）。
8. **路径格式**：所有文件路径使用 macOS 绝对路径格式。
9. **沟通风格**：零表情、极简、中文回复；方案用表格对比 + 结论。
10. **代码结构三层同构**：后端 Controller 文件、前端 API 文件、前端 Pages 文件夹必须同名同路径（详情见 `docs/30-项目规则/前后端代码结构统一规则-V1.md`）；新增模块必须三层同步创建；禁止在模块根目录扁平化放置文件；迁移完成后按该文档 §六 检查清单逐项验证。
11. **框架层改造准入**（`src/yzh-core/`，2026-09-23 明确）：框架层**允许且鼓励合理改造**（架构正在完善中），但必须同时满足以下 5 条——
    - ① **有明确理由**：消除真实缺陷 / 补齐真实缺口 / 提供扩展点。**禁止**"为了统一风格"而改。
    - ② **不破坏既有契约**：列名铁律（PascalCase 三处一致）、`ApiResponse` 信封、已登记例外 E1–E7 一律不动。
    - ③ **优先走扩展点**：能通过 `virtual` 钩子 / 特性 / 配置 / 新增类解决的，**不改核心逻辑**。
    - ④ **改动记档**：在 `.workbuddy-ai/memory/REFERENCE.md` 的「框架层已改动清单」登记（文件 / 改动 / 理由）。
    - ⑤ **改完回归验证**：后端编译 0 错误 + 服务能起来 + 前端 `node scripts/guards.mjs` 通过。
12. **前端改动必须跑测试**（2026-10-03 建立）：
    - 跑法：`cd src/certplatform-web/cert/cert-admin && ./node_modules/.bin/vitest run`
      ⚠️ vitest **只装在 `cert/cert-admin/node_modules`**（`certplatform-web/node_modules` 里没有）；`vue-tsc` 反过来在 `certplatform-web/node_modules/.bin`
    - 栈：vitest 1.6 + happy-dom + @vue/test-utils（`cert-admin/vitest.config.ts` 已配 `@` / `@share` / `@yzh-core` alias）
    - 范式文件：`cert-admin/src/pages/workflow/prompt-template/index.test.ts`（8 用例，覆盖 AI 生成自动落库 / 切换守卫 / 草稿恢复 / 状态提示 / 树高亮回拨）
    - ★ **stub 铁律**：带**作用域插槽**的组件（`el-tree` 等）stub 必须 `v-for` 渲染 `<slot :data="n" />` —— 只写 `<slot />` 会让 `#default="{ data }"` 拿到 undefined，**全部用例倒在渲染**；组件用到的指令（`v-loading`）须在 `global.directives` 补 stub
    - ⛔ 本机**无 Chromium**（`agent-browser` 未装 / playwright 缓存为空）⇒ 页面**逻辑**验证走组件测试；「页面**能否编译**」用 `curl --noproxy '*' http://127.0.0.1:9990/src/<路径>.vue`（返回 500 = Vite 编译失败 = 白屏）

## 业务菜单速览

> ★ **事实源 = DB 表 `Sys_Menu`**（以 `admin` Tag 为后台专家端，`auditor` Tag 为专家端）。
> 本节仅为速查，**改菜单后必须同步此处 + `docs/20-体系认证/03-详细设计/05-企业资料规范化/26-核心菜单功能设计（待审批）-V2.md` §五**。
> 事实源快照：`scripts/db/verify/menu-urls.tsv`（`./scripts/db/verify/sync_menu_urls.sh` 生成）。

**命名法（消除「定义 / 设计」歧义，铁律）**：
不带「设计」= 左树右表**清单**（可增删改查）｜带「设计」= **工作流设计器**（NC 与报告是两套完全不同的配置，⛔ 不得合并成同一入口）。

**后台（`Tag=admin`，端口 9990）—— 2 级侧栏（2026-10-02 重组）**

```
平台管理 MENU_00001
  机构-人员管理 · 角色-人员管理 · 角色-菜单管理 · 角色-接口管理
  菜单管理 · 接口管理 · 数据字典 · 日志管理 · 系统参数配置
业务管理 MENU_00002
├── 基础资料 MENU_00301
│   认证机构管理 · 标准管理 · 阶段管理 · 机构-标准关联 · 机构-阶段关联
│   标准资料清单 · NC 检查项 · 报告章节
├── 规则定义 MENU_00302
│   NC 规则设计 · 报告内容设计 · 文档提取规则
│   （预留：空白文档填写规则 —— 页面未开发，暂不建菜单）
└── 系统管理 MENU_00303
    标准核心字段 · Prompt 模板 · 技能管理 · AI 费用分析 · 队列监控
```

**专家端（`Tag=auditor`，端口 9991）—— 顶级「专家系统」MENU_AUD_00**

```
专家系统
  系统一览 · 企业管理 · 阶段标准关联 · 企业全局参数定义
  任务中心 · NC 检查结果 · 报告结论 · 资料库 · 组织与成员 · 系统设置
```

> ⚠️ **侧栏只渲染 2 级**：`yzh.vue.core/src/layouts/YzhAppLayout.vue:23-36` 是硬编码「一级 `el-sub-menu` + 二级 `el-menu-item`」，**无递归**。DB 与后端 `MenuController.BuildTree` 支持 N 级，但**第 3 级及以下不会显示**。新增第 3 级分组前必须先改该布局为递归组件（走框架层改造准入）。

## 与知识库的关系

- 本文件（根目录）是 **AI 工具自动加载的入口**：负责"启动时把 AI 指向正确的位置与约束"。
- `docs/10-YZH架构/` 是 **YZH 架构唯一权威入口**：负责"架构核心理念 + 编码规范"。
- `docs/30-项目规则/知识库/` 是 **知识底座**：负责"开发中按需查阅的接口签名、踩坑经验、边界约束"。
- 三处通过 `docs/00-工程体系/README.md` 登记关联；修改本文件后必须同步知识库副本。
*（内容由AI生成，仅供参考）*
