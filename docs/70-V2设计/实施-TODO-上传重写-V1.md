# 实施 TODO — 企业资料上传重写（V1）

> **依据**：`docs/70-V2设计/36-企业资料上传重写-V1.md`
> **用法**：完成一项勾一项；⛔ 未勾的不跳（后面依赖前面的产物）
> **状态**：**P0 / P1 / P2 / P3 已完成并实测**（2026-10-29）；**P4 暂缓（用户裁决，见 P4 节）**；P5 未开工
> **前置**：`实施-TODO-认证准备-V1.md` 阶段 1 已完成（iso13485 67 条款 × 9 属性）

---

## 开工前 6 条裁决 —— 已全部由用户裁定（2026-10-29）

| # | 议题 | 裁定 |
|---|---|---|
| D-1 | 主表原地改造 vs 新建表 | ★ **重建表，重新定义结构**（原「原地改造」建议被推翻） |
| D-2 | `RelFolderPath` 保留但不强制 | 保留（老 237 行要用） |
| D-3 | 槽位层企业侧停产 | 停产 |
| D-4 | 旧 profile 分析（`doc_group`/`doc_content`）停 | 停 |
| D-5 | 提取标准来源 | ★ **`cert_audit_plan`（一企业一有效计划）**，⛔ 不再从机构反推 |
| D-6 | 队列一任务内串行 | 保留 |
| D-7 | 一企业一计划的强约束方式 | 代码作废（新增即作废旧），⛔ 不用唯一索引（MySQL 无部分唯一索引） |

> ⚠️ **D-1 的连带影响**：P0/P1 的实际做法与最初计划完全不同 ——
> 不是「给老表删 `StageCode`」，而是**新建 4 张表 + 新服务 + 新控制器 + 新执行器**，
> 老表 `cert_enterprise_original_*` 原样保留到 P5 迁移后停用（⛔ 不 DROP）。

---

## P0 · 数据模型（✅ 已完成）

### P0.1 建 4 张新表

- [x] 脚本 `scripts/db/20261029_enterprise_doc_rebuild_V1.sql`（幂等 `CREATE TABLE IF NOT EXISTS`）
  - [x] `cert_enterprise_doc`（唯一一层：无阶段 / 无标准 / 无槽位）
  - [x] `cert_enterprise_doc_version`（⚠️ 表名避开已占用的 `cert_enterprise_file_version`）
  - [x] `cert_enterprise_doc_upload_task`（上传批次，去阶段）
  - [x] `cert_document_extraction`（文档 × 标准 = 一份 JSON）
- [x] 实库执行（`temp/apply_sql.py`，4 条 CREATE 全 OK）
- [x] 核对索引：`uk_code` / `idx_ent_path` / `idx_sha256` / `idx_upload_task` / `idx_extract_status` /
      `idx_file_ver` / `idx_ent_status` / `uk_file_std` / `idx_std_clauses` / `idx_ent` / `idx_status` 全部落位
- [x] 老表 237 行未动（脚本末尾验证 SELECT 打印 `237`）
- [x] `UpdateTime` 全部 `DATETIME DEFAULT NULL`（铁律，否则新增时炸「必填字段为空」）

### P0.2 实体同步

- [x] `Auditor/Entities/Dir/EnterpriseDoc.cs`（实现 `ISoftDelete` + `IIsValid`）
- [x] `Auditor/Entities/Dir/EnterpriseDocVersion.cs`
- [x] `Auditor/Entities/Dir/EnterpriseDocUploadTask.cs`
- [x] `Auditor/Entities/Dir/DocumentExtraction.cs`（★ 落在 `Dir/` 而非计划里的 `Doc/`，与同族实体同目录）

### P0.3 一企业一有效计划

- [x] `AuditPlanController.InvalidateOtherPlansAsync`（`OnAfterAdd` / `OnAfterUpdate` 触发）
  - ★ 放 **after** 不放 before：插入失败时旧计划已被作废 ⇒ 企业零有效计划，上传模块拿不到标准
  - ⛔ 只作废不删行不删明细（历史计划与部门×条款关系必须留痕）
  - `Remark` 追加「[时间 自动作废] 同一企业只允许一个有效计划，已被新计划取代。」
- [x] `save` 响应新增 `invalidatedPlans`（本次自动作废条数）
- [x] 新增 `GET active?enterpriseCode=`（当前有效计划 + `standardCodes` + `warnings`）
- [x] **实测**：同企业建第二个计划 → `invalidatedPlans=1`，旧计划 `IsValid 1→0` + Remark 留痕；
      测试数据已还原（旧计划 `IsValid=1`、`Remark=NULL`，临时计划物理删除）

**出口（达成）**：`dotnet build` 0 Error；4 表存在；索引齐全；237 行完整。

---

## P1 · 后端去阶段（✅ 已完成）

### P1.1 存储路径

- [x] `DocumentLibrary.EnterpriseDoc = 4` + `DocumentLibraryPath.EnterpriseDocPrefix = "enterprise-doc"`
      （加进 `Prefixes` / `PrefixOf` / `Resolve` 首段精确匹配）
- [x] `PathBuilder.EnterpriseDoc(ent, folder, fileName)` → `/enterprise-doc/{Ent}/{Folder}/{File}`
- [x] ⛔ **保留** `PathBuilder.EnterpriseOriginalSource`（老 237 行 `StoragePath` 是持久化值，解析要用）
- [x] 单测 `Prefixes_ContainsExactlyFourLibraries`（3→4）+ `Resolve_EnterpriseDoc_DoesNotCollideWithEnterpriseDocuments`
      + `PathBuilderTests.EnterpriseDoc_*` ×5（含「无阶段段」护栏、空身份抛异常、产物派生、归档）

### P1.2 新服务 `EnterpriseDocService`（~1000 行）

- [x] `EnterpriseTreeAsync()` —— 企业**一层**树（文件计数 + `HasPlan` + 计划标准清单）
- [x] `ListAsync(enterpriseCode, standardCode?, keyword?, onlyFailed?)` —— 无阶段；
      `standardCode` 非空 = 只返回该标准下有提取产物的文件
- [x] `StatusBarAsync(enterpriseCode)` —— 转换/提取计数 + `IsBusy`
- [x] `PlanUploadAsync` / `UploadInitAsync` / `UploadFileStepAsync` / `UploadConfirmAsync` / `UploadCancelAsync`
      —— **五段式入参全部不含 `StageCode` / `StandardCode`**
- [x] `ScopeKeyOf(ent) => $"ent:{code}"`（原 `{ent}@{stage}`）
- [x] `ResolvePlanStandardCodesAsync` —— 读 `cert_audit_plan`，空则返回**原因文案**（⛔ 不静默）
- [x] `GetActivePlanAsync` / `RecomputeExtractStatusAsync` / `MarkExtractionStaleAsync`
- [x] `DeleteAsync` / `BatchDeleteAsync` / `GetVersionsAsync` / `RestoreVersionAsync` / `RegenerateAsync`
- [x] `GetMarkdownAsync` / `QueueDetailAsync` / `DownloadAsync` / `GetPreviewPathAsync`

### P1.3 新控制器 `EnterpriseDocController`（`api/Auditor/EnterpriseDoc`）

- [x] `tree` / `list` / `status-bar` / `queue-detail` / `markdown/{fileCode}`
- [x] `upload/plan|init|file|confirm|cancel`
- [x] `delete` / `delete/batch` / `versions/{fileCode}` / `restore` / `regenerate`
- [x] `plan-standards?enterpriseCode=`（★ 标准来源唯一入口）
- [x] `download` / `file-preview/{fileCode}` / `file-markdown/{fileCode}`

### P1.4 新执行器 `EnterpriseDocConvertExecutor`

- [x] `TaskType = enterprise_doc_convert`（22 字符 < 30 列宽）
- [x] 格式归一 → PDF 链 → Markdown 链（**无分析段**，⛔ 不再打标签/画像）
- [x] 列级写回（⛔ 禁止 `UpdateAsync(entity)` 全列回写）
- [x] `SetExtractPendingAsync`：Markdown 就绪 ⇒ `ExtractStatus='pending'`；已 `done/running/partial` 不降级
- [x] DI 注册（`AddScoped<EnterpriseDocService>` + `AddSingleton<IYzhTaskExecutor>`）

### P1.5 实测（wzm 账号，企业 `测试开发企业`）

| 步骤 | 结果 |
|---|---|
| `plan-standards` | ✅ 返回计划 + 2 个标准 Code |
| `tree` | ✅ 2 家企业；`测试开发企业` files=1 HasPlan=True stds=2；`A公司` HasPlan=False |
| `upload/plan` | ✅ `Action=create`；`evil.exe` 被拦、`.DS_Store` 静默剔除（`FilteredCount=1`） |
| `upload/init` | ✅ 路径 `/enterprise-doc/{Ent}/1 体系文件/质量手册-临时验证.txt`（无阶段无标准） |
| `upload/file` | ✅ `Action=create` `VersionNumber=1` |
| `upload/confirm` | ✅ 队列 `Q-20261029-15661151f` |
| 转换完成 | ✅ `ConvertStatus=completed` + `MarkdownStatus=completed` + `ExtractStatus=pending` |
| PDF 预览 | ✅ HTTP 200，24720 字节，`PDF document, version 1.7, 1 pages` |
| Markdown 文本 | ✅ UTF-8 中文正确，51 字 |
| **替换（异 hash）** | ✅ `Action=replace` `VersionNumber=2`；主行 `StoragePath` **恒定**；v1 归档到 `_archive/质量手册-临时验证.txt.v1`；`versions` 端点返回 v1 |
| **幂等（同 hash）** | ✅ `Action=skip` |
| 删除 | ✅ 软删 + 删对象 + 级联软删提取产物 |
| 清理 | ✅ 4 张新表已清空（冒烟数据不留在实库） |

**出口（达成）**：`dotnet build` 0 Error；测试 **813/813**（`Admin.Tests` 774 + `Core.Api.Tests` 39）。

---

## ★ 顺带修掉的既存缺陷（P1 实测发现）

### 缺陷：纯文本文件（`.txt` / `.md` / `.csv`）转 Markdown 必失败且白烧一次 OCR

- **现象**：131 字节的 `.txt` → `MarkdownStatus=failed`，`MarkdownMessage=视觉识别失败：AI 调用失败（400）：The image format is illegal`
- **根因**：`FileConvertCore.ConvertMarkdownInnerAsync` 先调 anydoc；`.txt` 被判 `Unsupported`
  ⇒ 落「分支 B」⇒ 因为视觉模型可用，被丢去 `TryOcrAsync` ⇒ **把纯文本字节当图片送模型** ⇒ 恒 400
- **影响面**：4 个调用方全部中招（标准目录 / 企业资料库 / 企业原始资料 / 企业文档）
- **修法**：`FileConvertCore` 增「分支 0 纯文本直通」——
  `.txt/.md/.markdown/.csv` 不进 anydoc，直接按 UTF-8（失败则 GB18030）解码当 Markdown，
  去 BOM；两者都解不出才 `failed`（⛔ 不静默产乱码）
- **验证**：重转后 `MarkdownStatus=completed`、`MarkdownMessage=纯文本直通（无需转换）`

---

## P2 · L1 条款提取（✅ 已完成 2026-10-29，实测通过）

- [x] `DocumentExtractionService`（`Auditor/Services/Ent/DocumentExtractionService.cs`）
  - [x] `UpsertAsync(...)` —— **原地更新，`Code` 恒定**（⛔ 不「先删后插」：换 Code 会断下游引用）；
        曾软删的行**复活**而不是新插（uk `FileCode+StandardCode` 撞了报错文案看不出是 uk）
  - [x] `ListByClauseAsync(standardCode, clauseNumber)` —— `FIND_IN_SET` 参数化，⛔ 不用 `LIKE`
  - [x] `MarkStaleAsync(fileCode)` —— **只置 `IsStale`，不改 `ExtractStatus`**
        （改了会把「已完成的旧结果」伪装成「正在跑」，前端一直转圈）
  - [x] `SoftDeleteByFileAsync` —— 删文档时级联软删产物（否则「按条款查证据」查出已删文件的产物）
  - [x] `ParseContent` —— 坏 JSON 返回 `null` + 记日志，⛔ 不抛（一条脏数据不该打挂整页）
- [x] 提示词落 `cert_prompt_template`
  - [x] `PromptCode = doc_clause_extract`（**⛔ 不是 `doc_extract`** —— 那个已被 V1 四元组定位规则占用）
  - [x] 四条硬规则逐字写进正文；`scripts/db/20261029_doc_clause_extract_prompt_V1.sql` 已执行（幂等）
  - [x] C# 兜底常量 `DocExtractPromptService.FallbackTemplate` **与 SQL 正文逐字一致**（脚本生成，非手抄）
  - [x] 两级回退：标准级（`ScopeKey = iso13485`）→ 平台级 → 代码兜底；`fallback` 必须写进 `Message`
  - [x] 条款清单只给 `DocCheckMode ≠ none`（实测 iso13485 = 67 条）
- [x] `ClauseExtractExecutor`（`TaskType = enterprise_clause_extract`）
  - [x] 一任务 = 文档 × 标准；**单标准失败不阻断**其他标准（各自独立队列任务）
  - [x] `SourceSha256` + `IsStale` + `ExtractStatus=done` 三条件同时满足 ⇒ 幂等跳过（不调模型）
  - [x] 长文档按**段落边界**切块（30000 字符/块，上限 8 块），结果按条款号合并去重
  - [x] 编号不在清单内的输出**丢弃并计数**（模型自造编号会污染「按条款查证据」且不报错）
  - [x] 字段读取大小写不敏感（模型偶发 `clauseNumber`，严格匹配会整条丢弃且不报错）
  - [x] 完成后回写 `EnterpriseDocService.RecomputeExtractStatusAsync`
- [x] 无计划的企业 ⇒ 提取 `skipped` + 原因（`ResolvePlanStandardCodesAsync` 文案）
- [x] 自动接力：`EnterpriseDocConvertExecutor` 在 Markdown 就绪后入队提取（**尽力而为**，
      入队失败只记日志，⛔ 不让「转换成功」变成「转换失败」）
- [x] 手动入口：`POST EnterpriseDoc/extract`（可反复点，已完成的自动跳过）+ 自动接力 `sourceTag=convert`
- [x] 只读反查：`GET EnterpriseDoc/extraction/by-clause`（`检查表` 阶段的取数入口）

### P2 实测（企业 `测试开发企业`，计划 = iso13485 + iso9001）

| 项 | 结果 |
|---|---|
| 自动接力 | 转换完成后自动建 `enterprise_clause_extract` 队列，2 个任务（每标准 1 个） |
| iso13485（67 条清单） | **19 条**，锚点逐字命中 **19/19 = 100%** |
| iso9001（未配 `DocCheckMode` ⇒ 全部叶子 78 条） | **24 条**，锚点命中 **23/24 = 96%**，`Message` 如实提示「按全部叶子条款提取」 |
| 幂等 | 再点一次 ⇒ `Queued=0 / Skipped=2`，**不调模型** |
| 替换（改内容 → v2） | 两行 `IsStale=1` 且 hash 变 ⇒ 自动重跑：iso13485 **19→20 条**、iso9001 **24→29 条** |
| `by-clause` 精确性 | `4.2.4` 命中 1 行；`4.2.45` 命中 **0 行**（`FIND_IN_SET` 精确，⛔ `LIKE` 会误中） |
| 判定词泄漏 | 0 例（抽查命中的「不符合」全部来自**企业原文**如「内审不符合项数」，非模型结论） |

**⚠️ P2 遗留（已知，不阻断）**
1. **锚点标点归一**：模型把原文的 `"` 输出成中文引号 `“”` ⇒ 逐字比对会 miss（iso9001 的 1/24）。
   锚点率仍 96% 达标，但下游若要做「原文定位」必须**先归一标点再比**，⛔ 不能直接 `IndexOf`。
2. iso9001 / iso14001 的 `DocCheckMode` 尚未配置（各 40 行属性只配了一部分）⇒ 目前退化为「全部叶子条款」。
   补跑「条款属性预生成」即可恢复精确过滤（`Message` 已如实告知，⛔ 不静默）。

**出口（V0 三个数字）**：12 份样本 → 平均 **3–8 条款/份**；锚点率 **> 80%**；任一条款能拼 **≥2 份文档**。
→ 锚点率已达标（96–100%）；另两项需 P5 全量样本才能定论（单份手册命中 19–29 条属正常，手册本身覆盖面广）。

---

## P3 · 前端重写（✅ 已完成 2026-10-29 21:40，实测通过）

### P3.1 新增文件（4 个 + 1 单测，共 3,954 行）

| 文件 | 行数 | 职责 |
|---|---:|---|
| `cert-share/src/api/ent/enterprise-doc.ts` | 639 | 共享 API 层，导出前缀统一 `entDoc*` |
| `cert-auditor/src/pages/enterprise-doc/pure.ts` | 460 | 纯函数层（⛔ 无 Vue 状态 / 无请求 / 无 `ElMessage`） |
| `cert-auditor/src/pages/enterprise-doc/useUpload.ts` | 383 | 上传状态机 `createUploadState(host)` |
| `cert-auditor/src/pages/enterprise-doc/logic.ts` | 848 | `EnterpriseDocLogic extends SingleTableCore<any>` |
| `cert-auditor/src/pages/enterprise-doc/index.vue` | 1220 | 页面（`YzhTreeTableLayout` + `YzhTable`） |
| `cert-auditor/src/pages/enterprise-doc/logic.test.ts` | 404 | **42 个纯函数单测**（全绿） |

### P3.2 清单

- [x] 左树：企业**一层** + 「按标准查看」筛选视图（⛔ 不新增存储层）
  - `tree` 端点直接给 `HasPlan` / `StandardCodes`，筛选只在**前端过滤**（`standardCode` 传给 `list`，⛔ 不建第二层树）
  - 首屏自动选「有计划且有文件」的企业（避免空白页当第一印象）
- [x] 文件表（`YzhTable`，守卫 R6）：文件名 / 文件夹 / 大小 / **转换** / **提取** / 版本 / 操作
  - ⛔ 不内联 `<el-table>`；`data-loader` + `show-pagination=false`（后端已一次给全）
  - 提取列口径唯一走 `extractBadge()`（失败 → 已过期 → 提取中 → 待提取 N 个 → 全 done 合计条数 → skipped）
  - ★ `IsStale` **优先于** `ExtractStatus`（否则「已完成的旧结果」显示成「已提取」，用户以为不用重跑）
- [x] 上传弹窗：去掉阶段与标准选择；标准只读展示「来自计划」
  - 4 阶段：`pick` → `plan`（服务端 diff：create/replace/skip）→ `uploading` → `done`
  - 无计划时**不发上传请求**，直接给 `planReason` 文案
- [x] 提取结果抽屉：按标准 Tab（条款号 / 条款标题 / 锚点 / 摘录 / 置信度 / 定位原文）
  - Tab 顺序 = **计划标准顺序**，历史多出的标准补最后（⛔ 按字母序会打乱用户认知）
  - 「定位原文」→ 打开原文核对抽屉，锚点 `<mark>` 高亮 + 命中方式徽章
- [x] 队列进度横幅复用（`status-bar` + `queue-detail`）
- [x] 保留：PDF 预览 / MD 下载 / 版本时间线 / 替换 / 删除 / 5s 轮询
  - 轮询：5s 只刷 `status-bar`，`signature()` 变了才 `refresh()` 列表；`document.hidden` 跳过

### P3.3 ★ 关键设计决策（踩过的坑，写在这里防回退）

| # | 决策 | 理由 |
|---|---|---|
| 1 | 覆盖 `refresh()`：无 `_tableRef` 时直接 `await this.dataLoader()` | 零行时用 `YzhEmptyState` 占位 ⇒ `YzhTable` 未挂载 ⇒ 基类 `_tableRef?.refresh()` 对 `null` **静默无操作** ⇒ 首屏永远「共 0 份文件」 |
| 2 | 上传配对用**双指针顺序走**，⛔ 不建 `FileName → File` 字典 | 同名文件（不同文件夹）会塌缩成一个，静默少传 |
| 3 | 上传行 `row-key` 用 `RelFolderPath|FileName`，⛔ 不用 `FileName` | 同上 |
| 4 | `sha256OfFile` 超 64MB 或 `crypto.subtle` 不可用 ⇒ 返回 `''` | 降级为「替换」，⛔ 不能因为算不出 hash 就中断上传 |
| 5 | 锚点定位三级递进 `exact → punctuation → reflow` | P2 遗留 #1：模型把 `"` 输出成 `“”`，直接 `IndexOf` 恒 miss |
| 6 | `reflow` 级用 `stripWhitespace`（**剥掉**全部空白），⛔ 不是折叠成空格 | 折叠后原文 `程序，\n确保` 与锚点 `程序，确保` 仍差一个空格 ⇒ 永远匹配不上 |
| 7 | 方法名避开基类：`fileColumns` / `docRowActions` / `handleRowAction` / `onRowsSelected` | `columns` / `rowActions` / `rowActionButtons` 是基类 getter（TS2610）；`onRowAction` / `onSelectionChange` 是基类实例属性（TS2425） |
| 8 | `onNodeClick(node: any)` | `YzhTreeTableLayout` 回传组件自身的 `YzhTreeNode`，声明 `EntDocNode` 会 TS2345 |

### P3.4 实测（1440×900 视口，`wzm` 账号，企业 `测试开发企业`）

| 视图 | 结果 |
|---|---|
| 首屏 | 左树 2 家企业；页头「共 1 份文件，1 份可提取，1 份已提取」 |
| 文件表 | 7 列齐全；行 `已转换` / `已提取 49 条` / `v2` |
| 操作下拉 | 8 项：提取结果 / 预览 PDF / 查看原文 / 下载原件 / 历史版本 / 重新生成 / 重新提取 / 删除（danger） |
| 提取结果抽屉 | 2 个 Tab「ISO 13485 医疗器械质量管理体系（20 条）」「GB/T 19001-2016 质量管理体系 要求（29 条）」；头部 `doc_clause_extract / v1` + 提取时间；6 列表格，置信度全 95%，「定位原文」可见可点 |
| 原文核对抽屉 | 标题「原文 — 质量手册-提取验证.txt」，徽章「**逐字命中**」，条款 `4.2.4`，`4.2.4 文件控制` 整段 `<mark>` 黄底高亮 |

**出口（全部达成）**
- `node scripts/guards.mjs`：24 → **24**（`enterprise-doc` **零新增**；剩余 24 处全为 `audit-plan` 等历史债务，待裁决）
- `vue-tsc --noEmit` 两端 **0**；`npm run typecheck:all`（5 workspace）**0**
- `vitest run`（cert-auditor）**148/148**（新增 `enterprise-doc/logic.test.ts` **42/42**）
- vite 模块编译：4 个文件全部 HTTP 200
- 后端契约 curl 实测：`tree` / `list` / `markdown` 返回结构与本页类型定义**逐字段对齐**

---

## P4 · 下线与清理（⏸ **暂缓 —— 2026-10-29 用户裁决**）

> ### ★★★ 用户裁决（2026-10-29 21:55，原话）
> **「不动之前的代码，我们重新编写新的逻辑。我们是新的表、新的逻辑、新的菜单，待 V2 稳定后，再开始清理 V1 的残留。」**
>
> ⇒ **P4 整体暂缓**，V1 残留一律保留：
> `pages/resources/` · `pages/enterprise-original/` · `EnterpriseOriginalController/Service` ·
> `EnterpriseFileController/Service` · `MENU_AUD_04/11/12` 及其页面与单测 —— **一行不动**。
>
> ⇒ **新页面必须有自己的菜单**（P4.0 已补，见下）。

### P4.0 ✅ 新菜单（2026-10-29 已落地）

- [x] 新路由 `/enterprise-doc`（`name: EnterpriseDoc`）→ `pages/enterprise-doc/index.vue`
- [x] `/resources` 恢复指向 V1 老页面 `pages/resources/index.vue`（⛔ 不再被新页占用）
- [x] 新菜单 `MENU_AUD_18`「企业资料」`/enterprise-doc`，OrderNo **295**（紧跟「认证计划」290）
  - 脚本 `scripts/db/20261029_enterprise_doc_menu_V1.sql`（幂等，已执行；含 `Sys_RoleMenu` 授权 1 条）
- [x] `bash scripts/db/verify/sync_menu_urls.sh` 重导快照（52 条）
  - ★ 顺带修掉 R12 的 `/audit-plan` **假阳性**（旧快照过期，缺 `MENU_AUD_17`）⇒ 守卫 **24 → 23**
- [x] `vue-tsc` 全工作区 0

**当前专家端资源类菜单（两代并存）**

| 菜单 | Url | 代际 |
|---|---|---|
| `MENU_AUD_17` 认证计划 | `/audit-plan` | V2 |
| **`MENU_AUD_18` 企业资料** | **`/enterprise-doc`** | **V2 ★ 本轮新增** |
| `MENU_AUD_04` 认证资料管理 | `/resources` | V1 |
| `MENU_AUD_11` 企业原始资料 | `/enterprise-original` | V1 |
| `MENU_AUD_12` 企业资料规范化 | `/enterprise-normalize` | V1 |

### P4.1 盘点结论（只读，已核对代码 + 实库；**留作将来清理时的依据**）

| 对象 | 实际情况 | 对原计划的影响 |
|---|---|---|
| `pages/resources/` | 5 个文件 118 KB；**仅被 `router/index.ts` 一行注释提到**，页面已不再被引用 | ✅ 可整目录删 |
| `pages/enterprise-original/` | 4 个文件 128 KB；**仅被路由 + 自己内部引用** | ✅ 可整目录删 |
| `@share/api/ent/enterprise-original.ts` | ★ **仍被 `enterprise-normalize` 的 `logic.ts` / `create-logic.ts` 引用**（`fetchStageTree` → `POST /EnterpriseOriginal/stage-tree`） | ⛔ **不能删**，只能删页面 |
| `EnterpriseOriginalController` | ★ 同上，`stage-tree` 仍是在用端点 | ⛔ **不能整只下线**，只能留 `stage-tree` 一个动作 |
| 3 个老执行器（Ingest / Analyze / File） | `yzh_queue` 实查：`enterprise_original_file` 只有 `completed 24` / `failed 27` / `cancelled 2`，**零 pending/processing** | ✅ 可安全摘 DI 注册 |
| `EnterpriseFileService` | ★ **被 `EnterpriseStageController:475` 用**（`InitEnterpriseDirectoryAsync`） | ⛔ 服务不能停用，只能下线 `EnterpriseFileController` |
| `EnterpriseFileController` | 前端 **零调用**（`enterprise-file.ts` 只有定义没有调用方） | ✅ 是死代码，可整只删 |
| `cert_standard_directory_file` | 实查 **template 33 + enterprise 60** | ✅ 与计划一致（停用那 60 行） |
| `MENU_AUD_11`（企业原始资料 `/enterprise-original`） | 实库 `IsValid=1` | ✅ 置 0 |
| 菜单快照 `menu-urls.tsv` | ★ **过期**：实库有 `MENU_AUD_17 /audit-plan`（IsValid=1），快照里没有 ⇒ 守卫 R12 报「孤儿路由」是**假阳性** | ✅ 重新导出即可顺带消掉这条债务 |

### P4.2 前端（删 2 个目录 + 1 条路由）

- [ ] 删 `cert-auditor/src/pages/resources/`（5 文件）
- [ ] 删 `cert-auditor/src/pages/enterprise-original/`（4 文件）
- [ ] 删 `router/index.ts` 的 `/enterprise-original` 路由块 + 更新 `/resources` 那行注释
- [ ] ⛔ **保留** `cert-share/src/api/ent/enterprise-original.ts`（`enterprise-normalize` 还要 `fetchStageTree`）
  - 文件头补一句「⚠️ 页面已下线（P4），本文件只剩 `fetchStageTree` 在用；其余函数待 `enterprise-normalize` 改造后一并删」

### P4.3 后端

- [ ] `EnterpriseOriginalController` **只留 `stage-tree`**，其余 action 删除；类注释改「仅保留 `stage-tree`（`enterprise-normalize` 在用），其余端点随页面于 P4 下线」
- [ ] `EnterpriseOriginalService` **只留 `StageTreeAsync`**（其余方法删除）
- [ ] 摘掉 3 个老执行器的 `IYzhTaskExecutor` 注册（`EnterpriseOriginalIngestExecutor` / `AnalyzeExecutor` / `FileExecutor`）
  - ⚠️ 类文件先不删，只摘注册（留一周观察，确认无漏网任务再删文件）
  - 依据：实库零 pending/processing
- [ ] 删 `EnterpriseFileController.cs`（前端零调用）
- [ ] ⛔ **保留** `EnterpriseFileService`（`EnterpriseStageController` 在用）
- [ ] `cert_standard_directory_file` 企业侧 60 行标停用（⛔ 不物理删）
  - 判据 = `EnterpriseCode <> ''`；`IsValid=0` + `Remark` 追加「[2026-10-29 停用] 企业资料已迁至 cert_enterprise_doc」

### P4.4 数据

- [ ] `UPDATE Sys_Menu SET IsValid=0 WHERE Code='MENU_AUD_11'`（⛔ 不删行，保留可回滚）
- [ ] `bash scripts/db/verify/sync_menu_urls.sh` 重新导出快照
  - ★ 顺带修掉 R12 的 `/audit-plan` 假阳性（快照过期导致）

### P4.5 守卫基线

- [ ] 重跑 `node scripts/guards.mjs`，确认违规数**只减不增**
- [ ] 若 `ORPHAN_ALLOW` 里出现 `auditor:/resources` 之类的新孤儿，登记并写理由

### P4.9 裁决结果（2026-10-29 21:55，已定）

| # | 议题 | 裁定 |
|---|---|---|
| **D-P4-1** | `enterprise-normalize` 还在用 `EnterpriseOriginal/stage-tree` | **不动** —— V1 老控制器一行不改，等 V2 稳定后统一清理 |
| **D-P4-2** | `EnterpriseFileService` 被 `EnterpriseStageController` 用 | **不动** —— 同上 |
| **D-P4-3** | 删页面会掉单测 | **不删** —— 「待 V2 稳定后，再开始清理 V1 的残留」 |
| **D-P4-4**（新增） | 新页面用谁的菜单 | ★ **新菜单** `MENU_AUD_18` `/enterprise-doc`，⛔ 不复用 V1 的 `MENU_AUD_04` |

**V1 清理的前置条件（将来开工的判据）**：V2 企业资料页跑通「上传 → 转换 → 按标准 L1 提取 → 结果可用」全链路，且
`enterprise-normalize` 不再依赖 `EnterpriseOriginal/stage-tree`。届时 P4.1 的盘点表直接复用。

---

## P5 · 全量回归（未开工，0.5 天）

- [ ] 237 文件从 `cert_enterprise_original_*` 迁入 `cert_enterprise_doc`（⛔ 不搬 MinIO 对象，`StoragePath` 原样保留）
- [ ] 237 文件 × 计划标准（iso13485 + iso9001）全量 L1（增量跑）
- [ ] 对账：`cert_document_extraction` 行数 = 文件数 × 标准数（或 `skipped` 有原因）
- [ ] 抽 5 份看质量（手册 / 程序 / 记录 / 台账 / 证书 各 1）

**出口**：提取覆盖率 ≥ 90%；失败行 100% 有 `ExtractMessage`。

---

## 基线（每批出口必跑）

- `dotnet build CertPlatform.sln` 0 Error
- 后端测试 **826/826**（`Admin.Tests` 787 + `Core.Api.Tests` 39）
- 前端测试 **633/633**（`cert-admin` 485 + `cert-auditor` 148；P3 新增 42 个纯函数单测）
  - ⚠️ `vitest` **⛔ 不能在 workspace 根跑**（根无 `test` 脚本，`@share/*` 别名只在各 workspace 配 ⇒ 25 个文件报 `Failed to load url @share/...`）；必须分端跑
- `npx vue-tsc --noEmit` 两端 0；`npm run typecheck:all`（5 workspace）0
- `node scripts/guards.mjs` 零新增违规（当前 24 处历史债务，见下）

---

## ⚠️ 累积遗留（已知，不阻断后续阶段）

| # | 遗留 | 影响 | 处置 |
|---|---|---|---|
| 1 | 锚点标点归一（模型输出中文引号） | — | **P3 已修**：`locateAnchor` 三级递进（消费侧兜住） |
| 2 | iso9001 / iso14001 的 `DocCheckMode` 未配齐 | 退化为「全部叶子条款」 | 补跑条款属性预生成（`Message` 已如实告知） |
| 3 | `AiConnSettings` 是第 5 处 AI 配置读取副本 | 改配置要改 5 处 | `Shared` ⛔ 不引 `YZH.Core.Api` 的硬约束所致；应下沉到 `Shared/Services/Sys/` |
| 4 | **前端守卫 24 处历史债务** | 守卫不干净 | 全在 `audit-plan/index.vue`（S04/S07/S08 11 处 + R6 内联 `el-table`）+ R12 孤儿路由 + R17 两处 + R-B，**待用户裁决** |

---

## 风险提示

| # | 风险 | 盯法 |
|---|---|---|
| 1 | 老 237 行路径含 `{Stage}` 段，被「按规则重算」算错 | 路径一律读 DB `StoragePath`；`PathBuilder` 只用于新上传 |
| 2 | `enterprise-doc` 与 `enterprise-documents` 只差几个字母 | `Resolve` 首段**精确相等**；已有单测护栏 |
| 3 | 队列 ScopeKey 从 `{ent}@{stage}` 变 `ent:{ent}`，老队列悬空 | P4 停用老服务前先清空未完成任务 |
| 4 | 锚点被模型改写成中文引号 ⇒ 逐字比对 miss | P3 已三级递进兜住；⛔ 不要再直接 `IndexOf` |
| 5 | `DocCheckMode` 未配的标准 ⇒ 退化为「全部叶子条款」 | `ExtractMessage` 如实写「按全部叶子条款提取」，⛔ 不静默 |
| 6 | 提示词改了但库里旧行还在 ⇒ 改了不生效 | `ResolveAsync` 返回 `fallback/stored`，执行器把它写进 `Message` |
| 7 | 长文档超 8 块 ⇒ 尾部未提取 | `Message` 写「文档超过 N×M 字符，超出部分未提取」 |
| 8 | 无计划企业「提取一直不动」 | 上传时 `plan-standards` 立刻给原因；`ExtractStatus=skipped` |
| 9 | 一份文件 × N 标准 = N 次调用，成本被低估 | 先跑单企业子集，再全量 |
| 10 | 文档章节号冒充条款号 | 提示词硬规则 4 + L2 侧白名单二次校验 |
| 11 | 两代实现并存期，有人误调老端点 | 老控制器类注释标「已停用，见 `EnterpriseDocController`」 |
