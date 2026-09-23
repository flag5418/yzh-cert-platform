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
> 本文件为权威源；知识库副本位于 `docs/30-项目规则/知识库/AGENTS.md`，两处须保持一致。

## ⚠️ AI 编码前必读 3 条（违反 = 返工）

> AI 绕过 YZH 架构的**头号原因不是"不听话"，而是文档给了错的地址**。
> 以下 3 条是纠偏后的正确地址，**编码前逐条执行**。

**① 前端新建/修改页面 → 先读 `docs/10-YZH架构/样板页面指南-V1.md`，照抄指定样板。**

- 单表 CRUD 唯一模板：`src/certplatform-web/cert/cert-admin/src/pages/system/user/`（67 行，零手写 CRUD）
- 左树右表唯一模板：`.../foundation/iso-standard/`（⚠️ **只抄 `logic.ts` 的 `dataLoader` 骨架**，`index.vue` 不抄）
- 纯树节点模板：`.../system/role/`（`logic.ts` 仅 16 行、零覆写）
- ⛔ **不要**参考 `system/menu`、`system/api`、`system/role-api`、`system/role-menu`、`system/role-user`（自建逻辑、待迁移），**不要**参考 `src/old/**`
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

**改完必跑**：`cd src/certplatform-web && node scripts/guards.mjs`（0 违规）→ 对应端 `npm run build`（含 `vue-tsc`）。

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
- **知识底座**：`docs/30-项目规则/知识库/README.md` — Vol 能力清单 / YZH 增量 / 边界约束 / 代码模板 / 踩坑记录 / 速查手册
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
   - `07-标准页面开发流程.md`（**已废弃，请按 `docs/10-YZH架构/样板页面指南-V1.md` 的唯一样板 `cert/cert-admin/src/pages/system/user/` 作为模板**）
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

## 业务菜单速览

> 仅供参考，以 `80-功能设计/README.md` 功能总览地图为准。

```
体系认证平台
├── 基础配置
│   ├── 标准目录管理
│   ├── 系统参数配置（含阿里云标签页）
│   └── AI 费用监控
├── 审核规则库
│   ├── NC检查规则
│   └── 报告章节定义
├── 文档提取规则
└── Prompt 模板管理
```

## 与知识库的关系

- 本文件（根目录）是 **AI 工具自动加载的入口**：负责"启动时把 AI 指向正确的位置与约束"。
- `docs/10-YZH架构/` 是 **YZH 架构唯一权威入口**：负责"架构核心理念 + 编码规范"。
- `docs/30-项目规则/知识库/` 是 **知识底座**：负责"开发中按需查阅的接口签名、踩坑经验、边界约束"。
- 三处通过 `docs/00-工程体系/README.md` 登记关联；修改本文件后必须同步知识库副本。
*（内容由AI生成，仅供参考）*
