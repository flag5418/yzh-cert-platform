# 2026-08-13 NoTracking 静默失效 + 文件夹编码碰撞 + 重命名级联缺失 三类问题修复

> **日期**：2026-08-13 | **类型**：后端 EF Core + 数据库设计 | **严重度**：高
>
> **关联提交**：`651b3af`（修复上传队列状态/隔离与 doc 转换管道）
>
> **官方文档参考**：
> - EF Core Query Tracking：https://learn.microsoft.com/zh-cn/ef/core/querying/tracking
> - Vol 框架数据库访问：http://v3.volcore.xyz/docs/cs/dev/db.html

---

## 踩坑 1：VOLContext NoTracking 导致 465 个转换任务永久 stuck

### 现象
转换队列积压了 465 个待处理任务，无论多少时间都不动。Worker 持续领取同一任务，但状态始终停在 `pending`，从未进入 `processing` / `completed` / `failed`。

### 根因
`VOLContext` 的默认查询行为是 **NoTracking**（不跟踪实体变更）。`ConvertQueueManager` 和 `OfficeConvertService` 中多处"先查实体 → 改属性 → SaveChanges"的路径均未加 `.AsTracking()`，导致：
1. `ConvertQueueManager.ClaimNextJob()` 领取任务后修改 `job.Status = "processing"` 和 `job.LockedAt`，但 `SaveChanges()` 静默无操作。
2. `OfficeConvertService` 中 `job.Status = "failed"` 同样不落库。
3. Worker 每隔轮询周期重新领取同一任务（因为 DB 里状态还是 `pending`），形成死循环。

> **官方文档对照**：[EF Core Tracking](https://learn.microsoft.com/zh-cn/ef/core/querying/tracking) 明确说明：NoTracking 查询返回的实体不受 DbContext 追踪，对其属性所做的修改在 `SaveChanges()` 时不会持久化到数据库。Vol 框架的 `VOLContext` 继承了这一行为。

### 解决方案
在需要修改实体的查询后加 `.AsTracking()`：

```csharp
// ConvertQueueManager.ClaimNextJob()
var job = await db.Set<ConvertJob>()
    .FromSqlRaw(@"SELECT * FROM cert_file_convert_job ... FOR UPDATE SKIP LOCKED")
    .AsTracking()                    // ← 新增
    .FirstOrDefaultAsync();

// ConvertQueueManager.MarkFailedAsync()
var jobToUpdate = await db.Set<ConvertJob>()
    .AsTracking()                    // ← 新增
    .FirstOrDefaultAsync(j => j.Id == job.Id);

// ConvertQueueManager.CancelTasksAsync()
var pendingJobs = await db.Set<ConvertJob>()
    .AsTracking()                    // ← 新增
    .Where(j => j.TaskId == taskId && j.Status == "pending")
    .ToListAsync();
```

```csharp
// OfficeConvertService.ConvertAsync()
// 传入的 job 由 ConvertQueueManager 领取（另一 DbContext 实例 + NoTracking），
// 必须 Attach 到当前上下文，否则 job.Status 的更新在 SaveChanges 时不会落库。
if (_db.Entry(job).State == EntityState.Detached)
{
    _db.Set<ConvertJob>().Attach(job);
}
```

### 教训
- **凡是在同一方法内"查询 → 改属性 → SaveChanges"的路径，必须确保实体被 Tracking。**
- NoTracking 对只读查询（如列表展示）有性能优势，但对写操作路径是隐形陷阱。
- 发现"状态不变"的 Bug 时，优先检查是否用了 NoTracking 但没 `.AsTracking()` 或 `.Attach()`。
- Vol 框架批量保存场景（`SaveRange`）同样需要 Tracking，不能用 NoTracking 查询结果直接批量保存。

---

## 踩坑 2：GetMaxSequence 按 parentCode 分组导致跨父节点同级编码碰撞

### 现象
在不同父文件夹下分别创建 L02 层级的子文件夹时，生成的 `FolderCode` 出现重复（相同深度 + 相同序号），导致 MinIO 路径冲突或数据库唯一键冲突。

### 根因
原实现 `GetMaxSequence(directoryCode, parentCode)` 按 `(目录编码, 父文件夹编码)` 计算最大序号。当两个不同父文件夹（`FD-A`、`FD-B`）各自创建第一个子文件夹时，都会得到序号 1，产生：
- `FD-A|L02|S001` 和 `FD-B|L02|S001` → FolderCode 碰撞（若编码规则未含父节点信息）

### 解决方案
改为按 `(DirectoryCode, Depth)` 全局分配序号，同一深度层级在整个目录树中唯一：

```csharp
// 旧：按 ParentCode 分组
private int GetMaxSequence(string directoryCode, string parentCode)
{
    var query = _db.Set<StandardDirectoryFolder>()
        .Where(x => x.DirectoryCode == directoryCode 
                 && x.ParentCode == parentCode 
                 && x.Enable == true);
    var folders = query.ToList();
    return folders.Max(f => ExtractSequence(f.FolderCode)) + 1;
}

// 新：按 Depth 分组（同目录同深度全局唯一）
private int GetMaxSequence(string directoryCode, int depth)
{
    var folders = _db.Set<StandardDirectoryFolder>()
        .Where(x => x.DirectoryCode == directoryCode
                 && x.Depth == depth
                 && x.Enable == true)
        .ToList();
    return folders.Max(f => ExtractSequence(f.FolderCode)) + 1;
}
```

同时在 `StandardDirectoryFolder` 实体中增加了 `Depth` 字段（默认为 1），并在创建时显式赋值。

### 教训
- 编码设计时，序号的作用域要明确（全局 / 父节点内 / 深度内），避免隐式依赖数据结构。
- 当编码规则不含父节点信息时，必须用更高层级的作用域（如 Depth）来保证唯一性。

---

## 踩坑 3：文件夹重命名缺少 MinIO 级联同步

### 现象
用户在 DirectoryManager 页面重命名文件夹后，DB 中的 `FolderName` 更新了，但：
1. 该文件夹下所有子文件夹的 `FullPath` 未更新 → 面包屑显示旧名
2. 该文件夹下所有文件的 `StoragePath` / `ConvertedStoragePath` 未更新 → 下载/预览 404
3. MinIO 中旧路径的对象仍然存在，新路径无对应对象 → 存储混乱

### 根因
原 `RenameFolderAsync` 仅更新本文件夹的 `FolderName`，没有：
1. 递归更新后代文件夹的 `FullPath`（名称路径）
2. 递归更新后代文件的 `StoragePath` / `ConvertedStoragePath`
3. 在 MinIO 中移动对应对象（Copy + Delete）
4. NoTracking 问题同样影响此路径（`_db.SaveChangesAsync()` 静默失效）

### 解决方案
重写 `RenameFolderAsync`，实现完整级联：

```
重命名流程：
  1. 收集所有后代文件夹 + 后代文件（广度优先，保证父先于子）
  2. 构建 oldPathMap / newPathMap（名称路径段映射）
  3. MinIO 阶段：按映射逐一 RenameAsync（旧路径 → 新路径）
     - 任一移动失败 → 回滚已移动的对象，整体抛异常，DB 不动
  4. DB 阶段：
     - 本文件夹：FolderName + FullPath 更新
     - 后代文件夹：FullPath 级联更新（基于 newPathMap）
     - 后代文件：StoragePath / ConvertedStoragePath 替换名称路径段
     - 全部使用 AsTracking() + SaveChangesAsync()
```

关键实现要点：
- MinIO 操作在前，DB 操作在后；MinIO 失败立即回滚，不触碰 DB。
- DB 更新时必须用 `AsTracking()`，否则 `SaveChanges` 静默无操作。
- 非强制模式（`force=false`）下，若文件夹有子项则拒绝重命名并返回明确错误提示。
- 强制模式（`force=true`）下，前端弹窗确认后执行完整级联。

### 教训
- **文件夹/文件路径级联更新是高风险操作，必须原子化（先 MinIO 再 DB，失败回滚）。**
- NoTracking 在所有写路径（创建/更新/删除）都是隐形陷阱，必须显式加 `.AsTracking()`。
- 前端按钮应在有子项时禁用重命名，或明确展示 force 确认对话框。

---

## 踩坑 4：根目录级孤立文件不可见（FolderCode 退化问题）

### 现象
历史数据中直接上传到目录根部（无子文件夹）的文件，在文档提取规则页面的目录树中不显示（pdf/jpg 等尤其常见）。

### 根因
这些文件的 `FolderCode` 退化为 `DirectoryCode`（而非 `FD-` 开头的文件夹编码），导致 `GetFolderTree` 查询时 `WHERE FolderCode IN (folderCodes)` 过滤掉了它们。

### 解决方案
在 `StandardDirectoryService.GetStageFileTreeAsync()` 中增加根目录级孤立文件处理：
1. 查询所有文件后，分离出 `FolderCode` 不在有效文件夹集合中的文件。
2. 创建一个虚拟的"根目录"节点（Code = DirectoryCode，Name = "根目录"，Depth = 0）。
3. 将这些孤立文件挂入该虚拟节点的 Files 列表。
4. 将该虚拟节点插入结果树的第 0 位。

前端 `DirectoryManager/index.vue` 同样做了对应处理：根级别查询时，若 API 返回的文件夹节点无子节点但有文件，则直接展示这些文件。

### 教训
- 历史脏数据需要在查询层做兼容处理，而不是期望数据已经规整。
- 编码规则中 `FD-` 前缀是文件夹的唯一标识，任何脱离这个规则的记录都需要特殊处理。
