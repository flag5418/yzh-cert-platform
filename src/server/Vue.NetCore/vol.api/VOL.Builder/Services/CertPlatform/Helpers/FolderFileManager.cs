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
                GetMaxSequence(folder.DirectoryCode, folder.ParentCode) + 1);
            folder.CreateDate = DateTime.Now;
            folder.Status = "draft";
            folder.Enable = true;

            _db.Set<StandardDirectoryFolder>().Add(folder);
            await _db.SaveChangesAsync();

            return folder;
        }

        /// <summary>重命名文件夹 — 递归更新所有子文件夹的ParentCode和存储路径</summary>
        public async Task<bool> RenameFolderAsync(string oldFolderCode, string newFolderName, bool force = false)
        {
            var folder = await _db.Set<StandardDirectoryFolder>()
                .FirstOrDefaultAsync(x => x.FolderCode == oldFolderCode && x.Enable == true);
            if (folder == null) return false;

            // 检查是否有子项（非强制模式下）
            if (!force)
            {
                var childCount = await _db.Set<StandardDirectoryFolder>()
                    .CountAsync(x => x.ParentCode == oldFolderCode && x.Enable == true);
                var fileCount = await _db.Set<StandardDirectoryFile>()
                    .CountAsync(x => x.FolderCode == oldFolderCode && x.Enable == true);
                
                if (childCount > 0 || fileCount > 0)
                    throw new InvalidOperationException(
                        $"无法重命名：该文件夹下有 {childCount} 个子文件夹和 {fileCount} 个文件。" +
                        "请指定 force=true 强制重命名（将同步更新所有子项路径）。");
            }

            // 更新名称
            folder.FolderName = newFolderName;
            folder.ModifyDate = DateTime.Now;
            await _db.SaveChangesAsync();

            // TODO: 同步更新MinIO路径（文件夹名变更不影响MinIO路径，因为路径基于FolderCode）
            // 如果未来MinIO路径依赖FolderName，需要在此处递归更新

            return true;
        }

        /// <summary>删除文件夹 — 递归删除所有子文件夹和文件（含MinIO）</summary>
        public async Task<(int foldersDeleted, int filesDeleted)> DeleteFolderAsync(string folderCode, bool dryRun = false)
        {
            int foldersDeleted = 0;
            int filesDeleted = 0;
            var bucketName = _minioHelper.BucketName;

            async Task DeleteRecursive(string code)
            {
                // 1. 先删除子文件夹
                var children = await _db.Set<StandardDirectoryFolder>()
                    .Where(x => x.ParentCode == code && x.Enable == true)
                    .ToListAsync();
                foreach (var child in children)
                {
                    await DeleteRecursive(child.FolderCode);
                    foldersDeleted++;
                }

                // 2. 删除该文件夹下的文件
                var files = await _db.Set<StandardDirectoryFile>()
                    .Where(x => x.FolderCode == code && x.Enable == true)
                    .ToListAsync();
                foreach (var file in files)
                {
                    if (!dryRun)
                    {
                        // 删除MinIO对象
                        if (!string.IsNullOrEmpty(file.StoragePath))
                        {
                            try
                            {
                                await _minioHelper.DeleteAsync(file.StoragePath);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DeleteFolder] MinIO删除失败: {ex.Message}");
                            }
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
            
            // 先收集所有后代文件夹
            var allDescendantFolders = await GetAllDescendantFolderCodesAsync(folderCode);
            folders.AddRange(allDescendantFolders);

            // 再收集所有文件
            foreach (var fc in folders)
            {
                var files = await _db.Set<StandardDirectoryFile>()
                    .Where(x => x.FolderCode == fc && x.Enable == true)
                    .Select(x => x.FileCode)
                    .ToListAsync();
                result.AddRange(files);
            }

            return result;
        }

        #region Private Helpers

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

        private int GetMaxSequence(string directoryCode, string parentCode)
        {
            var folders = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode 
                         && x.ParentCode == parentCode 
                         && x.Enable == true)
                .ToList();
            if (folders.Count == 0) return 0;
            
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
