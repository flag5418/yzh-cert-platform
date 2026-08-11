# 当前项目规则整理 + 下次开发 TODO 清单 V1

> **版本**：V1.0 | **日期**：2026-08-10 | **状态**：成熟态（下次讨论直接按本清单执行）
> **定位**：把 AGENTS.md + 项目全局规则.md 中所有 AI 相关约束集中整理，并对齐本次讨论已完成的 DocExtractionRule 预览链修复 + 旧版 Office 转换方案评估，输出下次开发可直接执行的任务分解 TODO 清单。

---

## 第一部分：当前项目规则整理（AI 编程助手行为宪法）

> 来源：[AGENTS.md](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/AGENTS.md) + [项目全局规则.md](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/项目全局规则.md) + 用户画像规则。每次对话开始前必须对齐。

### 1. 技术栈锁定（宪法级，不可改）

| 层级 | 技术 | 版本 | 端口 | 说明 |
|---|---|---|---|---|
| 后端框架 | .NET 8 + Vol 框架（VOL.Vue.NetCore EF Core 版本） | .NET 8 | 9992 | 非重大缺陷不升级 Vol 版本 |
| 前端框架 | Vue 3.4+ + Element Plus | Vue 3.4+ | 后台管理 9990 / 审核员前端 9991 | 前端代码严格按 vol-skill.md §12 view-grid 组件模式 |
| 数据库 | MySQL | 8.0 | 3307 | 宿主机映射到容器 yzh-mysql |
| 缓存 | Redis | 7.x | 6380 | 容器 yzh-redis |
| 对象存储 | MinIO（最新稳定版） | 6.0.4 SDK | MinIO API 9000 / MinIO Console 9001 | bucket=cert-platform / AccessKey=admin / SecretKey=Yzh123456. |
| 移动端 | Flutter | - | - | Phase 2+，当前不开发 |

### 2. 后端代码红线（绝对禁止）

1. **禁改 Vol 框架源码 / .jsx 文件**：后端业务逻辑只改 `VOL.Sys/Services/System/Partial/` 下的 Partial Service
2. **禁直接用 EF SQL 绕过 EF LINQ**：绕过钩子体系，审计/权限过滤会失效（除非是后台批量脚本）
3. **禁直接删除旧版本文档**：旧版本统一移到 `docs/历史文档/`，历史文档/只进不出、不做二次整理
4. **禁在 docs/ 根目录直接放文档**：所有文档必须归属 00~60 分层目录
5. **禁主动提议引入新技术栈**：除非当前栈实在无法满足，否则只在宪法技术栈内解决

### 3. 前端代码红线（必须遵守）

1. **view-grid 组件模式**：新增页面严格按 `docs/60-AI工程设计/vol-skill.md §12` 写，不得自己造表格组件
2. **所有请求走 `http.js`**：禁止原生 fetch、禁止自己 axios.create 新实例（防止缺 Vol Authorization Header / lang / baseURL 注入）
3. **高度填满容器用 absolute top/left/right/bottom**：不要 height:100%，Vol 自带 el-scrollbar__view 父链无显式 height，height:100% 参考为 0 → 必定坍缩
4. **所有 Flex 嵌套子容器加 `min-height:0`**：W3C Flex 规范默认 min-height:auto，孙子节点的 height:100% 参考还是 0，是 80% 高度坍缩根因
5. **调试日志统一用 `console.log`**：Chrome Default levels 默认隐藏 console.debug/info，会导致你明明打了日志但用户一条都看不到，排查链直接断

### 4. 文档体系（文档即宪法）

来源：[项目全局规则.md §三 / §四](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/项目全局规则.md#L72-L177)

| 目录 | 职责 | 文档类型举例 |
|---|---|---|
| `docs/00-工程体系/` | 宪法级：管文档本身、管协作、管AI | 文档生命周期、协作模型、术语表、用户画像 |
| `docs/20-架构决策/` | 技术选型、架构图、性能评估 | 总体设计 V3 / DB 表设计 V2 / 旧版 Office 转换评估 V1 |
| `docs/40-领域设计/` | 业务建模（萌芽态为主） | |
| `docs/50-规划与优先级/` | 排期、迭代计划、Phase 划分 | YZH 改造路线 |
| `docs/60-AI工程设计/` | AI 宪法模板、Skills、知识库、踩坑记录 | vol-skill.md / DocExtractionRule 预览链 12 坑记录 |
| `docs/70-当前执行/` | 当前正在执行（开发中/待实施/待审核/研究中）文档 | Office 转换 / 标准目录 / YZH 升级 / 技术研究 |
| `docs/90-延展规划/` | 后续迭代参考（20/40/50 子目录） | 全生命周期领域设计、规模化评估 |
| `docs/历史文档/` | 过期文档统一存放 | 旧版本总体设计 V1/V2、旧版调研报告、10-调研与可行性/ 归档内容 |

**AI 检索流程强制遵守**：
用户提需求 → 识别关键字 → 读项目全局规则.md → 读目标目录 README.md → 读具体文档 → 对齐约束 → 执行

### 5. 用户画像 AI 协作偏好（沟通红线）

来源：`AGENTS.md` 用户规则 / `docs/00-工程体系/用户画像与AI协作偏好-V1.md`

1. **严禁非常详细的需求分析 + 重复问开发者细节 + 生成一堆 MD 文档**：有任务直接切入，先读代码再思考实现，不要上来就写需求分析
2. **严禁开发完成后直接调用测试命令验证**：只做静态 Diagnostics（GetDiagnostics / lint），不要跑测试框架/启动项目
3. **所有回复必须中文**
4. **除非开发者主动问，否则直接开始思考并实现代码逻辑**：不要先写「我来帮您...」
5. **杜绝一个任务拆成多次请求执行**：能在一轮里改完的文件，不要分成 2 轮
6. **最重要：完全以节约开发者 token 为目标**：少解释、直接给核心代码/结论；段落式分析换成表格对比；解释文字 1 个点不要超过 3 行

### 6. CertPlatform 7 个页面四周 padding 统一（本轮 V1~V12 已完成）

7 个路由：CertificationBody / OrgStage / OrgStandard / CertStage / ISOStandard / DirectoryConfig / DocExtractionRule
- 统一 padding：`top:16px / left:24px / right:24px / bottom:16px`
- DirectoryConfig 路由实际映射 DirectoryManager：`background:#f5f7fa`（浅灰）+ 左右卡片 `background:#fff border:1px solid #e4e7ed border-radius:4px`（对比色让留白肉眼可见）
- DocExtractionRule 三栏高度：六层 absolute + flex + min-height:0 链（见踩坑记录 §4）

---

## 第二部分：本次已完成工作（DocExtractionRule 预览链 V1~V13 全景）

| 序号 | 任务 | 状态 | 对应文档 / 代码锚点 |
|---|---|---|---|
| 1 | 7 个 CertPlatform 路由 padding 统一 16/24，DirectoryManager 背景/卡片对比色 | ✅ 完成 | DirectoryManager/index.vue L840-L934 |
| 2 | DocExtractionRule 三栏高度坍缩修复（六层 absolute+flex+min-height:0） | ✅ 完成 | DocExtractionRule/index.vue 根样式 L511-L525 |
| 3 | DocPreview 编译错误修复（previewUrl 重复声明 / onBeforeUnmount 未定义） | ✅ 完成 | DocPreview.vue Diagnostics 0 报错 |
| 4 | @vue-office 支持矩阵修正（xls 支持 / doc 不支持 / pptx 支持 / ppt 不支持） | ✅ 完成 | DocPreview.vue L133-L163 script 分支 |
| 5 | docx/xlsx/pptx OOXML ZIP Central Directory 子类型双重校验（防 JSZip central directory 报错） | ✅ 完成 | DocPreview.vue L192-L245 _detectOfficeKindFromZip |
| 6 | http.js 根修：fetch → http.get，Authorization Header 正确携带 | ✅ 完成 | DocPreview.vue _httpGetArrayBuffer / _httpGetBlob / _httpGetText |
| 7 | http.js 返回结构根修：去掉多余 `.data` 层（http.js resolve data 本身就是 response.data） | ✅ 完成 | DocPreview.vue L281-L378 loadPreview 主流程 |
| 8 | headers 不可见：魔数白名单 + payload 头 256 bytes 文本判断登录重定向（替代 Content-Type） | ✅ 完成 | DocPreview.vue _looksLikeAuthRedirect L229-L279 |
| 9 | 下载按钮根修：window.open → http.js blob + createObjectURL（避免拿到登录页 HTML） | ✅ 完成 | DocPreview.vue download() L388-L403 |
| 10 | 用户真实 .docx / .xlsx Network Hex 100% 合法性人工校验（排除后端/MinIO 损坏） | ✅ 完成 | 已手动验证 2 份样例 ZIP Central Directory 子类型正确 |
| 11 | 全链路调试日志统一 console.log（Default levels 必现 + 7 个锚点缺一不可） | ✅ 完成 | FileTree.vue / DocPreview.vue / index.vue 共 20+ 条日志 |
| 12 | ZIP Central Directory _rels/.rels 误判 docx 专属的致命 bug（本轮最终命中） | ✅ 完成 | DocPreview.vue L237-L245，只用 word/xl/ppt 子目录专属文件判断 |
| 13 | 旧版 Office 后端自动转换方案评估（NPOI xls→xlsx + LibreOffice doc→docx + MinIO 双存） | ✅ 完成 | 《旧版 Office 文档后端自动转换方案评估-V1.md》 |

---

## 第三部分：下次开发 TODO 清单（直接按 Phase 执行，无需再讨论方案）

> 对齐：《旧版 Office 文档后端自动转换方案评估-V1.md §五 改造时间评估》，每项都写了明确的交付物和验收标准。下次开发直接按顺序执行即可，不用再从头设计方案。

### Phase 1：DB 4 字段 + DownloadFile fallback（2 小时，最快见效）
**目标**：先把基础设施打平，后续 Phase 2/3 写转换服务直接复用

- [ ] 1.1 `StandardDirectoryFile.cs` 新增 4 字段：
  - `ConvertedStoragePath nvarchar(512)`（转换后 MinIO 路径）
  - `ConvertStatus nvarchar(20)`（pending / converting / converted / failed）
  - `ConvertMessage nvarchar(1024)`（失败原因）
  - `ConvertDate datetime`（完成时间，幂等去重用）
- [ ] 1.2 EF Core migration：生成 SQL 脚本，对齐 `docs/20-架构决策/sql/` 目录（命名 `cert_phase3_office_convert.sql`）
- [ ] 1.3 DTO 层同步新增 4 字段：FileTree 节点返回对象 / StandardDirectoryFile 列表接口 DTO
- [ ] 1.4 DocPreview.vue `buildFileUrl()` 1 行改造：优先 `props.file.convertedStoragePath ?? props.file.storagePath`
- [ ] 1.5 `StandardDirectoryController.DownloadFile` 新增可选参数 `bool downloadOriginal = false`
  - `downloadOriginal=false` 且 ConvertedStoragePath 非空且 ConvertStatus=converted → 拉转换后路径
  - 否则按原逻辑拉 StoragePath
- [ ] **Phase 1 验收**：手工 UPDATE 一条 StandardDirectoryFile 记录的 ConvertedStoragePath → 点 DocExtractionRule 预览 + 下载按钮，实际拉的是转换后路径的字节，byteLength 和 MinIO 里对应对象一致；`downloadOriginal=true` 时还是拉原始

### Phase 2：NPOI xls→xlsx 转换服务 + 后台队列（1.5 天）
**目标**：解决旧版 .xls 预览 + 提取；不做 .doc

- [ ] 2.1 `VOL.Core.csproj` 新增 NuGet 包引用：NPOI 2.7+（含 NPOI.OOXML），版本对齐 .NET 8
- [ ] 2.2 新建 `VOL.Builder/Services/CertPlatform/Convert/ConvertJob.cs`：任务记录（FileId / OriginalStoragePath / Ext / CreateTime）
- [ ] 2.3 新建 `ConvertHostedService : BackgroundService`（.NET 8 自带，零额外依赖）：
  - 内部用 `Channel<ConvertJob>` 有界队列（capacity 1000）+ `SemaphoreSlim(2)` 控制并发
  - 消费失败自动重试 3 次，第 3 次仍失败 → ConvertStatus=failed
  - 每个 Job 设 120s 超时（CancellationTokenSource.CancelAfter(120_000)）
- [ ] 2.4 新建 `VOL.Builder/Services/CertPlatform/Convert/XlsToXlsxConverter.cs`（NPOI IWorkbook 逐格迁移）：
  - 迁移顺序：Workbook → Sheets（Name/FreezePanes/MergeRegions）→ Rows（Height）→ Cells（Type/Value/Style 边框/对齐/字体/填充/DataFormat）→ Columns（Width）
  - 接受丢项清单：Pivot / VBA / 条件格式 / Chart / SmartArt / 复杂 Array Formula，ConvertMessage 里记录「丢失样式 count=N」
- [ ] 2.5 两个上传入口挂钩子：`StandardDirectoryService.UploadFile` / `UploadFileWithTask`（L871 / L1286）
  - ext == ".xls" → DB ConvertStatus=pending → `_channel.Writer.TryWrite(job)` 入队
  - ext != ".xls" / .doc → 不入队（Phase 3 处理 .doc）
- [ ] 2.6 MinIO 双存路径约定落地：转换后路径 = `Path.GetDirectoryName(originalPath) + "/.converted/" + Path.GetFileNameWithoutExtension(originalName) + ".xlsx"`
- [ ] **Phase 2 验收**：
  - ① 上传 10 份真实 .xls 质量记录表单，9/10 以上字段在 WPS 打开的 xls 和转换后的 xlsx 位置完全一致
  - ② 提取规则对同一份表单 xls 和转换后 xlsx 输出结果 byte-for-byte 相等（纯文本字段）
  - ③ DirectoryManager 目录树里 .xls 文件行的 ConvertStatus 30 秒内从 pending → converted

### Phase 3（强烈推荐，.doc 等于没解决就不做这步）：LibreOffice CLI doc→docx（1 天）
**目标**：解决 .doc 预览 + 提取，保真度 90%+

- [ ] 3.1 `VOL.WebApi/Dockerfile` + 本地开发 docker-compose 加 libreoffice-core + 中文字体层：
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
  RUN apt-get update && apt-get install -y --no-install-recommends \
        libreoffice-core libreoffice-writer libreoffice-calc \
        fonts-noto-cjk fonts-wqy-zenhei \
      && rm -rf /var/lib/apt/lists/*
  ```
- [ ] 3.2 本地开发环境（非 Docker）补充 macOS 安装说明：`brew install --cask libreoffice` + `ln -s /Applications/LibreOffice.app/Contents/MacOS/soffice /usr/local/bin/soffice`
- [ ] 3.3 新建 `DocToDocxConverter.cs`：
  - Process.Start soffice --headless --norestore --nolockcheck --nodefault --convert-to docx:"Office Open XML Text"
  - stdout/stderr 重定向捕获
  - 超时 120s 强制 Process.Kill(entireProcessTree:true)
  - exit code ≠ 0 或输出文件 byteLength < 100 → ConvertStatus=failed，ConvertMessage 记录 stderr 最后 1024 字符
- [ ] 3.4 上传钩子补充：ext == ".doc" 也入队（和 .xls 共享 Channel/HostedService）
- [ ] 3.5 转换后文件元数据补充：MinIO PutObject 时加 `x-converted-from: .doc yyyy-MM-dd HH:mm:ss` + ConvertJobId 做追溯链
- [ ] **Phase 3 验收**：
  - ① 上传 10 份真实 .doc 体系文件（表格嵌套 / 页眉页脚 / 图片 / 域代码 / 修订），WPS 打开原始 .doc 和转换后 .docx，肉眼对比差异 < 5%
  - ② 提取规则字段位置 10/10 一致
  - ③ 10MB .doc 平均转换时间 < 30s；20MB 最大 < 60s；失败率 < 5%，失败都有清晰 ConvertMessage

### Phase 4（锦上添花，运维体验）：ConvertStatus 徽标 + 重新转换（半天）
**目标**：运维同学不用查 DB，直接在前端看状态、点重跑

- [ ] 4.1 DocExtractionRule 左侧 FileTree 文件行：ConvertStatus=converted 绿色 ✅ / pending 灰色 ⏳ / converting 蓝色 🔄 / failed 红色 ❌ 徽标
- [ ] 4.2 FileTree 右键菜单：failed 行新增「重新转换」按钮 → 后端 `POST /api/standard-directory/reconvert/{fileId}` → ConvertStatus 重置为 pending + 重新入队
- [ ] 4.3 DirectoryManager 后台管理页：
  - 文件表格新增 ConvertStatus / ConvertMessage / ConvertDate 列
  - 顶部工具条加「批量补跑转换」：选中所有 ConvertStatus=failed 或 null 且 ext=doc/xls 的 → 批量入队
- [ ] **Phase 4 验收**：① 前端徽标和 DB 值实时一致；② 右键重新转换 30s 内状态刷新；③ 批量补跑 100 条记录 3 分钟内完成、无内存泄漏

### Phase 5（合规 / 历史数据）：历史补跑 + 哈希审计（脚本 ~几小时）
**目标**：用户之前上传的几千份 .doc/.xls 全部转换完，合规审计可追溯

- [ ] 5.1 一次性控制台脚本 `scripts/office-convert-history.csx`（或后台 Controller 临时接口）：
  - 全表遍历 StandardDirectoryFile：`WHERE (ConvertStatus IS NULL OR ConvertStatus='failed') AND FileType IN ('.doc','.xls') ORDER BY Id DESC`
  - 每条入队，进度打印到 Console
- [ ] 5.2 哈希审计脚本 `scripts/office-convert-audit.csx`：
  - 输出 CSV：FileId, FileName, OriginalStoragePath, OriginalSHA256, ConvertedStoragePath, ConvertedSHA256, ConvertStatus, ConvertMessage, ConvertDate
  - 导出到 `输出产物/2026-08-10_Office转换审计报告.csv`
- [ ] **Phase 5 验收**：
  - ① 存量 .doc/.xls ConvertStatus 100% 不是 pending（全 converted/failed）
  - ② 总失败率 < 3%，失败每条都有清晰 ConvertMessage，人工可处理
  - ③ 审计 CSV 导出成功，随机抽取 10 条记录 → 实际下载原始/转换后文件 → 人工对比 OK

### 关键 TODO 总览（Checklist 对齐 §三-六每项）

```
□ Phase 1：DB 4 字段 + EF migration + DTO + buildFileUrl + DownloadFile fallback（2h）
□ Phase 2：NPOI NuGet + ConvertHostedService(Channel+Semaphore) + XlsToXlsxConverter + 上传钩子 .xls（1.5d）
□ Phase 3：Dockerfile LibreOffice + 中文字体 + DocToDocxConverter + 上传钩子 .doc（1d）
□ Phase 4：ConvertStatus 徽标 + 右键重转换 + 后台批量补跑（0.5d）
□ Phase 5：历史补跑脚本 + 哈希审计报告 CSV（脚本几小时）
```

---

## 第四部分：下次开发执行顺序建议（按 ROI 排序，节约总工时）

**推荐执行顺序（按 ROI 从高到低，不用一次性做完所有 Phase）**：

| 顺序 | 阶段 | 总工时 | 解决的问题 | 为什么先做 |
|---|---|---|---|---|
| 1 | Phase 1 + Phase 2 | 1.5 天 + 2 小时 | .xls 预览 + 提取统一 xlsx | 不依赖 Docker，纯 .NET 代码；认证场景 .xls 文件量是 .doc 的 3~5 倍（质量记录/台账全是 xls），ROI 最高；做完立刻能验证方向正确性 |
| 2 | Phase 4（徽标/重跑） + Phase 5（历史补跑） | 1 天左右 | 存量 xls 全迁移 + 运维可见 | 转换逻辑已经 Phase 2 验证稳了，先补存量；运维同学能看到进度、出问题能手动重试，后面再加 doc 不会乱 |
| 3 | Phase 3（LibreOffice doc→docx） | 1 天 | .doc 预览 + 提取统一 docx | Docker 镜像改造 + soffice 稳定性调优最后做；.doc 文件量相对少（质量手册/程序文件），但只要做了 Phase 3 才叫「彻底解决旧版 Office」 |

→ **总工时预估：最快路径（只做 .xls）1.5 天；全量（xls+doc+运维）约 3 天**。

下次直接按本清单顺序执行，不用再做方案设计。遇到具体某步代码层面的问题（比如 NPOI 合并单元格迁移的 API 细节 / soffice 中文豆腐块字体没装对），再单独排查。
