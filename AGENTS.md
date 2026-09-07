---
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
> 本文件为权威源；知识库副本位于 `docs/60-AI工程设计/YZH-知识库/AGENTS.md`，两处须保持一致。

## ⚠️ 核心目的（最高优先级）

> **当前项目核心目的：完善新架构，用新架构重新构造原项目的接口和业务功能。**

| 层级 | 路径 | 状态 | 说明 |
|------|------|------|------|
| **新后端** | `src/certplatform-api/` | ✅ 唯一开发目标 | `CertPlatform.Shared/` + `Admin/` + `Auditor/` + `Enterprise/`，启动入口 `YZH.Core.Web` |
| **新前端** | `src/certplatform-web/` | ✅ 唯一开发目标 | `yzh.vue.core/` + `share/` + `admin/` + `auditor/` |
| **旧后端** | `src/server/Vue.NetCore/` | ⛔ **禁止修改** | Vol 框架，仅作参考 |
| **旧前端** | `src/admin/`、`src/auditor/` | ⛔ **禁止修改** | Vol 自带前端，仅作参考 |

**强制约束**：
- ❌ 禁止修改 `src/server/`、`src/admin/`、`src/auditor/` 下任何文件
- ❌ 禁止在新架构中调用旧架构的控制器/服务
- ✅ 新架构开发可参考旧架构的业务逻辑
- ✅ 所有接口/Bug 修复在新架构中实现

## 快速指针（编码前必读链路）

- **项目宪法**：`项目全局规则.md` — 项目概述/技术栈锁定/文档目录结构/AI 检索协议/端口规划/快速开始/禁止事项
- **文档导航**：`docs/00-工程体系/README.md` — 全项目文档索引（00/20/50/60/80/90 + 历史文档）
- **业务域设计**：按 `docs/00-工程体系/README.md` 导航进入对应目录（架构/全局 → 20-架构决策；功能细节 → 80-功能设计；AI 工程 → 60-AI工程设计）
- **知识底座**：`docs/60-AI工程设计/YZH-知识库/README.md` — Vol 能力清单 / YZH 增量 / 边界约束 / 代码模板 / 踩坑记录 / 速查手册
- **Vol 框架指南**：`docs/60-AI工程设计/vol-skill.md`（**已弃用，仅供历史参考**）
- **前端架构总纲（V4）**：`docs/00-工程体系/前端架构设计-V1.md`（原子化组件 + CrudPageLogic 基类 + 后端驱动 UI）
- **树形结构设计（V2）**：`docs/00-工程体系/树形结构设计-V2.md`（统一 TreeNode 虚拟结构 + treeUtils/treeOps 工具集 + TreeTableLogic 基类 + ActionPipeline + 后端 TreeControllerBase）
- **TreeTable 架构设计**：`docs/20-架构决策/TreeTableController-架构设计-V1.md`（左树右表架构、前后端协议、同构/异构树设计、Vol TreeTable 适配）
- **架构迁移指南**：`docs/60-工程设计/YZH-知识库/10-架构迁移指南-V1.md`（从旧 vol.web 迁移到 certplatform-web 的步骤）
- **编码规范**：`docs/60-AI工程设计/vol-csharp-coding-standards.md`（C#）、`docs/60-AI工程设计/vue-ts-coding-standards.md`（Vue3+TS）
- **脚本规范**：`scripts/README.md`（backend/db/frontend/storage/generate/tools 子目录）
- **Skill 清单**：`docs/60-AI工程设计/Skill清单-V1.md` — 全部 Skill 的编码/输入输出/绑定模式/实现类/编写规范

## 项目速览

- **项目**：映智汇认证审核管理系统（yzh-cert-platform），ISO 体系认证全流程（建档→任务分派→预审→复核→报告→NC）
- **核心目的**：完善 YZH.Core 新架构，重构原 Vol 框架的接口和业务功能
- **技术栈（新架构）**：.NET 8 + YZH.Core（后端）/ Vue 3 + TypeScript + Vite + Element Plus / MySQL 8.0 / Redis 7 / MinIO / Docker Compose
- **技术栈（旧架构，仅参考）**：.NET 8 + Vol（后端）/ Vue 3 + Element Plus（保留不修改）
- **端口**：后端 9992 / 后台管理 9990 / 审核员前端 9991 / MySQL 3307 / Redis 6380 / MinIO 9000+9001
- **开发模式**：独立开发，多 AI 协作机制不适用（项目全局规则 §十三）
- **前端架构（V4 2026-09 起）**：彻底抛弃 view-grid/VolProvider/VolBox/VolForm，全部使用自研 `YzhTable` + `YzhForm` + `YzhApiClient` + 手写 API
- **新前端结构（2026-09-05 起）**：`src/certplatform-web/` 目录，包含 `yzh.vue.core/`（核心组件库）、`share/`（业务共享层）、`admin/`（管理员端，端口 9990）、`auditor/`（审核员端，端口 9991）
- **新后端结构（2026-09 起）**：`src/certplatform-api/` 目录，包含 `CertPlatform.Shared/`（共享层）、`Admin/`、`Auditor/`、`Enterprise/`，启动入口 `YZH.Core.Web`

## 编码强制约定

1. **文档即宪法**：生成任何代码前，先查阅 `docs/` 中对应业务域的设计文档（见快速指针链路）；发现文档与实现不一致 → 更新文档，而非迁就代码。
2. **知识库前置**：编码前查 `YZH-知识库/` 以下条目，避免重复踩坑：
   - `10-架构迁移指南-V1.md`（**V4 新架构首选**）
   - `08-Vol框架实战速查手册.md` / `09-常见错误对照表.md`（**仅历史参考，新页面不再使用**）
   - `03-边界与约束.md` / `06-YZH与Vol边界定义.md`（不能碰的、不能改的）
   - `01-Vol能力清单.md` / `02-YZH增量清单.md`（能力索引）
   - `04-代码模板/`、`05-踩坑记录/`（直接引用/查重）
   - `07-标准页面开发流程.md`（**已废弃，请按 V4 试点页面 `src/pages/system/user/index.vue` 作为模板**）
3. **后端**：只改 `VOL.Sys/Services/System/Partial/` 下的 Partial Service，禁改 .jsx；使用 Vol 框架 ServiceBase 钩子优先；YZH 增量能力（YZHBaseEntity / 特性体系）按 `02-YZH增量清单.md` 使用。
4. **前端（新）**：所有新页面必须使用 V4 自研组件：
   - 核心组件库：`@yzh-core/components/*`（yzh.vue.core）
   - 业务共享层：`@share/*`（share）
   - 管理员端：`src/certplatform-web/admin/`（端口 9990，Element Plus）
   - 审核员端：`src/certplatform-web/auditor/`（端口 9991，Naive UI）
   - API 客户端：`yzhApi`（来自 yzh.vue.core）
   - **禁止** 新页面使用 view-grid、VolBox、VolForm、VolProvider、extension 自动生成的 .jsx
   - **注意**：旧 vol.web 保留历史版本，不删除，新代码写入 certplatform-web/
5. **数据库**：MySQL 8.0 @ 3307（yzh-mysql）/ Redis @ 6380（yzh-redis）；SQL 脚本遵循 `项目全局规则.md` §十一（脚本放 scripts/db/，禁止散落）。
6. **命名规范**：文档命名强制 `-V1` 后缀（见 `00-工程体系/文档生命周期管理规范-V1.md`）；脚本按 scripts/ 子目录归类。
7. **启停规范**：后端启停一律走 `scripts/` 脚本（backend/ 子目录），禁止手动 `kill` / 裸 `dotnet run &`（见项目全局规则 §十五）。
8. **路径格式**：所有文件路径使用 macOS 绝对路径格式。
9. **沟通风格**：零表情、极简、中文回复；方案用表格对比 + 结论。

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
- `docs/60-AI工程设计/YZH-知识库/` 是 **知识底座**：负责"开发中按需查阅的接口签名、踩坑经验、边界约束"。
- 两处通过 `YZH-知识库/README.md` 登记关联；修改本文件后必须同步知识库副本。
*（内容由AI生成，仅供参考）*
