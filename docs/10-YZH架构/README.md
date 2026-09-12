# YZH 架构文档 — 根入口

> **版本**：V1.1 | **日期**：2026-09-12 | **状态**：编写中
>
> 本文档是 yzh 架构的**唯一权威入口**。
> AI 编程助手（Cursor / Claude / Copilot / Aider）进入架构文档时，首先阅读本文档，再按需跳转各子文档。

---

## 〇、先看这里（AI 与新人必读）

YZH 框架当前处于**半成品状态**，近阶段工作重心即为完善其底层支撑体系。因此：

| 你的问题 | 去看 |
|----------|------|
| YZH 框架**现在有什么能力**？哪些可用、哪些还在完善？ | [**12-框架能力清单-V1.md**](./12-框架能力清单-V1.md) ← **先看这份** |
| 框架的**数据库表**长什么样？ | [13-框架数据库设计-V1.md](./13-框架数据库设计-V1.md) |
| 登录 / 菜单 / 角色授权 / 队列**怎么运作**？ | [14-框架核心功能设计-V1.md](./14-框架核心功能设计-V1.md) |
| 我要**写代码**，遵守什么规范？ | 见 §三「编码前必读」 |

> ⚠️ **误用警示**：`11-接口权限自动同步设计-V1.md` 是**未落地的设计稿**，其中引用的 `sys_api` / `sys_role_api` / `sys_user_permission` 三张表**在数据库中不存在**。阅读前请先看该文档顶部的状态横幅。
>
> ⚠️ **端点警示**：基类**没有 `getPageData` 端点**，查询一律走 `POST /{controller}/filter`。框架前端中残留的 `/api/Sys_Xxx/getPageData` 调用是旧 Vol 路由，属已知缺陷。

---

## 一、什么是 YZH 架构

YZH 架构是映智汇认证审核管理系统（yzh-cert-platform）的自研前后端框架，目标是：

- **配置驱动 UI**：前端不硬编码表格列和表单字段，由后端 JSON 配置决定
- **统一基类**：单表和左树右表两种布局均继承统一控制器基类
- **API 固定**：方法名、请求/响应格式全局统一，减少学习成本
- **业务差异隔离**：业务差异只通过配置和 virtual 方法覆盖实现
- **增量更新**：前端 Split 数据方法，不重新请求全量数据

与主流框架（Spring Boot + Vue Admin、NestJS + Angular）的核心差异：

| 维度 | 主流框架 | YZH 架构 |
|------|---------|---------|
| 表格列定义 | 前端硬编码 options/config | 后端 JSON 配置，前端动态获取 |
| API 规范 | 各模块自由定义 | 固定 11 个接口（/config /filter /add /update /delete /export /import /action /tree/*） |
| 树形结构 | 各项目独立实现 | 统一 `TreeTableControllerBase<T,V>` + `TreeTableLogic<V>` |
| 权限 | 手动拦截器 | 内置 `[YZHAuthorize]` + SSO 挤号 + Token 续租 |
| 生命周期 | 无统一约定 | 标准钩子（OnBeforeAdd / OnAfterUpdate / OnBeforeDelete 等） |

---

## 二、目录结构

```
docs/10-YZH架构/
├── README.md                    ← 本文件：架构入口 + 导航
│
│   ── 规范类：怎么写代码 ──
├── 01-架构总纲.md               核心理念 + 继承体系 + 核心约束
├── 02-后端架构.md               YzhControllerBase + TreeTableControllerBase + EntityService
├── 03-前端架构.md               CrudPageLogic + TreeTableLogic + 组件体系
├── 04-数据契约.md               FilterRequest / TreeItemDto / EntityConfig / TreeConfig
├── 05-权限体系.md               三类组织域 + 角色矩阵 + SSO + 三层权限
├── 06-代码结构规范.md           三层同构 + 路由约定 + 命名规范
├── 07-开发流程.md               新页面开发步骤 + 钩子速查 + 常见场景
├── 08-常见错误与修复.md         从踩坑记录提炼的高频问题
│
│   ── 治理类：架构自身的问题与规划 ──
├── 09-架构修复清单.md           P0/P1/P2/P3 问题清单 + TODO
├── 10-架构建设建议.md           权限表结构 + 待建设功能 + 路线图
├── 11-接口权限自动同步设计-V1.md  反射扫描 + ApiCode Hash + 自动同步机制
│                                ⚠️ 设计稿，未落地（见文首状态横幅）
│
│   ── 框架设计类：框架自己长什么样 ── ★
├── 12-框架能力清单-V1.md         能力索引：有什么、做到什么程度、项目怎么用
├── 13-框架数据库设计-V1.md       28 张框架表 × 8 组 + 字段 + 关联 + 状态
├── 14-框架核心功能设计-V1.md     登录 / 验证码 / SSO / 机构树 / 角色授权 / 队列
│
│   ── 对比类：新旧架构差异 ──
└── 15-YZH架构与传统架构对比-V1.md 逻辑落点 / 关联模型（Code vs Id）/ 接口契约 / UI 配置 + 代码量实证
```

> **三份「框架设计类」文档的分工**：
> `12` = **状态看板**（有什么、做到哪了） · `13` = **静态结构**（表与字段） · `14` = **动态流程**（怎么运作）。
> 三者互相引用，内容不重复。查"框架能不能做 X"看 12，查"表怎么设计"看 13，查"流程怎么走"看 14。

---

## 三、快速导航

### 编码前必读（按顺序）

| 步骤 | 文档 | 目的 |
|------|------|------|
| ① | [01-架构总纲](./01-架构总纲.md) | 理解架构设计理念，知道"为什么这样做" |
| ② | [04-数据契约](./04-数据契约.md) | 掌握前后端统一的数据格式 |
| ③ | [06-代码结构规范](./06-代码结构规范.md) | 知道文件放在哪里、怎么命名 |
| ④ | [07-开发流程](./07-开发流程.md) | 按步骤实现，不会遗漏 |

### 查阅用（按需跳转）

| 需求 | 文档 |
|------|------|
| **框架有哪些能力？项目怎么用？** | [**12-框架能力清单-V1.md**](./12-框架能力清单-V1.md) |
| **框架表结构 / 字段 / 关联** | [13-框架数据库设计-V1.md](./13-框架数据库设计-V1.md) |
| **框架功能流程（登录 / 授权 / 队列）** | [14-框架核心功能设计-V1.md](./14-框架核心功能设计-V1.md) |
| **新旧架构差在哪、为什么省代码** | [15-YZH架构与传统架构对比-V1.md](./15-YZH架构与传统架构对比-V1.md) |
| 我要写后端控制器 | [02-后端架构.md](./02-后端架构.md) |
| 我要写前端页面 | [03-前端架构.md](./03-前端架构.md) |
| 我要配置权限 | [05-权限体系.md](./05-权限体系.md) |
| 我遇到了错误 | [08-常见错误与修复.md](./08-常见错误与修复.md) |
| 框架还有哪些坑没填 | [09-架构修复清单.md](./09-架构修复清单.md) |
| 我要同步接口权限 | [11-接口权限自动同步设计-V1.md](./11-接口权限自动同步设计-V1.md)（⚠️ 未落地设计稿） |
| 这份文档是从哪来的（溯源） | [99-文档来源索引-V1.md](./99-文档来源索引-V1.md) |

---

## 四、关键文件路径速查

### 后端（src/yzh-core/）

| 文件 | 职责 |
|------|------|
| `YZH.Core.Api/Controllers/YzhControllerBase.cs` | 单表 CRUD 基类 |
| `YZH.Core.Api/Controllers/TreeTableControllerBase.cs` | 左树右表基类 |
| `YZH.Core.Api/Services/EntityService.cs` | 原子数据服务 |
| `YZH.Core.Stand/Models/Config/EntityConfig.cs` | 配置模型定义 |
| `YZH.Core.Stand/Models/Config/EntityConfigDto.cs` | 前端 DTO 定义 |
| `YZH.Core.Stand/Models/Config/TreeConfig.cs` | 树配置模型 |
| `YZH.Core.Stand/Models/Entity/BaseEntity.cs` | 实体基类（审计字段） |
| `YZH.Core.Stand/Models/ITreeNode.cs` | 树节点接口 |
| `YZH.Core.Web/Controllers/System/UserController.cs` | 单表控制器示例 |
| `YZH.Core.Web/Controllers/System/OrganizationController.cs` | 左树右表控制器示例 |

### 前端（src/certplatform-web/）

| 文件/目录 | 职责 |
|-----------|------|
| `cert/cert-share/src/logic/CrudPageLogic.ts` | 单表 Logic 基类 |
| `cert/cert-share/src/logic/TreeTableLogic.ts` | 左树右表 Logic 基类 |
| `cert/cert-share/src/types/contracts.ts` | 前后端统一契约类型 |
| `cert/cert-share/src/types/tree.ts` | 树节点类型定义 |
| `yzh.vue.core/src/components/table/YzhTable.vue` | 数据表格组件 |
| `yzh.vue.core/src/components/form/YzhForm.vue` | 表单组件 |
| `yzh.vue.core/src/components/layout/YzhTreeTable.vue` | 左树右表布局组件 |
| `cert/cert-admin/src/pages/system/user/index.vue` | 单表页面示例（参考） |
| `cert/cert-admin/src/pages/system/organization/index.vue` | 左树右表页面示例 |

---

## 五、三层同构速查

```
新增一个业务模块（以 ISOClause 为例）：

后端控制器：src/yzh-core/YZH.Core.Web/Controllers/Foundation/ISOClauseController.cs
         → 路由：/api/Foundation/ISOClause

前端 API：src/certplatform-web/cert/cert-share/src/api/cert/iso-clause.ts
         → 对应路由：/api/Foundation/ISOClause

前端页面：src/certplatform-web/cert/cert-admin/src/pages/foundation/iso-clause/index.vue
         → 路由 path：/foundation/iso-clause

EntityConfig：src/yzh-core/YZH.Core.Web/Assets/EntityConfigs/Foundation/ISOClause.json
```

---

## 六、与旧 Vol 框架的核心差异

| 维度 | Vol 框架（旧） | YZH 架构（新） |
|------|--------------|--------------|
| 表格列定义 | 前端 options.js 硬编码 | 后端 JSON，前端从 /config 获取 |
| 表单字段定义 | 前端 VolForm 硬编码 | 后端 JSON，BcFlag 控制 |
| 树形结构 | 各项目独立实现 | 统一 TreeTableControllerBase / TreeTableLogic |
| API 接口 | 各项目自由定义 | 固定 11 个接口，命名统一 |
| 权限校验 | 手动 Filter | 内置 [YZHAuthorize] + SSO 挤号 |
| 前端绑定 | VolProvider / view-grid | yzhApi + CrudPageLogic / TreeTableLogic |

> ⚠️ 旧前端代码（`src/old/server/Vue.NetCore/vol.web/`、`src/old/auditor/`）**禁止修改**，仅作参考。

---

## 七、状态标记说明

文档状态取值（与项目全局规则 §2.4.6 对齐）：

| 状态 | 含义 |
|------|------|
| `编写中` | 正在编写，内容可能不完整 |
| `评审中` | 已完成编写，等待审核 |
| `已定稿` | 审核通过，强制执行 |

---

## 八、关联文档

| 文档 | 关系 |
|------|------|
| [AGENTS.md](../../AGENTS.md) | AI 编码助手强制入口（自动加载） |
| [项目全局规则.md](../../项目全局规则.md) | 最高宪法（文档体系、技术栈锁定） |
| [docs/00-工程体系/README.md](../00-工程体系/README.md) | 全项目文档导航 |
| [docs/30-项目规则/前后端代码结构统一规则-V1.md](../30-项目规则/前后端代码结构统一规则-V1.md) | 三层同构详细规范（本文档 §06 的完整版本） |
| [docs/90-归档/旧版本/YZH-架构体系总纲-V1.md](../90-归档/旧版本/YZH-架构体系总纲-V1.md) | 原始架构总纲（已合并入本文档 01/02/03 章） |
| [docs/90-归档/旧版本/左树右表统一架构设计-V1.md](../90-归档/旧版本/左树右表统一架构设计-V1.md) | 原始树形架构设计（已合并入本文档 02/03/04 章） |
| [docs/20-体系认证/01-总体设计/权限体系/多角色复杂权限体系设计-V1.md](../20-体系认证/01-总体设计/权限体系/多角色复杂权限体系设计-V1.md) | 原始权限体系设计（已合并入本文档 05 章） |
| [docs/30-项目规则/知识库/](../30-项目规则/知识库) | 知识底座：能力清单、边界约束、代码模板、踩坑记录 |
