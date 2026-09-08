using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.DataBase;
using YZH.Core.Stand.Models;

namespace YZH.Core.Web.Controllers;

/// <summary>分页请求参数（前端使用 page/rows）</summary>
public class VolPageRequest
{
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 20;
    public string? Sort { get; set; }
    public string? Order { get; set; }
}

/// <summary>ISO 标准条款 - 兼容 Vol API 路径（Dapper 版）</summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ISOClauseController : ControllerBase
{
    private readonly IDbOrm _db;

    public ISOClauseController(IDbOrm db)
    {
        _db = db;
    }

    /// <summary>分页查询条款列表（Vol 格式）</summary>
    [HttpPost("getPageData")]
    public async Task<IActionResult> GetPageData([FromBody] VolPageRequest req)
    {
        int offset = (req.Page - 1) * req.Rows;

        var dataResult = await _db.SqlQueryAsync(
            @"SELECT Id, StandardCode AS standardCode, ParentCode AS parentCode, 
              ClauseNumber AS clauseNumber, Title AS title, Description AS description, 
              SortOrder AS sortOrder, create_date AS createDate
              FROM cert_iso_clause 
              WHERE delete_time IS NULL
              ORDER BY SortOrder ASC
              LIMIT @Limit OFFSET @Offset",
            new { Limit = req.Rows, Offset = offset });

        var countResult = await _db.SqlScalarAsync<int>(
            "SELECT COUNT(*) FROM cert_iso_clause WHERE delete_time IS NULL");

        var items = dataResult.Success ? dataResult.Data : new List<dynamic>();
        int total = countResult.Success ? countResult.Data : 0;

        return Ok(new { status = 0, msg = "ok", rows = items, total });
    }

    /// <summary>新增条款</summary>
    [HttpPost("add")]
    [AllowAnonymous]
    public async Task<IActionResult> Add([FromBody] ISOClauseDto dto)
    {
        var result = await _db.SqlExecuteAsync(
            @"INSERT INTO cert_iso_clause (Code, StandardCode, ParentCode, ClauseNumber, Title, Description, SortOrder, create_date, enable)
              VALUES (@Code, @StandardCode, @ParentCode, @ClauseNumber, @Title, @Description, @SortOrder, @CreateDate, 1)",
            new
            {
                Code = Guid.NewGuid().ToString(),
                dto.StandardCode,
                dto.ParentCode,
                dto.ClauseNumber,
                dto.Title,
                dto.Description,
                dto.SortOrder,
                CreateDate = DateTime.Now
            });

        return Ok(new { status = 0, msg = "新增成功", data = dto });
    }

    /// <summary>更新条款</summary>
    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] ISOClauseDto dto)
    {
        var result = await _db.SqlExecuteAsync(
            @"UPDATE cert_iso_clause 
              SET StandardCode = @StandardCode, ParentCode = @ParentCode, 
                  ClauseNumber = @ClauseNumber, Title = @Title, 
                  Description = @Description, SortOrder = @SortOrder
              WHERE Id = @Id AND delete_time IS NULL",
            dto);

        if (!result.Success)
            return Ok(new { status = 1, msg = "更新失败" });

        return Ok(new { status = 0, msg = "更新成功" });
    }

    /// <summary>删除条款</summary>
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] string ids)
    {
        if (string.IsNullOrEmpty(ids))
            return Ok(new { status = 1, msg = "请指定要删除的ID" });

        var idList = ids.Split(',').Select(long.Parse).ToList();
        var paramDict = new Dictionary<string, object?>();
        var placeholders = new List<string>();

        for (int i = 0; i < idList.Count; i++)
        {
            placeholders.Add($"@id{i}");
            paramDict[$"id{i}"] = idList[i];
        }

        var sql = $"UPDATE cert_iso_clause SET delete_time = @Now WHERE Id IN ({string.Join(", ", placeholders)})";
        paramDict["Now"] = DateTime.Now;

        var result = await _db.SqlExecuteAsync(sql, paramDict);

        return Ok(new { status = 0, msg = $"已删除 {result.Data} 条记录" });
    }
}

/// <summary>ISO 条款 DTO</summary>
public class ISOClauseDto
{
    public long Id { get; set; }
    public string StandardCode { get; set; } = string.Empty;
    public string ParentCode { get; set; } = string.Empty;
    public string ClauseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
