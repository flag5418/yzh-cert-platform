using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Entity.CertPlatform.Dir;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 文件夹文件管理器
    /// 封装文件夹的创建、重命名、删除等数据库+MinIO操作
    /// </summary>
    public interface IFolderFileManager
    {
        /// <summary>创建文件夹（含递归生成子路径）</summary>
        Task<StandardDirectoryFolder> CreateFolderAsync(StandardDirectoryFolder folder);

        /// <summary>重命名文件夹 — 递归更新所有子文件夹的ParentCode和存储路径</summary>
        /// <param name="oldFolderCode">原文件夹编码</param>
        /// <param name="newFolderName">新文件夹名称</param>
        /// <param name="force">强制重命名（忽略子项检查）</param>
        Task<bool> RenameFolderAsync(string oldFolderCode, string newFolderName, bool force = false);

        /// <summary>删除文件夹 — 递归删除所有子文件夹和文件（含MinIO）</summary>
        /// <param name="folderCode">文件夹编码</param>
        /// <param name="dryRun">仅检查不执行（返回将要删除的数量）</param>
        Task<(int foldersDeleted, int filesDeleted)> DeleteFolderAsync(string folderCode, bool dryRun = false);

        /// <summary>获取文件夹下的所有后代文件夹编码</summary>
        Task<List<string>> GetAllDescendantFolderCodesAsync(string folderCode);

        /// <summary>获取文件夹下的所有后代文件编码</summary>
        Task<List<string>> GetAllDescendantFileCodesAsync(string folderCode);
    }
}
