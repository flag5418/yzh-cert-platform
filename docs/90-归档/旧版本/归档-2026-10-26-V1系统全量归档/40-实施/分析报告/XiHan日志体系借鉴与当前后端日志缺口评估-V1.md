# XiHan.Framework 日志体系借鉴与当前后端日志缺口评估 — V1

> 评估范围：XiHan.Framework.Logging / XiHan.Framework.Auditing / XiHan.Framework.Observability 与 Web.Api 层的设计模式，结合当前 `YZH.Core.Web` + `CertPlatform.*` 后端现状，给出可落地的借鉴方案与裁决清单。

---

## 0. 结论

**整体不成立：当前后端「有审计日志，无运行日志」。**

用户观察到「没有独立的 logs 文件夹」是一个事实偏差：仓库里确实有一个 `src/yzh-core/YZH.Core.Web/Logs/Audit/` 目录（54 MB、38 个文件）。但真正的运行日志——尤其是异常堆栈、`ILogger` 全量输出——当前**只写 `/tmp/vol_backend_9992.log`**，而启动脚本每次以 `'w'` 截断打开该文件，**意味着每次重启后端，此前全部运行日志被彻底清零**。

---

## 1. 当前后端现状（逐条带证据）

### 1.1 三处日志出口

| 出口 | 内容 | 位置 | 持久性 |
| --- | --- | --- | --- |
| 控制台（运行日志） | ILogger 全量、异常堆栈、框架内部 MVC 日志 | `/tmp/vol_backend_9992.log` | ❌ 每次重启截断 |
| 审计文本文件 | 审计条目（用户+操作+耗时+IP） | `YZH.Core.Web/Logs/Audit/YYYY-MM-DD.log` | ✅ 按天保留，但无上限 |
| 数据库审计表 | 同上，供前端只读表格 | `sys_log` | ✅ 持久 |

### 1.2 关键缺陷（证据引用）

| # | 缺陷 | 证据 | 后果 |
| --- | --- | --- | --- |
| D1 | ~~**重启即丢运行日志**~~ ✅ 2026-10-17 已修 | 原 `run-backend.sh` 中 `log = open('$LOG_FILE', 'w')`（当时实际在 :117，非 :70） | 500 异常一旦发生且后端被重启排障，堆栈永远丢失 |
| D2 | **异常堆栈不落盘** | `GlobalExceptionFilter.cs:75` 仅 `_logger.LogError(...)` → 控制台；`SysLogDbWriter.BuildDetail()` 只拼接 `ex.Message` | 系统异常无法在审计表里还原现场 |
| D3 | **审计时间戳为 UTC** | `YzhAuditLogger.cs:46/64/88` `Timestamp = DateTime.UtcNow`；`:177` 文件名也用 UtcNow | 0:00–8:00 的日志写入「前一天」的文件，按日期检索漏数 |
| D4 | **审计格式不可检索** | 每条记录占用 8–12 行文本块（`FormatLogEntry`） | `grep` 无法单行匹配同一请求，awk 不可用 |
| D5 | **双消费者竞争队列** | `ExecuteAsync` 与 `EnqueueAndTryFlush→FlushQueueAsync` 同时 `TryDequeue` | 日志顺序不保证；并发量高时重复/丢失概率上升 |
| D6 | **请求参数无用** | `YzhAuditingFilter.cs:135` `SerializeParams` 对复杂对象用 `kvp.Value?.ToString()` | 对象参数落库为类型名，审计时无法还原入参 |
| D7 | **无 TraceId** | `sys_log` 无 TraceId 列；`appsettings.json` 无链路配置 | 前端报错、后端日志、数据库审计三处无法串成一根线 |
| D8 | **无 SQL 日志** | `SqlSugarDbOrm` 未启用 `Aop.OnLogExecuting` | 慢查询、死锁、SQL 异常时无法拿到最终 SQL |
| D9 | **无日志保留策略** | 当前 `Logs/Audit` 目录 54 MB、38 个文件线性增长，无按大小/数量清理 | 磁盘无限膨胀 |
| D10 | **控制台噪声极高** | `appsettings.json` 默认 `Microsoft.AspNetCore: Warning`，但 MVC 内部每条请求仍打印 7–9 行 | 7 小时已积累 24,491 行、2.4 MB |

---

## 2. XiHan 可借鉴的设计与对照

XiHan.Framework 的日志/审计/可观测性分层清晰，以下按「可直接借鉴」「需改造后借鉴」「不建议照搬」三档分类。

### 2.1 直接借鉴（设计模式，不改代码）

| XiHan 设计 | 当前对应 | 借鉴价值 |
| --- | --- | --- |
| **运行日志 ≠ 审计日志**（`docs/guide/logging.md` §1） | 当前审计与运行混在一个过滤器里，且审计文本格式像「美化版日志」 | 明确分层：运行日志用单行文本/结构化日志排障；审计日志用表追责。两条路互不相干。 |
| **TraceId 贯穿**（`XiHanTraceIdMiddleware`） | 无 | 把 W3C `Activity.TraceId` 写入响应头 `X-Trace-Id`，同时回写到 `HttpContext.Items`；日志模板带 `[TraceId]`。 |
| **异常日志独立模型**（`ExceptionLogRecord`） | `sys_log` 里只有 `ErrorMessage`（单字段） | 建议新建异常记录（或扩展表），必须含 `ExceptionType`、`ExceptionMessage`、`ExceptionStackTrace`、`RequestParams`、`RequestHeaders`、`TraceId`。 |
| **敏感数据脱敏前移**（`LogSanitizer`） | `YzhAuditingFilter` 有白名单，但只在审计侧 | 建立统一的「日志脱敏器」：在写日志/写审计前统一 mask，键名归一化（`api_key`→`xapikey`）并设元数据后缀豁免（`TokenType` 不脱敏）。 |
| **按级别分文件** | 审计全塞一个目录 | 建议至少两路：`app-YYYYMMDD.log`（Information+）与 `error-YYYYMMDD.log`（Warning+），排障时直接看 error 文件。 |
| **队列化落库**（`ILogQueue<T>` + Worker） | 审计用 `ConcurrentQueue` 但双消费者竞争 | 用有界 Channel（`Channel.CreateBounded`）+ 单 Worker（HostedService）攒批写入，配 `DropOnFull` 或 `BlockWhenFull` 策略。 |
| **请求日志降噪**（`IgnoredPathPrefixes`） | 无 | `/swagger`、`/favicon.ico`、`/hubs` 等默认跳过，减少无效行数。 |

### 2.2 需改造后借鉴

| XiHan 设计 | 注意点 |
| --- | --- |
| **Serilog 管道**（`AddXiHanSerilog`） | XiHan 目标框架为 `net10.0`，直接引用其 NuGet 包存在兼容性风险；建议改用 `Serilog.AspNetCore 8.0.3`（`net8.0` 线）自建管道。XiHan 文档自身承认有 7 个配置项「声明了但不生效」（`XiHanLoggingOptions` 的 `ContextProperties` / `Filters` / `EnableRequestLogging` / `RequestLoggingExcludePaths`，加 `XiHanFileLoggerOptions` 的 `BufferSize` / `FlushPeriod` / `EnableAsyncWrite`），应避免重蹈覆辙。 |
| **结构化日志模板** | 要求开发侧把 `_logger.LogInfo($"...")` 改为 `_logger.LogInfo("... {OrderId}", orderId)`；现有代码里插值写法极少（已统计 0 条），改造量低。 |
| **OpenTelemetry** | XiHan 的链路追踪默认关闭，且需要独立采集后端（Jaeger/Tempo）。当前项目为单体单租户，**不建议现在引入 OTel**。 |
| **六类审计记录**（Access/Operation/Exception/Api/Login/Diff） | 建议当前只保留 `Operation` + `Exception` 两类；`Login` 已有认证端点可补；`EntityDiff` 依赖 ORM AOP，现阶段投入产出比低。 |

### 2.3 不建议照搬

- **XiHan 的 `IXiHanLogger`**：它固定使用 `"{Message}{Args}"` 模板，不绑定调用方占位符，功能反而弱于原生 `ILogger<T>`。
- **XiHan 的 `XiHanFileLoggerProvider`**：文档写明它「每条日志同步 `File.AppendAllText`，既无缓冲也无异步」，性能差于 Serilog 文件 Sink。
- **ABP 式模块系统**（`XiHanModule` / `DependsOn`）：当前项目不使用 ABP，引入模块生命周期等于重写启动流程，与 `UseYzhCore` 冲突。

---

## 3. 目标态最小设计

> 以下目标态为「最小可用」口径，不引入 ABP、OTel、多租户，仅增加 Serilog 与少量基础设施。

### 3.1 目录与文件约定

```
src/yzh-core/YZH.Core.Web/
└── Logs/
    ├── app-20261017.log      ← Serilog 运行日志（全量，按日滚动）
    ├── error-20261017.log    ← Serilog 错误日志（Warning+，按日滚动）
    └── audit-20261017.log    ← 审计日志（改造后，单行 JSON/TSV）
```

- 保留策略：
  - `app-*.log`：保留 31 天，单文件 100 MB 上限。
  - `error-*.log`：保留 90 天（异常现场价值更高）。
  - `audit-*.log`：保留 31 天，单文件 50 MB 上限。
- `.gitignore` 已包含 `logs/`，符合既有约定。

### 3.2 日志行格式（统一单行，可 grep）

```
2026-10-17 15:46:42.123 [INF] [a1b2c3d4e5f6...] YZH.Core.Api.Filters.YzhAuditingFilter: [Audit] 开始执行 {Action} | User={UserCode} | IP={IpAddress}
```

- 时间戳：本地时间（`DateTime.Now`，符合 铁律十）。
- 级别：3 位缩写（`INF` / `WRN` / `ERR` / `FTL`）。
- TraceId：16 位 W3C trace id 截取，空时渲染 `[000...]`。
- SourceContext：类全名，便于按命名空间过滤。

### 3.3 配置节

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.AspNetCore.Mvc.Infrastructure": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Async",
        "Args": {
          "configure": [
            {
              "Name": "File",
              "Args": {
                "path": "Logs/app-.log",
                "rollingInterval": "Day",
                "retainedFileCountLimit": 31,
                "fileSizeLimitBytes": 104857600,
                "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{TraceId}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
              }
            }
          ]
        }
      },
      {
        "Name": "Async",
        "Args": {
          "configure": [
            {
              "Name": "File",
              "Args": {
                "path": "Logs/error-.log",
                "rollingInterval": "Day",
                "retainedFileCountLimit": 90,
                "restrictedToMinimumLevel": "Warning",
                "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{TraceId}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
              }
            }
          ]
        }
      }
    ],
    "Enrich": [ "FromLogContext" ]
  }
}
```

### 3.4 审计侧改造要点

1. **新增列（或新建 `sys_log_exception` 表）**：
   - `TraceId`（`varchar(32)`）
   - `ExceptionType`（`varchar(200)`）
   - `ExceptionStackTrace`（`text`）
   - `RequestParams`（`text`，脱敏后）
   - `RequestPath`（`varchar(200)`）
2. **时间戳**：`CreateTime` 已用 `DateTime.Now`（`BaseEntity` 默认）；`AuditLogEntry.Timestamp` 应同步改为 `DateTime.Now`。
3. **参数序列化**：把 `kvp.Value?.ToString()` 改为 `System.Text.Json.JsonSerializer.Serialize(kvp.Value)`，上限 4 KB，超限截断并标记 `[truncated]`。
4. **单行格式**：`FormatLogEntry` 改为 JSON 单行，字段名用 PascalCase，与 DB 列名对齐。
5. **队列修复**：去掉 `EnqueueAndTryFlush` 中的 `Task.Run(FlushQueueAsync)`，只保留后台 `ExecuteAsync` 单消费者，避免双消费者竞争。

---

## 4. 待裁决事项（需明确后进入实施）

| 编号 | 裁决项 | 倾向建议 | 影响范围 |
| --- | --- | --- | --- |
| R1 | **是否引入 Serilog**（新包依赖）？ | ✅ 建议引入 `Serilog.AspNetCore 8.0.3` + `Serilog.Sinks.Async 1.5.0`（`net8.0` 兼容） | `YZH.Core.Web.csproj`、启动配置 |
| R2 | **日志根目录**放哪？ | 建议仍放 `YZH.Core.Web/Logs/`（已有，`.gitignore` 已覆盖） | 部署脚本、日志收集脚本 |
| R3 | **异常信息**扩展现有 `sys_log` 还是新建 `sys_log_exception`？ | 倾向**扩展 `sys_log`**：加 `TraceId/ExceptionType/StackTrace/RequestParams/RequestPath` 五列 | DB 迁移脚本、EntityConfig、前端页 |
| R4 | **日志保留天数**？ | `app` 31 天 / `error` 90 天 / `audit` 31 天 | 磁盘容量、运维习惯 |
| R5 | **是否启用 SQL 慢查询日志**？ | 建议 P2 阶段再开，阈值暂设 500 ms | `SqlSugarDbOrm` AOP、新增配置节 |
| R6 | **是否把审计日志队列化**（有界 Channel + Worker）？ | P2 阶段再做；当前 `ConcurrentQueue` 够用，先修双消费者 | 并发量上限、丢日志策略 |
| R7 | **前端日志页**是否支持 TraceId 检索 + 错误筛选？ | P2 阶段做；P0 阶段先保证后端日志可 `grep` | 前端工作量 |

---

## 5. 实施分期（对应 L1–L5 分级）

### 阶段 0：救急（≤ 15 min，今天可做）✅ 已完成 2026-10-17

- `run-backend.sh`：`LOG_FILE` 由 `/tmp/vol_backend_9992.log` 改为 `src/yzh-core/YZH.Core.Web/Logs/backend-YYYYMMDD.log`，打开模式 `'w'` → `'a'`。
- 收益：重启不再丢日志；日志留在内容根目录（`dotnet clean` 不影响），按天分文件；`status` 子命令直接打印当日日志路径。
- 验证：`bash -n` 通过；`./run-backend.sh status` 解析出的路径为 `.../YZH.Core.Web/Logs/backend-20261017.log`。当前运行中的实例是改动前启动的，仍写 `/tmp`，下次重启生效。

### 阶段 1：P0 核心（≤ 2 h）

1. 引入 Serilog 包，配 `app-.log` + `error-.log` 双路异步文件 Sink。
2. `GlobalExceptionFilter` 在写 `ILogger` 的同时，把异常详情写进 `error-.log`（Serilog 自动处理）。
3. 配置 `MinimumLevel.Override` 把 `Microsoft.AspNetCore.Mvc.Infrastructure` 降到 `Warning`，削减 60%+ 控制台噪声。
4. 改造 `YzhAuditLogger`：去掉双消费者；`DateTime.UtcNow` → `DateTime.Now`；文件名用 `DateTime.Now:yyyy-MM-dd`。

### 阶段 2：P1 结构化（≤ 4 h）

1. 新增 `TraceIdMiddleware`：基于 `Activity.Current?.TraceId` 或 `Guid.NewGuid()`，写入响应头 `X-Trace-Id`。
2. `sys_log` 扩列（R3），`SysLogDbWriter` 写入新增列。
3. `YzhAuditingFilter.SerializeParams` 改为 JSON 序列化 + 截断 + 脱敏增强。
4. 统一日志脱敏器：建立 `LogSanitizer`（对标 XiHan），键名归一化 + 元数据后缀豁免。

### 阶段 3：P2 增强（可排期）

1. 审计落库队列化（有界 Channel + Worker + DropOnFull）。
2. SQL 慢查询 AOP（阈值 500 ms，落 `sql-slow-.log`）。
3. 前端日志页支持按 `TraceId`、`Level`、`时间范围` 检索。

---

## 6. 已知坑（实施前必读）

| # | 坑 | 规避方式 |
| --- | --- | --- |
| K1 | 日志目录若在 `bin/Debug/net8.0/Logs` 下，`dotnet clean` 会清空 | 写 `ContentRootPath/Logs`，不要写 `AppContext.BaseDirectory` 子目录 |
| K2 | `AddSerilog` 默认 `writeToProviders: false`，已有自定义 `ILoggerProvider` 会静默失效 | 若保留 `YzhAuditLogger` 的 `ILoggerProvider` 语义，需显式设 `writeToProviders: true`（但建议直接废弃它，改走 Serilog） |
| K3 | `AuditLogEntry.Timestamp = DateTime.Now` 后，与现有 `DateTime.UtcNow` 文件重名不冲突（同一天），但 0–8 点的历史 UTC 文件与新本地文件在跨日边界可能重复 | 首次改造时把旧 `Logs/Audit/*.log` 整体移入 `Logs/Audit/legacy/`，新文件从头开始 |
| K4 | `SerializeParams` 改用 JSON 后，可能出现循环引用或流对象不可序列化 | 用 `JsonSerializerOptions { ReferenceHandler = IgnoreCycles, MaxDepth = 5 }`，异常时 fallback 为 `[序列化失败]` |
| K5 | 脱敏规则「宁可多掩」会误伤审计价值（如 `TokenType` 被掩成 `***`） | 必须引入 XiHan 式的元数据后缀白名单（`time`/`type`/`lifetime`/`algorithm` 等不脱敏） |
| K6 | Serilog 文件 Sink 在 Linux/macOS 上默认无文件锁；并发多进程写同一文件会乱行 | 当前单体单进程，无风险；若未来水平扩展，需切到按进程 PID 分文件或集中日志后端 |
| K7 | `logs/` 在 `.gitignore` 第 107 行，但 macOS `Logs/` 大小写不敏感已匹配；新建 `Logs/app-.log` 不会进 git | 无需额外改动，但须确认 CI 构建脚本不把 `Logs/` 目录打包进产物 |

---

## 7. 复核记录（2026-10-17 二次核对）

| 项 | 复核结果 |
| --- | --- |
| `Logs/Audit` 体量 | 54 MB / 38 个文件，最大单日 2.5 MB（2026-09-23），无清理策略 |
| 运行日志出口 | 仅控制台 → `/tmp/vol_backend_9992.log`（改前每次启动 `'w'` 截断） |
| TraceId | `src/yzh-core` + `src/certplatform-api` 全仓 0 处 `Activity.Current` / `TraceIdentifier` / `X-Trace-Id` ⇒ 文件日志、`sys_log`、前端报错三者无法关联 |
| `sys_log` 结构 | 仅 `Module/Action/ApiPath/TargetType/Detail(json)/IpAddress/UserAgent`；错误只进 `Detail` 的一句话，无 `Level`/`ExceptionType`/`StackTrace`/`TraceId` 列 |
| 前端日志页 | `yzh.vue.core/src/pages/system/log/` 为只读表格（后端关闭增删改），无按级别/TraceId 检索 |
| 审计文本可读性 | 每条记录 8–12 行文本块（`FormatLogEntry`），`grep`/`awk` 无法按请求聚合 |

## 8. 评审意见与实施前硬约束（2026-10-17）

> 本节为对本文前七章的第二轮评审结论。前七章的问题诊断成立、方案方向正确，但**以下 6 条不改就实施，会分别导致泄密、盲区、返工**。

| 编号 | 硬约束 | 为什么必须补 |
| --- | --- | --- |
| B1 | **§3.4.3 的「JSON 序列化」在改脱敏之前不可实施** | `YzhAuditingFilter.SerializeParams` 的脱敏是**参数名**白名单（`SensitiveFields` 命中 `ActionArguments` 的 key）。`AuthController.Login([FromBody] LoginRequest req)` 的 key 是 `req`，DTO 内的 `Password` 永远不在白名单里。当前靠 `kvp.Value?.ToString()` 输出「类型名」**偶然**避开了泄露；一旦按本文改成 JSON 序列化，**密码会明文写入 `sys_log.Detail` 与审计文件**。必须先做对象图深度脱敏（键名归一化 + `TokenType` 一类元数据后缀白名单），再改序列化。 |
| B2 | **非 MVC 异常必须单独收口** | `GlobalExceptionFilter` 只覆盖 Controller 执行链。`QueueManager`（任务执行）、`ExpertTaskQueueRunner`、Office 转换、`ApiSyncHostedService` 抛出的异常只进 `ILogger` ⇒ 控制台；没有 500、没有 `sys_log` 行。这类后台静默失败正是「本地日志」最该覆盖的场景，本文完全未提。需补 `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`，并将 `HostOptions.BackgroundServiceExceptionBehavior` 显式设为 `Ignore` 并记录。 |
| B3 | **启动期日志用 bootstrap logger** | DI / 数据库连接 / EntityConfig 路径配错时进程在 `builder.Build()` 前就退出。阶段 1 的 `AddSerilog` 装配在 host 构建期，**捕获不到**这段。必须先用独立 logger 写启动日志（对标 XiHan 的 `XiHanLoggerBuilder` 思路，自建约 20 行即可）。 |
| B4 | **框架层改造准入未登记** | 日志装配落在 `src/yzh-core`（框架层），按 `AGENTS.md` 第 11 条必须同时满足：有明确理由、不破坏既有契约、优先走扩展点、在 `.workbuddy-ai/memory/REFERENCE.md` 登记、改完回归（后端 0 错误 + 服务能起 + `guards.mjs`）。另：改 `Timestamp` 后须跑 `scripts/backend/check-utcnow.sh`（铁律十），且 **`DurationMs` 的 `UtcNow` 差值属于「耗时统计」合法场景，不得一刀切改成 `Now`**。 |
| B5 | **不要再加 `RequestParams` 列；`Detail` 也装不下堆栈** | 实测 `sys_log.Detail` 是 **`varchar(2000)`**（仓库 `scripts/db/all_tables_ddl.sql` 写的 `json` 已过期），当前存的是 `用户=… | 参数={} | 耗时=3ms` 这类文本。§3.4.1 再加 `RequestParams` 是**同义双列**（铁律九/⑦ 反复清理的形态）；同时 2000 字符装不下 StackTrace，必须新增 `StackTrace mediumtext`。结论：只加 `TraceId varchar(32)` / `ExceptionType varchar(200)` / `StackTrace mediumtext` 三列，参数继续进 `Detail`（扩到 `text`）。 |
| B6 | **缺验收矩阵** | 本文只有方案没有可执行判据。最小验收：① 故意抛 500 ⇒ `Logs/error-*.log` 有完整堆栈；② 响应头 `X-Trace-Id` 与 `sys_log.TraceId`、日志行 `[TraceId]` 三处同值；③ 断开 MySQL 启动 ⇒ 启动日志有明确失败原因；④ 重启后端 ⇒ 上次日志仍在（已由阶段 0 满足）。 |

**优先级重排（评审后）**：阶段 0（已完）→ 修 `YzhAuditLogger` 双消费者 + 本地时间 → B2/B3（盲区收口）→ `TraceId` + `sys_log` 三列（B5）→ 最后才引 Serilog。**Serilog 是体验优化，B2/B3 是「能不能查到」的问题**，后者不可延后。

## 9. 参考索引

- XiHan 日志设计：`/Volumes/Expand/wangqingquan/Desktop/XiHan.Framework/docs/guide/logging.md`
- XiHan 审计设计：`/Volumes/Expand/wangqingquan/Desktop/XiHan.Framework/docs/guide/auditing.md`
- 当前审计实现：`src/yzh-core/YZH.Core.Api/Services/YzhAuditLogger.cs`
- 当前异常过滤器：`src/yzh-core/YZH.Core.Api/Filters/GlobalExceptionFilter.cs`
- 当前审计过滤器：`src/yzh-core/YZH.Core.Api/Filters/YzhAuditingFilter.cs`
- 启动脚本：`scripts/backend/run-backend.sh`
- 排查坑铁律：`docs/00-工程体系/运行期排查坑与沙箱铁律-V1.md`
