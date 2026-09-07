using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Models;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Controllers;

/// <summary>
///     控制器基类（Yzh = 映智汇）
///     
///     核心职责：
///     ┌─────────────────────────────────────────────────────────────┐
///     │  1. 属性定义     Config, Services, HttpContext, UserContext   │
///     │  2. 生命周期     钩子（Before/After/Committed）                │
///     │  3. 功能方法     CRUD + 分页 + 行操作 + 导入导出              │
///     │  4. 可扩展       虚方法 + 委托注册                            │
///     └─────────────────────────────────────────────────────────────┘
///     
///     方法分层：
///     - *Core() 方法：原子操作，返回 (T?, string?)，供任意代码调用
///     - 无后缀方法：HTTP API 端点，返回 ActionResult&lt;ApiResponse&lt;T&gt;&gt;
///     
///     异常控制：
///     - 每个方法内部控制 try-catch
///     - 返回详细错误信息（包含异常类型、数据库约束冲突等）
///     - 不完全依赖全局异常过滤器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class YzhControllerBase<T> : ControllerBase where T : BaseEntity
{
    // ========================================================
    // 一、属性定义
    // ========================================================

    /// <summary>实体操作服务（原子层）</summary>
    protected EntityService<T> Entity { get; }

    /// <summary>配置</summary>
    protected GridConfig Config { get; }

    /// <summary>当前用户上下文</summary>
    protected IUserContext UserContext { get; }

    /// <summary>Controller 名称（不含 Controller 后缀）</summary>
    protected string ControllerName => GetType().Name.Replace("Controller", "");

    // ========================================================
    // 二、构造函数
    // ========================================================

    protected YzhControllerBase(EntityService<T> entityService, IUserContext userContext)
    {
        Entity = entityService;
        UserContext = userContext;
        Config = LoadConfig();
    }

    /// <summary>加载配置（子类可覆盖以自定义配置来源）</summary>
    protected virtual GridConfig LoadConfig() => GridConfig.GetGridConfig<T>();

    // ========================================================
    // 三、原子方法（返回元组，供任意代码调用）
    // ========================================================

    #region 原子方法 - 查询

    /// <summary>分页查询原子方法</summary>
    /// <param name="request">分页请求参数</param>
    /// <returns>(pagedResult, error) — error=null 表示成功</returns>
    public virtual (PagedResult<T>? result, string? err) GetPageCore(PageRequest request)
    {
        try
        {
            // 1. 构建查询条件（子类可覆盖添加自定义过滤）
            var filters = OnBuildingQuery(request.Conditions);

            // 2. 执行分页查询
            var (pageData, err) = Entity.GetPage(new PagerOptions
            {
                Page = request.Page,
                PageSize = request.PageSize,
                SortBy = request.SortField,
                SortDirection = request.SortOrder,
                Filters = filters,
                SearchKey = request.SearchKey
            });

            if (err != null) return (null, err);

            // 3. 构建结果
            var result = new PagedResult<T>(pageData!.Value.items, pageData.Value.total, request.Page, request.PageSize);

            // 4. 查询后钩子（字典翻译、格式化等）
            OnQueried(result);

            return (result, null);
        }
        catch (Exception ex)
        {
            return (null, $"分页查询异常：{ex.Message}");
        }
    }

    /// <summary>获取配置原子方法</summary>
    public virtual (GridConfig? config, string? err) GetConfigCore()
    {
        try
        {
            OnConfigLoading(Config);
            return (Config, null);
        }
        catch (Exception ex)
        {
            return (null, $"配置加载异常：{ex.Message}");
        }
    }

    #endregion

    #region 原子方法 - 写入

    /// <summary>新增原子方法</summary>
    public virtual (T? entity, string? err) AddCore(T entity)
    {
        try
        {
            // 1. 校验
            var (valid, msg) = ValidateEntity(entity);
            if (!valid) return (null, msg);

            // 2. 新增前钩子
            OnBeforeAdd(entity);

            // 3. 执行新增
            var (result, err) = Entity.Insert(entity, UserContext.ClientIp);
            if (err != null) return (null, err);

            // 4. 新增后钩子
            OnAfterAdd(result!);
            OnAfterCommitted();

            return (result, null);
        }
        catch (Exception ex)
        {
            return (null, $"新增异常：{ex.Message}");
        }
    }

    /// <summary>修改原子方法</summary>
    public virtual (T? entity, string? err) UpdateCore(T entity)
    {
        try
        {
            // 1. 校验
            var (valid, msg) = ValidateEntity(entity);
            if (!valid) return (null, msg);

            // 2. 修改前钩子
            OnBeforeUpdate(entity);

            // 3. 获取可保存字段（基于 BCFlag）
            var saveableFields = Config.Columns
                .Where(c => c.BCFlag)
                .Select(c => c.FieldName)
                .ToArray();

            // 4. 执行修改
            var (result, err) = Entity.Update(
                entity, UserContext.ClientIp,
                updateFields: saveableFields.Length > 0 ? saveableFields : null);

            if (err != null) return (null, err);

            // 5. 修改后钩子
            OnAfterUpdate(result!);
            OnAfterCommitted();

            return (result, null);
        }
        catch (Exception ex)
        {
            return (null, $"修改异常：{ex.Message}");
        }
    }

    /// <summary>删除原子方法（按 Code 数组批量删除）</summary>
    public virtual (int count, string? err) DeleteCore(params string[] codes)
    {
        try
        {
            if (codes == null || codes.Length == 0)
                return (0, "未指定要删除的记录");

            // 1. 删除前钩子
            OnBeforeDelete(codes);

            // 2. 执行删除
            var (count, err) = Entity.DeleteBatch(codes, hardDelete: HardDelete, clientIp: UserContext.ClientIp);
            if (err != null) return (0, err);

            // 3. 删除后钩子
            OnAfterDelete(count);
            OnAfterCommitted();

            return (count, null);
        }
        catch (Exception ex)
        {
            return (0, $"删除异常：{ex.Message}");
        }
    }

    /// <summary>
    ///     是否硬删除（默认软删除）
    ///     子类可通过 [YZHDeleteStrategy] 特性或覆盖此属性控制
    /// </summary>
    protected virtual bool HardDelete => false;

    #endregion

    #region 原子方法 - 导入导出

    /// <summary>导出原子方法（返回文件字节和文件名）</summary>
    public virtual (byte[]? fileData, string fileName, string? err) ExportCore(RequestCondition[]? conditions)
    {
        try
        {
            // 1. 获取数据（不分页，全量导出）
            var (data, err) = BuildExportData(conditions);
            if (err != null) return (null, string.Empty, err);
            if (data == null || data.Count == 0)
                return (null, string.Empty, "没有可导出的数据");

            // 2. 生成 Excel（TODO: 接入 ExcelExporter）
            // var excelData = ExcelExporter.ToExcel(data, Config);
            // return (excelData, $"{Config.Title}_{DateTime.Now:yyyyMMdd}.xlsx", null);

            return (null, string.Empty, "导出功能待实现（需要引入 ExcelExporter）");
        }
        catch (Exception ex)
        {
            return (null, string.Empty, $"导出异常：{ex.Message}");
        }
    }

    /// <summary>导入原子方法</summary>
    public virtual (int imported, int updated, List<string> errors, string? err) ImportCore(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return (0, 0, new List<string>(), "未上传文件");

            // TODO: 接入 ExcelImporter
            // 1. 读取 Excel 数据
            // 2. 遍历行，逐条校验
            // 3. 插入或更新
            // 4. 返回统计

            return (0, 0, new List<string>(), "导入功能待实现（需要引入 ExcelImporter）");
        }
        catch (Exception ex)
        {
            return (0, 0, new List<string> { ex.Message }, $"导入异常：{ex.Message}");
        }
    }

    #endregion

    // ========================================================
    // 四、HTTP API 方法（返回 ActionResult&lt;ApiResponse&gt;）
    // ========================================================

    [HttpGet("config")]
    public virtual ActionResult<ApiResponse<GridConfig>> GetConfig()
    {
        var (config, err) = GetConfigCore();
        return err != null
            ? BadRequest(ApiResponse.Fail(err))
            : Ok(ApiResponse<GridConfig>.Ok(config!));
    }

    [HttpPost("page")]
    public virtual ActionResult<ApiResponse<PagedResult<T>>> GetPage([FromBody] PageRequest request)
    {
        var (result, err) = GetPageCore(request);
        return err != null
            ? BadRequest(ApiResponse.Fail(err))
            : Ok(ApiResponse<PagedResult<T>>.Ok(result!));
    }

    [HttpPost("add")]
    public virtual ActionResult<ApiResponse<T>> Add([FromBody] T entity)
    {
        var (result, err) = AddCore(entity);
        return err != null
            ? BadRequest(ApiResponse.Fail(err))
            : Ok(ApiResponse<T>.Ok(result!, "创建成功"));
    }

    [HttpPost("update")]
    public virtual ActionResult<ApiResponse<T>> Update([FromBody] T entity)
    {
        var (result, err) = UpdateCore(entity);
        return err != null
            ? BadRequest(ApiResponse.Fail(err))
            : Ok(ApiResponse<T>.Ok(result!, "修改成功"));
    }

    /// <summary>批量删除（前端传 Codes 数组）</summary>
    [HttpPost("delete")]
    public virtual ActionResult<ApiResponse> Delete([FromBody] string[] codes)
    {
        var (count, err) = DeleteCore(codes);
        return err != null
            ? BadRequest(ApiResponse.Fail(err))
            : Ok(ApiResponse.Ok($"已删除 {count} 条记录"));
    }

    [HttpPost("export")]
    public virtual IActionResult Export([FromBody] RequestCondition[]? conditions)
    {
        var (fileData, fileName, err) = ExportCore(conditions);
        if (err != null) return BadRequest(ApiResponse.Fail(err));
        // return File(fileData!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        return Ok(ApiResponse.Ok("导出功能待实现"));
    }

    [HttpPost("import")]
    public virtual ActionResult<ApiResponse> Import(IFormFile file)
    {
        var (imported, updated, errors, err) = ImportCore(file);
        if (err != null) return BadRequest(ApiResponse.Fail(err));

        return Ok(ApiResponse.Ok($"导入完成：新增 {imported} 条，更新 {updated} 条"));
    }

    // ========================================================
    // 五、行操作（委托注册模式）
    // ========================================================

    /// <summary>行操作委托类型</summary>
    protected delegate (ApiResponse? result, string? err) RowActionHandler(T entity);

    /// <summary>行操作字典</summary>
    private readonly Dictionary<string, RowActionHandler> _rowActions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册行操作（子类构造函数中调用）</summary>
    protected void RegisterRowAction(string actionName, RowActionHandler handler)
    {
        _rowActions[actionName.ToLowerInvariant()] = handler;
    }

    /// <summary>
    ///     统一行操作入口
    ///     前端通过 GridConfig.RowButtons.CustomButtons 字典知道按钮→方法名的映射
    ///     URL: POST api/{controller}/action/{methodName}
    /// </summary>
    [HttpPost("action/{methodName}")]
    public virtual ActionResult<ApiResponse> ExecuteAction(string methodName, [FromBody] T entity)
    {
        if (string.IsNullOrEmpty(methodName))
            return BadRequest(ApiResponse.Fail("操作名称不能为空"));

        if (!_rowActions.TryGetValue(methodName.ToLowerInvariant(), out var handler))
            return BadRequest(ApiResponse.Fail($"操作 [{methodName}] 未注册，请检查 RegisterRowAction 调用"));

        var (result, err) = handler(entity);
        return err != null
            ? BadRequest(ApiResponse.Fail(err))
            : (result != null ? Ok(result) : Ok(ApiResponse.Ok("操作成功")));
    }

    // ========================================================
    // 六、生命周期钩子（子类可覆盖）
    // ========================================================

    #region 查询钩子

    /// <summary>构建查询条件（覆盖以实现自定义过滤逻辑）</summary>
    protected virtual List<FilterItem> OnBuildingQuery(RequestCondition[]? conditions)
    {
        return conditions?.Select(c => new FilterItem
        {
            Field = c.Field,
            Operator = c.Operator,
            Value = c.Value
        }).ToList() ?? new List<FilterItem>();
    }

    /// <summary>查询后钩子（字典翻译、字段格式化、脱敏等）</summary>
    protected virtual void OnQueried(PagedResult<T> result) { }

    /// <summary>配置加载后钩子（注入动态配置）</summary>
    protected virtual void OnConfigLoading(GridConfig config) { }

    #endregion

    #region 新增钩子

    /// <summary>新增前钩子（数据填充、校验、取消操作）</summary>
    protected virtual void OnBeforeAdd(T entity) { }

    /// <summary>新增后钩子（数据已入库，事务内）</summary>
    protected virtual void OnAfterAdd(T entity) { }

    #endregion

    #region 修改钩子

    /// <summary>修改前钩子</summary>
    protected virtual void OnBeforeUpdate(T entity) { }

    /// <summary>修改后钩子</summary>
    protected virtual void OnAfterUpdate(T entity) { }

    #endregion

    #region 删除钩子

    /// <summary>删除前钩子（可检查关联数据、级联操作）</summary>
    protected virtual void OnBeforeDelete(string[] codes) { }

    /// <summary>删除后钩子</summary>
    protected virtual void OnAfterDelete(int count) { }

    #endregion

    #region 提交后钩子

    /// <summary>
    ///     提交后钩子（事务已完成，可执行通知/缓存清理等外部操作）
    ///     在 Add/Update/Delete 成功后都会调用
    /// </summary>
    protected virtual void OnAfterCommitted() { }

    #endregion

    #region 自定义校验

    /// <summary>自定义校验（覆盖以实现复杂业务逻辑校验）</summary>
    protected virtual string? OnCustomValidate(T entity) => null;

    #endregion

    #region 导出/导入钩子

    /// <summary>构建导出数据（覆盖以自定义导出字段和格式）</summary>
    protected virtual (List<T>? data, string? err) BuildExportData(RequestCondition[]? conditions)
    {
        try
        {
            // 默认使用分页查询逻辑，不分页获取全量
            var filters = OnBuildingQuery(conditions);
            var (pageData, err) = Entity.GetPage(new PagerOptions
            {
                Page = 1,
                PageSize = int.MaxValue,
                Filters = filters,
                NoPage = true
            });

            if (err != null) return (null, err);

            return (pageData!.Value.items, null);
        }
        catch (Exception ex)
        {
            return (null, $"构建导出数据异常：{ex.Message}");
        }
    }

    #endregion

    // ========================================================
    // 七、辅助方法
    // ========================================================

    /// <summary>
    ///     实体校验（基于 GridConfig 配置的 BCFlag/YXK 自动校验 + 自定义校验）
    ///     返回 (isValid, errorMessage)
    /// </summary>
    protected virtual (bool ok, string? msg) ValidateEntity(T entity)
    {
        foreach (var col in Config.Columns.Where(c => c.BCFlag && !c.YXK))
        {
            var prop = typeof(T).GetProperty(col.FieldName,
                System.Reflection.BindingFlags.IgnoreCase |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            var value = prop?.GetValue(entity);
            if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                return (false, $"{col.DesName} 不能为空");
        }

        // 自定义校验
        var customErr = OnCustomValidate(entity);
        if (customErr != null) return (false, customErr);

        return (true, null);
    }
}

// ========================================================
// 请求模型
// ========================================================

/// <summary>分页查询请求模型</summary>
public class PageRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortField { get; set; }
    public string? SortOrder { get; set; }
    public string? SearchKey { get; set; }
    public RequestCondition[]? Conditions { get; set; }
}

/// <summary>请求查询条件（前端传入的标准格式）</summary>
public class RequestCondition
{
    public string Field { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Operator { get; set; } = "eq";
}
