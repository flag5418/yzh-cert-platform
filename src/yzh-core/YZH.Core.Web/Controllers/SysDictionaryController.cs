using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Core.EFDbContext;
using YZH.Core.Stand.Models;
using YZH.Entity.DomainModels;

namespace YZH.Core.Web.Controllers;

/// <summary>
/// 数据字典 Controller - 提供分页数据接口
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class Sys_DictionaryController : ControllerBase
{
    private readonly VOLContext _db;

    public Sys_DictionaryController(VOLContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 分页获取字典列表
    /// POST /api/Sys_Dictionary/getPageData
    /// </summary>
    [HttpPost("getPageData")]
    public async Task<IActionResult> GetPageData([FromBody] PageRequestBody request)
    {
        var query = _db.Set<Sys_Dictionary>().AsQueryable();

        // 模糊搜索
        if (!string.IsNullOrEmpty(request.Where))
        {
            query = query.Where(x => x.DicName.Contains(request.Where) || x.DicNo.Contains(request.Where));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.OrderNo ?? 0)
            .Skip((request.Page - 1) * request.Rows)
            .Take(request.Rows)
            .Select(x => new
            {
                dict_Id = x.DicID,
                dictName = x.DicName,
                dictCode = x.DicNo,
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

public class PageRequestBody
{
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 20;
    public string? Where { get; set; }
}

/// <summary>
/// 字典实体（ Sys_Dictionary 表）
/// </summary>
[Table("Sys_Dictionary")]
public class Sys_Dictionary : BaseEntity
{
    [Key]
    [Column("Dic_ID")]
    public int DicID { get; set; }

    [Column("DicName")]
    public string DicName { get; set; } = string.Empty;

    [Column("DicNo")]
    public string DicNo { get; set; } = string.Empty;

    [Column("Enable")]
    public byte? Enable { get; set; }

    [Column("OrderNo")]
    public int? OrderNo { get; set; }

    [Column("CreateDate")]
    public DateTime? CreateDate { get; set; }
}
