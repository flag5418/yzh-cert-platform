# YZH 架构评估与完善建议 V1

> **评估日期**：2026-09-23
> **评估对象**：`src/yzh-core/`（框架层）+ `src/certplatform-api/`（新后端）+ `src/certplatform-web/`（新前端）
> **评估动机**：用户 2026-09-03 起重构架构，目标是**多角色多用户管理**，核心诉求是「**让代码受架构的制约，不允许 AI 随意发挥**」
> **方法**：① 提取 10 份架构文档的全部规范性条文（70 条）；② 逐条到源码核验；③ 对可运行时验证的条目做 HTTP 实测
> **证据口径**：**只采信源码与实测**。文档自述一律视为「待验证」，本报告已逐条核验，与文档冲突处均以实测为准并标注。

---

## 零、结论先行

### 一句话结论

> **骨架立起来了，但约束没有通电。**
>
> 架构的**设计**（基类体系、数据契约、三层同构、权限模型、ApiCode 机制）是完整且自洽的；
> 但架构的**约束力**——即"违反就报错/拦住"的那部分——**在后端几乎为零，在前端存在但未接线**。
>
> 当前架构能**引导** AI 按规范写代码，但**拦不住** AI 不按规范写代码。

### 三档评级

| 维度 | 评级 | 依据 |
|---|---|---|
| 架构**设计**完成度 | 🟢 **约 85%** | 五原则清晰；基类/契约/权限模型/代码结构规范齐全；文档量 34 份 |
| 架构**落地**完成度 | 🟡 **约 60%** | 后端基类覆盖 13/23 控制器；前端 Core 基类覆盖 14/27 页面；PascalCase 契约已统一 |
| 架构**约束力**（能否拦住违规） | 🔴 **约 15%** | 后端：无 .editorconfig、无分析器、无 CI、无启动自检（唯一自检类未接线）；前端：守卫脚本存在但未接门禁且不覆盖专家端 |

### 是否可继续推进专家系统

**可以继续，但建议先完成 P0-1 ~ P0-5（预计 1 个工作日）。**

理由：专家系统（`cert-auditor`）是**多角色多用户**的第一批真实负载，恰好踩在架构当前最薄弱的三处——**接口授权、SSO 挤号、前端守卫覆盖范围**。若此时继续开发，会以"专家端"的名义把这三个洞固化进新代码，之后再补成本翻倍。

**反向判断**：若只做"专家注册 + 专家登录"两件事（当前已完成并实测通过），不触碰权限敏感面，**也可以先推进**。但**一键 NC、报告生成**这类涉及跨机构数据的功能，必须在 P0 补齐后再做。

---

## 一、评估范围与方法

### 1.1 提取到的规范性条文

从 10 份架构文档中提取 **70 条**规范性条文（编号 C01–C70），按层分布：

| 层 | 条文数 |
|---|---|
| 后端架构 | 11 |
| 前端架构 | 6 |
| 数据契约 | 11 |
| 权限体系 | 9 |
| 代码结构 | 8 |
| 开发流程 | 7 |
| 框架能力边界 | 15 |
| 流程/入口 | 3 |

### 1.2 条文可校验性统计

| 可校验性 | 条文数 | 占比 | 现实状态 |
|---|---|---|---|
| 文档称"可自动校验" | 45 | 64% | ⚠️ **其中 41 条实际无任何校验机制存在** |
| 文档称"人工约定" | 25 | 36% | 与文档一致 |

> **这是本次评估最核心的发现**：文档为 64% 的条文标注了"可自动校验"（AST 扫描 / ESLint / 启动期校验 / 守卫脚本 / 集成测试），但**真正存在的校验机制只有 4 个**：
> ① `tsconfig` 的 `strict` 家族；② `guards.mjs`（4 条规则，仅扫 cert-admin）；③ `BizNamingRules`（**未接线**）；④ TypeScript 编译期泛型约束。
>
> 其余全部是**纸面校验**。

### 1.3 文档自身缺陷（影响评估可信度）

| 缺陷 | 表现 |
|---|---|
| 同目录文档互相矛盾 | `06-代码结构规范.md` §二 称重命名"尚未完成"，§9.2 称"已完成" |
| 声称存在的类不存在 | `05-权限体系.md` §2.2 的 `RoleIds.cs`（`YZH.Core.Stand/Constants/`）**全仓不存在**，该节示例代码不可编译 |
| 声称生效的机制未生效 | `12-框架能力清单-V1.md` §4.1 称 `BizNamingRules` 启动期校验"已生效" → **实际 `Program.cs` 零引用** |
| 声称不存在的表已存在 | `12` §3.2 称 `sys_api`/`sys_role_api`/`sys_user_permission` "三表未建" → **实际 399 / 90 / 存在** |
| 编号重复 | `09-架构修复清单.md` §五验收表中 P0-003 / P0-004 各出现两次 |
| 基类命名漂移 | 文档写 `CrudPageLogic<V>`；**实际实现是 `SingleTableCore<V>`**，`CrudPageLogic` 全仓不存在 |

**结论：文档不能作为事实依据。本次评估全部结论均基于源码与实测。**

---

## 二、设计意图 → 落地对照

按主题归并 70 条条文，给出落地状态。状态定义：
🟢 已落地且有强制 | 🟡 已落地但无强制 | 🔴 未落地 / 已失效

| # | 设计意图 | 条文 | 状态 | 实测证据 |
|---|---|---|---|---|
| T1 | 业务实体必须继承 `BaseEntity` | C01 | 🟢 | 61 个实体均继承；编译期约束 |
| T2 | 控制器必须继承 `YzhControllerBase<V>` / `TreeTableControllerBase<T,V>` | C49 | 🔴 | **23 个业务控制器中 10 个直接继承 `ControllerBase`** |
| T3 | 数据契约 PascalCase 逐字一致（铁律七） | C18/C54 | 🟡 | 后端 `PropertyNamingPolicy = null` ✅；前端 Logic 层已 PascalCase ✅；但无 ESLint 正则守卫 |
| T4 | EntityConfig JSON 只允许 5 个键 | C20/C21/C22 | 🟡 | 无 JSON schema 校验，纯约定 |
| T5 | 默认软删除，硬删除需显式特性 | C09 | 🟡 | `[YZHDeleteStrategy]` 机制存在，无校验 |
| T6 | 树基类必须配置 `TreeConfig` 四字段 | C05 | 🟡 | 无运行时断言 |
| T7 | 前端 Logic 必须继承 `CrudPageLogic`/`TreeTableLogic` | C12 | 🔴 | 文档基类名**不存在**；实际基类 `SingleTableCore`/`TreeTableCore`；27 页中 8 页纯手写 |
| T8 | `TreeNode` 不得混入 UI 状态 | C16 | 🟡 | 有类型定义，无强制 |
| T9 | 三层同构（Controller = API = Pages 同名同路径） | C39/C40/C41/C43 | 🟡 | 有规范文档与 4 条 shell 检查（未接线） |
| T10 | **接口权限（第 2 层）按 `sys_role_api` 校验** | C29–C32 | 🔴 | **`[RequirePermission]` 全库标注数 = 0** → 校验器从未被调用 |
| T11 | 生产模式 `RequireAuth=true` 使 `[YZHAnonymous]` 失效 | C34 | 🔴 | **`AuthSettings` 配置段全仓不存在** → 兜底恒失效 |
| T12 | SSO 挤号：新登录使旧 Token 失效 | C35 | 🔴 | **`YzhAuthFilter` 从未挂载** → 实测旧 Token 仍返回 200 |
| T13 | Token 剩余 <30min 自动续租，写 `X-New-Token` | C36 | 🔴 | 同上，续租代码从未执行 |
| T14 | 所有 UPDATE/DELETE 必须写审计日志 | C33 | 🟡 | `YzhAuditingFilter` 已全局注册；落库链路待单独验证 |
| T15 | 业务层禁止复用框架实体黑名单类名 | C53 | 🔴 | `BizNamingRules` 存在但**零调用** |
| T16 | 批量作业统一走队列，禁止 `Task.Run` | C56 | 🟡 | 无 AST 扫描 |
| T17 | 禁止 `getPageData` / 裸数组 / 手写 `{code,data}` 包装 | C47/C68 | 🟢 | `guards.mjs` R3/R3b 已固化（仅 cert-admin） |
| T18 | 原子组件零领域依赖 | C48 | 🟢 | `guards.mjs` R1 已固化 |
| T19 | `Code` 替代 `Id` 作业务唯一标识 | C55 | 🟡 | 部分页面仍用 `User_Id` 作主键（文档 `12` §5.3 自述） |
| T20 | 判断角色查 `Sys_RoleUser`，不读 `Sys_User.Role_Id` | C61 | 🟡 | 无 AST 扫描 |
| T21 | 旧项目 `src/old/` 禁止修改 | C69 | 🟡 | 无 CI 路径白名单 |
| T22 | 新增模块必须三层同步创建 | C38 | 🟡 | 纯人工 |

**统计**：🟢 3 项 / 🟡 13 项 / 🔴 6 项。**🔴 的 6 项全部集中在"权限与认证"+"基类强制"这两个最关键面。**

---

## 三、★ 约束失效点清单

按**危害性质**分类。这是本报告最重要的部分。

### 3.1 静默失效（不报错、无日志、测试看不出来）—— 最高危

#### 【P0-1】接口级授权（第 2 层权限）在全站未启用

**机制现状**：
- `PermissionService.HasPermissionAsync`（85 行）**实现完整且正确**：查用户直接权限（`sys_user_permission`）→ 查角色权限（`sys_role_api`）→ 兜底拒绝；异常时"安全优先"返回 false
- 数据齐备：`sys_api` **399** 行、`sys_role_api` **90** 行
- `PermissionFilter` 已全局注册（`Program.cs:82`）

**断裂点**：`PermissionFilter.cs:70`
```csharp
var requirePermission = context.ActionDescriptor.GetMethodAttribute<RequirePermissionAttribute>();
if (requirePermission != null)   // ← 全库没有任何端点标注该特性 → 恒 false
{
    var hasPermission = await CheckPermissionAsync(_userContext, requirePermission.PermissionCode);
    ...
}
```

**`[RequirePermission]` 全代码库标注数 = 0**（仅存在于 `PermissionFilter.cs` 自身定义与注释）。

**实测证据**（用已注册的 `ROLE_AUDIT_CLIENT_ADMIN` 账号，Token 625 字符）：

| 接口 | 该账号结果 | 应为 |
|---|---|---|
| `POST /api/System/Role/filter` | **[200]** `{"success":true,...}` | 403 |
| `POST /api/System/User/filter` | **[200] 返回全部用户，含 `UserPwd` 密文 `j79rYYvCz4vdhcboB1Ausg==`** | 403 |
| `GET /api/System/MenuManagement/tree/all` | **[200]** | 403 |
| `POST /api/Foundation/ISOStandard/filter` | [200] | 200（该接口有授权） |

**对照实验**：无 Token 调同批接口 → 全部 **[401]**。→ **认证生效，授权失效。**

> ⚠️ **`UserPwd` 明文（密文）外泄是独立的第二个问题**：即使授权修好，任何有 `System/User` 读权限的角色都能拿到全部密码哈希。建议在 `Sys_User` 实体或 EntityConfig 层对 `UserPwd` 做**输出脱敏**，而非仅靠权限。

**历史对照**：`09-架构修复清单.md` 的 P0-003 记录的是"`CheckPermission()` 空实现 `return true`"。**现状是"换了一种方式未修复"**——检查器写好了，**调用点没建**。文档标注"未修复"是准确的，但原因已经变了，容易误导后来者以为"只差实现"。

**影响**：任何登录用户（包括专家端注册的普通用户）可调用全部管理端接口，包括用户管理、角色管理、菜单管理、机构管理。**这是当前架构最严重的安全缺口。**

---

#### 【P0-2】SSO 挤号 + Token 续租完全未执行

**机制现状**：
- `TokenVersionService.ValidateVersionAsync` **实现完整**（Redis 无记录 → false；版本不一致 → false）
- `YzhAuthFilter`（115 行）**实现完整**：SSO 校验 + 续租 + 写 `X-New-Token`

**断裂点**：`YzhAuthFilter` **全仓仅 2 处引用**：
```
src/yzh-core/YZH.Core.Api/Filters/YzhAuthFilter.cs:20   ← 类声明
src/yzh-core/YZH.Core.Web/YzhWebBuilder.cs:162          ← AddScoped<YzhAuthFilter>() DI 注册
```
**它从未被挂载到任何过滤器管道**：
- `Program.cs:79-84` 的 `options.Filters.Add` 只有三条：`GlobalExceptionFilter` / `PermissionFilter` / `YzhAuditingFilter` —— **不含 `YzhAuthFilter`**
- `YZHAuthorizeAttribute`（26 行）只设 `AuthenticationSchemes` + `Policy`，**没有 `TypeFilter` / `ServiceFilter`**
- 无全局 `AuthorizeFilter`、无 `FallbackPolicy`

**实测证据**：
```
admin 登录 → Token A（548 字符）
admin 再次登录 → Token B（548 字符，与 A 不同）
用旧 Token A 请求 POST /api/System/User/filter → [200]  ← 挤号未生效
```

**连带后果**：
1. **SSO 挤号失效** —— 重新登录后旧 Token 依然有效，无法踢下线
2. **Token 续租失效** —— 响应头永远没有 `X-New-Token`；用户将在 Token 到期（`ExpirationMinutes = 43200` = 30 天）后被强制登出，且前端"无感续租"逻辑（`05-权限体系.md` §7.1）永不触发
3. **`[YZHAuthorize]` 目前买不到任何东西** —— 认证由 `PermissionFilter` 兜底（见 §3.2），所以 `[YZHAuthorize]` 与"裸 `ControllerBase` + 全局兜底"在**认证行为上完全等价**。它现在唯一的价值是"未来挂载 `YzhAuthFilter` 后的 SSO 能力"

> **记忆修正**：本报告推翻了此前"`TokenVersionService` 只写不校验"的判断。**实现是对的，接线是断的。** 这类"实现完整但未接线"的缺陷比"未实现"更隐蔽——代码审查时看到实现完整会误判为已生效。

---

#### 【P0-3】`[YZHAnonymous]` 的生产兜底失效 → 可冒充任意用户

**机制现状**：`YZHAnonymousAttribute`（129 行）继承 `AllowAnonymousAttribute` 并实现 `IAsyncAuthorizationFilter`：
```csharp
var authSection = config.GetSection("AuthSettings");
// 生产模式（RequireAuth=true）：免认证特性无效
if (bool.TryParse(authSection[RequireAuthKey], out var requireAuth) && requireAuth)
    return;                                   // ← 走正常 JWT 认证
// 否则：从请求读取 userCode，注入该用户身份
var userCode = ReadUserCodeFromRequest(context.HttpContext.Request);   // ?usercode= / X-User-Code:
if (!string.IsNullOrEmpty(userCode)) {
    var principal = await TryBuildUserPrincipalAsync(context, userCode);
    context.HttpContext.User = principal;     // ← 直接替换身份
}
```

**断裂点**：`AuthSettings` 配置段**全仓不存在**。
- `appsettings.json` 顶层键：`Logging / AllowedHosts / JwtSettings / PasswordSecret / YZH / Cors / Security / DatabaseConfigs / Storage / MinIO` —— **无 `AuthSettings`**
- `appsettings.Development.json` 同样无
- 全仓 `AuthSettings` 字符串只出现在 `YZHAnonymousAttribute.cs`（常量定义 + 注释）

→ `bool.TryParse(null, out _)` = **false** → **永远走"开发模式"分支**。

**后果**：任何标注 `[YZHAnonymous]` 的端点，攻击者只需 `?usercode=<任意用户Code>` 即可**冒充该用户，包括超级管理员**。且 `[YZHAnonymous]` 继承 `AllowAnonymousAttribute`，会让 `PermissionFilter` 在第 56 行提前 `return`（跳过认证检查），形成完整绕过链。

**当前风险等级**：**潜伏（Latent）** —— 全库 `[YZHAnonymous]` 标注数 = **0**，该洞尚未被触发。

**但这是最危险的一类缺陷**：
- 文档 `05-权限体系.md` §3.4 与 `12-框架能力清单-V1.md` C34 **明确宣传**"生产模式 `RequireAuth=true` 时 `YZHAnonymous` 强制失效"
- `YZHAnonymousAttribute` 的注释也写着"生产环境兜底"
- → **任何按文档使用 `[YZHAnonymous]` 的开发者或 AI，都会引入一个可冒充超管的漏洞，且不会有任何报错**

---

#### 【P0-4】启动期命名自检是死代码

`BizNamingRules.cs`（`YZH.Core.Stand/BizConventions/`，146 行）实现完整：
- `BannedEntityNames` 黑名单 13 个类名
- `ValidateEntityType` 违规抛 `InvalidOperationException`
- `ValidateAssembly(params Assembly[])` 批量校验

**断裂点**：**`Program.cs` 零引用**。`ValidateAssembly` 从未被调用。

**文档不实**：`12-框架能力清单-V1.md` §4.1 声称"已生效，违规抛 `InvalidOperationException`"。

**附带缺陷**：黑名单含 `Sys_UserRole` / `TreeNodeViewBase` / `ITreeNode` —— 其中 `Sys_UserRole` **实际不存在**（框架实体叫 `Sys_RoleUser`），`TreeNodeViewBase` 在 10 份文档中**仅此一处出现**。→ 即使接线，黑名单本身也需要校准。

---

#### 【P0-5】后端零自动化约束机制

| 机制 | 是否存在 |
|---|---|
| `.editorconfig` | ❌ |
| Roslyn 分析器（`Microsoft.CodeAnalysis` / `SonarAnalyzer` / `StyleCop` / `FxCop`） | ❌ |
| `.ruleset` / `AnalysisLevel` / `EnableNETAnalyzers` | ❌ |
| 自定义 `DiagnosticAnalyzer` | ❌ |
| 源生成器 | ❌ |
| CI（`.github/` / `.gitlab-ci.yml`） | ❌ |
| pre-commit hook（`.husky` / `.githooks`） | ❌ |
| 启动期自检 | ❌（`BizNamingRules` 未接线） |
| 单元测试 | ⚠️ `YZH.Core.Api.Tests` 存在但**不在 `CertPlatform.sln`**、**编译失败（4 个 CS1061）** |

`src/yzh-core/Directory.Build.props` 全文 9 行：
```xml
<TargetFramework>net8.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
<LangVersion>latest</LangVersion>
<TreatWarningsAsErrors>false</TreatWarningsAsErrors>
```

**结论**：**后端架构约束的强制执行率为 0%**。所有后端规范（C01–C11、C29–C36、C53–C61）**全部依赖 AI 与开发者的自觉**。这直接违背用户的原始动机——"不允许 AI 随意发挥"。

---

### 3.2 认证链路实况（澄清一个重要误解）

评估过程中曾出现"9 个控制器无认证特性 → 匿名可访问"的推断。**实测推翻了它**：

```
无 Token 请求 13 个端点 → 全部 [401]
```

**真实机制**：`PermissionFilter` 是**全局** `IAsyncAuthorizationFilter`（`Program.cs:82` 注册），其第 60-64 行：
```csharp
if (!_userContext.IsAuthenticated) {
    context.Result = new UnauthorizedResult();   // ← 全局认证兜底
    return;
}
```
`UserContext.IsAuthenticated => _httpContext?.User?.Identity?.IsAuthenticated ?? false`

→ **认证（Authentication）是全站生效的** ✅，与 `[YZHAuthorize]` 无关。

**但 `PermissionFilter` 有一个真实缺陷**：第 56 行检测 `[AllowAnonymous]` 用的是
```csharp
methodInfo.IsDefined(typeof(AllowAnonymousAttribute), true)
```
**`MethodInfo.IsDefined` 只查方法级特性，不查类级**。所以：
- 方法级 `[AllowAnonymous]` → 能跳过 ✅
- **类级 `[AllowAnonymous]` / `[YZHAnonymous]` → 跳不过** ⚠️（但类级 `[Authorize]` 会被 ASP.NET Core 内置机制处理，行为不对称）

当前 9 处 `[AllowAnonymous]` 全部是方法级，所以尚未暴露。**这是需要记住的坑**。

**实测的 9 处 `[AllowAnonymous]`**：

| 文件 | 行 | 评估 |
|---|---|---|
| `AuthController.cs` | 44 / 116 / 132 | ✅ 登录、验证码，合理 |
| `AuditorAuthController.cs` | 55 / 78 | ✅ 注册、机构下拉，合理 |
| `UserController.cs` | 62 | ✅ `getVierificationCode` 裸对象端点，合理 |
| `MenuController.cs` | 36 / 65 | ⚠️ Vol 兼容端点匿名，**建议收敛** |
| `WorkflowTestController.cs` | 214 | ⚠️ 业务控制器上有匿名端点，**需复核用途** |

---

### 3.3 显式失效（有报错/可发现）

#### 【P1-1】10 个控制器绕过基类，承载 93.75% 的端点

| 分类 | 控制器数 | 显式端点 |
|---|---|---|
| `YzhControllerBase<V>` | 11 | 6 |
| `TreeTableControllerBase<T,V>` | 2 | 0 |
| **裸 `ControllerBase`** | **10** | **90** |
| **合计** | **23** | **96** |

**裸 `ControllerBase` 的 10 个**（★ 标注有 `[Authorize]`）：

| 控制器 | 显式端点 | 类级认证 |
|---|---|---|
| `StandardDirectoryController` | **33** | 无 |
| `DocExtractionRuleController` | 15 | 无 |
| `DirectoryTemplateController` | 8 | 无 |
| `ReportDefinitionController` | 8 | 无 |
| `QueueMonitorController` | 7 | 无 |
| `WorkflowTestController` | 6 | ★ `[Authorize]` |
| `AIUsageController` | 4 | 无 |
| `CertOrgStandardController` | 3 | 无 |
| `CertOrgStageController` | 3 | 无 |
| `AuditorAuthController` | 3 | 无（2 个动作匿名） |

**违反条文**：C49（禁止 Controller 继承 `ControllerBase`）。

**实际代价**（当前）：
1. 不参与 `YzhAuthFilter`（虽然它现在也没挂载 —— **但一旦 P0-2 修复，这 10 个会静默地没有 SSO 保护**）
2. 不继承基类的 11 个标准端点 → 前端无法用统一 `filter`/`add`/`update`/`delete` 调用
3. 不走 `EntityConfig` 驱动 → 每个端点都要手写入参出参契约

**部分有正当理由**（文档 C50 承认 B 类模块例外：NC 设计器、文档提取规则、标准文件目录、机构-标准勾选、队列监控、AI 用量）。**但例外清单要求"响应必须统一 `ApiResponse<T>`"**，而 `DocExtractionRuleController` 全系列 15 端点返回的是 `{code, data, message}` **无 `success` 信封**（已登记例外 E7）—— 例外叠加例外。

**建议**：不要求全部回迁基类，但**例外必须显式登记且收敛**。当前 10 个例外、90 个端点，已经"例外即常态"。

---

#### 【P1-2】前端守卫存在但未接线，且不覆盖专家端

`src/certplatform-web/scripts/guards.mjs`（168 行，零依赖）—— **机制设计良好**：
- 4 条规则：R1 原子组件零领域依赖｜R2 TreeNode PascalCase｜R3/R3b 禁 `res.code === 200`
- **默认 `exit 1`** + 债务白名单（删完即达标）
- 用法：`node scripts/guards.mjs` / `--report`

**问题 1：未接入任何门禁**
```json
"build": "vue-tsc && vite build",     // ← 不含 guard
"test":  "vitest run",                // ← 不含 guard
"guard": "node scripts/guards.mjs"    // ← 需手动执行
```
无 CI、无 husky、无 lint-staged。**守卫只有"你记得跑"时才存在。**

**问题 2：扫描范围只覆盖 `cert-admin`**
```js
const CORE  = join(WEB, 'yzh.vue.core/src/components')
const PAGES = join(WEB, 'cert/cert-admin/src/pages')     // ← 只有 cert-admin
const API   = join(WEB, 'cert/cert-admin/src/api')       // ← 只有 cert-admin
```
**`cert-auditor`（专家端，5 个 .vue）/ `cert-enterprise` / `cert-share` 零覆盖**。

> 这意味着：**专家端从第一行代码起就在守卫的盲区里**。用户原始动机是"不让 AI 随意发挥"，而专家端恰是 AI 参与度最高的部分。

**问题 3：R2 的 4 处违规经核实为误伤**

```
cert/cert-admin/src/pages/foundation/iso-standard/logic.ts:56   nodes.forEach((n) => sortRec(n.children || []))
...（共 4 处）
```
源码核实：`type ClauseRow = Record<string, any>`（第 32 行），`children` 是**前端本地构建**的树属性（第 51 行 `parent.children.push(node)`），**不是核心库的 `TreeNode`**。

→ R2 正则 `\bn\.(code|name|extra|children|isLeaf|parentCode)\b` 匹配了**同名但语义不同**的对象。**守卫需要加类型判定或局部豁免**，否则会持续产生噪声，最终被忽略（"狼来了"效应）。

**问题 4：债务白名单有陈旧条目**
`yzh.vue.core/src/components/page/YzhCrudPage.vue` —— **该文件及 `page/` 目录均不存在**。白名单里的幽灵条目会让"删完即达标"的目标永远无法达成。

**问题 5：文档与实现命名漂移**

| 概念 | 文档名 | 实际实现 |
|---|---|---|
| 单表前端基类 | `CrudPageLogic<V>` | **`SingleTableCore<V>`** |
| 左树右表前端基类 | `TreeTableLogic<V>` | `TreeTableCore<T,V>` + `TreeTableLogic<T>`（**两个并存**） |
| 树节点接口 | `ITreeEntity` / `ITreeNode`（文档内部也不一致） | 待澄清 |

`CrudPageLogic` **全仓不存在**（仅出现在文档与注释）。→ AI 按文档写 `extends CrudPageLogic` 会找不到基类。

---

#### 【P1-3】前端实际遵守情况

| 项 | 数字 |
|---|---|
| `cert-admin/src/pages/` 页面 | 27 |
| 含 `logic.ts` | 19 |
| ├─ 使用 Core 基类 | 14 |
| └─ 未使用 Core 基类（`ApiLogic` / `BaseRoleTreeLogic` 等自建） | 5 |
| 既无 Core 也无 `logic.ts`（纯手写） | 8 |
| 直接用 `<el-table` / `<el-form` / `<el-pagination` | 13 文件 |
| `pages/` 下 `any` 密度 | 约 114 处 |
| `cert-share/src/api/` 文件 | 19（18/19 走 `yzhApi`，无 axios） |
| 页面直连 `@/api`（债务盘点） | 6 |

**约束 C12/C13（Logic 继承 + 主键 `code`）的遵守率约 52%（14/27）**，且**无任何自动校验**。

---

### 3.4 文档-实现漂移清单

| 项 | 文档声称 | 实际 |
|---|---|---|
| `RoleIds` 常量类 | `05` §2.2 给出完整代码，路径 `YZH.Core.Stand/Constants/RoleIds.cs` | **文件不存在，`Constants/` 目录不存在**；该节示例代码（§6.2）不可编译 |
| `BizNamingRules` 校验 | `12` §4.1 "已生效" | 未接线，零调用 |
| `sys_api`/`sys_role_api`/`sys_user_permission` | `12` §3.2 "三表未建" | 已建，399 / 90 / 存在 |
| `PermissionFilter.CheckPermission` | `09` P0-003 "空实现 `return true`" | 已改为调用 `PermissionService`（实现完整），**但调用点未建** |
| 目录重命名 | `06` §二 "尚未完成" vs §9.2 "已完成" | 实际已完成（`cert/cert-admin/` 等） |
| 前端基类名 | `CrudPageLogic` / `TreeTableLogic` | `SingleTableCore` / `TreeTableCore` |
| `RequireAuth` 生产兜底 | `05` §3.4 "生产模式强制失效" | 配置段不存在，兜底失效 |
| 硬编码 JWT Key | `09` P1-004 "用户决策暂不改" | 与 `Program.cs:41` 默认值一致，确认未改 |

---

### 3.5 业务层已确认缺陷（非架构层，但影响架构可信度）

| 缺陷 | 位置 | 后果 |
|---|---|---|
| 机构关联键不一致 | `cert_org_standard.OrgCode` 存 `cert_certification_body.Code`（GUID），注册写入 `Sys_Organization.OrgCode` = `CbCode`（`CB001`） | **静默查空**，表现为"可用标准 0 个"，不报错 |
| `ent_enterprise` 全局唯一约束 | `uk_enterprise_no` + `uk_credit_code` | 阻断"同一企业多工作区多档案"（初审张三、复审李四），推翻功能设计 D1 |
| `ROLE_AUDIT_CLIENT_ADMIN` 无菜单授权 | `Sys_RoleMenu` 中该角色 **0 条** | 注册用户登录后**侧边栏为空** |
| `Sys_User.UserPwd` 无脱敏 | `System/User/filter` 返回 | 密码密文外泄（见 P0-1） |
| 验证码缓存 key 不一致 | `AuthController.GetCaptcha` 写 `uuid`、读 `captcha_{uuid}` | 生产模式验证码必失败 |
| `SqlScalarAsync<T>` 不支持 `Nullable<T>` | `SqlSugarDbOrm.cs:416` | `int?` 抛异常被内部 catch **静默转 Fail**，值写库恒为 0 |
| `EntityService.Insert` 的 `FillCreateAudit` 覆盖 `CreateBy` | 匿名端点 | 审计字段失真 |

---

## 四、逐层详评

### 4.1 后端基类层 🟡

**做得好的**：
- `YzhControllerBase<V>`（917 行）能力完整：11 个标准端点 + 完整钩子体系（`OnBuildingFilter` / `OnBeforeInsert` / `OnAfterUpdate` …）
- `TreeTableControllerBase<T,V>` 与 `TreeConfig` 四字段设计清晰
- `EntityService<T>` 统一了 CRUD 与审计字段填充
- `ApiScanner` + `ApiSyncService` + ApiCode（`SHA256("{方法}|{控制器}|{动作小写}")`）机制设计精巧，启动时自动扫描同步

**问题**：
- 基类只覆盖 13/23 控制器、6/96 端点 → **"统一基类"原则在端点维度上是少数派**
- 例外条款（C50）被过度使用，且例外本身又叠加例外（E7 无信封）

### 4.2 接口授权层 🔴

见 P0-1。**这是本次评估最严重的发现**：机制齐备、数据齐备、过滤器已注册，唯独**没有端点声明需要什么权限**。修复成本极低（加标注 + 补同步），收益极高。

### 4.3 认证 / SSO 层 🔴

见 P0-2、P0-3。认证本身生效；SSO 与续租**从未执行**；`[YZHAnonymous]` 生产兜底**配置缺失**。

### 4.4 前端基类层 🟡

`yzh.vue.core/src/logic/` 实际有 6 个基类：`SingleTableCore` / `TreeTableCore` / `TreeTableLogic` / `AssociationTreeCore` / `CheckTreeCore` / `LinkTableCore`。
→ **能力丰富但命名与文档脱节**，且 `TreeTableCore` 与 `TreeTableLogic` 职责边界不清（需澄清文档或合并）。

### 4.5 代码结构三层同构 🟡

规范文档 + 4 条 shell 检查齐备，但**未接线**。实际三层同构在 `system/*` / `foundation/*` 域执行良好，在 `workflow/*` 域有偏离。

### 4.6 自动化约束机制 🔴

**后端 0%，前端 15%（有脚本未接线）。** 这是与用户核心动机差距最大的一层。

---

## 五、完善建议

### P0（建议立即完成，预计 1 个工作日）

| # | 动作 | 具体做法 | 验收标准 |
|---|---|---|---|
| **P0-1** | **接线接口授权** | ① 为管理端控制器端点标注 `[RequirePermission("<ApiCode>")]`；② 或改为**默认拒绝**：`PermissionFilter` 中 `requirePermission == null` 时查 `sys_role_api` 里该端点 ApiCode（无记录则 403），仅超管白名单放行 | 用 `ROLE_AUDIT_CLIENT_ADMIN` 账号调 `System/User/filter` 返回 **403** |
| **P0-2** | **挂载 `YzhAuthFilter`** | 在 `Program.cs:79-84` 的 `options.Filters.Add` 中加入 `YZH.Core.Api.Filters.YzhAuthFilter` | ① 连续登录两次，旧 Token 请求返回 **401**；② 临近过期请求响应头出现 `X-New-Token` |
| **P0-3** | **补 `AuthSettings` 配置** | 在 `appsettings.json` 加 `"AuthSettings": { "RequireAuth": true }`；开发环境在 `appsettings.Development.json` 显式置 `false` | 生产配置下 `?usercode=admin` 无法冒充（401） |
| **P0-4** | **接线 `BizNamingRules`** | 在 `Program.cs` 启动时调用 `BizNamingRules.ValidateAssembly(...)` 并**校准黑名单**（`Sys_UserRole`→`Sys_RoleUser`，确认 `TreeNodeViewBase`/`ITreeNode` 是否存在） | 故意定义一个 `Sys_User` 业务实体，启动应抛异常 |
| **P0-5** | **`UserPwd` 输出脱敏** | 在 `Sys_User` 实体或 EntityConfig 层将 `UserPwd` 标记为只写不读 | `System/User/filter` 响应不含 `UserPwd` |
| **P0-6** | **修复 `PermissionFilter` 类级特性检测** | 第 56 行改为同时检查方法级与类级：`methodInfo.IsDefined(...) \|\| methodInfo.DeclaringType.IsDefined(...)` | 类级 `[AllowAnonymous]` 的控制器可匿名访问 |

### P1（建议本轮完成，预计 1 个工作日）

| # | 动作 | 说明 |
|---|---|---|
| **P1-1** | **把 `guard` 接进门禁** | ① `cert-admin` 的 `build` 改为 `node ../../scripts/guards.mjs && vue-tsc && vite build`；② 加 pre-commit hook；③ 若引入 CI，加 `guard` 步骤 |
| **P1-2** | **扩大守卫扫描范围** | `PAGES`/`API` 改为数组，纳入 `cert-auditor` / `cert-enterprise` / `cert-share` |
| **P1-3** | **修 R2 误伤** | 为 R2 增加类型判定（仅当对象来自 `@yzh-core` 的 `TreeNode` 时校验），或把 `iso-standard/logic.ts` 的本地树工具移到 Core 并复用 `TreeNode` |
| **P1-4** | **清理陈旧债务项** | 删除 `YzhCrudPage.vue`（文件不存在）；复核 `system/_shared/useRoleTreeBadges.ts` |
| **P1-5** | **统一前端基类命名** | 二选一：① 文档改名为 `SingleTableCore`/`TreeTableCore`；② 在 Core 导出 `CrudPageLogic` 别名。并澄清 `TreeTableCore` vs `TreeTableLogic` 职责 |
| **P1-6** | **10 个裸 `ControllerBase` 控制器登记例外** | 在代码注释标注「已登记例外 C50」+ 例外理由；同时**要求响应统一 `ApiResponse<T>`**（`DocExtractionRuleController` 的 E7 需单独登记） |
| **P1-7** | **修复 `YZH.Core.Api.Tests`** | 要么加入 `.sln` 并修复 4 个 CS1061，要么显式标注为废弃。**当前状态是隐形炸弹** |
| **P1-8** | **修正文档不实之处** | 至少修正：`12` §4.1（`BizNamingRules` 未生效）、`05` §2.2（`RoleIds` 不存在）、`12` §3.2（三表已建）、`09` P0-003（原因已变）、`06` §二/§9.2（自相矛盾） |

### P2（可延后，但应在专家系统功能铺开前完成）

| # | 动作 |
|---|---|
| P2-1 | 引入 `.editorconfig` + `TreatWarningsAsErrors=true`（先只对框架层项目开启） |
| P2-2 | 引入 Roslyn 分析器（`Microsoft.CodeAnalysis.NetAnalyzers`）做基类/命名/契约校验，把 C01/C49/C54/C55/C61 从"纸面"变"编译期" |
| P2-3 | 写 3–5 个集成测试，覆盖：接口授权拒绝、SSO 挤号、`[YZHAnonymous]` 生产兜底 |
| P2-4 | 修复机构关联键不一致（统一用 GUID `Code`，另建 `cert_auditor_org` 存 `CbCode`） |
| P2-5 | 改造 `ent_enterprise` 唯一约束为 `UNIQUE(OrgCode, EnterpriseNo)` |
| P2-6 | 为 `ROLE_AUDIT_CLIENT_ADMIN` 补齐 `Sys_RoleMenu` 授权（否则专家端侧边栏为空） |
| P2-7 | 收敛 `MenuController` 的 2 个匿名 Vol 兼容端点 |
| P2-8 | 清理 `09`/`10` 中已过期的 TODO（权限表已建、`CheckPermission` 已实现） |

---

## 六、结论：整体架构是否基本满足当前系统开发？

### 6.1 明确回答

> **基本满足"开发"需求，尚不满足"约束"需求。**

拆开说：

| 问题 | 回答 |
|---|---|
| 架构骨架够不够开发专家系统？ | **够。** 基类、契约、三层同构、权限模型都已具备，专家端已端到端跑通注册+登录 |
| 架构能不能"不让 AI 随意发挥"？ | **不能。** 后端零门禁，前端守卫未接线且不覆盖专家端 |
| 能不能现在就继续推进专家系统？ | **能，但强烈建议先做 P0-1 ~ P0-6。** |

### 6.2 为什么建议先做 P0

用户重构架构的**唯一动机**是"让代码受架构的制约"。当前状态是：

- **后端**：所有约束靠自觉。AI 写一个继承 `ControllerBase` 的控制器、写一个 `[YZHAnonymous]` 端点、不写 `[RequirePermission]`——**全部畅通无阻，不会有任何报错**
- **前端**：专家端（`cert-auditor`）**完全在守卫盲区**。而专家端正是接下来 AI 参与度最高的地方

若此时推进专家系统：
1. 专家端会新增大量页面与端点，**全部绕过现有约束**
2. P0-1（接口授权）不修，专家端用户与管理员共享全部接口权限，**SaaS 多租户隔离在接口层为零**
3. P0-2（SSO）不修，专家付费账号可被无限共享（多设备同时登录），**直接影响商业模式**
4. P0-3 是定时炸弹，一旦有人按文档用 `[YZHAnonymous]`，就是可冒充超管的漏洞

**P0 全部 6 项，预计 1 个工作日**。这是一个"低成本、高杠杆"的修复窗口。

### 6.3 若坚持先推进专家系统

至少应做到：
1. **P0-1 + P0-2**（接口授权 + SSO）—— 这两项直接决定专家系统的商业可行性
2. **P1-2**（守卫纳入 `cert-auditor`）—— 让专家端从第一行代码起就受约束
3. 在专家端**显式标注** `[RequirePermission]`（即使 P0-1 未全站接线，专家端自己的端点先接上）

### 6.4 对"约束"这件事的架构级建议

用户的目标"让代码受架构的制约"，本质是要求**把规范从"文档"变成"可执行的门禁"**。建议按此优先级建设：

```
第 1 层（编译期）  ← 最强，无法绕过
  .editorconfig + Roslyn 分析器 + TreatWarningsAsErrors
  → 覆盖 C01/C49/C54/C55/C61

第 2 层（启动期）  ← 强，服务起不来
  BizNamingRules 接线 + TreeConfig 断言 + 权限表连通性自检
  → 覆盖 C05/C53

第 3 层（提交期）  ← 中，本地拦截
  guards.mjs 接 pre-commit + 扩大扫描范围 + 新增后端守卫
  → 覆盖 C18/C47/C48/C68

第 4 层（运行时）  ← 弱，但能兜底
  接口授权默认拒绝 + 审计日志
  → 覆盖 C29–C33

第 5 层（文档）    ← 最弱，仅引导
  保持文档与实现一致，并把"可自动校验"的虚假承诺删掉
```

**当前状态：只有第 5 层完整。第 1、2 层完全缺失，第 3 层有脚本未接线，第 4 层认证生效、授权缺失。**

---

## 附录 A：证据索引

### A.1 源码核验

| 结论 | 文件 | 关键位置 |
|---|---|---|
| 全局认证兜底 | `YZH.Core.Api/Filters/PermissionFilter.cs` | 56（`IsDefined` 只查方法级）、60-64（401）、70（`requirePermission != null`） |
| 授权校验器实现完整但未调用 | `YZH.Core.Api/Services/PermissionService.cs` | 28-84 |
| `YzhAuthFilter` 未挂载 | `YZH.Core.Web/Program.cs` | 79-84（三条 Filters，无 YzhAuthFilter） |
| 同上 | `YZH.Core.Api/Attributes/YZHAuthorizeAttribute.cs` | 21-25（无 TypeFilter） |
| `AuthSettings` 缺失 | `YZH.Core.Web/appsettings.json` | 顶层键无 `AuthSettings` |
| `[YZHAnonymous]` 冒充逻辑 | `YZH.Core.Api/Attributes/YZHAnonymousAttribute.cs` | 41（`RequireAuth` 判断）、47-58（注入身份） |
| `BizNamingRules` 未接线 | `YZH.Core.Stand/BizConventions/BizNamingRules.cs` + `Program.cs` | 全文 146 行 / 零引用 |
| 基类认证特性 | `YZH.Core.Api/Controllers/YzhControllerBase.cs` | 43-46 |
| 前端守卫 | `certplatform-web/scripts/guards.mjs` | 21-23（扫描范围）、61-122（4 规则）、167（exit 1） |
| 前端基类实际命名 | `yzh.vue.core/src/logic/*.ts` | `SingleTableCore` / `TreeTableCore` / `TreeTableLogic` |
| 无后端门禁 | `yzh-core/Directory.Build.props` | 全文 9 行，`TreatWarningsAsErrors=false` |

### A.2 HTTP 实测

| 实验 | 方法 | 结果 |
|---|---|---|
| 匿名访问（13 端点） | 无 Token | 全部 **401** → 认证生效 |
| 越权访问（4 端点） | `ROLE_AUDIT_CLIENT_ADMIN` Token | 全部 **200** → 授权失效；`UserPwd` 外泄 |
| SSO 挤号 | 登录 A → 登录 B → 用 A 请求 | **[200]** → 挤号未生效 |
| Token 续租 | 观察响应头 | 无 `X-New-Token` |
| 前端守卫 | `node scripts/guards.mjs --report` | R1/R3/R3b ✅；R2 4 处（经核实为误伤）；债务 6 个页面直连 `@/api` |

### A.3 数据核验

| 项 | 数值 |
|---|---|
| `sys_api` / `sys_role_api` / `Sys_RoleAuth` | 399 / 90 / 330 |
| `Sys_RoleMenu` 中 `ROLE_AUDIT_CLIENT_ADMIN` | **0** |
| 业务控制器 / 显式端点 | 23 / 96（裸 `ControllerBase` 占 90） |
| `cert-admin` 页面 / 使用 Core 基类 | 27 / 14 |
| `cert-auditor` .vue / `cert-enterprise` .vue | 5 / 0 |

---

## 附录 B：与既有文档的关系

| 文档 | 关系 |
|---|---|
| `docs/10-YZH架构/09-架构修复清单.md` | **本文是其续篇**。P0-003 状态需更新（原因已从"空实现"变为"调用点未建"）；P1-002/P1-003/P1-005 仍待修 |
| `docs/10-YZH架构/10-架构建设建议.md` | 其"权限表结构待建设"已部分完成（三表已建）；`PermissionFilter` 实现已完成，**仅差接线** |
| `docs/10-YZH架构/12-框架能力清单-V1.md` | §4.1 / §3.2 / §5 多处状态需更新（见 §3.4 漂移清单） |
| `docs/10-YZH架构/05-权限体系.md` | §2.2 `RoleIds` 不存在；§3.4 生产兜底不实；§6.2 示例代码不可编译 |
| `docs/50-任务/迁移计划/端到端迁移缺口清单-V1.md` | 关注"接口有无"，本文关注"接口是否受约束"，互补 |
| `docs/50-任务/开发计划/体系认证专家系统建设计划-V4.md` | 本文为其前置评估 |

---

*本报告全部结论基于 2026-09-23 的源码与运行时实测。若相关代码已变更，请重新核验。*
