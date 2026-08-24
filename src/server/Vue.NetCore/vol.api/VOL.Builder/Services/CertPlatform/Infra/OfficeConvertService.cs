/*
 * Office 文档转换服务（yzh 队列中心 file_convert 执行核心）
 * 由 OfficeConvertTaskExecutor 调用，负责：幂等检查 → 下载 → 转换 → 上传 → 文件状态联动
 * 任务状态机（pending/processing/completed/failed/cancelled/退避重试）由 YzhQueueManager 统一管理
 */
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Dir;
using VOL.Builder.Services.CertPlatform.Converters;

using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// Office 文档转换服务
    /// </summary>
    public class OfficeConvertService : IDependency
    {
        private readonly VOLContext _db;
        private readonly IConfiguration _configuration;
        private readonly IMinioClient _minioClient;
        private readonly XlsToXlsxConverter _xlsConverter;
        private readonly DocToDocxConverter _docConverter;
        
        public OfficeConvertService(VOLContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
            _xlsConverter = new XlsToXlsxConverter();
            _docConverter = new DocToDocxConverter();
            
            // 初始化 MinIO 客户端
            _minioClient = new MinioClient()
                .WithEndpoint(configuration["MinIO:Endpoint"] ?? "127.0.0.1:9000")
                .WithCredentials(
                    configuration["MinIO:AccessKey"] ?? "admin",
                    configuration["MinIO:SecretKey"] ?? "Yzh123456.")
                .WithSSL(false)
                .Build();
        }

        /// <summary>
        /// 执行文件转换（幂等）：成功返回 true；失败抛出异常（错误分类由执行器/队列管理器处理）
        /// </summary>
        public async Task<bool> ConvertAsync(FileConvertPayload payload, CancellationToken cancellationToken = default)
        {
            var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";

            // 幂等检查：文件已转换且目标未变 → 直接成功（至少一次投递语义，Worker 崩溃重跑不重复转换）
            var convertedRecord = await _db.Set<StandardDirectoryFile>().AsNoTracking()
                .FirstOrDefaultAsync(f => f.FileCode == payload.FileCode);
            if (convertedRecord != null && convertedRecord.ConvertStatus == "converted"
                && convertedRecord.ConvertedStoragePath == payload.TargetPath)
            {
                Console.WriteLine($"[OfficeConvertService] 幂等跳过（已转换）: {payload.FileCode}");
                return true;
            }

            // 更新文件记录状态为转换中
            await UpdateFileConvertStatus(payload.FileCode, "converting", null, null);
            
            // 从 MinIO 下载源文件
            var sourceObjectName = payload.SourcePath.TrimStart('/');
            var sourceStream = new MemoryStream();
            
            await _minioClient.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(sourceObjectName)
                    .WithCallbackStream(async (stream, ct) =>
                    {
                        await stream.CopyToAsync(sourceStream, ct);
                    }),
                cancellationToken);
            
            sourceStream.Position = 0;
            
            // 执行转换
            var targetStream = new MemoryStream();
            ConvertResult result = null;
            string contentType = "application/octet-stream";
            
            if (payload.ConvertType == "xls2xlsx")
            {
                result = await _xlsConverter.ConvertAsync(sourceStream, targetStream);
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }
            else if (payload.ConvertType == "doc2docx")
            {
                // 检查 LibreOffice 是否可用
                if (!_docConverter.IsAvailable())
                {
                    throw new InvalidOperationException("LibreOffice 不可用，无法转换 DOC 文件");
                }
                
                result = await _docConverter.ConvertAsync(sourceStream, targetStream);
                contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }
            else
            {
                throw new NotSupportedException($"不支持的转换类型: {payload.ConvertType}");
            }
            
            if (result == null || !result.Success)
            {
                throw new Exception(result?.Message ?? "转换失败");
            }
            
            // 上传转换后的文件到 MinIO
            targetStream.Position = 0;
            var targetObjectName = payload.TargetPath.TrimStart('/');
            
            await _minioClient.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(targetObjectName)
                    .WithStreamData(targetStream)
                    .WithObjectSize(targetStream.Length)
                    .WithContentType(contentType),
                cancellationToken);
            
            // 更新文件记录：转换完成 + 恢复可见
            await UpdateFileConvertStatus(payload.FileCode, "completed", payload.TargetPath, null);
            
            Console.WriteLine($"[OfficeConvertService] 转换成功: {payload.FileCode} -> {payload.TargetPath}");
            return true;
        }

        /// <summary>
        /// 更新文件转换状态（仅 completed 恢复 IsValid；失败/重试状态由 OfficeConvertTaskExecutor 统一联动）
        /// </summary>
        private async Task UpdateFileConvertStatus(string fileCode, string status, string convertedPath, string errorMessage)
        {
            var fileRecord = await _db.Set<StandardDirectoryFile>()
                .AsTracking()
                .FirstOrDefaultAsync(f => f.FileCode == fileCode);
            
            if (fileRecord != null)
            {
                fileRecord.ConvertStatus = status;
                fileRecord.ConvertedStoragePath = convertedPath;
                fileRecord.ConvertMessage = errorMessage;
                
                if (status == "completed")
                {
                    fileRecord.ConvertDate = DateTime.Now;
                    // 转换成功：恢复文件有效，文档提取规则页可见
                    fileRecord.IsValid = true;
                }
                
                await _db.SaveChangesAsync();
                Console.WriteLine($"[OfficeConvertService] 文件记录状态已更新: {fileCode} -> {status}");
            }
            else
            {
                Console.WriteLine($"[OfficeConvertService] 警告: 找不到文件记录: {fileCode}");
            }
        }

        /// <summary>
        /// 生成目标文件路径（公开方法）
        /// </summary>
        public string GenerateTargetPathPublic(string sourcePath, string convertType)
        {
            return GenerateTargetPath(sourcePath, convertType);
        }

        /// <summary>
        /// 生成目标文件路径
        /// </summary>
        private string GenerateTargetPath(string sourcePath, string convertType)
        {
            // 在同级目录下创建 .converted 隐藏目录存放转换后的文件
            var directory = Path.GetDirectoryName(sourcePath);
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            
            var targetFileName = convertType switch
            {
                "xls2xlsx" => $"{fileName}.xlsx",
                "doc2docx" => $"{fileName}.docx",
                _ => fileName
            };
            
            return $"{directory}/.converted/{targetFileName}";
        }
    }
}
