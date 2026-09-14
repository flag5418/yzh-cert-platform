extern alias SharedEntities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using SharedEntities::YZH.Entity.Admin.Platform.Cert;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 目录模板管理服务
/// 职责：管理标准目录模板（文件夹结构）
/// </summary>
public class DirectoryTemplateService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly ILogger<DirectoryTemplateService> _logger;

    public DirectoryTemplateService(
        IDbOrm db,
        IObjectStorage storage,
        ILogger<DirectoryTemplateService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// 获取目录模板树
    /// </summary>
    public async Task<List<DirectoryTemplate>> GetTreeAsync(string configCode)
    {
        var all = (await _db.GetListAsync<DirectoryTemplate>(
            x => x.ConfigCode == configCode)).Data ?? new();

        return all.Where(x => string.IsNullOrEmpty(x.ParentCode))
            .OrderBy(x => x.SortOrder).ToList();
    }

    public async Task<(bool ok, string? error, DirectoryTemplate? folder)> AddFolderAsync(
        DirectoryTemplate folder)
    {
        folder.Code = Guid.NewGuid().ToString("N");
        folder.CreateDate = DateTime.Now;
        var result = await _db.InsertAsync(folder);
        return result.Data != null ? (true, null, result.Data) : (false, "创建失败", null);
    }

    public async Task<(bool ok, string? error)> UpdateFolderAsync(DirectoryTemplate folder)
    {
        var existing = (await _db.GetOneAsync<DirectoryTemplate>(
            x => x.Code == folder.Code)).Data;
        if (existing == null) return (false, "文件夹不存在");

        existing.FolderName = folder.FolderName;
        existing.SortOrder = folder.SortOrder;
        await _db.UpdateAsync(existing);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> DeleteFolderAsync(string code)
    {
        var folder = (await _db.GetOneAsync<DirectoryTemplate>(x => x.Code == code)).Data;
        if (folder == null) return (false, "文件夹不存在");

        await _db.DeleteByCodeAsync<DirectoryTemplate>(code);
        return (true, null);
    }

    public async Task<(bool ok, string? error, string? storagePath)> UploadTemplateFileAsync(
        System.IO.Stream stream, string fileName, string configCode)
    {
        var objectName = $"templates/{configCode}/{fileName}";
        await _storage.UploadAsync(objectName, stream, stream.Length);
        return (true, null, objectName);
    }

    public async Task<(System.IO.Stream stream, string contentType)?> DownloadTemplateFileAsync(
        string storagePath)
    {
        try
        {
            return await _storage.DownloadAsync(storagePath.TrimStart('/'));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载模板文件失败: {Path}", storagePath);
            return null;
        }
    }

    public async Task<bool> DeleteTemplateFileAsync(string storagePath)
    {
        try
        {
            await _storage.DeleteAsync(storagePath.TrimStart('/'));
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> RenameTemplateFileAsync(string oldPath, string newPath)
    {
        try
        {
            await _storage.RenameAsync(oldPath.TrimStart('/'), newPath.TrimStart('/'));
            return true;
        }
        catch { return false; }
    }
}
