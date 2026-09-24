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
///     操作日志控制器（只读，框架层底座）
///
///     路由前缀：/api/System/Log
///     数据库：sys_log
///     GET  /config          页面配置
///     POST /filter          分页查询
/// </summary>
[ApiController]
[Route("api/System/Log")]
public class SysLogController : YzhControllerBase<SysLog>
{
    public SysLogController(
        EntityService<SysLog> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    /// <summary>加载 EntityConfig 配置（Assets/EntityConfigs/System/SysLog.json）</summary>
    protected override EntityConfig LoadConfig()
        => EntityConfigHelper.GetConfig<SysLog>();

    /// <summary>日志页面只读，禁用新增/删除</summary>
    protected override ToolbarConfig GetToolbar()
    {
        return new ToolbarConfig
        {
            Add = false,
            Delete = false,
            Export = true,
            Import = false
        };
    }

    /// <summary>日志页面只读，禁用行操作按钮</summary>
    protected override RowButtonConfig GetRowButtons()
    {
        return new RowButtonConfig
        {
            Edit = false,
            Delete = false
        };
    }
}
