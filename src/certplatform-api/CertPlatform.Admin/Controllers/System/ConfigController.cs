extern alias SharedEntities;

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Admin.Controllers.System;

/// <summary>
///     系统参数配置控制器
///
///     路由前缀：/api/System/Config
///     数据库：cert_sys_config
///     GET  /config          页面配置（EntityConfigDto，camelCase）
///     POST /filter          分页查询
///     POST /add             新增
///     POST /update          修改
///     POST /delete          软删除
///     POST /toggle-valid    启用/禁用
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
public class ConfigController : YzhControllerBase<SharedEntities::YZH.Entity.Admin.Platform.Sys.SysConfig>
{
    public ConfigController(
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Sys.SysConfig> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    /// <summary>加载 EntityConfig 配置（从 JSON 文件）</summary>
    protected override EntityConfig LoadConfig()
        => EntityConfigHelper.GetConfig<SharedEntities::YZH.Entity.Admin.Platform.Sys.SysConfig>();

    /// <summary>新增前校验：配置键唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        SharedEntities::YZH.Entity.Admin.Platform.Sys.SysConfig entity)
    {
        var exists = await Entity.ExistsAsync(e => e.ConfigKey == entity.ConfigKey);
        if (exists.Data)
            return (false, $"配置键【{entity.ConfigKey}】已存在");
        return (true, null);
    }

    /// <summary>修改前校验：配置键唯一（排除自身）+ 只读参数禁止修改</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        SharedEntities::YZH.Entity.Admin.Platform.Sys.SysConfig entity)
    {
        var existing = await Entity.GetByCode(entity.Code);
        if (existing.Data != null && existing.Data.IsReadonly == 1)
        {
            if (existing.Data.ConfigValue != entity.ConfigValue ||
                existing.Data.ConfigType != entity.ConfigType ||
                existing.Data.Category != entity.Category ||
                existing.Data.DisplayName != entity.DisplayName)
            {
                return (false, "只读参数仅允许修改状态（启用/禁用），不允许修改值");
            }
        }

        var exists = await Entity.ExistsAsync(e =>
            e.Code != entity.Code &&
            e.ConfigKey == entity.ConfigKey);
        if (exists.Data)
            return (false, $"配置键【{entity.ConfigKey}】已存在");
        return (true, null);
    }

    /// <summary>删除前校验：只读参数禁止删除</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        foreach (var code in codes)
        {
            var existing = await Entity.GetByCode(code);
            if (existing.Data != null && existing.Data.IsReadonly == 1)
                return (false, $"只读参数【{existing.Data.ConfigKey}】不允许删除");
        }
        return (true, null);
    }
}
