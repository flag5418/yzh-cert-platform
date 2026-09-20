# 后端 SQL→ORM 迁移 TODO 清单

> **文档版本**: V1
> **创建时间**: 2026-09-20
> **状态**: 待审批
> **关联方案**: `docs/50-任务/后端SQL迁移LINQ实施方案-V1.md`
> **核心目标**: 消灭手写 SQL 87+ 处 → Service 层统一使用 SqlSugarClient LINQ

---

## 一、修复原则

| 原则 | 说明 |
|------|------|
| **注入漏洞优先** | P0 两处高危必须最先修复 |
| **框架层先于业务层** | 先改 `IDbOrm` / `EntityService` / `ApiRepository`，再改业务 Service |
| **每阶段可验收** | 每阶段结束 grep 验证，确保 SQL 数量归零 |
| **保留合理原始 SQL** | `FOR UPDATE SKIP LOCKED`、`INSERT...SELECT...ON DUPLICATE KEY` 等 ORM 不支持的场景保留并加注释 |

---

## 二、阶段 P0：修复注入漏洞（0.5 天）

> **目标**: 消除 2 处 SQL 注入高危风险

| ID | 文件 | 行号 | 问题 | 修复方式 | 验收标准 |
|----|------|------|------|---------|---------|
| P0.1 | `src/yzh-core/YZH.Core.Api/Services/EntityService.cs` | 298 | IN 子句用 `string.Join` 拼接 | 改为 `@c0,@c1,...` 参数化 + `ExpandoObject` | 该文件无字符串拼接 SQL |
| P0.2 | `src/certplatform-api/CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs` | 798 | IN 子句用 `$"'{f.FileCode}'"` 拼接 | 改为 `@f0,@f1,...` 参数化 + `ExpandoObject` | 该文件无字符串拼接 SQL |

**验收命令**：
```bash
grep -rn "string\.Join.*'" src/yzh-core/YZH.Core.Api/Services/EntityService.cs
grep -rn "'{.*FileCode}'" src/certplatform-api/CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs
# 预期：无输出
```

---

## 三、阶段 P1：框架层改造（2 天）

> **目标**: `IDbOrm` 暴露 `Client`，框架层核心文件消灭手写 SQL

| ID | 文件 | 改动内容 | 验收标准 |
|----|------|---------|---------|
| P1.1 | `src/yzh-core/YZH.Core.DataBase/Interfaces/IDbOrm.cs` | 新增 `SqlSugarClient Client { get; }` 属性 | 编译通过 |
| P1.2 | `src/yzh-core/YZH.Core.DataBase/Implementations/SqlSugarDbOrm.cs` | 实现 `Client => _client` | 编译通过 |
| P1.3 | `src/yzh-core/YZH.Core.Api/Services/EntityService.cs` | `GetChildrenCountBatch` 改用 `.GroupBy().Select().ToDictionaryAsync()` | 该文件 `SqlQueryAsync` 调用为 0 |
| P1.4 | `src/yzh-core/YZH.Core.Api/Repositories/ApiRepository.cs` | 18 处全部改 ORM：`GetAllAsync`/`GetByCodeAsync`/`InsertBatchAsync`/`UpdateBatchAsync`/`DeleteBatchAsync`/`InsertRoleApiAsync`/`DeleteByRoleCodeAsync`/`GetApiCodesByRoleCodeAsync`/`InsertUserPermissionAsync`/`DeleteByUserCodeAsync`/`GetApiCodesByUserCodeAsync`/`GetAllRoleApiAssociationsAsync` | 该文件 `SqlExecuteAsync`/`SqlQueryAsync` 调用为 0 |
| P1.5 | `src/yzh-core/YZH.Core.Api/Interfaces/IApiRepository.cs` | 同步更新接口（如方法签名变化） | 编译通过 |

**验收命令**：
```bash
grep -rn "SqlExecuteAsync\|SqlQueryAsync\|QueryFirstOrDefaultAsync" src/yzh-core/YZH.Core.Api/Repositories/ApiRepository.cs
grep -rn "SqlQueryAsync" src/yzh-core/YZH.Core.Api/Services/EntityService.cs
# 预期：无输出
```

---

## 四、阶段 P2：认证权限层改造（1 天）

> **目标**: 认证/鉴权相关代码规范化，消灭手写 SQL

| ID | 文件 | 改动内容 | 验收标准 |
|----|------|---------|---------|
| P2.1 | `src/yzh-core/YZH.Core.Web/Controllers/AuthController.cs` | 登录用 `GetOneAsync<Sys_User>()`；更新 Token 用 `UpdateAsync()` | 该文件无手写 SQL |
| P2.2 | `src/yzh-core/YZH.Core.Api/Attributes/YZHAnonymousAttribute.cs` | 鉴权查用户/角色改 ORM | 该文件无手写 SQL |
| P2.3 | `src/yzh-core/YZH.Core.Api/Services/RoleService.cs` | 角色名批量查询 + 单用户角色查询改 ORM | 该文件无手写 SQL |
| P2.4 | `src/yzh-core/YZH.Core.Web/Controllers/System/MenuController.cs` | 菜单列表查询改 ORM | 该文件无手写 SQL |
| P2.5 | `src/yzh-core/YZH.Core.Web/Controllers/System/RoleMenuController.cs` | 删除角色菜单关联改 ORM | 该文件无手写 SQL |
| P2.6 | `src/yzh-core/YZH.Core.Web/Controllers/System/RoleController.cs` | 删除角色用户关联改 ORM | 该文件无手写 SQL |
| P2.7 | `src/yzh-core/YZH.Core.Web/Controllers/System/RoleApiController.cs` | 删除角色接口关联改 ORM | 该文件无手写 SQL |
| P2.8 | `src/yzh-core/YZH.Core.Api/Services/PermissionCacheService.cs` | 保留（批量同步 SQL 性能关键），加注释 | 有 `// ORM 不支持此语法，保留原始 SQL` 注释 |

**验收命令**：
```bash
grep -rn "SELECT \* FROM\|SELECT .* FROM Sys_" src/yzh-core/YZH.Core.Web/
grep -rn "SqlExecuteAsync\|SqlQueryAsync" src/yzh-core/YZH.Core.Api/Services/RoleService.cs
# 预期：PermissionCacheService 除外，其余无输出
```

---

## 五、阶段 P3：业务层实体补建（3 天）

> **目标**: 为 14 张无实体的表创建实体类，为 P4 改造做准备

| ID | 表名 | 实体类 | 放置路径 | `[SugarTable]` 注解 |
|----|------|--------|---------|---------------------|
| P3.1 | `cert_upload_task` | `CertUploadTask` | `src/certplatform-api/CertPlatform.Shared/Entities/` | `[SugarTable("cert_upload_task")]` |
| P3.2 | `cert_standard_directory_file` | `CertStandardDirectoryFile` | 同上 | `[SugarTable("cert_standard_directory_file")]` |
| P3.3 | `cert_standard_directory_folder` | `CertStandardDirectoryFolder` | 同上 | `[SugarTable("cert_standard_directory_folder")]` |
| P3.4 | `ent_extraction_result` | `EntExtractionResult` | 同上 | `[SugarTable("ent_extraction_result")]` |
| P3.5 | `ent_table_extraction_result` | `EntTableExtractionResult` | 同上 | `[SugarTable("ent_table_extraction_result")]` |
| P3.6 | `wf_execution_task` | `WfExecutionTask` | 同上 | `[SugarTable("wf_execution_task")]` |
| P3.7 | `wf_execution_task_item` | `WfExecutionTaskItem` | 同上 | `[SugarTable("wf_execution_task_item")]` |
| P3.8 | `wf_node_execution` | `WfNodeExecution` | 同上 | `[SugarTable("wf_node_execution")]` |
| P3.9 | `cert_ai_usage_log` | `CertAiUsageLog` | 同上 | `[SugarTable("cert_ai_usage_log")]` |
| P3.10 | `cert_ai_config` | `CertAiConfig` | 同上 | `[SugarTable("cert_ai_config")]` |
| P3.11 | `cert_sys_config` | `CertSysConfig` | 同上 | `[SugarTable("cert_sys_config")]` |
| P3.12 | `wf_skill_reflection` | `WfSkillReflection` | 同上 | `[SugarTable("wf_skill_reflection")]` |
| P3.13 | `yzh_queue_task` | `YzhQueueTask` | 同上 | `[SugarTable("yzh_queue_task")]` |
| P3.14 | `yzh_queue_resource_lock` | `YzhQueueResourceLock` | 同上 | `[SugarTable("yzh_queue_resource_lock")]` |

**验收标准**：
```bash
# 14 个实体类文件全部存在
ls src/certplatform-api/CertPlatform.Shared/Entities/Cert{UploadTask,StandardDirectoryFile,StandardDirectoryFolder,AiUsageLog,AiConfig,SysConfig}.cs
ls src/certplatform-api/CertPlatform.Shared/Entities/Ent{ExtractionResult,TableExtractionResult}.cs
ls src/certplatform-api/CertPlatform.Shared/Entities/Wf{ExecutionTask,ExecutionTaskItem,NodeExecution,SkillReflection}.cs
ls src/certplatform-api/CertPlatform.Shared/Entities/Yzh{QueueTask,QueueResourceLock}.cs
# 编译通过
dotnet build src/certplatform-api/CertPlatform.Shared/ --no-restore
```

---

## 六、阶段 P4：业务 Service 改造（5-8 天）

> **目标**: 业务层 Service 全部改用 ORM，仅保留 QueueManager + PermissionCacheService 的原始 SQL

| ID | 文件 | SQL 数量 | 改动内容 | 验收标准 |
|----|------|---------|---------|---------|
| P4.1 | `src/certplatform-api/CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs` | 20+ | 上传任务/文件/文件夹 CRUD 全改 ORM | 该文件 `SqlExecuteAsync`/`SqlQueryAsync` 调用为 0 |
| P4.2 | `src/certplatform-api/CertPlatform.Admin/Services/DocExtractionRuleService.cs` | 8 | 提取结果 CRUD + 规则查询改 ORM | 该文件无手写 SQL |
| P4.3 | `src/certplatform-api/CertPlatform.Admin/Services/DocExtractionRuleService.AI.cs` | 4 | AI 配置/模板查询改 ORM | 该文件无手写 SQL |
| P4.4 | `src/certplatform-api/CertPlatform.Admin/Services/Workflow/WfExecutionTaskService.cs` | 7 | 执行任务 CRUD 改 ORM | 该文件无手写 SQL |
| P4.5 | `src/certplatform-api/CertPlatform.Admin/Services/Workflow/AiNodeExecutor.cs` | 1 | AI 配置查询改 ORM | 该文件无手写 SQL |
| P4.6 | `src/certplatform-api/CertPlatform.Admin/Services/Workflow/CertSkillRegistry.cs` | 1 | 技能查询改 ORM | 该文件无手写 SQL |
| P4.7 | `src/certplatform-api/CertPlatform.Admin/Controllers/Workflow/AIUsageController.cs` | 5 | 分页/统计改 ORM | 该文件无手写 SQL |
| P4.8 | `src/yzh-core/YZH.Core.DataBase/Services/QueueManager.cs` | 10 | 保留，加注释 | 有 `// ORM 不支持 FOR UPDATE SKIP LOCKED` 注释 |

**验收命令**：
```bash
grep -rn "SqlExecuteAsync\|SqlQueryAsync" src/certplatform-api/ | grep -v QueueManager
# 预期：仅 PermissionCacheService（P2.8 已加注释）
```

---

## 七、总工作量

| 阶段 | 工作量 | 累计 |
|------|--------|------|
| P0：修复注入漏洞 | 0.5 天 | 0.5 天 |
| P1：框架层改造 | 2 天 | 2.5 天 |
| P2：认证权限层 | 1 天 | 3.5 天 |
| P3：实体补建 | 3 天 | 6.5 天 |
| P4：业务 Service 改造 | 5-8 天 | **11.5-14.5 天** |

---

## 八、风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| SqlSugar LINQ 不支持 `FOR UPDATE SKIP LOCKED` | QueueManager 无法改 ORM | 已确认保留，加注释 |
| `PermissionCacheService` 批量同步 SQL 改 ORM 后性能下降 | 权限缓存重建变慢 | 保留原始 SQL，加注释 |
| 实体类字段与数据库不一致 | 运行时报错 | P3 阶段逐一核对数据库表结构 |
| `YZHAnonymousAttribute` 是静态方法，无法注入 DI | 鉴权代码改 ORM 需重构 | 通过 `IServiceProvider` 获取 `IDbOrm` |
