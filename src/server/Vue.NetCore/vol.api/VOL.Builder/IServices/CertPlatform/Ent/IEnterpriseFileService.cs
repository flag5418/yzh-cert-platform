using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VOL.Core.Utilities;
using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 企业文件上传服务接口
    /// 职责：文件上传、查询、删除、版本管理、格式转换触发、自动提取
    /// </summary>
    public interface IEnterpriseFileService : IDependency
    {
        /// <summary>
        /// 上传企业文件
        /// </summary>
        /// <param name="enterpriseCode">企业编码</param>
        /// <param name="folderCode">文件夹编码</param>
        /// <param name="standardCode">标准编码</param>
        /// <param name="phaseCode">阶段编码</param>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="standardFileCode">标准文件编码（关联 cert_file_requirement.code，标记企业文件对应的标准文件模板）</param>
        /// <param name="fileName">文件名</param>
        /// <param name="stream">文件流</param>
        /// <param name="fileSize">文件大小</param>
        Task<WebResponseContent> UploadAsync(string enterpriseCode, string folderCode,
            string standardCode, string phaseCode, string folderPath,
            string standardFileCode,
            string fileName, Stream stream, long fileSize);

        /// <summary>
        /// 获取企业文档目录树
        /// </summary>
        Task<List<object>> GetDocumentTreeAsync(string enterpriseCode, string phaseCode);

        /// <summary>
        /// 获取文件列表（按文件夹）
        /// </summary>
        Task<(List<object> items, int total)> GetFileListAsync(string folderCode, int page, int rows);

        /// <summary>
        /// 删除文件（软删除）
        /// </summary>
        Task<WebResponseContent> DeleteFileAsync(string fileCode);

        /// <summary>
        /// 获取文件版本历史
        /// </summary>
        Task<List<object>> GetFileVersionsAsync(string fileCode);

        /// <summary>
        /// 触发文件转换（.doc→.docx, .xls→.xlsx）
        /// </summary>
        Task<WebResponseContent> TriggerConversionAsync(string fileCode);

        /// <summary>
        /// 按 fileCode 获取文件信息（供 DocExtractionRuleService 调用）
        /// </summary>
        Task<(string fileName, string storagePath, string convertedStoragePath, string convertStatus, string convertMessage)> GetFileInfoAsync(string fileCode);
    }
}
