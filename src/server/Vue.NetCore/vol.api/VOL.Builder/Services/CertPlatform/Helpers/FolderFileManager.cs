using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Dir;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 文件夹文件管理器实现
    /// 负责文件夹的创建、重命名、删除等数据库+MinIO操作
    ///
    /// 重要：VOLContext 默认 QueryTrackingBehavior=NoTracking，
    /// 所有"先查实体→改属性→SaveChanges"的路径必须使用 AsTracking()
    /// 或显式设置 EntityState.Modified，否则更新/软删除静默失效。
    /// </summary>
    public class FolderFileManager : IFolderFileManager
    {
        private readonly VOLContext _db;
        private readonly IMinIOHelper _minioHelper;
        private readonly ICodeGeneratorService _codeGenerator;

        public FolderFileManager(VOLContext db, IMinIOHelper minioHelper, ICodeGeneratorService codeGenerator)
        {
            _db = db;
            _minioHelper = minioHelper;
            _codeGenerator = codeGenerator;
        }

        /// <summary>创建文件夹（含递归生成子路径）</summary>
        public async Task<StandardDirectoryFolder> CreateFolderAsync(StandardDirectoryFolder folder)
        {
            // 生成编码
            folder.Code = Guid.NewGuid().ToString("N");
            folder.FolderCode = _codeGenerator.GenerateFolderCode(
                folder.DirectoryCode, 
                folder.Depth, 
                GetMaxSequence(folder.DirectoryCode, folder.Depth) + 1);
            folder.CreateDate = DateTime.Now;
            folder.Status = "draft";
            folder.Enable = true;
            folder.IsValid = true;
            // 计算名称路径（FullPath）
            folder.FullPath = await BuildNewFolderFullPathAsync(folder);

            _db.Set<StandardDirectoryFolder>().Add(folder);
            await _db.SaveChangesAsync();

            return folder;
        }

        /// <summary>
        /// 重命名文件夹 — 级联更新：
        ///   1. 本文件夹与所有后代文件夹的 FullPath（名称路径）
        ///   2. 所有直接/间接文件的 FullPath、StoragePath、ConvertedStoragePath
        ///   3. MinIO 对象：旧前缀 → 新前缀（Copy + Delete）
        /// </summary>
        public async Task<bool> RenameFolderAsync(string oldFolderCode, string newFolderName, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(newFolderName))
                return false;

            var folder = await _db.Set<StandardDirectoryFolder>()
                .FirstOrDefaultAsync(x => x.FolderCode == oldFolderCode && x.Enable == true);
            if (folder == null)
                return false;
            if (folder.FolderName == newFolderName)
                return true;

            // 非强制模式下，有子项则拒绝并要求确认
            if (!force)
            {
                var childCount = await _db.Set<StandardDirectoryFolder>()
                    .CountAsync(x => x.ParentCode == oldFolderCode && x.Enable == true);
                var fileCount = await _db.Set<StandardDirectoryFile>()
                    .CountAsync(x => x.FolderCode == oldFolderCode && x.Enable == true && x.IsValid == true);
                if (childCount > 0 || fileCount > 0)
                    throw new InvalidOperationException(
                        $"无法重命名：该文件夹下有 {childCount} 个子文件夹和 {fileCount} 个文件。请指定 force=true 强制重命名（将同步更新所有子项与 MinIO 路径）。");
            }

            // 计算名称路径前缀（MinIO 路径与 FullPath 均基于文件夹名称层级）
            var oldPrefix = await BuildFolderPathAsync(folder);
            var parentPrefix = string.IsNullOrEmpty(folder.ParentCode)
                ? ""
                : await BuildFolderPathByCodeAsync(folder.ParentCode);
            var newPrefix = string.IsNullOrEmpty(parentPrefix)
                ? newFolderName
                : $"{parentPrefix}/{newFolderName}";

            // 收集所有后代文件夹与文件（启用状态）
            var descendants = await GetAllDescendantFoldersAsync(oldFolderCode);
            var files = await GetAllDescendantFilesAsync(oldFolderCode);

            // 重命名前路径映射（旧值，必须在修改实体前捕获）与 新路径映射（广度优先，保证父先于子）
            // 注意：DB 段会就地修改 descendants 实体的 FullPath，因此文件级联必须基于映射而不是实体属性。
            var oldPathMap = new Dictionary<string, string> { [folder.FolderCode] = oldPrefix };
            var newPathMap = new Dictionary<string, string> { [folder.FolderCode] = newPrefix };
            foreach (var child in descendants)
            {
                oldPathMap[child.FolderCode] = child.FullPath?.Trim('/') ?? child.FolderName;
                var parentPath = child.ParentCode != null && newPathMap.TryGetValue(child.ParentCode, out var pp) ? pp : "";
                newPathMap[child.FolderCode] = string.IsNullOrEmpty(parentPath)
                    ? child.FolderName
                    : $"{parentPath}/{child.FolderName}";
            }

            // ===== 1) MinIO：先复制到新前缀（失败即中止并回滚，DB 不动） =====
            // 注意：StoragePath 是完整存储路径（含机构/标准/阶段前缀），不能用名称路径直接匹配。
            // 策略：对每个文件，取其所在文件夹的旧/新 FullPath（名称路径段），
            //       在 StoragePath 中把该整段替换为新路径段。
            var moved = new List<(string NewPath, string NewConverted)>();
            try
            {
                foreach (var file in files)
                {
                    var parentOldPath = GetParentOldPath(file, folder.FolderCode, oldPathMap, oldPrefix);
                    var parentNewPath = GetParentNewPath(file, folder.FolderCode, newPathMap, newPrefix);
                    var oldPath = file.StoragePath?.TrimStart('/') ?? "";
                    var newPath = ReplaceStorageDirSegment(oldPath, parentOldPath, parentNewPath);
                    var oldConverted = file.ConvertedStoragePath?.TrimStart('/') ?? "";
                    var newConverted = ReplaceStorageDirSegment(oldConverted, parentOldPath, parentNewPath);

                    // 移动前检查对象是否存在：历史脏数据/孤儿记录的对象可能已不在 MinIO，
                    // 缺失时跳过移动（DB 路径仍照常级联），避免整体回滚失败。
                    if (!string.IsNullOrEmpty(oldPath) && newPath != oldPath)
                    {
                        if (await _minioHelper.ExistsAsync(oldPath))
                            await _minioHelper.RenameAsync(oldPath, newPath);
                        else
                            Console.WriteLine($"[RenameFolder] 跳过移动（MinIO 对象不存在）: {oldPath}");
                    }
                    if (!string.IsNullOrEmpty(oldConverted) && newConverted != oldConverted)
                    {
                        if (await _minioHelper.ExistsAsync(oldConverted))
                            await _minioHelper.RenameAsync(oldConverted, newConverted);
                        else
                            Console.WriteLine($"[RenameFolder] 跳过转换文件移动（MinIO 对象不存在）: {oldConverted}");
                    }

                    moved.Add((newPath, newConverted));
                }
            }
            catch (Exception ex)
            {
                // 回滚已移动的对象，保持旧状态
                foreach (var item in moved)
                {
                    try { if (!string.IsNullOrEmpty(item.NewPath)) await _minioHelper.DeleteAsync(item.NewPath); } catch { }
                    try { if (!string.IsNullOrEmpty(item.NewConverted)) await _minioHelper.DeleteAsync(item.NewConverted); } catch { }
                }
                throw new InvalidOperationException($"重命名失败：MinIO 对象移动异常（已回滚，数据未变更）。{ex.Message}", ex);
            }

            // ===== 2) 数据库批量更新（NoTracking 模式下必须标记 Modified） =====
            folder.FolderName = newFolderName;
            folder.FullPath = newPrefix;
            folder.ModifyDate = DateTime.Now;
            _db.Entry(folder).State = EntityState.Modified;

            foreach (var child in descendants)
            {
                child.FullPath = newPathMap.TryGetValue(child.FolderCode, out var p) ? p : child.FullPath;
                child.ModifyDate = DateTime.Now;
                _db.Entry(child).State = EntityState.Modified;
            }

            foreach (var file in files)
            {
                var parentOldPath = GetParentOldPath(file, folder.FolderCode, oldPathMap, oldPrefix);
                var parentNewPath = GetParentNewPath(file, folder.FolderCode, newPathMap, newPrefix);
                file.FullPath = ReplaceStorageDirSegment(file.FullPath, parentOldPath, parentNewPath);
                file.StoragePath = ReplaceStorageDirSegment(file.StoragePath, parentOldPath, parentNewPath);
                file.ConvertedStoragePath = ReplaceStorageDirSegment(file.ConvertedStoragePath, parentOldPath, parentNewPath);
                file.ModifyDate = DateTime.Now;
                _db.Entry(file).State = EntityState.Modified;
            }

            await _db.SaveChangesAsync();

            return true;
        }

        /// <summary>删除文件夹 — 递归删除所有子文件夹和文件（数据库软删除 + MinIO 物理删除）</summary>
        public async Task<(int foldersDeleted, int filesDeleted)> DeleteFolderAsync(string folderCode, bool dryRun = false)
        {
            int foldersDeleted = 0;
            int filesDeleted = 0;

            // 先按存储路径前缀整体清理 MinIO（覆盖 .converted 与历史孤儿对象）
            // 注意：名称路径（FullPath）与存储路径（StoragePath，含机构/标准/阶段前缀）不一致，
            //       必须用 StoragePath 反推 MinIO 前缀。
            if (!dryRun)
            {
                var root = await _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefaultAsync(x => x.FolderCode == folderCode);
                if (root != null)
                {
                    var namePath = await BuildFolderPathAsync(root);
                    if (!string.IsNullOrEmpty(namePath))
                    {
                        try
                        {
                            // 取该文件夹下任意文件的 StoragePath，反推出 MinIO 存储前缀
                            var anyFile = await _db.Set<StandardDirectoryFile>()
                                .FirstOrDefaultAsync(x => x.FolderCode == folderCode && x.Enable == true);
                            var storagePrefix = BuildStoragePrefix(namePath, anyFile?.StoragePath);
                            if (!string.IsNullOrEmpty(storagePrefix))
                                await _minioHelper.DeletePrefixAsync(storagePrefix);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DeleteFolder] MinIO 前缀清理失败: {ex.Message}");
                        }
                    }
                }
            }

            async Task DeleteRecursive(string code)
            {
                // 1. 先删除子文件夹
                var children = await _db.Set<StandardDirectoryFolder>()
                    .AsTracking()
                    .Where(x => x.ParentCode == code && x.Enable == true)
                    .ToListAsync();
                foreach (var child in children)
                {
                    await DeleteRecursive(child.FolderCode);
                    foldersDeleted++;
                }

                // 2. 删除该文件夹下的文件（MinIO 对象 + 软删除记录）
                var files = await _db.Set<StandardDirectoryFile>()
                    .AsTracking()
                    .Where(x => x.FolderCode == code && x.Enable == true)
                    .ToListAsync();
                foreach (var file in files)
                {
                    if (!dryRun)
                    {
                        // 删除 MinIO 对象（原文件 + 转换产物）
                        if (!string.IsNullOrEmpty(file.StoragePath))
                        {
                            try { await _minioHelper.DeleteAsync(file.StoragePath); }
                            catch (Exception ex) { Console.WriteLine($"[DeleteFolder] MinIO删除失败: {ex.Message}"); }
                        }
                        if (!string.IsNullOrEmpty(file.ConvertedStoragePath))
                        {
                            try { await _minioHelper.DeleteAsync(file.ConvertedStoragePath); }
                            catch (Exception ex) { Console.WriteLine($"[DeleteFolder] MinIO删除转换文件失败: {ex.Message}"); }
                        }

                        // 软删除文件记录
                        file.Enable = false;
                        file.DeleteID = 1; // TODO: 从UserContext获取
                        file.Deleter = "system";
                        file.DeleteTime = DateTime.Now;
                        file.Status = "archived";
                    }
                    filesDeleted++;
                }

                // 3. 软删除文件夹本身
                var folder = await _db.Set<StandardDirectoryFolder>()
                    .AsTracking()
                    .FirstOrDefaultAsync(x => x.FolderCode == code);
                if (folder != null && !dryRun)
                {
                    folder.Enable = false;
                    folder.DeleteID = 1;
                    folder.Deleter = "system";
                    folder.DeleteTime = DateTime.Now;
                    folder.Status = "archived";
                }
            }

            await DeleteRecursive(folderCode);

            if (!dryRun)
                await _db.SaveChangesAsync();

            return (foldersDeleted, filesDeleted);
        }

        /// <summary>获取文件夹下的所有后代文件夹编码（递归）</summary>
        public async Task<List<string>> GetAllDescendantFolderCodesAsync(string folderCode)
        {
            var result = new List<string>();
            await CollectDescendants(folderCode, result);
            return result;
        }

        /// <summary>获取文件夹下的所有后代文件编码（递归）</summary>
        public async Task<List<string>> GetAllDescendantFileCodesAsync(string folderCode)
        {
            var result = new List<string>();
            var folders = new List<string> { folderCode };
            folders.AddRange(await GetAllDescendantFolderCodesAsync(folderCode));

            foreach (var fc in folders)
            {
                var files = await _db.Set<StandardDirectoryFile>()
                    .Where(x => x.FolderCode == fc && x.Enable == true && x.IsValid == true)
                    .Select(x => x.FileCode)
                    .ToListAsync();
                result.AddRange(files);
            }

            return result;
        }

        #region Private Helpers

        /// <summary>广度优先收集所有后代文件夹实体</summary>
        private async Task<List<StandardDirectoryFolder>> GetAllDescendantFoldersAsync(string folderCode)
        {
            var result = new List<StandardDirectoryFolder>();
            var queue = new Queue<string>();
            queue.Enqueue(folderCode);

            while (queue.Count > 0)
            {
                var code = queue.Dequeue();
                var children = await _db.Set<StandardDirectoryFolder>()
                    .Where(x => x.ParentCode == code && x.Enable == true)
                    .ToListAsync();
                foreach (var child in children)
                {
                    result.Add(child);
                    queue.Enqueue(child.FolderCode);
                }
            }

            return result;
        }

        /// <summary>收集文件夹自身 + 所有后代文件夹下的文件实体</summary>
        private async Task<List<StandardDirectoryFile>> GetAllDescendantFilesAsync(string folderCode)
        {
            var folderCodes = new List<string> { folderCode };
            folderCodes.AddRange((await GetAllDescendantFoldersAsync(folderCode)).Select(x => x.FolderCode));

            var files = new List<StandardDirectoryFile>();
            foreach (var fc in folderCodes)
            {
                // 只收集有效文件（IsValid=true）：上传预创建的孤儿记录不参与级联
                files.AddRange(await _db.Set<StandardDirectoryFile>()
                    .Where(x => x.FolderCode == fc && x.Enable == true && x.IsValid == true)
                    .ToListAsync());
            }

            return files;
        }

        /// <summary>计算文件夹的名称路径（FullPath）。已有 FullPath 直接用，否则沿 ParentCode 向上拼名称。</summary>
        private async Task<string> BuildFolderPathAsync(StandardDirectoryFolder folder)
        {
            if (!string.IsNullOrEmpty(folder.FullPath))
                return folder.FullPath.Trim('/');
            return await BuildFolderPathByCodeAsync(folder.FolderCode);
        }

        /// <summary>按 FolderCode 沿父链拼接名称路径（如：Python更新测试/内审记录）</summary>
        private async Task<string> BuildFolderPathByCodeAsync(string folderCode)
        {
            if (string.IsNullOrEmpty(folderCode))
                return "";

            var parts = new List<string>();
            var code = folderCode;
            var guard = 0;
            while (!string.IsNullOrEmpty(code) && guard++ < 100)
            {
                var f = await _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefaultAsync(x => x.FolderCode == code && x.Enable == true);
                if (f == null)
                    break;
                parts.Insert(0, f.FolderName);
                code = f.ParentCode;
            }

            return string.Join("/", parts);
        }

        /// <summary>新建文件夹时计算 FullPath（基于父文件夹路径 + 自身名称）</summary>
        private async Task<string> BuildNewFolderFullPathAsync(StandardDirectoryFolder folder)
        {
            if (string.IsNullOrEmpty(folder.ParentCode))
                return folder.FolderName;

            var parent = await _db.Set<StandardDirectoryFolder>()
                .FirstOrDefaultAsync(x => x.FolderCode == folder.ParentCode && x.Enable == true);
            var parentPath = parent == null ? "" : await BuildFolderPathAsync(parent);

            return string.IsNullOrEmpty(parentPath)
                ? folder.FolderName
                : $"{parentPath.Trim('/')}/{folder.FolderName}";
        }

        /// <summary>
        /// 获取文件所在文件夹的旧名称路径段（FullPath），基于旧路径映射（重命名前捕获）。
        /// </summary>
        private static string GetParentOldPath(StandardDirectoryFile file,
            string renamedFolderCode, Dictionary<string, string> oldPathMap, string oldPrefix)
        {
            if (file.FolderCode == renamedFolderCode)
                return oldPrefix;
            return oldPathMap.TryGetValue(file.FolderCode, out var p) ? p : oldPrefix;
        }

        /// <summary>
        /// 获取文件所在文件夹的新名称路径段（FullPath），基于新路径映射。
        /// </summary>
        private static string GetParentNewPath(StandardDirectoryFile file,
            string renamedFolderCode, Dictionary<string, string> newPathMap, string newPrefix)
        {
            if (file.FolderCode == renamedFolderCode)
                return newPrefix;
            return newPathMap.TryGetValue(file.FolderCode, out var p) ? p : newPrefix;
        }

        /// <summary>
        /// 在完整路径（FullPath 或 StoragePath）中，将"名称路径段"整段替换为新段。
        /// 匹配规则：段前必须是路径开头或 '/'，段后必须是 '/' 或结尾（防止误伤名称包含相同子串的路径）。
        /// 不匹配时保持原样，避免破坏历史脏数据。
        /// </summary>
        private static string ReplaceStorageDirSegment(string full, string oldSegment, string newSegment)
        {
            if (string.IsNullOrEmpty(full) || string.IsNullOrEmpty(oldSegment) || string.IsNullOrEmpty(newSegment))
                return full;
            if (oldSegment == newSegment)
                return full;

            var p = full.TrimStart('/');
            var idx = FindSegmentIndex(p, oldSegment);
            if (idx < 0)
                return full;

            var replaced = p.Substring(0, idx) + newSegment + p.Substring(idx + oldSegment.Length);
            return full.StartsWith("/") ? "/" + replaced : replaced;
        }

        /// <summary>
        /// 由名称路径 + 任意文件的存储路径反推出 MinIO 存储前缀。
        /// 例如：namePath="E2E测试-根/E2E子目录A"，storagePath="/CB001/ISO134852016/STAGE01/E2E测试-根/E2E子目录A/e2e.txt"
        ///       → 返回 "/CB001/ISO134852016/STAGE01/E2E测试-根/E2E子目录A"（找到名称路径段，取其前缀）。
        /// </summary>
        private static string BuildStoragePrefix(string namePath, string storagePath)
        {
            if (string.IsNullOrEmpty(namePath) || string.IsNullOrEmpty(storagePath))
                return "";

            var p = storagePath.TrimStart('/');
            var idx = FindSegmentIndex(p, namePath);
            if (idx < 0)
                return "";

            var prefix = p.Substring(0, idx + namePath.Length);
            return prefix.StartsWith("/") ? prefix : "/" + prefix;
        }

        /// <summary>在路径中查找完整段（段前为开头或'/'，段后为'/'或结尾）</summary>
        private static int FindSegmentIndex(string path, string segment)
        {
            var start = 0;
            while (start <= path.Length - segment.Length)
            {
                var idx = path.IndexOf(segment, start, StringComparison.Ordinal);
                if (idx < 0)
                    return -1;

                var beforeOk = idx == 0 || path[idx - 1] == '/';
                var afterIdx = idx + segment.Length;
                var afterOk = afterIdx >= path.Length || path[afterIdx] == '/';

                if (beforeOk && afterOk)
                    return idx;
                start = idx + 1;
            }
            return -1;
        }

        private async Task CollectDescendants(string parentCode, List<string> result)
        {
            var children = await _db.Set<StandardDirectoryFolder>()
                .Where(x => x.ParentCode == parentCode && x.Enable == true)
                .ToListAsync();
            foreach (var child in children)
            {
                result.Add(child.FolderCode);
                await CollectDescendants(child.FolderCode, result);
            }
        }

        /// <summary>
        /// 计算同一目录、同一层级下已用过的最大序号（含软删除记录，防止删除后序号复用导致编码冲突）
        /// </summary>
        private int GetMaxSequence(string directoryCode, int depth)
        {
            var folders = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode
                         && x.Depth == depth)
                .ToList();
            if (folders.Count == 0)
                return 0;

            int maxSeq = 0;
            foreach (var folder in folders)
            {
                var parts = folder.FolderCode.Split('|');
                var seqStr = parts.Length > 0 ? parts[parts.Length - 1] : "S001";
                var numStr = seqStr.Replace("S", "");
                if (int.TryParse(numStr, out int value) && value > maxSeq)
                    maxSeq = value;
            }

            return maxSeq;
        }

        #endregion
    }
}
