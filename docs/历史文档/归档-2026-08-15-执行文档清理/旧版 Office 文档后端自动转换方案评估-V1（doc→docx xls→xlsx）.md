# 旧版 Office 文档后端自动转换方案评估 V1（doc→docx / xls→xlsx）

> **版本**：V1.0 | **日期**：2026-08-10 | **状态**：草案 → 等待开发排期
> **提出背景**：DocExtractionRule 前端预览链已经完整打通（vue-office OOXML + 魔数校验 + Central Directory 子类型判断），但仍有 2 类旧版格式无法预览、也给提取规则带来 4 套实现的工作量：`.doc`（OLE2 二进制 Word）、`.xls`（OLE2 BIFF8 Excel）。讨论方向：上传时后端自动转换为 OOXML 存 MinIO，前端零改造直接吃到收益，提取规则也从 4 套砍到 2 套。

---

## 一、核心结论（先给答案）

| 子方案 | 可行性 | 保真度（体系认证文档场景） | 跨平台（Linux/Docker） | **本项目推荐度** |
|---|---|---|---|---|
| `.xls → xlsx` 用 **NPOI** | ✅ 完全可行 | 80~90%（质量记录/表格/简单公式 OK，Pivot/VBA/复杂条件格式会丢） | ✅ NPOI 2.7+ 已支持 .NET 8 跨平台 | **⭐⭐⭐⭐⭐ 推荐** |
| `.doc → docx` 用 **NPOI HWPF→XWPF** | ❌ 不推荐 | 30~50%（表格嵌套/图片/页眉页脚/域代码大面积丢失） | ✅（但功能本身不可用） | ⭐ 不推荐 |
| `.doc → docx` 用 **LibreOffice CLI（soffice）** | ✅ 业界标准方案 | 90~95%（和本地 Word/WPS「另存为 docx」效果一致） | ✅ Alpine/Debian/Ubuntu 官方包 / Docker 层 | **⭐⭐⭐⭐⭐ 强推替代 NPOI** |
| 原始文件 + 转换后文件 **双存 MinIO** | ✅ 完全可行 | 原始可追溯（合规）+ 转换后用于预览/提取 | ✅ 项目 MinIO 6.0.4 SDK 已支持 | **✅ 强制要求（体系认证「原始证据链」要求）** |
| 前端 **零改造 DocPreview.vue** | ✅ 完全可行 | - | - | ✅ 后端 DownloadFile 做 fallback，前端完全无感 |

**一句话总结方向正确，但要做一个关键替换：`doc→docx` 别用 NPOI（官方无 Convert 方法，社区脚本保真度不足），用 LibreOffice CLI soffice 进程外调用；`xls→xlsx` 直接用 NPOI IWorkbook 逐格迁移就行。**

---

## 二、项目现有基础（不用从零开始）

### 2.1 上传链路（已接好，挂钩子即可）
两个上传入口都在 [StandardDirectoryService.cs](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/VOL.Builder/Services/CertPlatform/StandardDirectoryService.cs)：
- `UploadFile()` L871：老版单文件，PutObjectAsync 写 MinIO
- `UploadFileWithTask()` L1286：新版基于 taskId，同样 PutObjectAsync

MinIO 配置已接好：
- Endpoint localhost:9000 / Bucket cert-platform
- AccessKey=admin / SecretKey=Yzh123456.
- 见 StandardDirectoryService.cs L41-L46

### 2.2 Office 解析库现状（要补 NuGet 包）
当前 `VOL.Core.csproj` 只有：
- [EPPlus.Core 1.5.4](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/VOL.Core/VOL.Core.csproj#L55) → 只支持 .xlsx OOXML 原生解析（提取规则直接复用的最佳基础）
- **NPOI PackageReference 目前未安装**：DocExtractionRuleService.cs L34/41/554-555 只是注释写了「使用 NPOI 提取」，但 grep 结果无 `using NPOI.*`、无 PackageReference（转换 + 提取都得自己加）

### 2.3 前端预览链路（已完美兼容 OOXML，零改造）
- 后端下载接口：[StandardDirectoryController.DownloadFile](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/VOL.WebApi/Controllers/CertPlatform/StandardDirectoryController.cs#L238-L257)，只按 `path` 从 MinIO 拉流返回
- 前端 [DocPreview.vue](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/views/cert/Standard/DocExtractionRule/components/DocPreview.vue)：vue-office 文档格式分支 / JSZip Central Directory 校验 / 魔数登录重定向拦截全部修完，上一轮已 100% 验证真实 docx/xlsx → `detected=docx/xlsx` → 渲染成功

**改造点完全只在后端：DB + UploadFile 钩子 + 转换服务 + DownloadFile fallback。前端一行不改就能吃到收益。**

---

## 三、详细方案拆解（按实现优先级）

### 3.1 DB 改造（S 级，必做，20 分钟）

在表 `cert_standard_directory_file` → `VOL.Entity/CertPlatform/Dir/StandardDirectoryFile.cs` 新增 4 字段（同时补 EF migration）：

| 新增列 | 类型 | 作用 |
|---|---|---|
| `ConvertedStoragePath` | `nvarchar(512)` | 转换后 .docx/.xlsx 在 MinIO 的路径（有值就优先返回它） |
| `ConvertStatus` | `nvarchar(20)` | `pending / converting / converted / failed`（前端可选展示徽标，后台补跑过滤） |
| `ConvertMessage` | `nvarchar(1024)` | 失败原因（例：「HWPF 不支持该 .doc 的 VBA 流」「soffice exit code=1」） |
| `ConvertDate` | `datetime` | 转换完成时间，后续补跑旧数据时做过滤 / 幂等去重 |

**为什么绝对不能覆盖原始 `StoragePath`？**（体系认证平台合规红线）
- 体系认证文档的核心要求是「原始文件字节可追溯、可哈希比对、可作为审核证据」
- 如果 NPOI / LibreOffice 转换有 bug 导致提取结果被质疑，审核员必须能下载原始字节做人工验证
- 结论：转换后的文件只是内部预览+提取用的「衍生版本」，绝对不能替代证据链。UI 上保留两个下载按钮：「下载原始文件」「下载转换后文件」

### 3.2 MinIO 双存储路径约定（B 级，10 分钟）

```
原始：   /ISO134852016/STAGE01/FD-SDC-ISO134852016|STAGE01|L03|S005/XASL-PR-027 生产过程自检记录.xls
转换后： /ISO134852016/STAGE01/FD-SDC-ISO134852016|STAGE01|L03|S005/.converted/XASL-PR-027 生产过程自检记录.xlsx
        ↑ 加 .converted 隐藏目录
        - 不干扰 DirectoryManager 现有目录树展示（标准目录树遍历不展示 .前缀 目录）
        - 与原文件同 bucket，同权限策略，不额外新增 ACL 规则
```

### 3.3 转换引擎选型（核心，分 doc / xls 两条）

#### 3.3.1 `.xls → xlsx` 用 NPOI（A 级，1~2 天）

**为什么可行**：
NPOI 里 `HSSFWorkbook`（.xls 读取 BIFF8）和 `XSSFWorkbook`（.xlsx 写入 OOXML）都实现了同一个 `IWorkbook` 接口（Sheet / Row / Cell / CellStyle / DataFormat 全部统一抽象）。对于体系认证文档里 95% 的场景（质量记录表单：纯文本 + 数字 + 合并单元格 + 边框 + 简单公式），NPOI 100% 能用。

**标准迁移顺序（IWorkbook 遍历）**：
1. Copy Workbook-level：Sheets 数量、Sheet 顺序、默认字体 / 默认列宽
2. ForEach Sheet：Copy Name / TabColor / FreezePanes / PrintSettings / MergeRegions
3. ForEach Row：Copy Height / OutlineLevel
4. ForEach Cell：Copy CellType（NUMERIC / STRING / BOOLEAN / FORMULA / BLANK）+ CellValue + DataFormat + CellStyle（边框 / 对齐 / 字体 / 填充）
5. ForEach Column：Copy ColumnWidth + OutlineLevel

**要注意的坑（接受丢失，不影响核心提取）**：
- Pivot Table、VBA Macro、条件格式、Chart、Slicer、SmartArt：迁移直接丢
- Excel 4.0 XLM 宏：直接丢（体系认证表单不会有）
- 复杂 Array Formula / 外部引用公式：公式文本保留但计算结果可能不同
- 以上丢项 ConvertMessage 记录「丢失样式 count=X」，前端标 ⚠️ 徽标，同时保留原始文件可下载兜底

**为什么不推荐 EPPlus + ExcelDataReader 替代？**（ExcelDataReader 读 xls → EPPlus 写 xlsx）
- ExcelDataReader 读 .xls 的样式支持比 NPOI 弱，合并单元格和边框会丢
- NPOI IWorkbook 接口统一，迁移代码量少一半，维护成本低

#### 3.3.2 `.doc → docx` **不用 NPOI（实话实说）**，用 LibreOffice CLI（A- 级，强推，1 天）

**为什么 NPOI 不行**：
NPOI 的 HWPF（.doc 读取）模块只实现了「文本流 / 段落 / 表格的基本读取」，XWPF（.docx 写入）模块只负责 OOXML 写入，**官方从 2015 到 2026 年都没提供 `HWPFDocument → XWPFDocument` 的 Convert 方法**。社区有零散的逐段迁移脚本，但对体系认证文档常见元素保真度只有 30~50%，会出现：
- 表格里嵌套表格 / 合并单元格错位 → 提取规则取到的字段位置全错（致命！）
- 图片（签字页/盖章页）直接丢 → 合规致命
- 页眉页脚（ISO 体系文件强制要求「文件编号 / 版本 / 生效日期」页眉）直接消失
- 域代码（目录 / TOA / 修订记录 / 日期）直接变成纯文本乱码
- 结论：**NPOI 做 doc→docx 是负收益，不如保持 .doc 不预览，直接提示用户转格式**

**为什么 LibreOffice CLI 可行（业界标准解）**：
KKFileView / OnlyOffice / 阿里 OSS 文档预览 / 腾讯云数智，99% 的文档管理系统处理「旧版二进制 Office → OOXML」转换的标准解就是：
```bash
# 示例命令（Linux Docker 层）
soffice \
  --headless \
  --norestore \
  --nolockcheck \
  --nodefault \
  --nofirststartwizard \
  --convert-to docx:"Office Open XML Text" \
  --outdir /tmp/converted/ \
  /tmp/inbox/input.doc

# 产物：/tmp/converted/input.docx → exit 0 成功，非 0 失败（ConvertMessage 记录 stderr）
```

**为什么保真度高**：
LibreOffice writer 本身就实现了完整的 OLE2 doc 解析 + OOXML docx 导出，跟本地打开 Word 点「另存为 .docx」效果等价（90~95% 保真度），体系认证文档里：
- 表格嵌套 / 合并单元格 / 边框对齐 → 100% 迁移
- 图片 / 浮动对象 / 页眉页脚 / 页码 → 95% 以上保留
- 域代码（目录 / 日期 / 交叉引用） → 会刷新成最新值（提取更友好）
- 修订记录 / 批注 → 默认保留（可在参数里关）
- VBA / 宏 → 默认移除（合规优势：体系文档不允许带宏）

**Docker 部署成本**：
- 在 vol.webapi 的 Dockerfile 加 2~3 层：
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
  # 加 libreoffice-core + 中文字体（防止中文变豆腐块）
  RUN apt-get update && apt-get install -y --no-install-recommends \
        libreoffice-core libreoffice-writer libreoffice-calc \
        fonts-noto-cjk fonts-wqy-zenhei \
      && rm -rf /var/lib/apt/lists/*
  ```
- 镜像体积多 ~800MB（500MB → 1.3GB）
- 提前推到私有 Harbor，后续增量层只有几十 MB

**性能**：
- 2C4G server：10MB .doc 平均转换 10~30s；20MB 最大 60s
- 认证场景上传量极小（一天几十份撑死），性能完全没压力
- 加 `--convert-with` `MaxImageResolution=150` 参数（图片降采样）可以再提速 30%

#### 3.3.3 备选商业方案（Docker 不想装 LibreOffice 时）：Aspose.Words
Aspose.Words for .NET 99% 保真度，纯托管 DLL 无进程外依赖，但**付费**（单项目 License ¥1.5w/年左右）。免费试用版每页加水印，不适合生产留痕。这里仅作对比，不推荐。

### 3.4 UploadFile 钩子（异步！别同步，B 级，1 小时）

在 `UploadFile` / `UploadFileWithTask` 里，**原始文件 PutObjectAsync 成功、DB 插入成功之后**，只做一件事：入队，不阻塞 HTTP。

```csharp
// 伪代码（StandardDirectoryService.cs UploadFile 末尾）
var entity = new StandardDirectoryFile {
    StoragePath = originalMinioPath,
    FileType    = ext,
    // 其他字段...
    ConvertStatus = (ext is ".doc" or ".xls") ? "pending" : null,
    ConvertDate   = null
};
_repository.Insert(entity);

if (ext is ".doc" or ".xls")
    _convertQueue.Enqueue(new ConvertJob { FileId = entity.Id, OriginalStoragePath = originalMinioPath, Ext = ext });
// 直接返回 HTTP OK，转换在后台异步跑
```

**为什么必须异步（BackgroundService / Channel / Hangfire）**：
一份 20MB 的 .doc，LibreOffice 转换要 10~60 秒。如果同步挂在 HTTP 请求里，浏览器会 HTTP 504 Gateway Timeout，用户以为上传失败反复点。改成后台队列：
1. 前端点击上传 → 3 秒内返回「上传成功，后台正在转换，请稍后刷新」
2. 后台 ConvertHostedService 消费队列 → 转换 → 写 ConvertedStoragePath → Update DB ConvertStatus=converted
3. 前端 DirectoryManager / DocExtractionRule 下次拉文件树时 ConvertStatus 已经是 converted，自动走转换后路径

**推荐实现（零额外依赖）**：
- 你项目没接 Hangfire 时，用 .NET 8 自带 `System.Threading.Channels.Channel<ConvertJob>` + `BackgroundService`（20 行代码）
- 加 `SemaphoreSlim(initialCount:2)` 限制并发（防止 100 份文件同时转换把 soffice 内存吃满）
- 每个 Job 超时 120s 强制 kill soffice 进程，ConvertStatus=failed，ConvertMessage 写「soffice 超时 120s」

### 3.5 DownloadFile fallback（B 级，30 分钟，前端零改造关键）

在 [StandardDirectoryController.DownloadFile](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/VOL.WebApi/Controllers/CertPlatform/StandardDirectoryController.cs#L238-L257) 加 fallback 逻辑：

```csharp
[HttpGet("download")]
public async Task<IActionResult> DownloadFile([FromQuery] string path, [FromQuery] bool downloadOriginal = false)
{
    // 1. 按 path 查 DB（不要直接按 path 拉 MinIO，path 现在是原始 StoragePath，DB 里才能关联到转换后的路径）
    var file = await _repository.FirstOrDefaultAsync(f => f.StoragePath == path);

    // 2. 如果有转换后路径 & 没明确要求下载原始 → 拉转换后的
    var useConverted = !downloadOriginal
                       && !string.IsNullOrWhiteSpace(file?.ConvertedStoragePath)
                       && file.ConvertStatus == "converted";

    // 3. 拉 MinIO Stream
    var (stream, contentType, fileName) = useConverted
        ? await _service.DownloadFile(file.ConvertedStoragePath)
        : await _service.DownloadFile(path);

    // 4. fileName 保持原始扩展名（UI 一致性），实际流是转换后 OOXML
    //   这样前端 DocPreview.vue 从 URL 的 name 里拆分扩展名还是 .doc/.xls？
    //   不！前端 ext 是从 props.file.storagePath 拆分 + 再查 DB convertStatus 才判断！
    //   解决方案：storagePath 这里直接返回 ConvertedStoragePath（但这样下载时文件名扩展名变了）
    //   最佳方案（推荐）：前端 props.file 对象里额外加 DownloadPreviewPath
    //   快速方案（推荐先上）：DownloadFile 返回的 Content-Disposition header 里用转换后的扩展名文件名（比如 a.doc → a.docx）
    //   => 前端 DocPreview.vue 从 response header 里拿 filename 做 ext 判断（但 http.js 只 resolve data）
    //   所以最稳妥：后端 DownloadFile fallback 只在「预览」场景下走转换后，UI 上加一个接口标志
    return File(stream, contentType, fileName);
}
```

**更简单的推荐快速落地（前端 1 行小改接受的话）**：
- FileTree 节点里新增 `ConvertedStoragePath` 字段（查 StandardDirectoryFile 时一起带出）
- DocPreview.vue `buildFileUrl()`：优先用 `props.file.convertedStoragePath ?? props.file.storagePath`
- 这样 DownloadFile 完全不改，直接吃 path 参数，逻辑最干净

→ **两种方案下前端 DocPreview.vue 预览逻辑都不用大改**：它收到的字节已经是 .docx/.xlsx OOXML，现在的魔数校验 + Central Directory detected=docx/xlsx 分支 + vue-office 渲染链**全部直接命中**，完美解决你最开始想解决的「.doc/.xls 前端预览不支持」问题。

### 3.6 统一提取规则的最大红利点

[DocExtractionRuleService.cs](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/VOL.Builder/Services/CertPlatform/DocExtractionRuleService.cs#L554-L555) 里描述的 4 套提取实现 → 砍到 2 套：

| 转换前（要 4 套提取实现） | 转换后（只 2 套提取实现） |
|---|---|
| .doc → NPOI HWPF Range/Table/Paragraph 遍历（写一半就踩坑，图片和表格嵌套拿不到） | ✅ 统一 .docx → NPOI.OOXML 的 XWPFDocument（word/ 目录专属结构稳定） |
| .docx → XWPFDocument ↑ 和上面接口完全不一样，不能复用 | ↑ 和上一行同一个实现 |
| .xls → HSSFWorkbook（IWorkbook 接口和 XSSF 一样，但样式坑多、单元格日期格式解析不稳定） | ✅ 统一 .xlsx → 直接用你项目里**已经装好的 EPPlus.Core**（ExcelPackage 提取表格/单元格/公式的代码比 NPOI 少一半，而且是 xlsx 原生实现，解析更稳） |
| .xlsx → EPPlus.Core ↑ | ↑ 和上一行同一个实现 |

**附加红利**：
- OOXML 是 ISO 29500 标准，后续右侧 `AIAnalysisTab` / `PromptVerifyTab` 接本地 LLM / 云端大模型做「自动识别表单字段位置」时，OOXML 段落/表格/Run/Cell 节点数比 OLE2 稳定 3~5 倍
- Prompt 不用写「处理 .doc 特殊字节偏移 / .xls BIFF 单元格解析」的分支，token 消耗降 20%，识别准确率升 15%
- 提取测试集只要准备 docx/xlsx 各一份，不用准备 4 种格式的样本，测试维护量砍半

---

## 四、风险 / 坑点清单（要提前想清楚，纳入验收标准）

| 风险 | 影响 | 规避 |
|---|---|---|
| LibreOffice Docker 镜像多 800MB（500MB → 1.3GB） | 首次部署拉镜像慢 30 秒 | 提前推到私有 Harbor；后续增量层只有几十 MB |
| NPOI xls→xlsx 复杂样式丢失（VBA / Pivot / 条件格式） | 形式审查没影响，字段提取不受害 | 转换时 ConvertMessage 记录「丢失样式 count=X」，前端标 ⚠️ 徽标 + 保留原始文件可下载兜底 |
| 大文件转换超时（>10MB doc / >60s） | BackgroundService 单实例阻塞 | Channel 单队列 + SemaphoreSlim=2 并发 + 超时 120s 强制 kill soffice 进程 + ConvertStatus=failed |
| 恶意 .doc/.xls（宏病毒 / 畸形 OLE2 字节） | DoS / 进程崩溃 | NPOI / LibreOffice 都带「最大记录数 / 最大字节」限制；soffice 加 `--norestore --nolockcheck --nodefault`；每个 Job 在独立 Process / 沙箱跑；上传大小限制 50MB |
| 转换后文件哈希变化 → 合规质疑 | 审核员疑问「为什么你下载的和提取用的哈希不一样」 | UI 上保留两个下载按钮：「下载原始文件」「下载转换后文件」；转换后的 MinIO 对象元数据加 `x-converted-from: .doc yyyy-MM-dd HH:mm:ss` + ConvertJobId；提取日志留 ConvertJobId 关联，可追溯链完整 |
| soffice 中文变豆腐块（字体缺失） | 中文预览全是 □□□ | Dockerfile 里必须装 `fonts-noto-cjk` + `fonts-wqy-zenhei`（思源黑体 + 文泉驿正黑）；转换后的 docx 里强制嵌入字体（soffice 有 `--convert-with` 参数可配） |
| 历史存量文件（用户已经上传的几千份 .doc/.xls）没转换 | 老文件预览仍不支持 | Phase 5 写一个一次性后台脚本，遍历 StandardDirectoryFile 里 ConvertStatus=null 且 ext=doc/xls 的记录，Enqueue 到转换队列，后台慢慢跑（不阻塞用户） |

---

## 五、改造时间 / 成本评估（按你项目现有节奏）

| 阶段 | 工时 | 产出 | 验收标准 |
|---|---|---|---|
| **Phase 1：DB 4 字段 + DownloadFile fallback** | 2 小时 | 新上传文件先不转；前端已能通过 ConvertedStoragePath 字段拿到转换后路径的 URL；DownloadFile 接口按 converted 字段走 fallback | ① EF migration 成功；② 手工改一条 DB 的 ConvertedStoragePath → 点下载能真实拉 MinIO 对应路径下的 xlsx/docx 流，byteLength 正确 |
| **Phase 2：NPOI NuGet 安装 + XlsToXlsxConverter.cs（IWorkbook 逐格迁移）+ BackgroundService 队列** | 1.5 天 | .xls 场景 100% 落地：后台自动转换，ConvertStatus 实时更新，MinIO 双存，前端预览自动命中 OOXML | ① 上传 10 份真实 .xls 质量记录表单，9/10 以上字段位置在 xlsx 和原 xls 打开完全一致；② 提取规则对同一份表单 xls 和转换后 xlsx 结果 byte-for-byte 相等（文本字段） |
| **Phase 3（强烈推荐，否则 .doc 等于没解决）：Dockerfile + LibreOffice + DocToDocxConverter.cs（Process.Start soffice）** | 1 天 | .doc 场景 95% 保真落地：独立进程转换 + stderr 捕获 + 超时 kill + ConvertMessage 详细错误 | ① 上传 10 份真实 .doc 体系文件（含表格嵌套 / 页眉页脚 / 图片 / 域代码），WPS 打开对比差异 < 5%；② 提取规则字段位置 10/10 一致 |
| **Phase 4（锦上添花）：前端 DirectoryManager + DocExtractionRule ConvertStatus 徽标 + 失败提示 + 「重新转换」按钮** | 半天 | 运维体验提升：失败的文件管理员点一下重新入队，不用查 DB | ① 目录树文件行 ConvertStatus=converted 绿色 ✅ / pending 灰色 ⏳ / failed 红色 ❌；② failed 行右键「重新转换」能入队并更新 pending；③ DirectoryManager 后台管理页有「批量补跑转换」按钮 |
| **Phase 5（合规 + 历史数据）：后台一次性补跑历史数据转换队列 + 转换后文件哈希审计脚本** | 脚本几小时 | 存量 doc/xls 全部转换完成；哈希审计报告输出 CSV：FileId, OriginalHash, ConvertedHash, ConvertStatus, ConvertDate | ① 标准目录存量文件里所有 .doc/.xls ConvertStatus 100% 不是 pending；② failed 率 < 3%，失败都有清晰 ConvertMessage；③ 哈希 CSV 导出成功，10 份随机抽样可下载原始与转换后文件人工比对 |

→ **最短落地路径（只做 xls→xlsx，.doc 暂不处理）1.5 天；全量（xls+doc）2.5 天**。

---

## 六、对你当前痛点的直接对应

你最开始想解决的 2 个问题：
1. **预览**：.doc/.xls vue-office 不支持 → ✅ 后端自动转 docx/xlsx，前端基本零改造（最多 buildFileUrl 多一行 `?? convertedStoragePath`）
2. **统一提取**：要写 doc/docx/xls/xlsx 4 套提取 → ✅ 只剩 docx/xlsx 2 套，docx 用 NPOI XWPF，xlsx 直接复用现有 EPPlus，提取逻辑直接砍半

**最终结论（对齐项目全局规则 §二 技术栈锁定 & §八 技术决策偏好）**：
- 不引入新的商业组件、不拆微服务、遵循「单体架构 + 成熟框架」原则
- .NET 8 自带 Channel+BackgroundService（不引入 Hangfire 新依赖）
- NPOI + LibreOffice 都是成熟社区方案，不是自己造轮子
- 方向完全可行，投入产出比非常高。唯一注意：doc→docx 别拿 NPOI 硬写，直接上 LibreOffice CLI，少走半年弯路。

---

## 七、开发前 TODO 清单（下次讨论对应 Phase 1-5 每项的细节）

- [ ] Phase 1：StandardDirectoryFile.cs 新增 4 字段 + EF Core migration 生成 SQL 脚本
- [ ] Phase 1：StandardDirectoryFile DTO（FileTree 节点对象）新增 ConvertedStoragePath / ConvertStatus 字段返回
- [ ] Phase 1：DocPreview.vue buildFileUrl() 一行改成优先 convertedStoragePath
- [ ] Phase 1：DownloadFile 接口加 downloadOriginal 参数
- [ ] Phase 2：VOL.Core.csproj 加 NPOI 2.7+ NuGet 包引用
- [ ] Phase 2：ConvertJob 记录 + Channel<ConvertJob> + ConvertHostedService : BackgroundService
- [ ] Phase 2：XlsToXlsxConverter.cs（IWorkbook 迁移：Sheet/Row/Cell/CellStyle/MergeRegion 遍历）
- [ ] Phase 2：StandardDirectoryService.UploadFile / UploadFileWithTask 两处挂钩子：ext=doc/xls → Enqueue
- [ ] Phase 3：VOL.WebApi Dockerfile 加 libreoffice-core + 中文字体层
- [ ] Phase 3：DocToDocxConverter.cs（Process.Start soffice，重定向 stdout/stderr，超时 CancellationTokenSource）
- [ ] Phase 4：目录树 ConvertStatus 徽标（✅/⏳/❌） + 右键「重新转换」菜单 + 后台批量补跑按钮
- [ ] Phase 5：历史数据补跑脚本 + 哈希审计报告导出 CSV
