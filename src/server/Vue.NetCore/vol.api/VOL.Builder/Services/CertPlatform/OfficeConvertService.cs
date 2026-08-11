/*
 * Office 文档转换服务
 * 管理转换任务队列和执行转换
 */
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Dir;
using VOL.Builder.Services.CertPlatform.Converters;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// Office 文档转换服务
    /// </summary>
    public class OfficeConvertService
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
        /// 创建转换任务
        /// </summary>
        public async Task CreateConvertJobAsync(string fileCode, string sourcePath, string convertType)
        {
            // 检查是否已存在待处理的任务
            var existingJob = await _db.Set<ConvertJob>()
                .FirstOrDefaultAsync(j => j.FileCode == fileCode && j.Status == "pending");
            
            if (existingJob != null)
            {
                return; // 已存在待处理任务
            }
            
            // 生成目标路径
            var targetPath = GenerateTargetPath(sourcePath, convertType);
            
            var job = new ConvertJob
            {
                FileCode = fileCode,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                ConvertType = convertType,
                Status = "pending",
                CreateTime = DateTime.Now
            };
            
            _db.Set<ConvertJob>().Add(job);
            await _db.SaveChangesAsync();
            
            // 更新文件记录的转换状态
            var fileRecord = await _db.Set<StandardDirectoryFile>()
                .FirstOrDefaultAsync(f => f.FileCode == fileCode);
            
            if (fileRecord != null)
            {
                fileRecord.ConvertStatus = "pending";
                await _db.SaveChangesAsync();
            }
        }
        
        /// <summary>
        /// 获取下一个待处理的任务
        /// </summary>
        public async Task<ConvertJob> GetNextPendingJobAsync()
        {
            // 使用 AsTracking 确保实体被跟踪，以便后续更新
            return await _db.Set<ConvertJob>()
                .AsTracking()
                .Where(j => j.Status == "pending" && j.RetryCount < j.MaxRetryCount)
                .OrderBy(j => j.CreateTime)
                .FirstOrDefaultAsync();
        }
        
        /// <summary>
        /// 执行转换任务
        /// </summary>
        public async Task ExecuteConvertAsync(ConvertJob job, CancellationToken cancellationToken = default)
        {
            var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";
            
            try
            {
                // 更新状态为处理中
                job.Status = "processing";
                job.ProcessTime = DateTime.Now;
                await _db.SaveChangesAsync();
                
                // 更新文件记录状态
                await UpdateFileConvertStatus(job.FileCode, "converting", null, null);
                
                // 从 MinIO 下载源文件
                var sourceObjectName = job.SourcePath.TrimStart('/');
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
                
                if (job.ConvertType == "xls2xlsx")
                {
                    result = await _xlsConverter.ConvertAsync(sourceStream, targetStream);
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                }
                else if (job.ConvertType == "doc2docx")
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
                    throw new NotSupportedException($"不支持的转换类型: {job.ConvertType}");
                }
                
                if (result == null || !result.Success)
                {
                    throw new Exception(result?.Message ?? "转换失败");
                }
                
                // 上传转换后的文件到 MinIO
                targetStream.Position = 0;
                var targetObjectName = job.TargetPath.TrimStart('/');
                
                await _minioClient.PutObjectAsync(
                    new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(targetObjectName)
                        .WithStreamData(targetStream)
                        .WithObjectSize(targetStream.Length)
                        .WithContentType(contentType),
                    cancellationToken);
                
                // 更新任务状态为完成
                job.Status = "completed";
                job.CompleteTime = DateTime.Now;
                await _db.SaveChangesAsync();
                Console.WriteLine($"[OfficeConvertService] 任务状态已更新为 completed");
                
                // 更新文件记录
                await UpdateFileConvertStatus(job.FileCode, "completed", job.TargetPath, null);
                
                Console.WriteLine($"[OfficeConvertService] 转换成功: {job.FileCode} -> {job.TargetPath}");
            }
            catch (Exception ex)
            {
                // 更新任务状态为失败
                job.Status = "failed";
                job.ErrorMessage = ex.Message;
                job.RetryCount++;
                await _db.SaveChangesAsync();
                
                // 更新文件记录
                await UpdateFileConvertStatus(job.FileCode, "failed", null, ex.Message);
                
                Console.WriteLine($"[OfficeConvertService] 转换失败: {job.FileCode}, 错误: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 更新文件转换状态
        /// </summary>
        private async Task UpdateFileConvertStatus(string fileCode, string status, string convertedPath, string errorMessage)
        {
            // 使用 AsTracking 确保实体被跟踪
            var fileRecord = await _db.Set<StandardDirectoryFile>()
                .AsTracking()
                .FirstOrDefaultAsync(f => f.FileCode == fileCode);
            
            if (fileRecord != null)
            {
                fileRecord.ConvertStatus = status;
                fileRecord.ConvertedStoragePath = convertedPath;
                fileRecord.ConvertMessage = errorMessage;
                
                if (status == "completed" || status == "failed")
                {
                    fileRecord.ConvertDate = DateTime.Now;
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
