using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using VOL.Core.Utilities;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 标准目录模板管理服务接口
    /// 职责：文件夹树 CRUD、文件要求管理、模板参考文件上传/下载/删除/改名
    /// </summary>
    public interface IDirectoryTemplateService : IDependency
    {
        /// <summary>
        /// 获取标准-阶段配置下的目录树
        /// </summary>
        Task<List<object>> GetTreeAsync(string configCode);

        /// <summary>
        /// 新增文件夹
        /// </summary>
        Task<WebResponseContent> AddFolderAsync(DirectoryTemplate entity);

        /// <summary>
        /// 修改文件夹
        /// </summary>
        Task<WebResponseContent> UpdateFolderAsync(DirectoryTemplate entity);

        /// <summary>
        /// 删除文件夹（级联删除子文件夹和文件要求）
        /// </summary>
        Task<WebResponseContent> DeleteFolderAsync(string folderCode);

        /// <summary>
        /// 获取文件夹下的文件要求列表
        /// </summary>
        Task<List<FileRequirement>> GetFileRequirementsAsync(string folderCode);

        /// <summary>
        /// 新增/修改文件要求
        /// </summary>
        Task<WebResponseContent> SaveFileRequirementAsync(FileRequirement entity);

        /// <summary>
        /// 删除文件要求
        /// </summary>
        Task<WebResponseContent> DeleteFileRequirementAsync(string requirementCode);

        // ===== 模板文件管理（标准目录参考文件） =====

        /// <summary>
        /// 上传标准目录模板文件到 MinIO
        /// OSS 路径：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// </summary>
        /// <param name="requirementCode">文件要求编码（cert_file_requirement.code）</param>
        /// <param name="fileName">文件名</param>
        /// <param name="stream">文件流</param>
        /// <param name="fileSize">文件大小</param>
        Task<WebResponseContent> UploadTemplateFileAsync(string requirementCode, string fileName, Stream stream, long fileSize);

        /// <summary>
        /// 下载标准目录模板文件
        /// </summary>
        /// <param name="requirementCode">文件要求编码</param>
        Task<(Stream stream, string fileName, string contentType)> DownloadTemplateFileAsync(string requirementCode);

        /// <summary>
        /// 删除标准目录模板文件（仅删除 MinIO 上的文件，不清除数据库记录中的路径）
        /// </summary>
        Task<WebResponseContent> DeleteTemplateFileAsync(string requirementCode);

        /// <summary>
        /// 重命名标准目录模板文件
        /// </summary>
        Task<WebResponseContent> RenameTemplateFileAsync(string requirementCode, string newFileName);
    }
}
