using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YZH.Core.EFDbContext;

namespace YZH.Core.Web.Controllers;

/// <summary>分页请求参数（前端使用 page/rows）</summary>
public class VolPageRequest
{
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 20;
    public string? Sort { get; set; }
    public string? Order { get; set; }
}

/// <summary>ISO 标准条款 - 兼容 Vol API 路径</summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ISOClauseController : ControllerBase
{
    private readonly VOLContext _db;

    public ISOClauseController(VOLContext db)
    {
        _db = db;
    }

    /// <summary>分页查询条款列表（Vol 格式）</summary>
    [HttpPost("getPageData")]
    public async Task<IActionResult> GetPageData([FromBody] VolPageRequest req)
    {
        var query = _db.Set<YZH.Entity.Admin.Platform.Cert.ISOClause>()
            .OrderBy(c => c.SortOrder)
            .AsQueryable();

        var total = await query.CountAsync();

        var items = await query
            .Skip((req.Page - 1) * req.Rows)
            .Take(req.Rows)
            .Select(c => new
            {
                id = c.Id,
                standardCode = c.StandardCode,
                parentCode = c.ParentCode,
                clauseNumber = c.ClauseNumber,
                title = c.Title,
                description = c.Description,
                sortOrder = c.SortOrder,
                createDate = c.CreateDate
            })
            .ToListAsync();

        return Ok(new { status = 0, msg = "ok", rows = items, total });
    }

    /// <summary>新增条款</summary>
    [HttpPost("add")]
    [AllowAnonymous]
    public async Task<IActionResult> Add([FromBody] YZH.Entity.Admin.Platform.Cert.ISOClause entity)
    {
        entity.CreateDate = DateTime.Now;
        entity.Enable = true;
        _db.Set<YZH.Entity.Admin.Platform.Cert.ISOClause>().Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { status = 0, msg = "新增成功", data = entity });
    }

    /// <summary>更新条款</summary>
    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] YZH.Entity.Admin.Platform.Cert.ISOClause entity)
    {
        var existing = await _db.Set<YZH.Entity.Admin.Platform.Cert.ISOClause>()
            .FirstOrDefaultAsync(c => c.Id == entity.Id);
        if (existing == null)
            return Ok(new { status = 1, msg = "条款不存在" });

        existing.StandardCode = entity.StandardCode ?? existing.StandardCode;
        existing.ParentCode = entity.ParentCode ?? existing.ParentCode;
        existing.ClauseNumber = entity.ClauseNumber ?? existing.ClauseNumber;
        existing.Title = entity.Title ?? existing.Title;
        existing.Description = entity.Description ?? existing.Description;
        existing.SortOrder = entity.SortOrder;

        await _db.SaveChangesAsync();
        return Ok(new { status = 0, msg = "更新成功" });
    }

    /// <summary>删除条款</summary>
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] string ids)
    {
        if (string.IsNullOrEmpty(ids))
            return Ok(new { status = 1, msg = "请指定要删除的ID" });

        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = await _db.Set<YZH.Entity.Admin.Platform.Cert.ISOClause>()
            .Where(c => idList.Contains(c.Id)).ToListAsync();
        _db.Set<YZH.Entity.Admin.Platform.Cert.ISOClause>().RemoveRange(items);
        await _db.SaveChangesAsync();
        return Ok(new { status = 0, msg = $"已删除 {items.Count} 条记录" });
    }
}
