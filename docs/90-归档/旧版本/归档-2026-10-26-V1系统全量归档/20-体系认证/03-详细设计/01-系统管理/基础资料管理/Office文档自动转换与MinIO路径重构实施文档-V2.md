# Office 文档自动转换与 MinIO 路径重构实施文档 V2

> **版本**：V2.0 | **日期**：2026-08-10 | **状态**：已实施（2026-08-15 更新）
> **背景**：解决旧版 Office 文档（.doc/.xls）预览问题，重构 MinIO 存储路径为四级结构

> ⚠️ **实施状态（2026-08-15）**：本计划中的后台任务已由 **yzh 队列框架（YzhQueueManager + OfficeConvertTaskExecutor）** 落地，替代原计划的 `ConvertHostedService`（Channel/Semaphore）方案；DB 4 字段（converted_storage_path / convert_status / convert_message / convert_date）、MinIO `.converted` 双存、.doc/.xls 入队转换均已实现。MinIO 路径已按 V3 双顶层结构执行（见 [OSS存储结构重新设计-V1.md](./OSS存储结构重新设计-V1.md)），不再是本文档 1.1 的四级结构。

---

## 一、需求确认

### 1.1 MinIO 路径重构

**新路径规则**：`{企业编码}/{ISO标准编码(去冒号横杠)}/{阶段编码}/{文件夹}/{文件}`

| 层级 | 来源 | 示例 |
|------|------|------|
| 第1级 | 企业基础资料编码 | `CB001` |
| 第2级 | ISO标准编码（去特殊字符） | `ISO134852016` |
| 第3级 | 阶段编码 | `STAGE01` |
| 第4级 | 用户上传文件夹+文件 | `质量手册/程序文件.doc` |

**路径示例**：
```
原始文件：/CB001/ISO134852016/STAGE01/质量手册/XASL-QM-001.doc
转换后：  /CB001/ISO134852016/STAGE01/质量手册/.converted/XASL-QM-001.docx
```

### 1.2 旧版 Office 自动转换

| 格式 | 转换方案 | 保真度 |
|------|---------|--------|
| `.xls → .xlsx` | NPOI (IWorkbook 逐格迁移) | 80-90% |
| `.doc → .docx` | LibreOffice CLI (soffice) | 90-95% |

**转换时机**：上传后立即异步转换  
**失败处理**：仅提示，不要求重新上传  
**存储策略**：双存储（原始文件 + 转换后文件）

---

## 二、数据库变更

### 2.1 新增字段（cert_standard_directory_file 表）

```sql
ALTER TABLE `cert_standard_directory_file`
ADD COLUMN `converted_storage_path` VARCHAR(512) NULL COMMENT '转换后文件在 MinIO 的存储路径（.docx/.xlsx）',
ADD COLUMN `convert_status` VARCHAR(20) NULL COMMENT '转换状态：null/pending/converting/converted/failed',
ADD COLUMN `convert_message` VARCHAR(1024) NULL COMMENT '转换失败原因或丢失的样式信息',
ADD COLUMN `convert_date` DATETIME NULL COMMENT '转换完成时间';

CREATE INDEX `idx_convert_status` ON `cert_standard_directory_file`(`convert_status`);
CREATE INDEX `idx_file_type` ON `cert_standard_directory_file`(`FileType`);
```

**SQL 文件位置**：`src/server/Vue.NetCore/DB/mysql/add_file_convert_fields.sql`

### 2.2 实体类更新

**文件**：`VOL.Entity/CertPlatform/Dir/StandardDirectoryFile.cs`

新增字段已完成：
- `ConvertedStoragePath` - 转换后文件路径
- `ConvertStatus` - 转换状态
- `ConvertMessage` - 转换消息
- `ConvertDate` - 转换时间

---

## 三、实施阶段与 TODO 清单

### Phase 1：基础改造（数据库 + 路径重构 + 前端适配）

#### TODO-1.1 ✅ 数据库新增转换字段
- [x] `StandardDirectoryFile.cs` 新增 4 个字段
- [x] 生成 MySQL 迁移脚本 `add_file_convert_fields.sql`
- [ ] 执行 SQL 脚本到数据库
- [ ] 验证字段创建成功

#### TODO-1.2 🔄 修改上传逻辑 - 新路径规则
- [ ] 修改 `ICodeGeneratorService.cs` - 新增 `GenerateStoragePathV2` 方法
- [ ] 修改 `CodeGeneratorService.cs` - 实现四级路径生成
- [ ] 修改 `StandardDirectoryService.UploadFile` - 使用新路径方法
- [ ] 修改 `StandardDirectoryService.UploadFileWithTask` - 使用新路径方法
- [ ] 路径格式：`/{orgCode}/{cleanStandardCode}/{phaseCode}/{folderPath}/{fileName}`

#### TODO-1.3 ⏳ 前端适配新路径 + 优先使用 ConvertedStoragePath
- [ ] 修改 `DocPreview.vue` - `buildFileUrl()` 优先使用 `convertedStoragePath`
- [ ] 修改 `FileTree.vue` - 节点展示转换状态徽标
- [ ] 修改 `DirectoryManager/index.vue` - 添加转换状态列

---

### Phase 2：xls→xlsx 转换（NPOI）

#### TODO-2.1 ⏳ NPOI 包安装
- [ ] `VOL.Core.csproj` 添加 NPOI 2.7+ NuGet 包
- [ ] 验证 NPOI 引用正确

#### TODO-2.2 ⏳ XlsToXlsxConverter 实现
- [ ] 创建 `Converters/XlsToXlsxConverter.cs`
- [ ] 实现 IWorkbook 逐格迁移（Sheet/Row/Cell/CellStyle/MergeRegion）
- [ ] 处理日期格式、公式、合并单元格
- [ ] 记录丢失的样式到 ConvertMessage

#### TODO-2.3 ⏳ 异步转换队列
- [ ] 创建 `Models/ConvertJob.cs` - 转换任务模型
- [ ] 创建 `Services/OfficeConvertService.cs` - 转换服务
- [ ] 创建 `HostedServices/ConvertHostedService.cs` - 后台服务
- [ ] `Program.cs` 注册 HostedService
- [ ] 配置 Channel 队列 + SemaphoreSlim(2) 并发限制
- [ ] 实现超时 120s 强制终止

#### TODO-2.4 ⏳ 上传钩子集成
- [ ] `UploadFile` 方法检测 .xls 后缀，入队转换任务
- [ ] `UploadFileWithTask` 方法检测 .xls 后缀，入队转换任务
- [ ] 设置初始状态 `ConvertStatus = "pending"`

---

### Phase 3：doc→docx 转换（LibreOffice）

#### TODO-3.1 ⏳ Dockerfile 配置
- [ ] 修改 `vol.api/Dockerfile`
- [ ] 添加 LibreOffice 安装层
- [ ] 添加中文字体（fonts-noto-cjk, fonts-wqy-zenhei）
- [ ] 构建测试镜像验证

**Dockerfile 片段**：
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends \
      libreoffice-core libreoffice-writer libreoffice-calc \
      fonts-noto-cjk fonts-wqy-zenhei \
    && rm -rf /var/lib/apt/lists/*
```

#### TODO-3.2 ⏳ DocToDocxConverter 实现
- [ ] 创建 `Converters/DocToDocxConverter.cs`
- [ ] 实现 Process.Start 调用 soffice
- [ ] 参数配置：`--headless --norestore --nolockcheck --convert-to docx`
- [ ] 重定向 stdout/stderr 捕获输出
- [ ] 超时处理（120s）

#### TODO-3.3 ⏳ 上传钩子集成
- [ ] `UploadFile` 方法检测 .doc 后缀，入队转换任务
- [ ] `UploadFileWithTask` 方法检测 .doc 后缀，入队转换任务

---

### Phase 4：前端体验优化

#### TODO-4.1 ⏳ 转换状态徽标
- [ ] `FileTree.vue` - 文件节点显示状态图标
  - `converted` → 绿色 ✅
  - `converting/pending` → 灰色 ⏳
  - `failed` → 红色 ❌
- [ ] Tooltip 显示 ConvertMessage

#### TODO-4.2 ⏳ 转换失败提示
- [ ] `DocPreview.vue` - 预览失败时显示友好提示
- [ ] 提供「下载原始文件」按钮兜底

---

### Phase 5：历史数据补跑（可选）

#### TODO-5.1 ⏳ 存量文件转换脚本
- [ ] 创建 `Tools/BackfillConvertTool.cs`
- [ ] 查询所有 .doc/.xls 且 ConvertStatus = null 的文件
- [ ] 批量入队转换任务
- [ ] 生成转换报告 CSV

---

## 四、文件变更清单

### 后端文件

| 文件路径 | 操作 | 说明 |
|---------|------|------|
| `VOL.Entity/CertPlatform/Dir/StandardDirectoryFile.cs` | 修改 | 新增 4 个转换字段 ✅ |
| `VOL.Builder/IServices/CertPlatform/ICodeGeneratorService.cs` | 修改 | 新增 GenerateStoragePathV2 |
| `VOL.Builder/Services/CertPlatform/CodeGeneratorService.cs` | 修改 | 实现四级路径生成 |
| `VOL.Builder/Services/CertPlatform/StandardDirectoryService.cs` | 修改 | 上传逻辑 + 转换入队 |
| `VOL.Core/Converters/XlsToXlsxConverter.cs` | 新增 | NPOI xls→xlsx 转换 |
| `VOL.Core/Converters/DocToDocxConverter.cs` | 新增 | LibreOffice doc→docx 转换 |
| `VOL.Core/Models/ConvertJob.cs` | 新增 | 转换任务模型 |
| `VOL.Core/Services/OfficeConvertService.cs` | 新增 | 转换服务 |
| `VOL.Core/HostedServices/ConvertHostedService.cs` | 新增 | 后台转换服务 |
| `VOL.WebApi/Program.cs` | 修改 | 注册 HostedService |
| `VOL.WebApi/Dockerfile` | 修改 | 添加 LibreOffice + 字体 |

### 前端文件

| 文件路径 | 操作 | 说明 |
|---------|------|------|
| `DocExtractionRule/components/DocPreview.vue` | 修改 | 优先使用 convertedStoragePath |
| `DocExtractionRule/components/FileTree.vue` | 修改 | 转换状态徽标 |
| `Standard/DirectoryManager/index.vue` | 修改 | 转换状态列 |

### SQL 文件

| 文件路径 | 操作 | 说明 |
|---------|------|------|
| `DB/mysql/add_file_convert_fields.sql` | 新增 ✅ | 数据库迁移脚本 |

---

## 五、验收标准

### 功能验收

| 验收项 | 通过标准 |
|--------|---------|
| 新路径格式 | 上传文件后 MinIO 路径符合 `{org}/{iso}/{stage}/{folder}/{file}` |
| xls 转换 | 上传 .xls 后自动转换为 .xlsx，预览正常，表格内容一致 |
| doc 转换 | 上传 .doc 后自动转换为 .docx，预览正常，格式保真度 >90% |
| 状态显示 | 前端正确显示转换状态（✅⏳❌） |
| 失败处理 | 转换失败时显示提示，可下载原始文件 |

### 性能验收

| 验收项 | 标准 |
|--------|------|
| 上传响应 | < 3 秒（不等待转换完成） |
| xls 转换 | < 10 秒（10MB 文件） |
| doc 转换 | < 30 秒（10MB 文件） |
| 并发控制 | 最多 2 个文件同时转换 |

---

## 六、风险与规避

| 风险 | 规避措施 |
|------|---------|
| LibreOffice 镜像增大 800MB | 提前推到私有 Harbor |
| NPOI 复杂样式丢失 | 记录 ConvertMessage，保留原始文件 |
| 大文件转换超时 | 120s 强制终止，状态标记 failed |
| 中文显示豆腐块 | Dockerfile 安装思源黑体 + 文泉驿 |
| 恶意文件攻击 | 50MB 上传限制，沙箱进程隔离 |

---

## 七、当前进度

| 阶段 | 进度 | 状态 |
|------|------|------|
| Phase 1 - 数据库字段 | 100% | ✅ 完成 |
| Phase 1 - 路径重构 | 0% | 🔄 进行中 |
| Phase 1 - 前端适配 | 0% | ⏳ 待开始 |
| Phase 2 - xls→xlsx | 0% | ⏳ 待开始 |
| Phase 3 - doc→docx | 0% | ⏳ 待开始 |
| Phase 4 - 前端优化 | 0% | ⏳ 待开始 |

---

**下一步行动**：完成 TODO-1.2（上传逻辑路径重构）
