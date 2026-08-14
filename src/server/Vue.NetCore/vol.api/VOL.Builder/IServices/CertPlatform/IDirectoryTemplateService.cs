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
    /// 职责：文件夹树 CRUD、文件要求管理、模板参考文件上传
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
    }
}
