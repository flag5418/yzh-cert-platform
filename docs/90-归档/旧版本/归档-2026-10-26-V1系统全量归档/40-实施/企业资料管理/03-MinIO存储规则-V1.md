# 03 — MinIO 存储规则 V1

> **实现权威**：`/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/certplatform-api/CertPlatform.Shared/Storage/PathBuilder.cs`（318 行，**唯一路径构造器**）
> **抽象层**：`YZH.Core.Stand/Interfaces/IObjectStorage.cs` + `YZH.Core.DataBase/Infra/MinioObjectStorage.cs`
> **铁律**：**除 `PathBuilder` 外，任何地方不得手工拼接存储路径**。

---

## 一、连接配置（实测）

| 项 | 值 | 来源 |
|---|---|---|
| Endpoint | `127.0.0.1:9000`（容器 `yzh-minio`，控制台 9001） | `src/yzh-core/YZH.Core.Web/appsettings.json` → `MinIO:Endpoint` |
| AccessKey / SecretKey | `admin` / `Yzh123456.` | 同上 |
| **Bucket** | **`cert-platform`** | `MinIO:BucketName`（`MinioObjectStorage` 默认值同为 `cert-platform`） |
| 客户端 | `Minio` SDK（`IMinioClient`），DI 在 `YZH.Core.Web/Extensions/StorageServiceExtensions.cs` | — |
| 可插拔 | `IObjectStorage` 实现：`MinioObjectStorage` / `AliyunOssStorage`（换存储零业务改动） | — |

> **MinIO 语义**：S3/MinIO 是**扁平 key 存储**，控制台按 `/` 分组显示成"目录"。⇒「建库 / 建文件夹」**不需要任何 API 调用**，路径在代码里算好即可；只有对象（文件）才真实存在。

---

## 二、双文档库（前缀即归属）

`CertPlatform.Shared/Entities/Dir/DocumentLibrary.cs` —— **库前缀的唯一权威源**：

| 库 | 枚举 | 前缀（首段） | 归属主体 | 启用状态 |
|---|---|---|---|---|
| 标准目录库 | `DocumentLibrary.StandardDirectory = 1` | `standard-directory` | 机构（`OrgCode`） | ✅ 已用 |
| 企业文档库 | `DocumentLibrary.EnterpriseDocuments = 2` | `enterprise-documents` | 企业（`EnterpriseCode`） | ⚠️ **已预留、零调用 —— 本模块首次启用** |

`DocumentLibraryPath` 提供：`PrefixOf()` / `Normalize()` / `Resolve(path)`（**只按首段匹配**，防 `foo/standard-directory/…` 伪造路径）/ `IsAllowedStoragePath(path)`（白名单 = 已知库前缀 + 无 `..`/`.`/空段）/ `IsUnder(path, library)`。

> ⚠️ 历史缺陷已修：旧 `ControllerSafetyExtensions` 硬编码 `StartsWith("standard-directory/")`，会让**企业库**的 `file-preview`/`file-markdown`/`download` 请求被**静默拒绝**（业务失败恒 HTTP 200，前端只看到"未找到文件"）。现统一委托 `DocumentLibraryPath.IsAllowedStoragePath`。

---

## 三、路径规则（四类，全部由 `PathBuilder` 产出）

### 3.1 标准目录文件（模板/机构侧）

```
/standard-directory/{OrgCode}/{StandardCode}/{StageCode}/{FolderPath}/{FileName}
```

`PathBuilder.StandardFile(orgCode, standardCode, stageCode, folderPath, fileName)`

### 3.2 **企业资料文件（本模块）**

```
/enterprise-documents/{EnterpriseCode}/{StandardCode}/{StageCode}/{FolderPath}/{FileName}
```

`PathBuilder.EnterpriseFile(enterpriseCode, standardCode, stageCode, folderPath, fileName)`

**为什么首段是企业 Code（2026-09-27 用户裁定）**：两库统一规则「首段 = 资料归属主体的 Code」；`cert_enterprise` 与机构 1:1（`OrgCode` 单列、uk 含 `OrgCode`）⇒ `EnterpriseCode` 已隐含机构，无需冗余机构段；且某一企业的**全部材料聚在同一前缀**下 ⇒ 整企业导出/清理/审计是**单前缀操作**。
（原结构 `{Org}/{Std}/{Stage}/{Enterprise}` 把企业段夹在第 4 层，一个企业的材料散落在每个"标准×阶段"组合下，无法单前缀取全 —— 已作废。）

### 3.3 产物路径（预览 PDF / 提取 Markdown，**从源路径派生**）

```
源  ： /enterprise-documents/{Ent}/{Std}/{Stage}/4记录文件/风险管理报告.doc
PDF ： /enterprise-documents/{Ent}/{Std}/{Stage}/4记录文件/pdf/风险管理报告.doc.pdf
MD  ： /enterprise-documents/{Ent}/{Std}/{Stage}/4记录文件/markdown/风险管理报告.doc.md
```

`PathBuilder.Product(storagePath, productKind, targetExt)`，`productKind ∈ {"pdf","markdown"}`（其他值抛异常）。
**与库前缀无关** ⇒ 企业库文件**零改动复用**同一转换链（这是本模块能直接复用 `file_convert` 队列的根本原因）。

| 设计点 | 说明 |
|---|---|
| 为什么派生而非重拼 | ① 免疫旧 `GenerateConvertedStoragePath("","","","",name)` 空参 bug（产物全落 `/standard-directory/.converted/{名}` 且互相覆盖，**实测已丢数据**）；② 文件夹改名后产物跟随；③ 天然隔离租户/标准/阶段 |
| 产物文件名保留**完整原文件名** | 同文件夹内可能存在同 stem 不同扩展名（如 `XASL-QR-014 年度内审计划.doc` 与 `.xls`，本项目实测存在）⇒ 只取 stem 会互相覆盖 |
| 返回空串的契约 | 源路径为空/段数不足 ⇒ 返回 `""`（**不抛异常**）：4 处调用方以 `IsNullOrEmpty(targetPath)` 判定"源文件缺存储路径"并标 failed；收紧成抛异常会变未捕获异常 |
| `IsProductPath(path)` | 判断是否位于 `pdf/` 或 `markdown/` 段下（删除时避免把产物目录误当业务目录） |

### 3.4 归档路径（**仅企业库使用**）

```
源 ： …/{Ent}/{Std}/{Stage}/4记录文件/风险管理报告.doc
归档：…/{Ent}/{Std}/{Stage}/4记录文件/_archive/风险管理报告.doc.v3
PDF ： …/{Ent}/{Std}/{Stage}/4记录文件/pdf/风险管理报告.doc.pdf
归档：…/{Ent}/{Std}/{Stage}/4记录文件/pdf/_archive/风险管理报告.doc.pdf.v3
```

`PathBuilder.Archive(storagePath, versionNumber)`

| 规则 | 说明 |
|---|---|
| 算法 | 在**文件名前**插入 `_archive` 段，文件名追加 `.v{版本号}` |
| 幂等 | 归档目标带 `.v{n}` ⇒ 重试不互相覆盖 |
| **禁止二次归档** | 入参已是归档路径（含 `_archive`）⇒ **抛 `InvalidOperationException`**（二次归档 = 调用方状态机出错，必须响） |
| 标准目录库**不用**归档 | 标准侧是"纯覆盖"语义；`_archive` 仅企业资料库使用（`PathBuilder` 注释明确） |

---

## 四、保留段（业务命名禁区）

`PathBuilder.ReservedSegments = ["pdf", "markdown", "_archive"]`

- **业务文件夹名与文件名不得使用这三个段名**，否则会与产物/归档目录撞车（例：业务文件夹叫 `pdf` ⇒ 被 `IsProductPath` 误判为产物路径）。
- 校验落点：四段式上传 Step1 的 `ValidateFolderOrFileName`（文件夹名/文件名白名单校验）应扩展为同时拒绝保留段；**本模块需补此校验**（见 06 P2）。

---

## 五、`IObjectStorage` 能力（企业模块直接可用）

| 方法 | 签名 | 语义 |
|---|---|---|
| `UploadAsync` | `(objectName, Stream, long size, string contentType="application/octet-stream", ct)` | PUT 对象（内部 `TrimStart('/')`） |
| `DownloadAsync` | `(objectName, ct) → (Stream, ContentType)` | 先 `StatObject` 取 content-type，再 `GetObject` 读入 `MemoryStream` 返回 |
| `DeleteAsync` | `(objectName, ct)` | `RemoveObject` |
| `RenameAsync` | `(old, new, ct)` | **下载 → 以旧 content-type 上传新路径 → 删旧**（非服务端 copy，大文件有开销） |
| `ListObjectsAsync` | `(prefix, ct) → List<string>` | 递归列举（用于整目录导出/清理） |
| `DeletePrefixAsync` | `(prefix, ct)` | 递归删除，1000 个/批（`RemoveObjects`） |
| `ExistsAsync` | `(objectName, ct) → bool` | `StatObject` 成功即 true，异常即 false |

> ⚠️ **`RenameAsync` 是全量下载+上传**：企业文件"替换"若用 `RenameAsync` 归档，大文件会双倍 IO。`EnterpriseFileService.ReplaceFileAsync` 当前正是此实现（见 04 §五）；如需优化，应改为"复制到新 key"或直接"新文件按 `.v{n}` 落归档 + 覆盖原 key"。

---

## 六、`PathBuilder` API 与三条构造规则

| API | 产出 | 异常语义 |
|---|---|---|
| `StandardFile(org, std, stage, folderPath, fileName)` | `/standard-directory/…` | 身份段为空 ⇒ **抛异常** |
| `EnterpriseFile(ent, std, stage, folderPath, fileName)` | `/enterprise-documents/…` | 同上 |
| `Product(sourcePath, kind, ext)` | 派生产物路径 | `kind` 非 `pdf/markdown` ⇒ 抛异常；源为空/段数<2 ⇒ 返回 `""` |
| `Archive(storagePath, versionNumber)` | `…/_archive/{名}.v{n}` | `n<1` ⇒ 抛异常；已是归档路径 ⇒ 抛异常；段数<2 ⇒ 抛异常 |
| `Segments(path)` / `SegmentAt(path, i)` | 拆段（**全项目唯一拆段入口**） | 反斜杠→正斜杠、去空白/前导`/`/空段/穿越段 |
| `IsProductPath` / `IsArchivePath` | 段判定 | — |

**三条构造规则**（`PathBuilder` 头注释原文要点）

1. **身份段取实体 `Code` 原文（GUID），⛔ 不做 `CleanCode`** —— 旧 `CleanCode` 会删掉 `-`，而 GUID 含 `-` ⇒ 路径段与 DB 值**不可逆不一致**（`846dec4b-c534-…` 写成 `846dec4bc534…`），从此无法由路径反查实体。
2. **身份段不得为空，空则抛异常** —— 旧实现 `Where(s => !IsNullOrEmpty(s))` **静默丢弃空段**，产出的路径丢掉全部上下文（实测已导致 1 处数据互相覆盖）。**宁可上传失败，也不静默写错位置**。
3. **文件夹段与文件名段只去路径分隔符**，其余（空格 / 连字符 / 中文 / 括号）**原样保留** —— 例：`3制度文件（可根据企业实际修改）` 全角括号保留。

---

## 七、现存对象与历史脏数据（实测背景）

| 项 | 实测 |
|---|---|
| 历史问题 | 路径格式改过 ≥4 次，每次**新增生成方法而不删旧** ⇒ MinIO 同时躺着 5 套格式（`standard-directory/{.converted, CB001CODE, ISO134852016, ISO90012015, iso40012016}/`） |
| 后果 | 1182 个对象中只有 **177 个**仍被数据库引用 |
| 根治手段 | 2026-09-26 起 `PathBuilder` 收口为唯一构造器（本分册即为该规则的执行约束） |
| 本模块 | 新增对象只允许出现在 `/enterprise-documents/…`（+ 其 `pdf/`、`markdown/`、`_archive/` 子段） |

---

## 八、安全校验（下载 / 预览 / 删除）

| 动作 | 校验 | 说明 |
|---|---|---|
| 下载 / 预览 PDF / 预览 Markdown | `DocumentLibraryPath.IsAllowedStoragePath(storagePath)` | 必须位于已知库下、无穿越段；否则返回业务失败（HTTP 200 + `success=false` + `err="非法的文件路径"`） |
| 删除文件 | 只删**该文件行自己的** `StoragePath` / `PreviewPdfPath` / `MarkdownPath` / `ConvertedStoragePath` | ⛔ **不得** `DeletePrefixAsync` 企业或标准根前缀（会误删同目录其它文件） |
| 目录级清理（删标准/删企业） | `DeletePrefixAsync('/enterprise-documents/{Ent}/{Std}/{Stage}')` | 仅在"确认级联删除"场景使用，且需二次确认弹窗 |
| 预览透传 | 图片 / PDF 常走"透传"：`PreviewPdfPath == StoragePath` | ⛔ 作废旧产物时**不能删源文件本身**（`InvalidateProductsAsync` 已按此判断） |

---

## 九、企业资料路径实例（本模块验收基准）

以实测模板（`4记录文件/技术类/XASL-TR-001 年度验证计划.doc`）为例：

```
上传（企业 EB-xxxx，标准 846dec4b-…(9001)，阶段 29c1bcc3-…(复审)）：
  /enterprise-documents/EB-xxxx/846dec4b-c534-4983-94e6-8cf04982b7d9/29c1bcc3a18942e1865b2497a0262504/4记录文件/技术类/XASL-TR-001 年度验证计划.doc

转换产物：
  …/4记录文件/技术类/pdf/XASL-TR-001 年度验证计划.doc.pdf
  …/4记录文件/技术类/markdown/XASL-TR-001 年度验证计划.doc.md

首次替换（旧件归档为 v1）：
  …/4记录文件/技术类/_archive/XASL-TR-001 年度验证计划.doc.v1
（新件仍写原路径覆盖）
```

**同一文件进 3 个标准**（需求 3）：3 个标准 = 3 个不同 `StandardCode` 段 ⇒ 3 条独立对象、3 行 DB（互不覆盖）：

```
/enterprise-documents/{Ent}/{StdA}/{Stage}/…
/enterprise-documents/{Ent}/{StdB}/{Stage}/…
/enterprise-documents/{Ent}/{StdC}/{Stage}/…
```

---

## 十、注意事项与待补

| # | 事项 | 归属 |
|---|---|---|
| N1 | 上传 Step1 需**拒绝保留段**（`pdf`/`markdown`/`_archive`）作为文件夹名/文件名 | P2 |
| N2 | 文件名需过滤路径分隔符与空段（`Sanitize` 已提供，Step1 校验需同步） | P2 |
| N3 | 归档用 `RenameAsync`（下载+上传）大文件代价高；建议改为"原 key 复制为归档 key"或"上传新件到新版本 key + 覆盖原 key" | P4（可选优化） |
| N4 | 企业库前缀已加入白名单，但**下载/预览端点需在企业侧可用**（现 `EnterpriseFileController` 无下载/预览端点） | P2 |
| N5 | 桶不存在时需确保已创建（`cert-platform`）；部署脚本/初始化需登记 | 部署 |
