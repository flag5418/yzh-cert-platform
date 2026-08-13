using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// MinIO 操作帮助服务
    /// 封装所有MinIO相关操作：上传、下载、删除、重命名（移动）、列出文件夹
    /// </summary>
    public interface IMinIOHelper
    {
        string BucketName { get; }

        /// <summary>上传文件到MinIO</summary>
        Task UploadAsync(string objectName, Stream stream, long size, string contentType = null);

        /// <summary>从MinIO下载文件</summary>
        Task<(Stream stream, string contentType)> DownloadAsync(string objectName);

        /// <summary>删除MinIO对象</summary>
        Task DeleteAsync(string objectName);

        /// <summary>重命名/移动MinIO对象（Copy+Delete）</summary>
        Task RenameAsync(string oldObjectName, string newObjectName);

        /// <summary>列出指定前缀下的所有对象（递归）</summary>
        Task<List<string>> ListObjectsAsync(string prefix);

        /// <summary>递归删除指定前缀下的所有对象（用于删除文件夹时清理整棵存储树）</summary>
        Task DeletePrefixAsync(string prefix);

        /// <summary>检查对象是否存在</summary>
        Task<bool> ExistsAsync(string objectName);
    }
}
