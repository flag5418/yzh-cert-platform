using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Interfaces;
using YZH.Core.Api.Models;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
/// 同步结果
/// </summary>
public class SyncResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public string? Error { get; set; }
    public bool Success => string.IsNullOrEmpty(Error);
}

/// <summary>
/// 接口同步服务
/// 负责将扫描到的接口同步到数据库
/// </summary>
public class ApiSyncService
{
    private readonly IApiRepository _apiRepo;
    private readonly IPermissionCacheService _permissionCache;
    private readonly ILogger<ApiSyncService> _logger;
    
    public ApiSyncService(
        IApiRepository apiRepo,
        IPermissionCacheService permissionCache,
        ILogger<ApiSyncService> logger)
    {
        _apiRepo = apiRepo;
        _permissionCache = permissionCache;
        _logger = logger;
    }
    
    /// <summary>
    /// 同步接口到数据库（增量更新）
    /// </summary>
    public async Task<SyncResult> SyncAsync(List<ApiDescriptor> discoveredApis)
    {
        var result = new SyncResult();
        
        if (discoveredApis == null || !discoveredApis.Any())
        {
            _logger.LogWarning("没有发现接口，跳过同步");
            return result;
        }
        
        try
        {
            var existingApis = await _apiRepo.GetAllAsync();
            var existingDict = existingApis.ToDictionary(a => a.Code);
            var discoveredDict = discoveredApis.ToDictionary(a => a.ApiCode);
            
            // 1. 计算需要删除的接口
            var codesToDelete = existingDict.Keys.Except(discoveredDict.Keys).ToList();
            if (codesToDelete.Any())
            {
                // 先清理关联表中的无效记录
                await _apiRepo.CleanupRoleApiAsync(codesToDelete);
                await _apiRepo.CleanupUserPermissionAsync(codesToDelete);
                
                await _apiRepo.DeleteBatchAsync(codesToDelete);
                _logger.LogInformation("删除废弃接口 {Count} 个: {Codes}", 
                    codesToDelete.Count, string.Join(", ", codesToDelete.Take(5)));
                result.Deleted = codesToDelete.Count;
            }
            
            // 2. 计算需要新增的接口
            var codesToAdd = discoveredDict.Keys.Except(existingDict.Keys).ToList();
            if (codesToAdd.Any())
            {
                var newApis = discoveredApis.Where(a => codesToAdd.Contains(a.ApiCode))
                    .Select(a => MapToEntity(a, GetGroupPath(a))).ToList();
                await _apiRepo.InsertBatchAsync(newApis);
                _logger.LogInformation("新增接口 {Count} 个: {Codes}", 
                    codesToAdd.Count, string.Join(", ", codesToAdd.Take(5)));
                result.Added = codesToAdd.Count;
            }
            
            // 3. 计算需要更新的接口
            var codesToUpdate = existingDict.Keys.Intersect(discoveredDict.Keys)
                .Where(code => 
                    existingDict[code].Name != discoveredDict[code].Description
                    || existingDict[code].Author != discoveredDict[code].Author
                    || existingDict[code].GroupPath != GetGroupPath(discoveredDict[code]))
                .ToList();
            if (codesToUpdate.Any())
            {
                var updateApis = discoveredApis.Where(a => codesToUpdate.Contains(a.ApiCode))
                    .Select(a => MapToEntity(a, GetGroupPath(a))).ToList();
                await _apiRepo.UpdateBatchAsync(updateApis);
                _logger.LogInformation("更新接口 {Count} 个: {Codes}", 
                    codesToUpdate.Count, string.Join(", ", codesToUpdate.Take(5)));
                result.Updated = codesToUpdate.Count;
            }
            
            // 4. 刷新权限缓存
            await _permissionCache.RefreshAllAsync();
            
            _logger.LogInformation("接口同步完成：新增 {Added} 个，更新 {Updated} 个，删除 {Deleted} 个",
                result.Added, result.Updated, result.Deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接口同步失败");
            result.Error = ex.Message;
        }
        
        return result;
    }
    
    /// <summary>
    /// 获取接口分组路径（从 TreePath 第一段 + 第二段拼接）
    /// </summary>
    private string GetGroupPath(ApiDescriptor descriptor)
    {
        if (descriptor.TreePath.Length >= 2)
            return $"{descriptor.TreePath[0]}/{descriptor.TreePath[1]}";
        if (descriptor.TreePath.Length == 1)
            return descriptor.TreePath[0];
        return descriptor.ControllerName;
    }

    private SysApi MapToEntity(ApiDescriptor descriptor, string groupPath)
    {
        return new SysApi
        {
            Code = descriptor.ApiCode,
            Method = descriptor.Method,
            Path = descriptor.Path,
            GroupPath = groupPath,
            Name = descriptor.Description,
            Author = descriptor.Author,
            Enable = true,
            CreateDate = descriptor.CreatedAt,
            UpdateDate = descriptor.UpdatedAt
        };
    }
}
