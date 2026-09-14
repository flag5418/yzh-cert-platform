extern alias SharedEntities;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using SharedEntities::YZH.Entity.Admin.Platform.Dir;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// Office 文件转换服务
/// 职责：doc→docx, xls→xlsx 格式转换
/// 说明：当前为骨架实现，完整转换需要 LibreOffice/NPOI 依赖
/// </summary>
public class OfficeConvertService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly CodeGeneratorService _codeGenerator;
    private readonly ILogger<OfficeConvertService> _logger;

    public OfficeConvertService(
        IDbOrm db,
        IObjectStorage storage,
        CodeGeneratorService codeGenerator,
        ILogger<OfficeConvertService> logger)
    {
        _db = db;
        _storage = storage;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    /// <summary>
    /// 执行文件转换
    /// </summary>
    public async Task<bool> ConvertAsync(FileConvertPayload payload)
    {
        _logger.LogInformation("开始转换: {FileCode} ({ConvertType})", payload.FileCode, payload.ConvertType);

        // 1. 查找文件记录
        var file = (await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.FileCode == payload.FileCode)).Data;
        if (file == null)
        {
            _logger.LogWarning("文件记录不存在: {FileCode}", payload.FileCode);
            return false;
        }

        // 2. 更新转换状态
        file.ConvertStatus = "converting";
        await _db.UpdateAsync(file);

        try
        {
            // 3. 从 MinIO 下载源文件
            var sourcePath = payload.SourcePath?.TrimStart('/') ?? "";
            var (stream, _) = await _storage.DownloadAsync(sourcePath);

            // 4. 执行转换（当前为骨架，实际需要 LibreOffice/NPOI）
            using var ms = new System.IO.MemoryStream();
            await stream.CopyToAsync(ms);
            var convertedBytes = await PerformConversion(ms.ToArray(), payload.ConvertType);

            // 5. 上传转换后文件到 MinIO
            var targetPath = payload.TargetPath?.TrimStart('/') ?? "";
            using var targetStream = new System.IO.MemoryStream(convertedBytes);
            var contentType = payload.ConvertType == "doc2docx"
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            await _storage.UploadAsync(targetPath, targetStream, convertedBytes.Length, contentType);

            // 6. 更新文件记录
            file.ConvertedStoragePath = "/" + targetPath;
            file.ConvertStatus = "completed";
            file.ConvertDate = DateTime.Now;
            file.IsValid = 1;
            await _db.UpdateAsync(file);

            _logger.LogInformation("转换完成: {FileCode}", payload.FileCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转换失败: {FileCode}", payload.FileCode);
            file.ConvertStatus = "failed";
            file.ConvertMessage = ex.Message;
            await _db.UpdateAsync(file);
            return false;
        }
    }

    /// <summary>
    /// 执行格式转换（骨架实现）
    /// 实际实现需要：
    /// - doc2docx: LibreOffice (Docker 容器 yzh-libreoffice)
    /// - xls2xlsx: NPOI (XlsToXlsxConverter)
    /// </summary>
    private async Task<byte[]> PerformConversion(byte[] sourceBytes, string convertType)
    {
        // TODO: 接入实际转换库
        // 当前返回源文件作为占位
        _logger.LogWarning("格式转换尚未实现，返回原始文件: {ConvertType}", convertType);
        return await Task.FromResult(sourceBytes);
    }
}
