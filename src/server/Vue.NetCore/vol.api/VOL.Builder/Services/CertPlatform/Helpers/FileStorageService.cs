using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Dir;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 文件存储服务实现
    /// 封装文件的上传、下载、删除、重命名等数据库+MinIO操作
    /// </summary>
    public class FileStorageService : IFileStorageService
    {
        private readonly VOLContext _db;
        private readonly IMinIOHelper _minioHelper;
        private readonly ICodeGeneratorService _codeGenerator;

        public FileStorageService(VOLContext db, IMinIOHelper minioHelper, ICodeGeneratorService codeGenerator)
        {
            _db = db;
            _minioHelper = minioHelper;
            _codeGenerator = codeGenerator;
        }

        /// <summary>上传文件到指定文件夹</summary>
        public async Task<StandardDirectoryFile> UploadFileAsync(string folderCode, IFormFile file, string orgCode = "CB001")
        {
            // 获取文件夹信息
            var folder = await _db.Set<StandardDirectoryFolder>()
                .FirstOrDefaultAsync(x => x.FolderCode == folderCode && x.Enable == true);
            if (folder == null)
                throw new ArgumentException($"文件夹不存在: {folderCode}");

            // 解析目录信息
            var dirParts = folder.DirectoryCode.Replace("SDC-", "").Split('|');
            var standardCode = dirParts.Length > 0 ? dirParts[0] : "";
            var phaseCode = dirParts.Length > 1 ? dirParts[1] : "";

            // 生成文件编码
            var fileCode = _codeGenerator.GenerateFileCode(folderCode, file.FileName);

            // 生成存储路径
            var storagePath = _codeGenerator.GenerateStoragePathV2(
                orgCode, standardCode, phaseCode, 
                folderCode.Replace("|", "-"), file.FileName);

            // 上传到MinIO
            using var stream = file.OpenReadStream();
            await _minioHelper.UploadAsync(
                storagePath, 
                stream, 
                file.Length, 
                file.ContentType);

            // 创建文件记录
            var fileRecord = new StandardDirectoryFile
            {
                Code = Guid.NewGuid().ToString("N"),
                FileCode = fileCode,
                FolderCode = folderCode,
                DirectoryCode = folder.DirectoryCode,
                FileName = file.FileName,
                FileType = Path.GetExtension(file.FileName)?.TrimStart('.') ?? "file",
                StoragePath = storagePath,
                UploadStatus = "uploaded",
                IsValid = true,
                Enable = true,
                CreateDate = DateTime.Now,
            };

            _db.Set<StandardDirectoryFile>().Add(fileRecord);
            await _db.SaveChangesAsync();

            return fileRecord;
        }

        /// <summary>下载文件</summary>
        public async Task<(Stream stream, string contentType, string fileName)> DownloadFileAsync(string fileCode)
        {
            var file = await _db.Set<StandardDirectoryFile>()
                .FirstOrDefaultAsync(x => x.FileCode == fileCode && x.Enable == true);
            if (file == null || string.IsNullOrEmpty(file.StoragePath))
                throw new FileNotFoundException($"文件不存在: {fileCode}");

            var (stream, contentType) = await _minioHelper.DownloadAsync(file.StoragePath);
            return (stream, contentType, file.FileName);
        }

        /// <summary>删除文件（数据库软删除 + MinIO物理删除）</summary>
        public async Task<bool> DeleteFileAsync(string fileCode)
        {
            var file = await _db.Set<StandardDirectoryFile>()
                .FirstOrDefaultAsync(x => x.FileCode == fileCode && x.Enable == true);
            if (file == null) return false;

            // 删除MinIO对象
            if (!string.IsNullOrEmpty(file.StoragePath))
            {
                try
                {
                    await _minioHelper.DeleteAsync(file.StoragePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FileStorageService.Delete] MinIO删除失败: {ex.Message}");
                    // MinIO删除失败不阻断数据库操作
                }
            }

            // 软删除
            file.Enable = false;
            file.DeleteID = 1; // TODO: 从UserContext获取
            file.Deleter = "system";
            file.DeleteTime = DateTime.Now;
            file.Status = "archived";

            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>重命名文件 — 同步更新MinIO路径</summary>
        public async Task<bool> RenameFileAsync(string fileCode, string newFileName)
        {
            var file = await _db.Set<StandardDirectoryFile>()
                .FirstOrDefaultAsync(x => x.FileCode == fileCode && x.Enable == true);
            if (file == null) return false;

            if (file.FileName == newFileName) return true;

            // 检查是否有子项依赖（TODO: 后续检查提取规则/校验规则绑定）
            // var hasRules = await CheckFileRulesAsync(fileCode);
            // if (hasRules) throw new InvalidOperationException("该文件有绑定的规则，无法重命名");

            var oldStoragePath = file.StoragePath;
            var newStoragePath = _codeGenerator.GenerateStoragePathV2(
                "CB001", // TODO: 从配置文件获取
                "", // 从DirectoryCode解析
                "",
                file.FolderCode.Replace("|", "-"),
                newFileName);

            // 同步MinIO
            try
            {
                await _minioHelper.RenameAsync(oldStoragePath, newStoragePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStorageService.Rename] MinIO重命名失败: {ex.Message}");
                // MinIO重命名失败不阻断数据库操作
            }

            // 更新数据库
            file.FileName = newFileName;
            file.FileType = Path.GetExtension(newFileName)?.TrimStart('.') ?? "file";
            file.StoragePath = newStoragePath;
            file.ModifyDate = DateTime.Now;

            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>替换文件（上传新文件并删除旧文件）</summary>
        public async Task<StandardDirectoryFile> ReplaceFileAsync(string fileCode, IFormFile newFile)
        {
            var oldFile = await _db.Set<StandardDirectoryFile>()
                .FirstOrDefaultAsync(x => x.FileCode == fileCode && x.Enable == true);
            if (oldFile == null)
                throw new ArgumentException($"文件不存在: {fileCode}");

            // 删除旧文件（MinIO + 数据库软删除）
            if (!string.IsNullOrEmpty(oldFile.StoragePath))
            {
                try { await _minioHelper.DeleteAsync(oldFile.StoragePath); } catch { /* ignore */ }
            }
            oldFile.Enable = false;
            oldFile.Status = "archived";

            // 上传新文件
            var newFileRecord = await UploadFileAsync(oldFile.FolderCode, newFile);

            await _db.SaveChangesAsync();
            return newFileRecord;
        }

        #region Private Helpers

        private async Task<bool> CheckFileRulesAsync(string fileCode)
        {
            // TODO: 后续实现 - 检查是否有提取规则/校验规则绑定到此文件
            return false;
        }

        #endregion
    }
}
