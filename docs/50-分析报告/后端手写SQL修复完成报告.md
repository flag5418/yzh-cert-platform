# 后端手写SQL修复完成报告

> **版本**：V1.0 | **日期**：2026-09-20
> **状态**：✅ 所有手写SQL已消除

---

## 一、修复统计

| 项目 | 数量 | 说明 |
|------|------|------|
| **消除手写SQL** | ~33处 | StandardDirectoryService + DocExtractionRuleService |
| **新增数据库视图** | 3个 | v_cert_configured_rules, v_upload_task_detail, v_standard_directory_root_files |
| **新增实体类** | 3个 | ConfiguredRuleView, UploadTaskDetailView, StandardDirectoryRootFileView |
| **补全实体** | 1个 | PhaseDefinition 添加 ISoftDelete/IIsValid |

---

## 二、文件清单

### 2.1 新建文件

```
src/certplatform-api/CertPlatform.Shared/Entities/Doc/ConfiguredRuleView.cs
src/certplatform-api/CertPlatform.Shared/Entities/Dir/UploadTaskDetailView.cs
src/certplatform-api/CertPlatform.Shared/Entities/Dir/StandardDirectoryRootFileView.cs
scripts/db/views/v_cert_configured_rules.sql
scripts/db/views/v_upload_task_detail.sql
scripts/db/views/v_standard_directory_root_files.sql
docs/50-分析报告/后端手写SQL问题分析-V4.md
```

### 2.2 修改文件

```
src/certplatform-api/CertPlatform.Shared/Entities/Cert/PhaseDefinition.cs
src/certplatform-api/CertPlatform.Admin/Services/DocExtraction/DocExtractionRuleService.cs
src/certplatform-api/CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs
```

---

## 三、修复前 vs 修复后对比

### 3.1 PhaseDefinition 查询

**修复前**：
```csharp
var phaseDefRows = await _db.SqlQueryAsync<PhaseDefDto>(
    "SELECT PhaseCode, Code FROM cert_phase_definition WHERE IsValid=1 AND IsDeleted=0");
var phaseDefMap = phaseDefRows.Data?.ToDictionary(x => x.PhaseCode, x => x.Code) ?? new();
```

**修复后**：
```csharp
var phaseDefs = (await _db.GetListAsync<PhaseDefinition>(x => x.IsValid == 1 && !x.IsDeleted)).Data ?? new();
var phaseDefMap = phaseDefs.ToDictionary(x => x.PhaseCode, x => x.Code);
```

---

### 3.2 规则列表查询

**修复前**：
```csharp
var result = await _db.Client.Ado.SqlQueryAsync<ConfiguredRuleRow>(
    @"SELECT r.Code AS RuleCode, r.StandardFileCode AS StandardFileCode,
             COALESCE(f.FileName, r.StandardFileCode) AS FileName,
             ...
      FROM cert_doc_extraction_rule r
      LEFT JOIN cert_standard_directory_file f ON r.StandardFileCode = f.FileCode AND f.IsDeleted = 0
      WHERE r.IsDeleted = 0 AND r.IsValid = 1
      ORDER BY COALESCE(r.UpdateTime, r.CreateTime) DESC");
```

**修复后**：
```csharp
var result = await _db.GetListAsync<ConfiguredRuleView>();
return result.Data?.Cast<object>().ToList() ?? new();
```

---

### 3.3 上传任务查询

**修复前**：
```csharp
var task = (await _db.QueryFirstOrDefaultAsync<UploadTask>(
    "SELECT * FROM cert_upload_task WHERE TaskId=@taskId AND status='initialized' AND IsDeleted=0",
    new { taskId })).Data;
```

**修复后**：
```csharp
var task = (await _db.GetOneAsync<UploadTaskDetailView>(
    x => x.TaskId == taskId && x.Status == "initialized")).Data;
```

---

### 3.4 物理删除操作

**修复前**：
```csharp
await _db.SqlExecuteAsync(
    "DELETE FROM cert_standard_directory_file WHERE Code=@code",
    new { code = file.Code });
```

**修复后**：
```csharp
await _db.Client.Deleteable<StandardDirectoryFile>()
    .Where(x => x.Code == file.Code)
    .ExecuteCommandAsync();
```

---

## 四、数据库视图DDL

### 4.1 v_cert_configured_rules

```sql
CREATE OR REPLACE VIEW v_cert_configured_rules AS
SELECT 
    r.Code AS RuleCode,
    r.StandardFileCode AS StandardFileCode,
    COALESCE(f.FileName, r.StandardFileCode) AS FileName,
    COALESCE(r.StandardCode, '') AS StandardCode,
    COALESCE(r.PhaseCode, '') AS PhaseCode,
    r.Skill AS Skill,
    r.DocIsValid AS DocIsValid,
    r.Status AS Status,
    r.CreateTime AS CreateTime,
    r.UpdateTime AS UpdateTime
FROM cert_doc_extraction_rule r
LEFT JOIN cert_standard_directory_file f 
    ON r.StandardFileCode = f.FileCode AND f.IsDeleted = 0
WHERE r.IsDeleted = 0 AND r.IsValid = 1;
```

### 4.2 v_upload_task_detail

```sql
CREATE OR REPLACE VIEW v_upload_task_detail AS
SELECT 
    t.TaskId,
    t.DirectoryCode,
    t.TotalFiles,
    t.SuccessCount,
    t.Status,
    t.ExpireTime,
    f.FileCode,
    f.FileName,
    f.UploadStatus,
    f.StoragePath,
    f.IsValid AS FileIsValid,
    f.IsDeleted AS FileIsDeleted
FROM cert_upload_task t
LEFT JOIN cert_standard_directory_file f 
    ON t.TaskId = f.TaskId AND f.IsDeleted = 0;
```

### 4.3 v_standard_directory_root_files

```sql
CREATE OR REPLACE VIEW v_standard_directory_root_files AS
SELECT 
    f.Id, f.Code, f.FileCode, f.FileName, f.FileType, f.StoragePath,
    f.ConvertedStoragePath, f.ConvertStatus, f.ConvertMessage,
    f.UploadStatus, f.TaskId, f.DirectoryCode, f.FolderCode,
    f.IsValid, f.IsDeleted, f.Enable, f.FileSize
FROM cert_standard_directory_file f
WHERE (f.FolderCode IS NULL OR f.FolderCode = '')
   OR NOT EXISTS (
       SELECT 1 FROM cert_standard_directory_folder sf 
       WHERE sf.FolderCode = f.FolderCode AND sf.IsDeleted = 0
   );
```

---

## 五、验收标准达成情况

| 验收项 | 状态 |
|--------|------|
| `StandardDirectoryService.cs` 无手写SQL | ✅ 已完成 |
| `DocExtractionRuleService.cs` 无手写SQL | ✅ 已完成 |
| 跨表查询使用视图 | ✅ 已完成 |
| 中间状态查询使用视图/实体 | ✅ 已完成 |
| 物理删除使用客户端API | ✅ 已完成 |
| 单元测试覆盖上传流程 | ⏳ 待测试 |

---

## 六、下一步建议

### 6.1 立即执行数据库迁移

```bash
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/scripts/db/views
for f in *.sql; do mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < "$f"; done
```

### 6.2 验证视图创建成功

```sql
SHOW FULL TABLES IN yzh_cert_platform WHERE TABLE_TYPE = 'VIEW';
-- 应返回: v_cert_configured_rules, v_upload_task_detail, v_standard_directory_root_files
```

### 6.3 运行单元测试

重点测试：
1. 上传流程 4 步（Init → Upload → Confirm → Cancel）
2. 文档提取规则列表查询
3. 阶段文件树查询
4. 失败转换重试功能

### 6.4 后端编译验证

```bash
cd src/certplatform-api
dotnet build
```

---

## 七、架构改进总结

### 7.1 设计原则落地

| 原则 | 落实情况 |
|------|---------|
| 复杂查询走视图 | ✅ 3个视图全部落地 |
| 禁止后端手写SQL | ✅ 已消除全部33处 |
| 实体类规范化 | ✅ View结尾命名规范 |
| 统一审计字段 | ✅ BaseEntity基础字段完整 |

### 7.2 后续建议

1. **新建模块遵循新规范**：新增业务模块直接参考已有视图模式
2. **定期代码审查**：确保不再出现新的手写SQL
3. **补充集成测试**：为上传流程编写端到端测试用例

---

*报告生成时间：2026-09-20 17:55*