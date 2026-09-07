using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YZH.Core.EFDbContext;
using YZH.Core.Stand.Models;
using YZH.Entity.DomainModels;

namespace YZH.Core.Web.Controllers;

/// <summary>
/// 菜单管理 Controller - 提供分页数据接口
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class Sys_MenuController : ControllerBase
{
    private readonly VOLContext _db;

    public Sys_MenuController(VOLContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 分页获取菜单列表
    /// POST /api/Sys_Menu/getPageData
    /// </summary>
    [HttpPost("getPageData")]
    public async Task<IActionResult> GetPageData([FromBody] PageRequestBody request)
    {
        var query = _db.Set<Sys_Menu>().AsQueryable();

        // 模糊搜索
        if (!string.IsNullOrEmpty(request.Where))
        {
            query = query.Where(x => x.MenuName.Contains(request.Where));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.ParentId).ThenBy(x => x.OrderNo ?? 0)
            .Skip((request.Page - 1) * request.Rows)
            .Take(request.Rows)
            .Select(x => new
            {
                menu_Id = x.Menu_Id,
                menuName = x.MenuName,
                menuUrl = x.Url,
                menuIcon = x.Icon,
                parentId = x.ParentId,
                sort = x.OrderNo,
                enable = x.Enable,
                createDate = x.CreateDate
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            data = items,
            total,
            page = request.Page,
            rows = request.Rows
        }));
    }
}
