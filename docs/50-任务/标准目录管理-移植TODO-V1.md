# 标准目录管理 — 移植 TODO 清单

> 关联文档：[标准目录管理-移植实施计划-V1.md](./标准目录管理-移植实施计划-V1.md)
> 状态：⏳ 待开始

---

## 阶段 1: Service 层核心移植

### StandardDirectoryService 方法补充

- [ ] **1.1** 补充 `GetStageFileTree` 方法
  - 来源：老项目 325-517 行
  - 移植到：新项目 `StandardDirectoryService.cs`
  - 改动：EF Core → IDbOrm，同步 → 异步

- [ ] **1.2** 补充 `GetFilesByDirectory` 方法
  - 来源：老项目 708 行
  - 移植到：新项目 `StandardDirectoryService.cs`

- [ ] **1.3** 补充 `CreateFile` 方法
  - 来源：老项目 728-830 行
  - 移植到：新项目 `StandardDirectoryService.cs`

- [ ] **1.4** 补充 `ExportAsZip` 方法 + 辅助方法
  - 来源：老项目 833-993 行
  - 移植到：新项目 `StandardDirectoryService.cs`
  - 辅助方法：ExpandFolderCodes, AddChildFolderCodes, GetFolderPath, AddDirectoryToZip
  - ⚠️ **OSS 改写**：老项目直接调 `IMinioClient.GetObjectAsync`，新项目必须改为 `_storage.DownloadAsync()`（IObjectStorage 接口）

- [ ] **1.5** 补充 `UploadFile` 旧版方法
  - 来源：老项目 1041-1249 行
  - 移植到：新项目 `StandardDirectoryService.cs`
  - 辅助方法：ValidateUploadFileType, GetMaxSequence, ResolveOrgCodeAsync 等

- [ ] **1.6** 补充 `GetFileLockStatus` 方法
  - 来源：老项目 1350 行
  - 移植到：新项目 `StandardDirectoryService.cs`

- [ ] **1.7** 补充 `RetryFailedConversions` 方法
  - 来源：老项目 2348-2494 行
  - 移植到：新项目 `StandardDirectoryService.cs`
  - 辅助方法：DeriveOrgCodeFromPath, SourceExistsAsync
  - ⚠️ **OSS 改写**：`SourceExistsAsync` 中老项目调 `_minioClient.StatObjectAsync`，新项目必须改为 `_storage.ExistsAsync()`（IObjectStorage 接口）

- [ ] **1.8** 编译验证，修正 ORM 不兼容问题
  - 检查所有 `_db.Set<T>()` → `_db.GetListAsync<T>()` / `_db.GetOneAsync<T>()`
  - 检查所有 `await _db.SaveChangesAsync()` → 无（InsertAsync/UpdateAsync 自动保存）
  - 检查所有 `FromSqlRaw` → `_db.SqlQueryAsync<T>()`

---

## 阶段 2: Service 层补充移植

### DirectoryTemplateService 方法补充

- [ ] **2.1** 补充 `GetFileRequirements` 方法
  - 来源：老项目 DirectoryTemplateService.cs
  - 移植到：新项目 `DirectoryTemplateService.cs`

- [ ] **2.2** 补充 `SaveFileRequirement` 方法
  - 来源：老项目 DirectoryTemplateService.cs

- [ ] **2.3** 补充 `DeleteFileRequirement` 方法
  - 来源：老项目 DirectoryTemplateService.cs

### CodeGeneratorService 方法补充

- [ ] **2.4** 补充 `GenerateEnterpriseDocumentPath` 方法
  - 来源：老项目 CodeGeneratorService.cs 179-196 行

- [ ] **2.5** 补充 `GenerateEnterpriseConvertedPath` 方法
  - 来源：老项目 CodeGeneratorService.cs 202-220 行

- [ ] **2.6** 编译验证

---

## 阶段 3: Controller 层补全

### StandardDirectoryController 补充 API

- [ ] **3.1** 补充 `ExportAsZip` API
  - 路由：POST `configs/{directoryCode}/export`
  - 调用：`_service.ExportAsZipAsync()`
  - 返回：File(stream, "application/zip", fileName)

- [ ] **3.2** 补充 `GetStageFileTree` API
  - 路由：GET `stage-files/{directoryCode}`
  - 调用：`_service.GetStageFileTreeAsync()`
  - 返回：`Ok(new { code = 200, data = result })`

- [ ] **3.3** 补充 `UploadFile` (旧版) API
  - 路由：POST `upload-file`
  - 调用：`_service.UploadFileAsync(file, directoryCode, relativePath)`
  - 特性：`[DisableRequestSizeLimit]`

- [ ] **3.4** 补充 `CreateFile` API
  - 路由：POST `folders/{folderCode}/files/create`
  - 调用：`_service.CreateFileAsync(file)`

- [ ] **3.5** 补充 `GetDirectoryFiles` API
  - 路由：GET `directory-files`
  - 调用：`_service.GetFilesByDirectoryAsync(directoryCode)`

- [ ] **3.6** 补充 `GetFileLockStatus` API
  - 路由：POST `file-lock-status`
  - 调用：`_service.GetFileLockStatusAsync(fileCodes)`

- [ ] **3.7** 补充 `RetryFailedConversions` API
  - 路由：POST `convert/retry-failed`
  - 调用：`_service.RetryFailedConversionsAsync()`

### DirectoryTemplateController 补充 API

- [ ] **3.8** 补充 `GetFileRequirements` API
  - 路由：GET `fileRequirements`
  - 调用：`_service.GetFileRequirementsAsync(folderCode)`

- [ ] **3.9** 补充 `SaveFileRequirement` API
  - 路由：POST `saveFileRequirement`
  - 调用：`_service.SaveFileRequirementAsync(entity)`

- [ ] **3.10** 补充 `DeleteFileRequirement` API
  - 路由：POST `deleteFileRequirement`
  - 调用：`_service.DeleteFileRequirementAsync(requirementCode)`

- [ ] **3.11** 编译验证

---

## 阶段 4: 前端验证

- [ ] **4.1** 验证目录管理主页面功能完整
- [ ] **4.2** 验证上传 4 步流程
- [ ] **4.3** 验证下载功能
- [ ] **4.4** 验证文件夹 CRUD
- [ ] **4.5** 验证文件 CRUD
- [ ] **4.6** 验证导出 ZIP 功能
- [ ] **4.7** 验证文件要求管理功能

---

## 阶段 5: 集成测试

- [ ] **5.1** 后端编译通过
- [ ] **5.2** 前端编译通过
- [ ] **5.3** 后端启动成功
- [ ] **5.4** 前端启动成功
- [ ] **5.5** API 联调测试
- [ ] **5.6** 完整业务流程测试

---

## 已完成项

<!-- 完成后将 ❌ 改为 ✅ -->

---

## ⚠️ OSS 接口化红线

移植过程中，所有涉及文件存储的操作 **必须通过 `IObjectStorage` 接口**，禁止直接引用 Minio SDK：

| 老项目（禁止） | 新项目（必须） |
|--------------|--------------|
| `_minioClient.PutObjectAsync(args)` | `_storage.UploadAsync(objectName, stream, size, contentType)` |
| `_minioClient.GetObjectAsync(args)` | `_storage.DownloadAsync(objectName)` |
| `_minioClient.StatObjectAsync(args)` | `_storage.ExistsAsync(objectName)` |
| `_minioClient.RemoveObjectAsync(args)` | `_storage.DeleteAsync(objectName)` |
| `_minioClient.ListObjectsEnumAsync(args)` | `_storage.ListObjectsAsync(prefix)` |

> 后续如需切换阿里 OSS，只需在 `Program.cs` 中更换 `AddSingleton<IObjectStorage, AliyunOssStorage>()`，业务代码无需改动。

---

## 备注

- 老项目代码位置：`/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/old/server/Vue.NetCore/vol.api/`
- 新项目代码位置：`/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/certplatform-api/`
- ORM 改写详见实施计划文档第八章
- OSS 接口详见实施计划文档 §8.1
