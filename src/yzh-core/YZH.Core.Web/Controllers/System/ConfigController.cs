
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     系统参数配置控制器（框架层底座）
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
public class ConfigController : YzhControllerBase<SysConfig>
{
    public ConfigController(
        EntityService<SysConfig> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    /// <summary>加载 EntityConfig 配置（Assets/EntityConfigs/System/SysConfig.json）</summary>
    protected override EntityConfig LoadConfig()
        => EntityConfigHelper.GetConfig<SysConfig>();

    /// <summary>新增前校验：配置键唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        SysConfig entity)
    {
        var exists = await Entity.ExistsAsync(e => e.ConfigKey == entity.ConfigKey);
        if (exists.Data)
            return (false, $"配置键【{entity.ConfigKey}】已存在");
        return (true, null);
    }

    /// <summary>修改前校验：准则 A（Code 定位）+ 配置键唯一（排除自身）+ 只读参数禁止修改</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        SysConfig entity)
    {
        // 准则 A：更新必须带业务键 Code，空 → 响亮失败（禁止回退 Id / ConfigKey 分流）
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        var existing = await Entity.GetByCode(entity.Code);
        if (existing.Data == null)
            return (false, "记录不存在");

        if (existing.Data.IsReadonly == 1)
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
