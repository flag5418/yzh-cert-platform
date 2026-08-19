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

## 快速指针（编码前必读链路）

- **项目宪法**：`项目全局规则.md` — 项目概述/技术栈锁定/文档目录结构/AI 检索协议/端口规划/快速开始/禁止事项
- **文档导航**：`docs/00-工程体系/README.md` — 全项目文档索引（00/20/50/60/80/90 + 历史文档）
- **业务域设计**：按 `docs/00-工程体系/README.md` 导航进入对应目录（架构/全局 → 20-架构决策；功能细节 → 80-功能设计；AI 工程 → 60-AI工程设计）
- **知识底座**：`docs/60-AI工程设计/YZH-知识库/README.md` — Vol 能力清单 / YZH 增量 / 边界约束 / 代码模板 / 踩坑记录 / 速查手册
- **Vol 框架指南**：`docs/60-AI工程设计/vol-skill.md`
- **编码规范**：`docs/60-AI工程设计/vol-csharp-coding-standards.md`（C#）、`docs/60-AI工程设计/vue-ts-coding-standards.md`（Vue3+TS）
- **脚本规范**：`scripts/README.md`（backend/db/frontend/storage/generate/tools 子目录）
- **Skill 清单**：`docs/60-AI工程设计/Skill清单-V1.md` — 全部 Skill 的编码/输入输出/绑定模式/实现类/编写规范

## 项目速览

- **项目**：映智汇认证审核管理系统（yzh-cert-platform），ISO 体系认证全流程（建档→任务分派→预审→复核→报告→NC）
- **技术栈**：.NET 8 + Vol（后端）/ Vue 3 + TypeScript + Element Plus + Vite（admin 与 auditor 双端）/ MySQL 8.0 / Redis 7 / MinIO / Docker Compose（OrbStack）
- **端口**：后端 9992 / 后台管理 9990 / 审核员前端 9991 / MySQL 3307 / Redis 6380 / MinIO 9000+9001
- **开发模式**：独立开发，多 AI 协作机制不适用（项目全局规则 §十三）

## 编码强制约定

1. **文档即宪法**：生成任何代码前，先查阅 `docs/` 中对应业务域的设计文档（见快速指针链路）；发现文档与实现不一致 → 更新文档，而非迁就代码。
2. **知识库前置**：编码前查 `YZH-知识库/` 以下条目，避免重复踩坑：
   - `08-Vol框架实战速查手册.md` / `09-常见错误对照表.md`（★★★ 必读）
   - `03-边界与约束.md` / `06-YZH与Vol边界定义.md`（不能碰的、不能改的）
   - `01-Vol能力清单.md` / `02-YZH增量清单.md`（能力索引）
   - `04-代码模板/`、`05-踩坑记录/`（直接引用/查重）
   - `07-标准页面开发流程.md`（前端页面标准流程）
3. **后端**：只改 `VOL.Sys/Services/System/Partial/` 下的 Partial Service，禁改 .jsx；使用 Vol 框架 ServiceBase 钩子优先；YZH 增量能力（YZHBaseEntity / 特性体系）按 `02-YZH增量清单.md` 使用。
4. **前端**：使用 Vol 框架 view-grid 组件模式（参考 `vol-skill.md` §12 与 `YZH-知识库/07、08`）；YZH 自有组件优先。
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
