# YZH 架构安全改进 TODO 计划

> 依据：`docs/10-YZH架构/YZH架构安全分析与改进建议.md`
> 日期：2026-09-12
> 状态：待评审

---

## 〇、计划概览

| 维度 | 数值 |
|------|------|
| 总项数 | 30 项（P0 9 / P1 14 / P2 7）+ Docker 清单 3 组 + 文档修正 4 项 |
| 预计工期 | P0 2.5 周 / P1 3 周 / P2 2 周（串行）|
| 关键路径 | P0-1 权限校验 → P0-7 前端归一 → P1-14 契约生成 → P1-22 前端治理 |
| 并行面 | Docker 安全清单可与 P0 同步；文档修正可与 P1 同步 |

---

## 一、第一阶段：立即修复（P0）— 安全阻断 + 结构解耦

**目标**：消除上线阻断级安全漏洞，理顺框架与业务的依赖方向。
**工期**：约 2.5 周（10 个工作日）
**准入条件**：无；可随时启动
**退出标准**：9 项全部合入主干，配套单测通过，CI 不挂

| # | 改进项 | 位置 | 类型 | 工作量 | 负责人建议 | 依赖 | 产出物 |
|:--:|--------|------|:------:|:------:|-----------|------|--------|
| 1 | **实现接口级权限校验** + 数据范围注入点 | `PermissionFilter.cs:114-138` | 安全 | 3d | 后端核心 | 无 | `PermissionFilter` 重写 + `[Permission]` 特性 + 单测 3 个 |
| 2 | **移除 CORS `AllowAll`**，改配置化白名单 | `Program.cs:55-61/168` | 安全 | 0.5d | 后端核心 | 无 | `appsettings.json` 新增 `Cors` 节 + `AddCors` 改写 |
| 3 | **移除硬编码默认密钥兜底** | `YzhWebBuilder.cs:68/79` | 安全 | 0.5d | 后端核心 | 无 | 启动时校验 `YZH:EncryptionKey`/`YZH:JwtSecret` 存在且长度 ≥32，缺则抛异常 |
| 4 | **`SortField` 白名单校验** | `SqlSugarDbOrm.cs:99-104` | 安全 | 1d | 后端核心 | 无 | `ValidateSortField` 方法（正则 + 实体属性反射验证）+ 请求参数 SQL 关键字拦截中间件 |
| 5 | **统一软删除/有效标志过滤实现** | `SqlSugarDbOrm.cs:79-88` vs `:243-291` | 约定 | 2d | 后端核心 | 无 | `ISoftDelete`/`IValidFlag` 接口 + ORM 层统一表达式过滤 + 清理反引号硬编码 |
| 6 | **补无 `Code` 实体通道** | `SqlSugarDbOrm.cs:152-215` | 约定 | 1.5d | 后端核心 | 5 | 无 Code 实体走 `Id` 定位的降级分支 + `[RequireCode]` 启动期校验 + 迁移脚本 |
| 7 | **框架 → 业务反向依赖修复** | `YzhCrudPage.vue` | 结构 | 2d | 前端框架 | 无 | `YzhCrudPage` 改 props 注入（`apiClient`/`schema`/`dataTransform`）+ 删除 `@share/*` import |
| 8 | **System 域控制器迁入能力层** | `YZH.Core.Web/Controllers/System/*` → `YZH.Core.Api` | 结构 | 1.5d | 后端核心 | 无 | 8 个控制器文件迁移 + `extern alias` 调整 + 启动验证 |
| 9 | **前端 HTTP 入口归一** | `yzh.vue.core/src/utils/http.ts` | 前后端关系 | 1.5d | 前端框架 | 7 | `api/client.ts` 统一封装 + `http.ts` 标记 `@deprecated` + 存量引用迁移脚本 |

### P0 阶段关键路径

```
第 1 周：1(权限) → 2(CORS) → 3(密钥) → 4(排序白名单)
第 2 周：5(软删除统一) → 6(无Code通道) → 8(System迁移)
         7(前端反向依赖) → 9(HTTP归一，可与7并行)
```

### P0 阶段风险

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 权限校验 (#1) 与角色-接口权限实施计划冲突 | 重复设计 | **先对齐**：`docs/50-任务/角色-接口权限实施计划-V1.md` 7 个待拍板决策需在本阶段前关闭 |
| `YzhCrudPage` props 注入 (#7) 导致 cert-admin 4 个已跑通页面崩溃 | 回滚 | 改动前在 `cert-admin` 建分支；每改一个组件跑一遍端到端 |
| System 域迁移 (#8) 打破 `extern alias` 编译 | 无法启动 | 迁移后必须执行 `dotnet build` 全量通过，再提 PR |

---

## 二、第二阶段：短期补齐（P1）— 能力落地 + 工程治理

**目标**：让框架能力清单中"完善中/规划中"项真正可运行，建立工程化约束。
**工期**：约 3 周（15 个工作日）
**准入条件**：P0 全部完成且稳定运行 3 天
**退出标准**：14 项完成，框架能力清单中 📋 项减少 50%

| # | 改进项 | 位置 | 工作量 | 负责人建议 | 依赖 | 产出物 |
|:--:|--------|------|:------:|-----------|------|--------|
| 10 | **操作日志落库** | 清单 §3.3 能力 10 | 2d | 后端业务 | 1, 17 | `sys_log` 表启用 + `YzhAuditLogger` 改写入 DB + 脱敏字段字典 |
| 11 | **字典项加载做实** | 清单 §3.3 能力 8 | 1d | 后端核心 | 5, 6 | `LoadDictItems()` 实现（读 `Sys_DictionaryList`）+ 缓存前缀隔离 `dbdict:{dicNo}` |
| 12 | **系统参数双表决策并实施** | 清单 §3.3 能力 9 | 1.5d | 后端核心 | 无 | **决策文档**（`sys_config` vs `cert_sys_config`）+ 迁移脚本 + 统一访问接口 |
| 13 | **分页 `MaxPageSize` 钳制** | `FilterRequest.cs:7`、`PagerOptions.cs:12` | 0.5d | 后端核心 | 无 | `MaxPageSize = 500`（可配置），超限抛 400 |
| 14 | **契约生成与漂移校验** | 前后端 | 3d | 全栈 | 9 | OpenAPI 生成 → `openapi-typescript` → `contracts.ts`；CI 加契约比对 |
| 15 | **约定外控制器登记白名单** | `MenuController` / `AuthController` / `ApiSyncController` | 1d | 后端核心 | 1 | 白名单注册表 + 启动期扫描校验（非白名单裸 Controller 抛警告） |
| 16 | **上传/导入统一校验策略** | `YzhControllerBase.cs:370-374` | 1d | 后端核心 | 无 | 扩展名/MIME/大小白名单 + 病毒扫描钩子接口 |
| 17 | **审计写入改有界 Channel** | `YzhAuditLogger.cs` | 1.5d | 后端核心 | 无 | `System.Threading.Channels` 替换 `ConcurrentQueue` + 批量 Flush + 上限 1000 |
| 18 | **缓存统一门面** | `CacheManager` / `INoSql` / `DictService` | 2d | 后端核心 | 11 | `ICacheManager` 接口 + `CacheKeys` 集中声明 + 存量替换 |
| 19 | **假参数清零** | `EntityService.cs:134-195` | 1d | 后端核心 | 无 | `includeDeleted` 等未传递参数全量核对修复 + 单测补全 |
| 20 | **更改密码 + Token 即时失效** | `AuthController` / `TokenVersionService.cs:23` | 1d | 后端核心 | 1 | `change-password` 端点 + 改密后 `BumpVersionAsync` + Token 验证时查版本 |
| 21 | **导出改流式** | `YzhControllerBase.cs:329` | 1.5d | 后端核心 | 17 | `FileStreamResult` 流式导出 + 大数据量异步导出（队列中心） |
| 22 | **前端基类接入率治理** | `pages/**` | 3d | 前端业务 | 7, 9 | 38 个页面逐一审计：`CrudPageLogic`/`TreeTableLogic` 接入率统计 + 不合规页面整改 |
| 23 | **`Sys_WorkFlow*` 正式标注弃用** | 清单 §3.4 | 0.5d | 后端核心 | 无 | `[Obsolete]` 标注 + 清单状态改 🗑️ + 文档说明 |

### P1 阶段里程碑

- **W3 结束**：#10~#13、#15~#16、#19、#23 完成（后端硬约束补齐）
- **W4 结束**：#17~#18、#20~#21 完成（性能与缓存治理）
- **W5 结束**：#14、#22 完成（前后端契约对齐 + 前端接入率达标）

### P1 阶段依赖外部决策

| 决策 | 来源 | 阻塞项 |
|------|------|--------|
| D-1 系统参数双表合并方向 | `迁移工作台/03-迁移TODO-V1.md` §六 | #12 |
| D-2 接口权限自动同步是否推进 | `迁移工作台/03-迁移TODO-V1.md` §六 | #1, #10, #15 |
| D-3 `Sys_WorkFlow*` 弃用 or 改造 | `迁移工作台/03-迁移TODO-V1.md` §六 | #23 |
| D-4 文件存储 OSS vs MinIO | `迁移工作台/03-迁移TODO-V1.md` §六 | #16 |

---

## 三、第三阶段：中期加固（P2）— 机制化 + 可持续性

**目标**：让框架能力状态可自动校验，建立长期安全与工程健康度机制。
**工期**：约 2 周（10 个工作日）
**准入条件**：P1 全部完成
**退出标准**：7 项完成，CI 新增 3 个门禁

| # | 改进项 | 工作量 | 负责人建议 | 依赖 | 产出物 |
|:--:|--------|:------:|-----------|------|--------|
| 24 | **能力清单加 `code_refs` + CI 一致性校验** | 2d | 后端核心 | 1, 23 | `[Capability]` 特性 + CI 脚本校验"✅ 能力不含 TODO" |
| 25 | **`BizNamingRules` 黑名单与实体清单同源生成** | 1d | 后端核心 | 无 | 代码生成器或 Source Generator 同步实体清单 |
| 26 | **依赖集中清单 + SCA 扫描** | 2d | DevOps | 无 | `Directory.Packages.props` + GitHub Actions OWASP Dependency-Check |
| 27 | **前端树工具合并** | 1d | 前端框架 | 7 | `treeOps.ts` + `treeUtils.ts` → 统一 `treeKit.ts` |
| 28 | **框架层内部四工程准入清单写入架构文档** | 1d | 架构 | 8 | `01-架构总纲` 增补《框架层内部分层准入清单》 |
| 29 | **审计脱敏字段字典 + 单测** | 1.5d | 后端核心 | 10 | `SensitiveFields` HashSet + `SanitizeParams` 单测 |
| 30 | **退役 `YZH.Core.CodeGenerators` 与遗留元数据** | 1.5d | 后端核心 | 无 | 清理未引用文件 + 验证编译通过 |

---

## 四、并行线：Docker 部署安全清单

可与 P0/P1 同步推进，建议由运维/后端在每次发版前检查。

| # | 检查项 | 优先级 | 实施位置 | 产出 |
|:--:|--------|:------:|---------|------|
| D-1 | 敏感文件拦截（`appsettings.Production.json`、`*.key`、`logs/`） | P0 | `docker-compose.yml` + Dockerfile | Secrets 挂载 + 只读卷 |
| D-2 | Dockerfile 多阶段构建 + 非 root 用户 | P1 | `docker/Dockerfile` | 多阶段 Dockerfile + `USER appuser` |
| D-3 | docker-compose 环境变量注入密钥 | P0 | `docker-compose.yml` | `secrets:` 定义 + `environment:` 引用 |
| D-4 | 健康检查端点 | P1 | `docker-compose.yml` + 后端 | `/api/health` 端点 + `HEALTHCHECK` |
| D-5 | 运行时安全选项（`no-new-privileges:true`、`read_only`） | P1 | `docker-compose.yml` | `security_opt` + `read_only: true` |

---

## 五、并行线：文档修正

可与 P0/P1 同步，由架构负责人执笔。

| # | 修正项 | 位置 | 优先级 |
|:--:|--------|------|:------:|
| Doc-1 | 补充"开发期宿主"与"生产期宿主"区分说明 | `01-架构总纲` | P3 |
| Doc-2 | 将依赖方向问题从 P0 降为 P3（文档澄清） | `09-架构修复清单` | P3 |
| Doc-3 | 新增《数据安全权限实现规范》 | `10-YZH架构/` 新建 | P2 |
| Doc-4 | 新增《框架层内部分层准入清单》 | `01-架构总纲` 增补 | P2 |

---

## 六、风险与升级策略

| 风险场景 | 概率 | 应对 |
|---------|:----:|------|
| P0-1 权限校验实现与现有 `RoleController.check*` 冲突 | 中 | 先出设计评审稿，不直接改代码；`check/add`/`check/remove` 等自定义端点需单独测试 |
| P0-7 `YzhCrudPage` props 注入导致已有 4 页面回归失败 | 高 | 每次只改一个 props，保留默认值兜底；端到端测试覆盖 organization/role/role-menu/role-user |
| P1-14 OpenAPI 生成在 .NET 8 项目里配置复杂 | 中 | 若 Swashbuckle 集成成本 >2d，降级为"手写契约 + CI 文本比对" |
| P1-22 前端 38 页面接入率整改工作量大 | 高 | 分两波：先改 system/*（框架内部），再改 cert/*（业务）；未改完页面允许共存旧代码，但禁止新增旧模式页面 |
| 双表决策 (D-1) 长期悬而未决 | 中 | 设决策截止日期（建议 P0 结束前）；超期默认保留 `cert_sys_config`，`sys_config` 标注弃用 |

---

## 七、验收标准

### P0 验收

- [ ] `PermissionFilter.CheckPermission` 不再硬编码 `return true`；无角色用户访问 admin 接口返回 403
- [ ] CORS 预检请求来自未配置域名时返回 204 No Content（或浏览器拦截）
- [ ] 删除 `appsettings.json` 中 `YZH:EncryptionKey` 后启动抛异常，不兜底
- [ ] 传 `SortField=Code; DROP TABLE` 返回 400，不执行恶意 SQL
- [ ] 软删除与有效标志过滤只有一套实现，且无反引号硬编码
- [ ] `Sys_RoleUser`（无 Code 实体）可正常 Update/Delete
- [ ] `YzhCrudPage` 不直接 import `@share/*`；通过 `props.apiClient` 注入
- [ ] `YZH.Core.Web` 不再包含 System 域控制器；`dotnet build` 通过
- [ ] 全项目无 `import { http } from '@yzh-core/utils/http'`（除 deprecated 注释外）

### P1 验收

- [ ] `sys_log` 表有写入记录；审计参数中 `UserPwd` 显示为 `[REDACTED]`
- [ ] 字典页 `/items/{code}` 返回真实数据，非空数组
- [ ] 分页请求 `PageSize=999999` 返回 400
- [ ] 前端 `contracts.ts` 由 OpenAPI 生成，非手写
- [ ] `YzhAuditLogger` 使用 Channel，队列上限 1000
- [ ] 改密码后旧 Token 在 5 秒内失效
- [ ] 导出 10 万行数据不 OOM（流式或异步）
- [ ] 前端 38 页面中 ≥30 个继承 `CrudPageLogic` 或 `TreeTableLogic`

### P2 验收

- [ ] CI 中标记 ✅ 的能力其关键方法不含 `TODO`
- [ ] `dotnet list package --vulnerable` 无高危漏洞（或已记录 accepted risk）
- [ ] 审计脱敏字段单测通过
- [ ] `YZH.Core.CodeGenerators` 目录已删除，编译通过

---

## 八、附录：Issue 编号速查

| 文档原编号 | 本文档编号 | 章节 |
|-----------|:----------:|------|
| 问题 1 | #1 | P0 |
| 问题 2 | #1（数据范围） | P0 |
| 问题 3 | #2 | P0 |
| 问题 4 | #3 | P0 |
| 问题 5 | #4 | P0 |
| 问题 6 | #5 | P0 |
| 问题 7 | #6 | P0 |
| 问题 8 | #7 | P0 |
| 问题 9 | #8 | P0 |
| 问题 10 | #9 | P0 |
| 问题 11 | #29 | P2 |
| 问题 12 | #15 | P1 |
| 问题 13 | #30 | P2 |
| 问题 14 | #26 | P2 |
| 问题 15 | — | P2（已并入 JWT 统一配置，建议在 #3 中顺带） |
| 问题 16 | #24 | P2 |
| 问题 17 | — | P2（已并入 #22） |
| 问题 18 | #23 | P1 |
| 问题 19 | #17 | P1 |
| 问题 20 | #18 | P1 |
| 问题 21 | #20 | P1 |
| 问题 22 | #21 | P1 |

---

*本计划按"安全阻断优先、结构解耦次之、能力落地再次、机制化最后"排序。*
*所有代码改动须遵循 `项目全局规则.md` 与 `docs/10-YZH架构/` 规范，禁止修改 `src/old/`。*
