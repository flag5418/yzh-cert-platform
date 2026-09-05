using Microsoft.AspNetCore.Mvc;
using YZH.Core.DataBase;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController<T> : ControllerBase where T : BaseEntity
{
    protected readonly IRepository<T> Repository;

    protected BaseController(IRepository<T> repository)
    {
        Repository = repository;
    }

    [HttpGet]
    [HttpGet("list")]
    public virtual ActionResult<ApiResponse<PagedResult<T>>> List([FromQuery] PagerOptions options)
    {
        var (items, total) = Repository.GetPage(options);
        return Ok(ApiResponse<PagedResult<T>>.Ok(new PagedResult<T>(items, total, options.Page, options.PageSize)));
    }

    [HttpGet("{id}")]
    public virtual ActionResult<ApiResponse<T>> Get(string id)
    {
        var entity = Repository.GetById(id);
        return entity != null
            ? Ok(ApiResponse<T>.Ok(entity))
            : Ok(ApiResponse<T>.Fail("数据不存在", 404));
    }

    [HttpPost]
    public virtual ActionResult<ApiResponse<T>> Create([FromBody] T entity)
    {
        var result = Repository.Insert(entity);
        return Ok(ApiResponse<T>.Ok(result, "创建成功"));
    }

    [HttpPut]
    public virtual ActionResult<ApiResponse<T>> Update([FromBody] T entity)
    {
        var result = Repository.Update(entity);
        return Ok(ApiResponse<T>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    public virtual ActionResult<ApiResponse> Delete(string id)
    {
        Repository.Delete(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpDelete("batch")]
    public virtual ActionResult<ApiResponse> DeleteBatch([FromBody] string[] ids)
    {
        Repository.DeleteBatch(ids);
        return Ok(ApiResponse.Ok($"已删除 {ids.Length} 条记录"));
    }
}
