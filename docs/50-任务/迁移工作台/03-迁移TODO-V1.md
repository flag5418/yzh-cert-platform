# 迁移 TODO — 待办清单

> **版本**：V1.0 | **日期**：2026-09-12 | **状态**：生效
>
> 本文是**唯一反映"现在该干什么"的迁移待办清单**。
> 状态聚合视图见 [01-迁移总表-V1.md](./01-迁移总表-V1.md)，旧代码定位见 [02-历史项目地图-V1.md](./02-历史项目地图-V1.md)。

---

## 一、优先级定义

| 级别 | 含义 | 处理原则 |
|:----:|------|---------|
| **P0** | 必现故障——已建功能点开即错 | 立即处理，不排期 |
| **P1** | 能力未接入——框架缺口阻塞业务 | 排期补齐 |
| **P2** | 一致性瑕疵——能用但不规范 | 顺手修，不单独立项 |
| **P3** | 待评估——方向未定 | 拍板后再动 |

---

## 二、P0 —— 阻断项

### 2.0 总览：前端 API 路由失配矩阵

> 对 `certplatform-web` 全量实测：**15 处 `getPageData` 调用**，而新基类**从未提供过该端点**。
> 这不是单点 bug，而是**一次系统性的路由命名迁移**。

**框架层 10 个 API 文件的路由对齐情况：**

| API 文件 | 前端实际调用 | 后端实际路由 | 对齐 |
|---------|-------------|-------------|:----:|
| `api/system/organization.ts` | `/api/System/Organization/*` | `api/System/Organization` | ✅ |
| `api/system/role.ts` | `/api/System/Role/filter` | `api/System/Role` | ✅ |
| `api/system/role-menu.ts` | `/api/RoleMenu/*` | `api/System/RoleMenu`（双路由） | ✅ |
| `api/system/role-user.ts` | `/api/Role/check/*` | `api/System/Role` 的 `check/*` | ✅ |
| `api/system/menu.ts` | —（无路由定义） | `api/System/Menu` | ⚠️ 空文件 |
| `api/system/user.ts` | `/api/Sys_User/*` | `api/System/User` | ❌ |
| `api/system/dictionary.ts` | `/api/Sys_Dictionary/*`<br>`/api/Sys_DictionaryList/*` | `api/System/Dictionary` | ❌ |
| `api/system/menu-management.ts` | `/api/Sys_Menu/*` | `api/System/MenuManagement` | ❌ |
| `api/system/log.ts` | `/api/Sys_Log/*` | ❌ 控制器不存在 | ❌ |
| `api/system/param.ts` | `/api/Sys_Parameter/*` | ❌ 控制器不存在 | ❌ |

**结论**：10 个框架 API 文件中，**4 个已对齐、1 个空、5 个失配**。

---

### P0-1 用户管理路由失配

| 项 | 内容 |
|----|------|
| **现象** | 前端调 `/api/Sys_User/*`，后端实际路由 `api/System/User` |
| **位置** | `cert-admin/src/api/system/user.ts` |
| **实际调用** | `getPageData`、`add`、`update`、`delete` |
| **后端** | `YZH.Core.Web/Controllers/System/UserController.cs`，路由 `api/System/User` |
| **修复** | 路由改为 `api/System/User`，`getPageData` → `POST /filter` |
| **影响** | 用户管理页**无法加载数据** |

### P0-2 字典管理路由失配

| 项 | 内容 |
|----|------|
| **现象** | 前端调 `/api/Sys_Dictionary/*`、`/api/Sys_DictionaryList/*` |
| **位置** | `cert-admin/src/api/system/dictionary.ts` |
| **后端** | `api/System/Dictionary` |
| **修复** | 路由改为 `api/System/Dictionary`，`getPageData` → `POST /filter` |
| **影响** | 字典页**无法加载数据** |

### P0-3 菜单管理路由失配

| 项 | 内容 |
|----|------|
| **现象** | 前端调 `/api/Sys_Menu/getPageData` 与 `/api/Sys_Menu/getTree` |
| **位置** | `cert-admin/src/api/system/menu-management.ts` |
| **后端** | `api/System/MenuManagement`（基类端点为 `filter`，无 `getTree`） |
| **修复** | 路由改为 `api/System/MenuManagement`，查询改 `POST /filter`；树接口改走 `api/System/Menu` |
| **影响** | 菜单管理页**无法加载数据** |

### P0-4 系统参数路由指向不存在的控制器

| 项 | 内容 |
|----|------|
| **现象** | 前端调 `/api/Sys_Parameter/getPageData`、`getList`、`getValue`、`save`、`delete` |
| **位置** | `cert-admin/src/api/system/param.ts` |
| **后端** | ❌ **`Sys_Parameter` 控制器根本不存在**（框架层无系统参数控制器） |
| **修复** | 先按 §六 决策项 **D-1** 定表，再建控制器；前端路由同步 |
| **影响** | 系统参数页**无法加载数据** |

### P0-5 日志页 import 路径不存在

| 项 | 内容 |
|----|------|
| **现象** | `import { getLogPage } from '@share/api/system-log'` —— `@share/api/` 下**无 `system-log.ts`** |
| **位置** | `cert-admin/src/pages/system/log/index.vue` 第 4-5 行 |
| **旁证** | `cert-admin/src/api/system/log.ts` 存在（指向 `/api/Sys_Log/getPageData`），但页面没引用它 |
| **修复** | 统一到一处：在 `cert-share/src/api/system/log.ts` 建文件并修正 import；后端控制器见 P1-2 |
| **影响** | **编译 / 运行报错** |

### P0-6 字典项读不出来

| 项 | 内容 |
|----|------|
| **现象** | `DictionaryController.LoadDictItems()` 为空实现，恒返回空列表 |
| **位置** | `YZH.Core.Web/Controllers/System/DictionaryController.cs` |
| **根因** | `Sys_DictionaryList` 无实体、无服务（见 P1-1） |
| **影响** | 所有依赖字典的下拉框**均为空** |

### P0-7 唯一已迁移业务模块查询 404 ★新发现

| 项 | 内容 |
|----|------|
| **现象** | 前端调 `/api/Foundation/ISOClause/getPageData`，**新基类无 `getPageData` 端点** |
| **位置** | `cert-share/src/api/cert/iso-clause.ts` 第 6 行 |
| **后端** | `CertPlatform.Admin/Controllers/Foundation/ISOClauseController.cs`（95 行，已迁移） |
| **基类端点** | `config` / `filter` / `add` / `update` / `delete` / `export` / `import` / `import/template` / `action/{methodName}` / `toggle-valid` |
| **修复** | `getPageData` → `POST /api/Foundation/ISOClause/filter` |
| **影响** | ISO 条款页**查询必然失败** |

> **15 处 `getPageData` 的完整分布**：
> `cert-admin/src/api/system/` 5 处（dictionary / menu-management / param / log / user）、
> `cert-share/src/api/cert/` 4 处（iso-clause / iso-standard / cert-stage / certification-body）、
> `cert-share/src/api/workflow/` 6 处（ai-usage / enterprise / nc-config / report-rule / job-skill / queue）。
>
> 排查命令：在 `certplatform-web/` 下检索 `getPageData`。

---

## 三、P1 —— 框架能力补齐（7 条）

| # | 能力 | 缺口 | 建议动作 |
|:-:|------|------|---------|
| 1 | 字典管理 | `Sys_DictionaryList` 无实体、无服务 | 建实体 + 服务，实现 `LoadDictItems()` |
| 2 | 操作日志 | 无控制器、无落库逻辑（表 0 行） | 建 `SysLogController` + 落库切面 |
| 3 | 系统参数 | 无控制器、无实体指向框架表 | 见 §六 决策项 D-1 |
| 4 | 系统参数 | `sys_config` 与 `cert_sys_config` 双表并存 | 见 §六 决策项 D-1 |
| 5 | 更改密码 | 无接口（`Sys_User.LastModifyPwdDate` 字段已预留） | 补 `AuthController` 端点 |
| 6 | 用户-机构归属 | `Sys_UserDepartment` 零引用 + **类型不匹配**：`DepartmentId varchar(36)` ↔ `Sys_Organization.Id int` | 先定类型再接线 |
| 7 | 接口管理 | `sys_api` / `sys_role_api` / `sys_user_permission` 三表未建 | 见 §六 决策项 D-2 |

---

## 四、P2 —— 一致性瑕疵（5 条）

| # | 问题 | 位置 | 处理 |
|:-:|------|------|------|
| 1 | 前端字段命名混用：`dictionary` 页 camelCase（`dictName`），`user` 页 PascalCase（`UserName`） | 框架前端 | 统一为一种 |
| 2 | 用户页主键用 `User_Id`（旧 Vol 命名），违背 `Code` 主键约定 | `api/system/user.ts` | 改 `Code` |
| 3 | 控制器路由注释与实现不一致：`DictionaryController` 注释写 `api/SysDictionary`，实际 `api/System/Dictionary` | 后端 | 修注释 |
| 4 | 三个控制器挂双路由（`api/System/X` + `api/X`）未说明原因 | `Organization` / `Role` / `RoleMenu` | 补注释或收敛 |
| 5 | `BizNamingRules` 黑名单含不存在的类名（`Sys_UserRole`、`Sys_AuditLog`） | `YZH.Core.Stand/BizConventions/` | 清理 |

---

## 五、业务后端迁移排期

> 旧后端 25 个业务控制器，已迁移 1 个。**建议按依赖关系分波推进**，而非按行数排序。

### 第一波 —— 基础主数据（低风险，打样板）

| 序 | 模块 | 旧控制器 | 行数 | 前端页面 | 说明 |
|:--:|------|---------|:----:|---------|------|
| 1 | 认证机构 | `Partial/CertCertificationBodyController` | 21 | `foundation/certification-body` | 逻辑最薄，适合验证流程 |
| 2 | 认证阶段 | `Partial/CertStageController` | 21 | `foundation/cert-stage` | 同上 |
| 3 | ISO 标准 | `Partial/ISOStandardController` | 21 | `foundation/iso-standard` | 同上 |
| 4 | ISO 条款 | `ISOClauseController` | 75 | `foundation/iso-clause` | ✅ 已迁，**待修 P0-7** |

> **目标**：跑通"后端控制器 → EntityConfig JSON → 前端 API → 页面"完整链路，
> 沉淀出可复制的三件套模板。

### 第二波 —— 配置类模块（中等复杂度）

| 序 | 模块 | 旧控制器 | 行数 | 前端页面 |
|:--:|------|---------|:----:|---------|
| 5 | NC 检查规则 | `ValidationRuleController` | 78 | `workflow/nc-config` |
| 6 | 报告章节定义 | `ReportDefinitionController` | 173 | `workflow/report-rule` |
| 7 | Prompt 模板 | `PromptTemplateController` | 88 | `workflow/prompt-template` |
| 8 | 技能分类 | `WfSkillCategoryController` | 51 | `workflow/job-skill` |
| 9 | 技能管理 | `WfSkillController` | 109 | `workflow/job-skill` |
| 10 | 机构关联 | `OrgLinkController` | 102 | `workflow/link-org-*` |
| 11 | 系统参数 | `SysConfigController` | 66 | `workflow/sys-config` |
| 12 | AI 费用监控 | `AIUsageController` | 72 | `workflow/ai-usage` |

### 第三波 —— 核心业务（高复杂度）

| 序 | 模块 | 旧控制器 | 行数 | 前端页面 |
|:--:|------|---------|:----:|---------|
| 13 | 目录模板 | `DirectoryTemplateController` | 160 | ❌ 未建 |
| 14 | 企业管理 | `EnterpriseController` | 78 | `workflow/enterprise` |
| 15 | 企业文件 | `EnterpriseFileController` | 106 | `workflow/file-upload` |
| 16 | 标准目录管理 | `StandardDirectoryController` | **494** | `workflow/directory` |
| 17 | 文档提取规则 | `DocExtractionRuleController` | **251** | `workflow/doc-extraction` |
| 18 | 队列中心 | `QueueController` | 130 | `workflow/queue` |
| 19 | 工作流定义 | `WorkflowDefinitionController` | 70 | `workflow/workflow/list` |

### 第四波 —— 前端页面缺失，需先补页面

| 序 | 模块 | 旧控制器 | 行数 | 缺失页面 |
|:--:|------|---------|:----:|---------|
| 20 | 审核任务 | `AuditTaskController` | 21 | ❌ |
| 21 | 认证申请 | `CertPlatformController` | 49 | ❌ |
| 22 | 页面配置 | `PageConfigController` | 87 | ❌ |
| 23 | 消息中心 | `MessageController` | 58 | ❌ |
| 24 | 审核员档案 | `AuditorController` | 79 | `cert-auditor`（待完善） |

### 第五波 —— 框架层遗留

| 序 | 模块 | 旧控制器 | 行数 | 前置条件 |
|:--:|------|---------|:----:|---------|
| 25 | Vol 工作流 | `Sys_WorkFlowController` + 4 个子控制器 | 398+ | 先决策：复用还是弃用（见 D-3） |
| 26 | 定时任务 | `Sys_QuartzOptionsController` | 94 | 先决策：是否复用队列中心 |
| 27 | 文件存储 | `OSS/AliOSSController` | — | 先决策：OSS vs MinIO |

---

## 六、待拍板决策项

> 以下 4 项**必须先决策再动手**，否则会返工。

| 编号 | 决策项 | 选项 | 影响范围 |
|:----:|--------|------|---------|
| **D-1** | 系统参数双表合并方向 | **A**：保留框架表 `sys_config`（21 字段 / 0 行），迁移 `cert_sys_config` 的 22 行数据过来<br>**B**：保留项目表 `cert_sys_config`（14 字段 / 22 行），废弃框架表 | 框架能力 9、业务模块 11、两个前端页面 |
| **D-2** | 接口权限自动同步是否推进 | **A**：推进 `11-接口权限自动同步设计-V1.md`，建 3 张表<br>**B**：暂缓，改用角色-菜单粒度授权 | `system/api`、`system/role-api` 两个页面 |
| **D-3** | `Sys_WorkFlow*` 5 张表去向 | **A**：弃用，统一走自研工作流引擎<br>**B**：改造复用 | 框架表、`workflow/workflow/*` 页面 |
| **D-4** | 文件存储方案 | **A**：沿用阿里云 OSS（旧架构已跑通）<br>**B**：切换到 MinIO（技术栈已锁定） | 所有上传功能 |

> 决策结果请写入 [`../../40-实施/`](../../40-实施/) 并在本表标注。

---

## 七、已完成（归档记录）

| 完成日期 | 事项 | 证据 |
|---------|------|------|
| 2026-09-12 | 文档体系重构：七层结构、136 项文件迁移、181 条链接修复 | [`../../README.md`](../../README.md) |
| 2026-09-12 | ISO 条款模块迁移（首个业务模块） | `CertPlatform.Admin/Controllers/Foundation/ISOClauseController.cs` |
| 2026-09-12 | 框架能力清单 / 框架数据库设计 / 框架核心功能设计 三份文档成稿 | [`../../10-YZH架构/`](../../10-YZH架构/) |
| — | 框架层 7 个模块迁移（用户/角色/机构/菜单/角色-菜单/角色-人员/登录） | `YZH.Core.Web/Controllers/` |

---

## 八、维护约定

1. **完成即标**：某项完成后**不删除**，移入 §七 并注明日期与证据。
2. **不重复登记**：框架能力缺口的**权威源**是
   [`../../10-YZH架构/12-框架能力清单-V1.md`](../../10-YZH架构/12-框架能力清单-V1.md) §5，
   本表 P1/P2 与其保持同步，**冲突时以 12 号文档为准**。
3. **本表只登记迁移相关待办**，纯业务功能需求写进
   [`../../20-体系认证/`](../../20-体系认证/) 对应模块文档。

---

## 同组文档

[01-迁移总表-V1.md](./01-迁移总表-V1.md) · [02-历史项目地图-V1.md](./02-历史项目地图-V1.md) · [README.md](./README.md)
