using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Tree;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Api.Controllers;

/// <summary>
///     树形结构 Controller 基类（V2.1 视图驱动版）
///     
///     定位：统一单表/多表树的薄基类
///     - 同构树：部门、菜单、数据字典分类等（单表自关联）
///     - 异构树：机构→标准→阶段（通过 MySQL VIEW 统一）
///     
///     核心能力：
///     1. 树形数据获取（扁平列表，前端自动组装）
///     2. 懒加载子节点（getChildren）
///     3. isLeaf 批量预判（一次 SQL GROUP BY，杜绝 N+1）
///     4. 树节点增删改（含级联删除）
///     5. 移动节点（专用接口，含防环/防深度校验）
///     
///     泛型约束：
///     - T 必须是 TreeNodeViewBase 子类（视图实体）
///     
///     路由约定：
///     - GET  api/{controller}/tree        获取树（扁平列表）
///     - POST api/{controller}/getChildren  懒加载子节点
///     - POST api/{controller}/add         新增节点
///     - POST api/{controller}/update      修改节点
///     - POST api/{controller}/move        移动节点
///     - POST api/{controller}/delete      批量删除（含子树）
///     - POST api/{controller}/page        表格分页（继承自 YzhControllerBase）
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class TreeControllerBase<T> : YzhControllerBase<T>
    where T : TreeNodeViewBase, new()
{
    protected TreeControllerBase(EntityService<T> entityService, IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    // ========================================================
    // 一、树查询（扁平列表 + 批量计算 isLeaf）
    // ========================================================

    /// <summary>
    ///     获取树形结构（扁平列表，前端自动组装）
    ///     GET api/{controller}/tree?parentCode=xxx
    ///     
    ///     返回直接子级（非递归整树），配合前端懒加载
    /// </summary>
    [HttpGet("tree")]
    public virtual async Task<ActionResult<ApiResponse<List<T>>>> GetTree(
        [FromQuery] string? parentCode = null)
    {
        try
        {
            var items = await Entity.GetViewList(parentCode);

            // ★ 批量计算 isLeaf（一次 SQL）
            await FillIsLeafBatch(items);

            return Ok(ApiResponse<List<T>>.Ok(items));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<T>>.Fail($"获取树失败：{ex.Message}"));
        }
    }

    /// <summary>
    ///     懒加载子节点
    ///     POST api/{controller}/getChildren
    ///     
    ///     请求体：{ parentCode: string }
    ///     响应：{ items: T[] }
    /// </summary>
    [HttpPost("getChildren")]
    public virtual async Task<ActionResult<ApiResponse<GetChildrenResponse<T>>>> GetChildren(
        [FromBody] GetChildrenRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ParentCode))
                return BadRequest(ApiResponse.Fail("parentCode 不能为空"));

            var items = await Entity.GetViewList(request.ParentCode);

            // 批量计算 isLeaf
            await FillIsLeafBatch(items);

            return Ok(ApiResponse<GetChildrenResponse<T>>.Ok(new GetChildrenResponse<T> { Items = items }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<GetChildrenResponse<T>>.Fail($"加载子节点失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 二、树节点增删改（含级联）
    // ========================================================

    /// <summary>
    ///     新增节点（覆盖基类，增加 parentCode 校验）
    ///     POST api/{controller}/add
    /// </summary>
    [HttpPost("add")]
    public override async Task<ActionResult<ApiResponse<T>>> Add([FromBody] T entity)
    {
        try
        {
            // 校验 parentCode 存在性
            if (!string.IsNullOrEmpty(entity.ParentCode))
            {
                var parentResult = await Entity.GetByCode(entity.ParentCode);
                if (!parentResult.Success || parentResult.Data == null)
                    return BadRequest(ApiResponse.Fail($"父节点 {entity.ParentCode} 不存在"));
            }

            // 调用基类 Add
            return await base.Add(entity);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"新增节点失败：{ex.Message}"));
        }
    }

    /// <summary>
    ///     修改节点（覆盖基类，禁止修改 parentCode 为自身/后代）
    ///     POST api/{controller}/update
    /// </summary>
    [HttpPost("update")]
    public override async Task<ActionResult<ApiResponse<T>>> Update([FromBody] T entity)
    {
        try
        {
            // 防环校验：不能将 parentCode 改为自身或自身后代
            if (!string.IsNullOrEmpty(entity.ParentCode))
            {
                if (entity.ParentCode == entity.Code)
                    return BadRequest(ApiResponse.Fail("不能将父节点设为自己"));

                // 检查是否为后代
                var descendants = await GetDescendantCodes(entity.Code);
                if (descendants.Contains(entity.ParentCode))
                    return BadRequest(ApiResponse.Fail("不能将父节点设为自己的后代（会形成循环）"));
            }

            return await base.Update(entity);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"修改节点失败：{ex.Message}"));
        }
    }

    /// <summary>
    ///     移动节点（专用接口，含完整校验）
    ///     POST api/{controller}/move
    /// </summary>
    [HttpPost("move")]
    public virtual async Task<ActionResult<ApiResponse<T>>> Move(
        [FromBody] MoveNodeRequest request)
    {
        try
        {
            // 1. 校验节点存在
            var nodeResult = await Entity.GetByCode(request.Code);
            if (!nodeResult.Success || nodeResult.Data == null)
                return BadRequest(ApiResponse.Fail("节点不存在"));

            var node = nodeResult.Data;

            // 2. 校验目标父节点存在
            if (!string.IsNullOrEmpty(request.NewParentCode))
            {
                if (request.NewParentCode == request.Code)
                    return BadRequest(ApiResponse.Fail("不能移动到自己"));

                var newParentResult = await Entity.GetByCode(request.NewParentCode);
                if (!newParentResult.Success || newParentResult.Data == null)
                    return BadRequest(ApiResponse.Fail("目标父节点不存在"));

                // 防环
                var descendants = await GetDescendantCodes(request.Code);
                if (descendants.Contains(request.NewParentCode))
                    return BadRequest(ApiResponse.Fail("不能移动到自己的子树下"));
            }

            // 3. 防深度越限
            if (MaxDepth > 0)
            {
                var nodeDepth = await GetSubtreeDepth(request.Code);
                var newParentLevel = string.IsNullOrEmpty(request.NewParentCode)
                    ? 0
                    : await GetNodeLevel(request.NewParentCode);
                if (newParentLevel + 1 + nodeDepth > MaxDepth)
                    return BadRequest(ApiResponse.Fail($"移动后深度将超过限制 ({MaxDepth})"));
            }

            // 4. 执行移动
            node.ParentCode = request.NewParentCode;
            return await base.Update(node);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"移动节点失败：{ex.Message}"));
        }
    }

    /// <summary>
    ///     批量删除（含子树级联删除）
    ///     POST api/{controller}/delete
    ///     
    ///     前端传 code 数组，后端自动收集所有子孙 code 一并删除
    /// </summary>
    [HttpPost("delete")]
    public override async Task<ActionResult<ApiResponse<object?>>> Delete([FromBody] string[] codes)
    {
        try
        {
            if (codes == null || codes.Length == 0)
                return BadRequest(ApiResponse<object?>.Fail("未指定要删除的节点"));

            // 收集所有子孙 code
            var allCodes = new List<string>(codes);
            foreach (var code in codes)
            {
                var descendants = await GetDescendantCodes(code);
                allCodes.AddRange(descendants);
            }

            // 去重
            allCodes = allCodes.Distinct().ToList();

            // 调用基类批量删除
            var result = await Entity.DeleteBatch(allCodes, hardDelete: HardDelete, clientIp: UserContext.ClientIp);
            if (!result.Success)
                return BadRequest(ApiResponse<object?>.Fail(result.Error));

            return Ok(ApiResponse<object?>.Ok($"已删除 {result.Data} 个节点（含子节点）"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object?>.Fail($"删除失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 三、Virtual 方法（子类可覆盖）
    // ========================================================

    /// <summary>
    ///     最大层级深度（0=不限）
    ///     子类覆盖此属性限制树的深度
    /// </summary>
    protected virtual int MaxDepth => 0;

    // ========================================================
    // 四、辅助方法
    // ========================================================

    /// <summary>
    ///     批量填充 isLeaf 字段（一次 SQL GROUP BY，杜绝 N+1）
    ///     使用 EntityService.GetChildrenCountBatch 实现
    /// </summary>
    protected virtual async Task FillIsLeafBatch(List<T> items)
    {
        if (items == null || items.Count == 0) return;

        try
        {
            var codes = items.Select(i => i.Code).Distinct().ToList();

            // 一次 SQL 查询所有子节点数量
            var childrenCount = await Entity.GetChildrenCountBatch(codes);

            foreach (var item in items)
            {
                item.IsLeaf = !childrenCount.ContainsKey(item.Code) || childrenCount[item.Code] == 0;
            }
        }
        catch
        {
            // 查询失败时，设置默认值（不影响主流程）
            foreach (var item in items)
            {
                item.IsLeaf = false;
            }
        }
    }

    /// <summary>
    ///     获取指定节点的所有子孙 code（递归）
    ///     用于级联删除
    /// </summary>
    protected virtual async Task<List<string>> GetDescendantCodes(string code)
    {
        var result = new List<string>();
        await CollectDescendants(code, result);
        return result;
    }

    private async Task CollectDescendants(string parentCode, List<string> result)
    {
        var children = await Entity.GetViewList(parentCode);
        foreach (var child in children)
        {
            result.Add(child.Code);
            await CollectDescendants(child.Code, result);
        }
    }

    /// <summary>
    ///     获取子树深度（用于防深度越限）
    /// </summary>
    protected virtual async Task<int> GetSubtreeDepth(string code)
    {
        var descendants = await GetDescendantCodes(code);
        if (descendants.Count == 0) return 1;

        // 获取每个子孙节点的层级，取最大值
        int maxDepth = 1;
        foreach (var descendant in descendants)
        {
            var level = await GetNodeLevel(descendant);
            maxDepth = Math.Max(maxDepth, level);
        }
        return maxDepth - await GetNodeLevel(code) + 1;
    }

    /// <summary>
    ///     获取节点层级（从根到该节点的深度）
    /// </summary>
    protected virtual async Task<int> GetNodeLevel(string code)
    {
        int level = 0;
        var currentResult = await Entity.GetByCode(code);
        while (currentResult.Success && currentResult.Data != null && !string.IsNullOrEmpty(currentResult.Data.ParentCode))
        {
            level++;
            currentResult = await Entity.GetByCode(currentResult.Data.ParentCode);
        }
        return level;
    }
}

// ========================================================
// 请求/响应模型
// ========================================================

/// <summary>懒加载请求参数</summary>
public class GetChildrenRequest
{
    /// <summary>父节点编码</summary>
    public string ParentCode { get; set; } = string.Empty;
}

/// <summary>懒加载响应</summary>
public class GetChildrenResponse<T>
{
    /// <summary>子节点列表</summary>
    public List<T> Items { get; set; } = new();
}

/// <summary>移动节点请求参数</summary>
public class MoveNodeRequest
{
    /// <summary>要移动的节点编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>新父节点编码（null 表示移到根级）</summary>
    public string? NewParentCode { get; set; }
}
