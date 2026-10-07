# 58 · 共享层（`CertPlatform.Shared`）建设方案 V1

> **缘起**：用户 2026-10-06 第三次裁定 —— 原话：
>
> > 「**`assest` 可以放 `entityconfig` 的 `json` 文件，我们 `share` 不引用 `controller` 我支持，但前后端比如针对语义的分析方法，对填充的操作，对数据源的引用包括 `ai` 的执行测试，这些都可以放置到 `share` 中，因为后端和审核端很多功能都需要相互引用，去掉重复的实体或逻辑是关键。**」
>
> **用户给的开发策略（本册即第 1 步）**：
> 1. **先确定 `share` 如何建立** ← 本册
> 2. 建立最核心的「**企业资料 → 单个标准文件**」核心队列主线
> 3. 完善前后端串联的逻辑
>
> **本册任务**：把第 1 步落成可执行的东西 —— `Shared` 的**边界 / 目录 / 接线 / 下沉清单 / 验收**。
> **日期**：2026-10-06｜**方式**：`csproj` + 目录实测 + 逐文件读码。

---

## 〇、一句话

> **`Shared` 不是「只放实体的纯域层」，而是「三端共用的完整业务层」** —— 它**可以**引 `DataBase`、**可以**放查库服务、**可以**放 `Assets/EntityConfigs`；**只有 Controller 不放**（用户支持）。
> ⇒ 因此「哪些逻辑该下沉」的答案变得很简单：**凡是 ≥2 端需要的逻辑，全部下沉；下沉的判据不是「能不能」，而是「沉了是不是真的只剩一份」。**
> ⇒ 本册给出 **4 类逻辑、13 个文件** 的具体落点，以及 **3 个接线点**（缺一即静默失效）。

---

## 一、边界：`Shared` 能放什么、不能放什么

| 类别 | 能不能放 | 依据 | 备注 |
|------|:---:|------|------|
| **实体**（`Entities/**`） | ✅ | 已有 31 个 | 判据 = `Auditor.csproj:21-27` 成文规则（双端共用才进） |
| **纯函数 / 纯模型 / DTO** | ✅ | 已有 `Fill/` `Office/` `Storage/` | **这是资产，⛔ 不许为「方便」让它查库** |
| **查库服务**（持 `IDbOrm`） | ✅ | **用户 2026-10-06 裁定**；实测**无环**（`DataBase → Stand`，`Stand` 零项目引用） | `Shared.csproj` 加 `DataBase` 引用即可 |
| **`Assets/EntityConfigs/*.json`** | ✅ | **用户裁定**；`Program.cs:59-65` 支持多目录 | ⚠️ 必须同时改 `Program.cs` + `csproj`（见 §四） |
| **`Assets/` 其他静态资源** | ✅ | 同上 | — |
| **Controller** | ⛔ **不放** | **用户裁定「我支持」** + `57` V2 §四 D7（4 个坑） | 但**路径保留**：将来确需时按 D7 清单四件事一起做 |
| **前端代码** | ⛔ | 前端共享层是 `cert-share/`（另一个概念） | 见 §六 |

> **⚠️ 一条硬纪律（V2 立）**：`Shared` 可以查库，**但 `Shared/Fill/` 与 `Shared/Office/` 必须保持纯函数**。
> 需要查库的部分**另建类**放 `Shared/Services/`，**⛔ 不许就地改造引擎**（理由：`FillModels.cs:115` 的第二个理由 ——「引擎必须可单测、可离线跑」）。

---

## 二、目标目录结构

`Shared` 已预留完整分层目录（实测**全部存在且全空**）—— 本方案就是**把它们启用**：

```
CertPlatform.Shared/                      (现 75 个 .cs)
├─ Assets/EntityConfigs/          ← ★ 新建（用户裁定）
│    └─ Cert/FillParamDef.json 等「多端共用」的 config
├─ Entities/                      ← 已有 31 个（A1/A2/A3 上移后 +3）
│    ├─ Cert/  Dir/  Doc/  Rpt/  Sys/  Wf/  Audit/
├─ Fill/                          ← 已有：★ 纯函数层，⛔ 不许查库
│    ├─ DocumentFillEngine.cs · ParamValueResolver.cs · FillModels.cs · IFillResolver.cs
│    ├─ AnchorClassifier.cs · SemanticHints.cs
│    └─ Resolvers/  (GlobalParam / Replace / AiGenerate / HeaderFooter)
├─ Office/                        ← 已有：NPOI 写入（Word/Excel）
├─ DocExtraction/                 ← 已有：含 LlmInvokeService（AI 执行内核，已在 Shared ✅）
├─ Storage/                       ← 已有：PathBuilder（路径唯一权威）
├─ Services/                      ← ★ 预留且空 ⇒ 本次启用
│    ├─ Ai/                       ← ★ 新建：AI 执行（下沉 5 个文件）
│    ├─ Fill/                     ← ★ 新建：取值内核（新建 1 个 + 瘦身 2 个）
│    └─ Semantic/                 ← ★ 新建：语义分析内核（拆分 PromptWorkbenchService）
├─ Controllers/                   ← 预留，⛔ 本次不启用
├─ IServices/  Repositories/  IRepositories/  WorkflowEngine/  Data/
│                                 ← 预留，**本方案暂不使用**（避免过度设计）
└─ Compat/  Constants/  Exceptions/
```

> **为什么用 `Services/` 而不是往 `Fill/` 里塞**：`Fill/` 是**纯函数资产**（有单测、可离线跑）。
> 把查库代码放进去 = 让整层失去「可单测」属性。**用目录边界把「纯」和「不纯」隔开**，是最便宜的防腐手段。

---

## 三、四类逻辑的下沉清单（逐文件）

### 3.1 AI 执行 → `Shared/Services/Ai/`

| # | 文件 | 现位置 | 行数 | 目标 | 阻塞点 |
|:--:|------|--------|:--:|------|------|
| 1 | `AiFillSettings.cs` | `Admin/Services/Workflow/Skills/Fill/Ai/` | 86 | → `Shared/Services/Ai/` | ✅ 无（只读 `cert_sys_config` 六键） |
| 2 | `AiFillInvoker.cs` | 同上 | 154 | → `Shared/Services/Ai/` | ✅ 无 —— **且可顺手修掉签名**：`InvokeAsync(IDbOrm db, …)`（`:35`）那个「把 `db` 当参数传」的丑陋写法，下沉后改为**构造注入** |
| 3 | `AiFillJsonReader.cs` | 同上 | 194 | → `Shared/Services/Ai/` | ✅ 无（纯函数） |
| 4 | `AiFillModels.cs` | 同上 | 131 | → `Shared/Services/Ai/` | ✅ 无（纯模型） |
| 5 | `AiFillPromptBuilder.cs` | 同上 | 369 | → `Shared/Services/Ai/` | ⚠️ **引 `Admin.Entities.Doc.DocFillPrompt`**（`:7` / `:246-254`）⇒ 见下方「★ 连带裁定」 |
| — | `LlmInvokeService.cs` | **已在 `Shared/DocExtraction/`** ✅ | 313 | 留（可选迁 `Services/Ai/`） | ✅ 无 —— **它本来就在 Shared**，注释里那句「Admin 层组装…Shared 层不读库」正是要消除的半份实现 |
| — | `SrcAiFieldSkill` / `SrcAiTableSkill` / `SrcSemanticSkill` | `Admin/…/Fill/Ai/` | 135/139/119 | ⛔ **留 Admin**（Skill 壳，绑 `SkillResult`） | — |

**收益**：`IAiFillInvoker` 从「调用方必须自己传 `IDbOrm`」变成「构造注入」⇒ **Auditor 侧要用 AI 时不必再自己组装六键**；AI 执行的 5 个坑（重试 / `MaxTokens` 截断 / `JsonElement` 转换 / 六键注入 / 围栏剥离）**三端只收口一次**。

> **★ 连带裁定（必须一起做，否则编译不过）**：`AiFillPromptBuilder` 依赖 `DocFillPrompt`（表 `cert_doc_fill_prompt`）。
> 这张表**本来就是双端共用**（Admin 配、Auditor 用）⇒ 按 `57` §二 P2 判据，**`DocFillPrompt` 实体也应上移 `Shared/Entities/Doc/`**。
> ⇒ **这说明「实体归位」与「逻辑归位」是同一件事** —— 正好印证用户「去掉重复的**实体或逻辑**」。

### 3.2 语义分析 → `Shared/Services/Semantic/`

| # | 文件 | 现位置 | 目标 | 阻塞点 |
|:--:|------|--------|------|------|
| 1 | `PromptWorkbenchService.cs` | `Admin/Services/Workflow/` | **★ 拆分**：<br>① **分析内核**（`AnalyzeForQueueAsync` 一类）→ `Shared/Services/Semantic/`<br>② **工作台交互**（`GenerateAsync` 生成草稿 / `TestAsync` 上传试跑）**⛔ 留 Admin** | 引 4 个 Admin 实体命名空间（`Cert`/`Dir`/`Doc`/`Wf`，`:13-16`）⇒ 需逐个按 P2 判据复核上移 |
| 2 | `SemanticHints.cs` | **已在 `Shared/Fill/`** ✅ | 留 | ✅ 无 —— 注释里已写明「`Admin` ⛔ 不引用 `Auditor` ⇒ 只能下沉 `Shared`；⛔ 不要在本类里再抄一份」 |
| 3 | `PromptTemplateService.cs` / `PromptRenderer.cs` | `Admin/Services/Workflow/` | 🟡 视分析内核依赖决定（若依赖则一并下沉） | 待逐个复核 |
| 4 | `PromptMarkdownCache.cs` | 同上 | ⛔ 留 Admin（Redis 交互 + 工作台专用） | — |
| 5 | `StandardDocContractController.cs` | `Admin/Controllers/Workflow/` | ⛔ 留 Admin（**Controller 不进 Shared**） | — |
| 6 | `EnterpriseOriginalAnalyzeExecutor.cs` | `Auditor/Services/Ent/` | 改为**调 Shared 内核**（删掉本地的口径拷贝） | — |

> **为什么必须拆分而不是整体搬**：`PromptWorkbenchService` 里**混了两件事** ——
> 「**分析口径**」（双端共用，必须唯一）与「**工作台交互**」（后台维护人员专用）。
> 整体搬会把「后台专用」也变成共用；整体留则双端各抄一份口径（= 已发生的 `D1` 模式）。
> ⇒ **拆点 = 「有没有 `HttpContext` / 是不是给页面用的」**。

### 3.3 填充操作 → `Shared/Services/Fill/`

| # | 文件 | 现位置 | 行数 | 目标 | 说明 |
|:--:|------|--------|:--:|------|------|
| 1 | `SourceResolver.cs` | `Admin/Services/Ent/` | 160 | **★ 瘦身**：编排留 Admin，**取值内核 → `Shared/Services/Fill/`** | 它**还有**「锚点层编排」职责（`SourceSpec.sources[]` 回退链）⇒ 编排留端 |
| 2 | `SrcGlobalParamSkill.cs` | `Admin/…/Skills/Fill/` | 229 | ⛔ 留 Admin，但**把取值内核抽出去** | 绑死 `SkillResult` ⇒ 基础设施耦合，非项目边界 |
| 3 | `FillCellSkill.cs` / `FillTableSkill.cs` | 同上 | 95/134 | ⛔ 留 Admin | 只服务工作流 |
| 4 | `Shared/Fill/**` + `Shared/Office/**` | **已在 Shared** | — | ⛔ **不动** | **纯函数资产**，V2 明确不许为「方便」让其查库 |

### 3.4 数据源引用 → `Shared/Services/Fill/`（与 3.3 合并为同一目录）

| # | 项 | 现位置 | 目标 | 说明 |
|:--:|------|--------|------|------|
| 1 | **`FillParamValueProvider`** | ⛔ **不存在** | **★ 新建 → `Shared/Services/Fill/`** | 只做「查 `cert_fill_param_value` + 组装 `FillValue`」⇒ **同时消灭 D1（手抄副本 + 漂移）与 D6（`MapToInfo` ×2）** |
| 2 | `ParamValueResolver.cs` | **已在 `Shared/Fill/`** ✅ | 留（纯函数） | 「选最具体」的去重逻辑在此 |
| 3 | `Resolvers/*`（4 个） | **已在 `Shared/Fill/Resolvers/`** ✅ | 留（纯函数） | 文本层 `{{token}}` 链式 |
| 4 | `FillParamValue` 实体 | `Auditor/Entities/Cert/` | **→ `Shared/Entities/Cert/`** | `57` 表 A 的 **A1**（与已在 Shared 的 `FillParamDef` 成对） |
| 5 | `FillParamDef` 实体 | **已在 `Shared/Entities/Cert/`** ✅ | — | — |

> **⇒ 3.3 + 3.4 合并结论**：`Shared/Services/Fill/` 只需 **1 个新类**（`FillParamValueProvider`）+ **2 处改为调它**（`SourceResolver` / `SrcGlobalParamSkill`）。
> **这就是「搬『值』不搬『壳』」**：把「查库 + 组装」这段**取值与映射**逻辑收敛，把「编排 / Skill / 执行器」这些**壳**留在各自端。

---

## 四、三个接线点（**缺一即静默失效**）

### 4.1 `CertPlatform.Shared.csproj` —— 加 `DataBase` 引用

```xml
<ItemGroup>
  <ProjectReference Include="..\..\yzh-core\YZH.Core.Stand\YZH.Core.Stand.csproj" />
  <!-- ★ V2 新增：允许查库服务。实测无环：DataBase → Stand，Stand 零 <ProjectReference> -->
  <ProjectReference Include="..\..\yzh-core\YZH.Core.DataBase\YZH.Core.DataBase.csproj" />
</ItemGroup>

<!-- ★ 用户裁定：Assets 放 EntityConfig JSON ⇒ 必须让它在输出目录可见 -->
<ItemGroup>
  <Content Include="Assets\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

> ⛔ **`Shared.csproj:13` 已有的 `<FrameworkReference Include="Microsoft.AspNetCore.App" />` 不要删** —— 它是 `Minio` / `IHttpClientFactory` / `IObjectStorage` 等的前提。

### 4.2 业务服务自注册 —— 新增 `AddCertPlatformSharedServices()`

现状（`Program.cs:68-71`）：只有 `AddCertPlatformAdminServices()` 与 `AddCertPlatformAuditorServices()`。

```csharp
// 业务服务自注册（启动工程不感知具体业务实现）
builder.Services.AddCertPlatformSharedServices();   // ★ 新增，必须在 Admin/Auditor 之前
builder.Services.AddCertPlatformAdminServices();
builder.Services.AddCertPlatformAuditorServices();
```

**新建 `CertPlatform.Shared/CertPlatformSharedServiceExtensions.cs`**（⛔ 不要写进 Admin 的扩展类）：

```csharp
public static IServiceCollection AddCertPlatformSharedServices(this IServiceCollection services)
{
    // 无状态 → 单例；持 IDbOrm/IObjectStorage（Scoped）→ Scoped
    services.AddScoped<FillParamValueProvider>();   // 持 IDbOrm ⇒ Scoped
    services.AddScoped<AiFillInvoker>();            // 持 IDbOrm ⇒ Scoped
    services.AddSingleton<AiFillPromptBuilder>();   // 无状态
    // ⚠️ LlmInvokeService 已在 Admin 注册 ⇒ 下沉后必须【移到本方法】，否则 Admin 不注册时 Auditor 侧解析失败
    return services;
}
```

> ⚠️ **顺序敏感**：`Shared` 在**最前**。因为 `AddScoped` 是「后注册覆盖先注册」的语义（同一接口多次注册取最后一个）——
> 若 Admin 也注册同名服务，**Admin 的会赢**。⇒ 下沉后必须**同步删除 Admin 侧的重复注册**（否则又是静默分叉）。

### 4.3 `EntityConfig` 路径 —— `Program.cs` 加一行（**且必须放首位**）

```csharp
var bizRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "certplatform-api"));
options.BusinessEntityConfigPaths = new List<string>
{
    Path.Combine(bizRoot, "CertPlatform.Shared", "Assets", "EntityConfigs"),   // ★ 新增，放首位
    Path.Combine(bizRoot, "CertPlatform.Admin",  "Assets", "EntityConfigs"),
    Path.Combine(bizRoot, "CertPlatform.Auditor","Assets", "EntityConfigs"),
};
```

> **为什么放首位**：`Program.cs:57` 注释写明 ——「**搜索顺序 = 本列表顺序，同名文件先命中者生效**」。
> `Shared` 放首位 ⇒ **共用实体的 config 以 `Shared` 为唯一权威**；再把 Admin/Auditor 里的**同名副本删掉**（⛔ 否则两份 JSON = 静默分叉）。
> **为什么必须加这行**：`Program.cs:53-56` 已写明后果 —— 漏列**不会报任何错**，但 `EntityConfigHelper` 会静默落到 `NewEmptyConfig`，症状 = **标题等于类型名、`Columns=[]`、页面「有数据行却一列都不显示」**（2026-09-25 实测过：`Auditor` 的 `Enterprise.json` 就这么丢过）。

---

## 五、分步实施（对应用户的三步策略）

### S1 · 建 `Shared`（**本册 = 这一步**）

| 步 | 动作 | 出口判据 |
|:--:|------|------|
| S1-1 | `Shared.csproj` 加 `DataBase` 引用 + `Assets\**` 复制规则（§4.1） | `dotnet build YZH.Core.Web.csproj` 通过 |
| S1-2 | 新建 `CertPlatformSharedServiceExtensions.cs` + `Program.cs` 调用（§4.2） | 启动日志无 DI 解析失败 |
| S1-3 | 建 `Shared/Assets/EntityConfigs/` + `Program.cs` 加路径且置首（§4.3） | 打开任一实体页面，列正常显示（非「一列不显示」） |
| S1-4 | 建 `Shared/Services/{Ai,Fill,Semantic}/` 三个目录（空） | — |
| S1-5 | **首个下沉件**：`FillParamValueProvider`（§3.4-1）+ 两处改调它（§3.3-1/2） | **`grep -rn "MapToInfo" CertPlatform.Admin` 只剩 1 处**；452/452 绿 |
| S1-6 | 下沉 AI 执行 5 文件（§3.1）+ `DocFillPrompt` 实体上移 | `grep -rn "IDbOrm db," ` 在 Ai 目录为 0（签名已修） |

> **S1 的定位**：**它不是「重构」，是「开一条路」**。做完 S1 之后，「下沉」就是一件**有模板的常规动作**（建类 → 加 DI → 改调用方），不再是架构讨论。

### S2 · 核心队列主线「企业资料 → 单个标准文件」

| 步 | 动作 |
|:--:|------|
| S2-1 | 实体上移收口（`57` 表 A：A1/A2/A3） |
| S2-2 | `EnterpriseDocNormalizationExecutor` 落点裁定（`57` §八 B1）并归位 |
| S2-3 | 收敛为 `DocumentFillOrchestrator`（`55` §四：`FillOneAsync`），补 5 处缺失（画像输入 / `profile` 来源 / 锁前置 / 账本写入 / `editable` 归一产物） |
| S2-4 | 干跑 `plan`（纯读零副作用）+ 单文件真跑 |
| S2-5 | 出口 = `G0`（数据）+ `G1`（编译测试）+ `G5`（单文件跑通） |

### S3 · 前后端串联

| 步 | 动作 |
|:--:|------|
| S3-1 | 22 端点落 `Auditor/Controllers/EnterpriseNormalizeController.cs`（⚠️ `ControllerBase` 例外须登记） |
| S3-2 | 前端 API 单一入口 `cert-share/src/api/ent/enterprise-normalize.ts` |
| S3-3 | 页面 `cert-auditor/src/pages/enterprise-normalize/` |
| S3-4 | 菜单 `MENU_AUD_12` `/enterprise-normalize`（⚠️ 实测空号，须新建）+ `sync_menu_urls.sh` |

---

## 六、`Shared` 与 `cert-share` 的关系（⛔ 别混）

| | `CertPlatform.Shared`（后端） | `cert-share`（前端） |
|---|---|---|
| 层 | .NET 项目 | 前端目录 |
| 放什么 | 实体 / 服务 / 纯函数 / `Assets` | 组件 / API 函数 / 类型 |
| 本次裁定 | ✅ 可引 `DataBase`、可放服务、可放 `Assets` | 不变（已有 `api/ent/*`、`components/CertBizTree.vue`） |
| ⛔ | **不放 Controller** | — |

---

## 七、守卫与验收（建议做成脚本，防回潮）

| # | 守卫 | 期望 |
|:--:|------|------|
| G-1 | `grep -rn "CertPlatform.Auditor.Entities" CertPlatform.Admin` | **恒为 0**（Admin 看不见 Auditor） |
| G-2 | `grep -rn "MapToInfo" CertPlatform.Admin` | **≤1 处定义**（现 2 处，D6） |
| G-3 | `grep -rn "using CertPlatform.Admin.Entities" CertPlatform.Shared` | **恒为 0**（Shared 不许反向依赖） |
| G-4 | `grep -rn "IDbOrm" CertPlatform.Shared/Fill CertPlatform.Shared/Office` | **恒为 0**（纯函数层不许查库） |
| G-5 | `CertPlatform.Admin.Tests` | **452/452 绿**（基线） |
| G-6 | `dotnet build YZH.Core.Web.csproj` | 通过（⚠️ **只编这一个，须串行**） |
| G-7 | `grep -rn "ControllerBase" CertPlatform.Shared/Controllers` | **恒为 0**（本方案不放 Controller） |

---

## 八、待裁（3 条，均**不阻塞 S1**）

| # | 裁决点 | 选项 | 建议 |
|:--:|--------|------|------|
| **C1** | `PromptWorkbenchService` 的**拆分点**划在哪？ | ① 按「有没有 `HttpContext`」拆（`AnalyzeForQueueAsync` 下沉）② 整体下沉 ③ 整体留 Admin | **①** —— ② 会把「后台专用」变共用；③ 会双端抄口径 |
| **C2** | `DocFillPrompt` 实体是否上移 `Shared`？ | ① 上移（与 `AiFillPromptBuilder` 一起）② 改为 DTO 传参 | **①** —— 它本就双端共用（Admin 配 / Auditor 用） |
| **C3** | `Shared/IServices` · `Repositories` · `WorkflowEngine` · `Data` 这四个预留目录 | ① 本次不用（⛔ 避免过度设计）② 现在就规划 | **①** —— 需要时再启用，⛔ 不为「整齐」而建空抽象 |

---

## 九、证据索引（可复现）

| 断言 | 证据 |
|------|------|
| `Shared` 已有完整预留目录且全空 | `find CertPlatform.Shared -type d` ⇒ `Controllers`/`Services`/`IServices`/`Repositories`/`IRepositories`/`WorkflowEngine`/`Data` **均存在、均 0 文件** |
| `Shared → DataBase` 无环 | `YZH.Core.DataBase.csproj:22` 只引 `Stand`；`YZH.Core.Stand.csproj` **零 `<ProjectReference>`** |
| `Shared.csproj` 已有 ASP.NET Core 引用 | `CertPlatform.Shared.csproj:13` |
| `LlmInvokeService` 已在 Shared | `CertPlatform.Shared/DocExtraction/LlmInvokeService.cs:82`（注释：「共享层，决策 D-5」） |
| `AiFillInvoker` 把 `db` 当参数传 | `AiFillInvoker.cs:35` `Task<AiFillInvokeResult> InvokeAsync(IDbOrm db, …)` |
| `AiFillPromptBuilder` 引 Admin 实体 | `AiFillPromptBuilder.cs:7` `using CertPlatform.Admin.Entities.Doc;` + `:246-254` `GetListAsync<DocFillPrompt>` |
| `bizRoot` 指向源码目录 | `Program.cs:58` `Path.Combine(ContentRootPath, "..", "..", "certplatform-api")` |
| `EntityConfig` 漏列 = 静默空配置 | `Program.cs:53-56`（2026-09-25 实测 `Auditor` 的 `Enterprise.json` 丢过） |
| 搜索顺序 = 列表顺序 | `Program.cs:57` |
| 文件行数 | `wc -l`：Ai 目录 8 文件 1327 行；`Fill/` 根 4 文件 523 行；`SourceResolver` 160 行 |
