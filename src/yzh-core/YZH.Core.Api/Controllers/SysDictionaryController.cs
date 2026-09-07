using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Entity.DomainModels;

namespace YZH.Core.Api.Controllers;

/// <summary>
///     字典管理控制器（新架构版）
///     
///     继承 YzhControllerBase 获得：
///     - 标准 CRUD（Add/Update/Delete/GetPage/GetConfig）
///     - 原子方法（AddCore/UpdateCore/DeleteCore/GetPageCore）
///     - 生命周期钩子
///     - 行操作注册
///     
///     路由：api/SysDictionary
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SysDictionaryController : YzhControllerBase<Sys_Dictionary>
{
    private readonly IDictService _dictService;

    public SysDictionaryController(
        EntityService<Sys_Dictionary> entityService,
        IUserContext userContext,
        IDictService dictService)
        : base(entityService, userContext)
    {
        _dictService = dictService;
        // 注册行操作
        RegisterRowAction("Enable", EnableDict);
        RegisterRowAction("Disable", DisableDict);
        RegisterRowAction("GetItems", GetDictItemsAsync);
    }

    #region 查询钩子

    /// <summary>查询后处理 - 填充字典项数量</summary>
    protected override void OnQueried(PagedResult<Sys_Dictionary> result)
    {
        // TODO: 可在结果中填充字典项数量等统计信息
        base.OnQueried(result);
    }

    /// <summary>
    ///     获取字典项列表（特殊查询，不走分页）
    ///     GET api/SysDictionary/items/{dictCode}
    /// </summary>
    [HttpGet("items/{dictCode}")]
    public virtual Result<List<DictItem>> GetItems(string dictCode)
    {
        try
        {
            var result = LoadDictItems(dictCode);
            return Result<List<DictItem>>.Ok(result);
        }
        catch (Exception ex)
        {
            return Result<List<DictItem>>.Fail($"获取字典项失败：{ex.Message}");
        }
    }

    #endregion

    #region 新增钩子

    /// <summary>新增前处理 - 校验编码唯一性</summary>
    protected override async Task OnBeforeAdd(Sys_Dictionary entity)
    {
        // 校验编码唯一性
        var result = await Entity.ExistsByCodeAsync(entity.DicNo);
        if (result.Data == true)
        {
            throw new InvalidOperationException($"字典编码 {entity.DicNo} 已存在");
        }

        // 设置默认启用
        entity.Enable = 1;
    }

    #endregion

    #region 修改钩子

    /// <summary>修改前处理</summary>
    protected override async Task OnBeforeUpdate(Sys_Dictionary entity)
    {
        // 校验编码唯一性（排除自身）
        var result = await Entity.ExistsByCodeAsync(entity.DicNo);
        if (result.Data == true)
        {
            throw new InvalidOperationException($"字典编码 {entity.DicNo} 已存在");
        }
    }

    #endregion

    #region 行操作

    /// <summary>启用字典（POST api/SysDictionary/action/Enable）</summary>
    private async Task<Result<ApiResponse<object?>>> EnableDict(Sys_Dictionary entity)
    {
        // 使用 DicNo 作为业务键查询（Sys_Dictionary 无 Code 属性）
        var result = await Entity.GetOne(e => e.DicNo == entity.DicNo);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("字典不存在");

        var dict = result.Data;
        dict.Enable = 1;
        var updateResult = await Entity.Update(dict, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        // 清除字典缓存
        _dictService.InvalidateDict(ControllerName, dict.DicNo);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已启用该字典"));
    }

    /// <summary>禁用字典（POST api/SysDictionary/action/Disable）</summary>
    private async Task<Result<ApiResponse<object?>>> DisableDict(Sys_Dictionary entity)
    {
        // 使用 DicNo 作为业务键查询（Sys_Dictionary 无 Code 属性）
        var result = await Entity.GetOne(e => e.DicNo == entity.DicNo);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("字典不存在");

        var dict = result.Data;
        dict.Enable = 0;
        var updateResult = await Entity.Update(dict, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        // 清除字典缓存
        _dictService.InvalidateDict(ControllerName, dict.DicNo);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已禁用该字典"));
    }

    /// <summary>获取字典项（POST api/SysDictionary/action/GetItems）</summary>
    private async Task<Result<ApiResponse<object?>>> GetDictItemsAsync(Sys_Dictionary entity)
    {
        var result = GetItems(entity.DicNo);
        return result.Map(items => ApiResponse<object?>.Ok(items));
    }

    #endregion

    #region 私有方法

    /// <summary>加载字典项</summary>
    private List<DictItem> LoadDictItems(string dictCode)
    {
        // TODO: 从数据库加载字典项
        // 这里暂时返回空列表，实际应从 Sys_DictionaryList 表加载
        return new List<DictItem>();
    }

    #endregion
}
