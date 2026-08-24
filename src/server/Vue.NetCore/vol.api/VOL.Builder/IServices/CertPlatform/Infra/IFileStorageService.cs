using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VOL.Entity.CertPlatform.Dir;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 文件存储服务
    /// 封装文件的上传、下载、删除、重命名等数据库+MinIO操作
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>上传文件到指定文件夹</summary>
        Task<StandardDirectoryFile> UploadFileAsync(string folderCode, IFormFile file, string orgCode = "CB001");

        /// <summary>下载文件</summary>
        Task<(Stream stream, string contentType, string fileName)> DownloadFileAsync(string fileCode);

        /// <summary>删除文件（数据库软删除 + MinIO物理删除）</summary>
        Task<bool> DeleteFileAsync(string fileCode);

        /// <summary>重命名文件 — 同步更新MinIO路径</summary>
        Task<bool> RenameFileAsync(string fileCode, string newFileName);

        /// <summary>替换文件（上传新文件并删除旧文件）</summary>
        Task<StandardDirectoryFile> ReplaceFileAsync(string fileCode, IFormFile newFile);
    }
}
