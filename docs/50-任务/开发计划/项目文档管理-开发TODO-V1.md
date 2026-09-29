# 项目文档管理 — 开发 TODO 清单 V1

> 创建时间：2026-09-13
> 用途：AI 持续开发跟踪，每完成一项自动勾选
> 规则：`[ ]` = 待完成，`[x]` = 已完成，`[~]` = 进行中

---

## P0：YZH 核心能力（对象存储 + Queue）

### P0-1：对象存储基础设施

> 设计原则：接口抽象 `IObjectStorage`，实现可插拔（MinIO / 阿里 OSS / 本地文件系统）。
> 配置驱动：`appsettings.json` 中 `Storage:Provider` 选择后端（"minio" / "aliyun-oss" / "local"）。

- [x] 1.1 `YZH.Core.DataBase` 添加 Minio NuGet 包引用
- [x] 1.2 新建 `YZH.Core.Stand/Interfaces/IObjectStorage.cs`（通用对象存储接口，7个方法）
- [x] 1.3 新建 `YZH.Core.DataBase/Infra/MinioObjectStorage.cs`（MinIO 实现，实现 IObjectStorage）
- [x] 1.4 新建 `YZH.Core.DataBase/Infra/AliyunOssStorage.cs`（阿里 OSS 实现，实现 IObjectStorage，预留骨架）
- [x] 1.5 新建 `YZH.Core.Web/Extensions/StorageServiceExtensions.cs`（按配置 Provider 注册对应实现）
- [x] 1.6 `appsettings.json` 添加存储配置段（`Storage:Provider` / `MinIO:*` / `AliyunOss:*`）
- [x] 1.7 `YzhWebBuilder.cs` 调用 `AddYzhStorage()`
- [x] 1.8 编译验证 0 错误

### P0-2：Queue 实体

- [x] 2.1 新建 `YZH.Core.Stand/Models/Queue/YzhQueue.cs`（主表，参考旧 Queue.cs）
- [x] 2.2 新建 `YZH.Core.Stand/Models/Queue/YzhQueueTask.cs`（子任务，参考旧 QueueTask.cs）
- [x] 2.3 新建 `YZH.Core.Stand/Models/Queue/YzhQueueResourceLock.cs`（资源锁，参考旧 QueueResourceLock.cs）
- [x] 2.4 编译验证 0 错误

### P0-3：Queue 接口

- [x] 3.1 新建 `YZH.Core.Stand/Interfaces/IYzhTaskExecutor.cs`（TaskType + ExecuteAsync）
- [x] 3.2 新建 `YZH.Core.Stand/Interfaces/IYzhQueueNotifier.cs`（NotifyAsync）
- [x] 3.3 新建 `YZH.Core.Stand/Interfaces/IYzhQueueCancelHandler.cs`（OnQueueCancelledAsync）
- [x] 3.4 编译验证 0 错误

### P0-4：Queue 引擎

- [x] 4.1 新建 `YZH.Core.DataBase/Services/QueueManager.cs`（从旧项目 1110 行迁移，16个 public 方法）
- [x] 4.2 新建 `YZH.Core.DataBase/Services/QueueHostedService.cs`（BackgroundService Worker）
- [x] 4.3 新建 `YZH.Core.Web/Extensions/QueueServiceExtensions.cs`（DI 注册）
- [x] 4.4 `YzhWebBuilder.cs` 调用 `AddYzhQueue()`
- [x] 4.5 编译验证 0 错误

### P0-5：Queue DDL

- [x] 5.1 编写 `scripts/db/create_queue_tables.sql`（yzh_queue + yzh_queue_task + yzh_queue_resource_lock）
- [x] 5.2 执行 DDL 到 yzh_cert_platform（表已存在，结构正确）
- [x] 5.3 验证表结构

---

## P1：业务 Service 迁移

### P1-1：OfficeConvertService（转换核心）

- [ ] 6.1 新建 `CertPlatform.Shared/Services/OfficeConvertService.cs`（从旧项目迁移）
- [ ] 6.2 新建 `CertPlatform.Shared/Services/OfficeConvertTaskExecutor.cs`（实现 IYzhTaskExecutor，TaskType=file_convert）
- [ ] 6.3 编译验证 0 错误

### P1-2：CertQueueNotifier + UploadQueueCancelHandler

- [ ] 7.1 新建 `CertPlatform.Shared/Services/CertQueueNotifier.cs`（实现 IYzhQueueNotifier）
- [ ] 7.2 新建 `CertPlatform.Shared/Services/UploadQueueCancelHandler.cs`（实现 IYzhQueueCancelHandler）
- [ ] 7.3 编译验证 0 错误

### P1-3：DirectoryTemplateService

- [ ] 8.1 新建 `CertPlatform.Shared/Services/DirectoryTemplateService.cs`（11个方法，从旧项目迁移）
- [ ] 8.2 编译验证 0 错误

### P1-4：StandardDirectoryService（核心，2575行）

- [ ] 9.1 新建 `CertPlatform.Shared/Services/StandardDirectoryService.cs`（27个方法，从旧项目迁移）
- [ ] 9.2 编译验证 0 错误

---

## P2：Controller 迁移

### P2-1：DirectoryTemplateController

- [ ] 10.1 新建 `CertPlatform.Admin/Controllers/Foundation/DirectoryTemplateController.cs`（11个 API）
- [ ] 10.2 编译验证 0 错误
- [ ] 10.3 后端重启 + API 验证

### P2-2：StandardDirectoryController

- [ ] 11.1 新建 `CertPlatform.Admin/Controllers/Workflow/StandardDirectoryController.cs`（21个 API）
- [ ] 11.2 编译验证 0 错误
- [ ] 11.3 后端重启 + API 验证

### P2-3：QueueMonitorController

- [ ] 12.1 新建 `CertPlatform.Admin/Controllers/System/QueueMonitorController.cs`（6个 API）
- [ ] 12.2 编译验证 0 错误
- [ ] 12.3 后端重启 + API 验证

---

## P3：前端迁移

### P3-1：Composable 补全

- [ ] 13.1 重写 `cert-share/src/composables/useDirectoryApi.ts`（对接后端 API）
- [ ] 13.2 重写 `cert-share/src/composables/useFileTree.ts`（对接后端 API）

### P3-2：CertDirectoryTree.vue 补全

- [ ] 14.1 重写 `cert-share/src/components/CertDirectoryTree.vue`（含队列锁定/搜索/轮询）

### P3-3：DirectoryManager 主页面

- [ ] 15.1 新建 `cert-admin/src/pages/workflow/directory/index.vue`（主页面布局）
- [ ] 15.2 新建 `cert-admin/src/pages/workflow/directory/ConvertProgressPanel.vue`（转换进度面板）

### P3-4：QueueMonitor 页面

- [ ] 16.1 新建 `cert-admin/src/pages/system/queue-monitor/index.vue`

### P3-5：路由

- [ ] 17.1 更新 `cert-admin/src/router/index.ts`（directory-manager + queue-monitor 路由）

---

## P4：集成验证

- [ ] 18.1 后端全部编译 0 错误
- [ ] 18.2 前端 Vite 转换 0 新增错误
- [ ] 18.3 目录模板管理 CRUD 验证
- [ ] 18.4 标准目录管理 TreeTable 验证
- [ ] 18.5 文件上传 → MinIO 存储验证
- [ ] 18.6 文件下载 → MinIO 流式返回验证
- [ ] 18.7 文件转换 → 队列入队 → 执行 → 完成验证
- [ ] 18.8 队列监控 → 取消/重试验证

---

## 进度统计

| 阶段 | 总项 | 已完成 | 完成率 |
|------|------|--------|--------|
| P0（核心能力） | 21 | 21 | 100% |
| P1（Service） | 8 | 0 | 0% |
| P2（Controller） | 9 | 0 | 0% |
| P3（前端） | 7 | 0 | 0% |
| P4（集成验证） | 8 | 0 | 0% |
| **总计** | **53** | **21** | **40%** |
