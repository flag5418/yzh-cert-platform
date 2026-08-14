using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Ent;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 企业文件上传服务实现
    /// OSS 路径：/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
    /// </summary>
    public class EnterpriseFileService : IEnterpriseFileService
    {
        private readonly VOLContext _db;
        private readonly IMinIOHelper _minio;
        private readonly ICodeGeneratorService _codeGenerator;

        public EnterpriseFileService(VOLContext dbContext, IMinIOHelper minio, ICodeGeneratorService codeGenerator)
        {
            _db = dbContext;
            _minio = minio;
            _codeGenerator = codeGenerator;
        }

        /// <summary>
        /// 上传企业文件
        /// </summary>
        public async Task<WebResponseContent> UploadAsync(string enterpriseCode, string folderCode,
            string standardCode, string phaseCode, string folderPath,
            string fileName, Stream stream, long fileSize)
        {
            // 1. 查询企业信息
            var enterprise = await _db.Set<Enterprise>()
                .Where(x => x.Code == enterpriseCode && x.Enable == true)
                .Select(x => new { x.EnterpriseNo, x.OrgCode })
                .FirstOrDefaultAsync();

            if (enterprise == null)
                return new WebResponseContent().Error("企业不存在");

            // 2. 生成 OSS 路径
            var storagePath = _codeGenerator.GenerateEnterpriseDocumentPath(
                enterprise.EnterpriseNo, enterprise.OrgCode,
                standardCode, phaseCode, folderPath, fileName);

            // 3. 上传到 MinIO
            await _minio.UploadAsync(storagePath, stream, fileSize);

            // 4. 计算文件哈希
            stream.Position = 0;
            var fileHash = ComputeSHA256(stream);

            // 5. 写入数据库
            var file = new EnterpriseFile
            {
                Code = Guid.NewGuid().ToString("N"),
                EnterpriseCode = enterpriseCode,
                FolderCode = folderCode,
                FileName = fileName,
                FileType = Path.GetExtension(fileName).TrimStart('.').ToLower(),
                FileSize = fileSize,
                StoragePath = storagePath,
                FileHash = fileHash,
                CurrentVersion = 1,
                UploadStatus = "active",
                Enable = true,
                Status = "active"
            };

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            file.FillCreateInfo(userId, userName, enterprise.OrgCode);

            _db.Set<EnterpriseFile>().Add(file);

            // 6. 保存版本记录
            var version = new FileVersion
            {
                Code = Guid.NewGuid().ToString("N"),
                FileCode = file.Code,
                VersionNumber = 1,
                FileSize = fileSize,
                StoragePath = storagePath,
                FileHash = fileHash,
                UploadBy = userId
            };
            _db.Set<FileVersion>().Add(version);

            await _db.SaveChangesAsync();

            // 7. 如果是 .doc/.xls 文件，触发转换（后续实现队列任务）
            var ext = file.FileType;
            if (ext == "doc" || ext == "xls")
            {
                // TODO: 写入 yzh_queue_task 触发转换
                file.ConvertStatus = "pending";
                await _db.SaveChangesAsync();
            }

            return new WebResponseContent().OK("上传成功", file.Code);
        }

        /// <summary>
        /// 获取企业文档目录树
        /// </summary>
        public async Task<List<object>> GetDocumentTreeAsync(string enterpriseCode, string phaseCode)
        {
            var query = _db.Set<EnterpriseDocument>()
                .Where(x => x.EnterpriseCode == enterpriseCode && x.Enable == true);

            if (!string.IsNullOrEmpty(phaseCode))
                query = query.Where(x => x.PhaseCode == phaseCode);

            var docs = await query
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            return docs.Select(d => (object)new
            {
                d.Code,
                d.EnterpriseCode,
                d.PhaseCode,
                d.Scope,
                d.TemplateFolderCode,
                d.ParentCode,
                d.FolderName,
                d.SortOrder
            }).ToList();
        }

        /// <summary>
        /// 获取文件列表（按文件夹）
        /// </summary>
        public async Task<(List<object> items, int total)> GetFileListAsync(string folderCode, int page, int rows)
        {
            var query = _db.Set<EnterpriseFile>()
                .Where(x => x.FolderCode == folderCode && x.Enable == true);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.CreateDate)
                .Skip((page - 1) * rows)
                .Take(rows)
                .Select(x => new
                {
                    x.Code,
                    x.FileName,
                    x.FileType,
                    x.FileSize,
                    x.StoragePath,
                    x.ConvertedStoragePath,
                    x.ConvertStatus,
                    x.ConvertMessage,
                    x.FileHash,
                    x.CurrentVersion,
                    x.UploadStatus,
                    x.CreateDate,
                    x.Creator
                })
                .ToListAsync();

            return (items.Cast<object>().ToList(), total);
        }

        /// <summary>
        /// 删除文件（软删除）
        /// </summary>
        public async Task<WebResponseContent> DeleteFileAsync(string fileCode)
        {
            var file = await _db.Set<EnterpriseFile>()
                .FirstOrDefaultAsync(x => x.Code == fileCode);
            if (file == null)
                return new WebResponseContent().Error("文件不存在");

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            file.MarkAsDeleted(userId, userName);
            file.UploadStatus = "deleted";

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("删除成功");
        }

        /// <summary>
        /// 获取文件版本历史
        /// </summary>
        public async Task<List<object>> GetFileVersionsAsync(string fileCode)
        {
            var versions = await _db.Set<FileVersion>()
                .Where(x => x.FileCode == fileCode)
                .OrderByDescending(x => x.VersionNumber)
                .Select(x => new
                {
                    x.Code,
                    x.VersionNumber,
                    x.FileSize,
                    x.StoragePath,
                    x.FileHash,
                    x.ChangeNotes,
                    x.UploadBy
                })
                .ToListAsync();

            return versions.Cast<object>().ToList();
        }

        /// <summary>
        /// 触发文件转换（.doc→.docx, .xls→.xlsx）
        /// </summary>
        public async Task<WebResponseContent> TriggerConversionAsync(string fileCode)
        {
            var file = await _db.Set<EnterpriseFile>()
                .FirstOrDefaultAsync(x => x.Code == fileCode);
            if (file == null)
                return new WebResponseContent().Error("文件不存在");

            var ext = file.FileType?.ToLower();
            if (ext != "doc" && ext != "xls")
                return new WebResponseContent().Error("文件格式不需要转换");

            // TODO: 写入 yzh_queue_task 触发转换任务
            file.ConvertStatus = "pending";
            await _db.SaveChangesAsync();

            return new WebResponseContent().OK("转换任务已提交");
        }

        /// <summary>
        /// 按 fileCode 获取文件信息（供 DocExtractionRuleService 调用）
        /// </summary>
        public async Task<(string fileName, string storagePath, string convertedStoragePath, string convertStatus, string convertMessage)> GetFileInfoAsync(string fileCode)
        {
            var file = await _db.Set<EnterpriseFile>()
                .Where(x => x.Code == fileCode)
                .Select(x => new { x.FileName, x.StoragePath, x.ConvertedStoragePath, x.ConvertStatus, x.ConvertMessage })
                .FirstOrDefaultAsync();

            if (file == null)
                return (null, null, null, null, null);

            return (file.FileName, file.StoragePath, file.ConvertedStoragePath, file.ConvertStatus, file.ConvertMessage);
        }

        /// <summary>
        /// 计算 SHA256 哈希
        /// </summary>
        private static string ComputeSHA256(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
