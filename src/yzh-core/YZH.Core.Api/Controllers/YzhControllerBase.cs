using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using YZH.Core.Api.Attributes;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Entity;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Api.Controllers;

/// <summary>
///     控制器基类（Yzh = 映智汇）
///
///     核心职责：
///     ┌─────────────────────────────────────────────────────────────┐
///     │  1. 属性定义     Config, Services, UserContext               │
///     │  2. 生命周期     钩子（Before/After/Committed）                │
///     │  3. 功能方法     CRUD + 过滤 + 行操作 + 导入导出              │
///     │  4. 可扩展       虚方法 + 委托注册                            │
///     └─────────────────────────────────────────────────────────────┘
///
///     认证约定：
///     - 基类标记 [YZHAuthorize]，所有子类默认强认证
///     - 临时测试接口：方法上标记 [YZHAnonymous]
///
///     方法分层：
///     - *Core() 方法：原子操作，返回 Result&lt;T&gt;，供任意代码调用
///     - 无后缀方法：HTTP API 端点，返回 ActionResult&lt;ApiResponse&lt;T&gt;&gt;
///
///     异常控制：
///     - 每个方法内部控制 try-catch
///     - 返回详细错误信息（包含异常类型、数据库约束冲突等）
///     - 不完全依赖全局异常过滤器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[YZHAuthorize]
public abstract class YzhControllerBase<V> : ControllerBase where V : class, new()
{
  // ========================================================
  // 一、属性定义
  // ========================================================

  /// <summary>实体操作服务（原子层）</summary>
  protected EntityService<V> Entity { get; }

  /// <summary>配置</summary>
  protected EntityConfig Config { get; }

  /// <summary>当前用户上下文</summary>
  protected IUserContext UserContext { get; }

  /// <summary>Controller 名称（不含 Controller 后缀）</summary>
  protected string ControllerName => GetType().Name.Replace("Controller", "");

  // ========================================================
  // 二、构造函数
  // ========================================================

  protected YzhControllerBase(EntityService<V> entityService, IUserContext userContext)
  {
    Entity = entityService;
    UserContext = userContext;
    Config = LoadConfig();
  }

  /// <summary>加载配置（子类可覆盖以自定义配置来源）</summary>
  protected virtual EntityConfig LoadConfig()
  {
    if (typeof(BaseEntity).IsAssignableFrom(typeof(V)))
    {
      var config = EntityConfigHelper.GetEntityConfig<V>();
      // 缺 JSON 兜底：触发 OnConfigMissing 钩子（默认 warning；子类可 override 改 throw）
      if (config.Columns == null || config.Columns.Count == 0)
        OnConfigMissing(typeof(V).Name);
      return config;
    }
    return new EntityConfig
    {
      Title = typeof(V).Name,
      Columns = new List<DefineColumn>()
    };
  }

  /// <summary>
  ///     是否启用严格配置加载（默认 false，保持向后兼容）
  ///
  ///     false（默认）：缺 JSON 时仅 warning，前端打开页面是空白
  ///     true：缺 JSON 时 throw InvalidOperationException，强制开发期暴露
  ///
  ///     推荐用法：在业务 Controller 子类中显式 override 为 true
  ///     ```
  ///     protected override bool StrictConfigLoad => true;
  ///     ```
  ///     或在 YzhWebBuilder 中通过反射批量打开
  ///     ```
  ///     builder.Services.Configure<YzhCoreOptions>(o => o.GlobalStrictConfigLoad = true);
  ///     ```
  /// </summary>
  protected virtual bool StrictConfigLoad => false;

  /// <summary>
  ///     EntityConfig 缺失钩子
  ///
  ///     触发场景：EntityConfig JSON 不存在或 Columns 为空
  ///     典型原因：
  ///     1. 新建 Controller 后忘了在 Assets/EntityConfigs/{Domain}/{TypeName}.json 创建配置
  ///     2. JSON 文件名与 V 类型名不一致（不区分大小写匹配，但下划线会失败）
  ///     3. JSON 写错字段（PropertyNameCaseInsensitive=true 容错大但非全免疫）
  ///
  ///     默认行为：Console.WriteLine warning + 返回（前端页面空白但服务不中断）
  ///     严格模式：当 StrictConfigLoad=true 时 throw，强制开发期暴露问题
  ///
  ///     V4 铁律：永远不要默认返回空配置（前端打开页面是空白且无报错，排查极困难）
  ///     推荐：所有新 Controller 子类显式 override `StrictConfigLoad => true`
  /// </summary>
  protected virtual void OnConfigMissing(string typeName)
  {
    if (StrictConfigLoad)
    {
      throw new InvalidOperationException(
          $"[YZH] 实体 {typeName} 缺少 EntityConfig JSON 配置（预期路径 Assets/EntityConfigs/**/{typeName}.json）。" +
          $"开发期必须创建；如需降级为 warning，请 override StrictConfigLoad 返回 false。");
    }

    // 默认降级：仅 warning（向后兼容，避免 YZH.Core 自带 Controller 启动失败）
    Console.WriteLine(
        $"[YZH.WARN] 实体 {typeName} 缺少 EntityConfig JSON 配置（预期路径 Assets/EntityConfigs/**/{typeName}.json）。" +
        $"前端打开该页面将为空白。修复方法：创建 JSON 配置，或在 Controller 子类 override StrictConfigLoad => true 强制 throw 暴露。");
  }

  // ========================================================
  // 三、原子方法（返回元组，供任意代码调用）
  // ========================================================

  #region 原子方法 - 查询

  /// <summary>过滤查询原子方法（/filter）</summary>
  public virtual async Task<Result<PagedResult<V>>> FilterCore(FilterRequest request)
  {
    try
    {
      // 1. 将前端 FilterItem 转换为后端 FilterItem
      var filters = request.Filters?.Select(f => new FilterItem
      {
        Field = f.Field,
        Operator = f.Operator,
        Value = f.Value
      }).ToList() ?? new List<FilterItem>();

      // 2. 构建查询条件（子类可覆盖添加额外过滤）
      filters = OnBuildingFilter(filters);

      // 3. 执行分页查询
      var result = await Entity.GetPageAsync(new PagerOptions
      {
        Page = request.Page,
        PageSize = request.PageSize,
        SortBy = request.SortField,
        SortDirection = request.SortOrder,
        Filters = filters
      });

      if (!result.Success) return Result<PagedResult<V>>.Fail(result.Error);

      // 4. 查询后钩子（字典翻译、格式化等）
      OnQueried(result.Data!);

      return Result<PagedResult<V>>.Ok(result.Data!);
    }
    catch (Exception ex)
    {
      return Result<PagedResult<V>>.Fail($"过滤查询异常：{ex.Message}");
    }
  }

  /// <summary>
  ///     获取配置原子方法
  ///     自动注入 NewEntity（空实体模板）和 Schema（字段结构描述）
  ///     来源：后端反射实体类生成（EntitySchemaHelper）
  /// </summary>
  public virtual Result<EntityConfig> GetConfigCore()
  {
    try
    {
      // 反射实体生成空实体模板 → 前端直接用此初始化表单
      Config.NewEntity = EntitySchemaHelper.GetEmptyEntity<V>();
      Config.Schema = EntitySchemaHelper.GetSchema<V>();

      OnConfigLoading(Config);
      return Result<EntityConfig>.Ok(Config);
    }
    catch (Exception ex)
    {
      return Result<EntityConfig>.Fail($"配置加载异常：{ex.Message}");
    }
  }

  #endregion

  #region 原子方法 - 写入

  /// <summary>新增原子方法</summary>
  public virtual async Task<Result<V>> AddCore(V entity)
  {
    try
    {
      // 0. Code 为空时自动生成（前端可能不传）
      var codeProp = typeof(V).GetProperty("Code");
      if (codeProp != null && string.IsNullOrEmpty(codeProp.GetValue(entity) as string))
      {
        codeProp.SetValue(entity, Guid.NewGuid().ToString("N"));
      }

      // 1. 校验
      var (valid, msg) = ValidateEntity(entity);
      if (!valid) return Result<V>.Fail(msg);

      // 2. 新增前钩子（可取消）
      var (ok, cancelMsg) = await OnBeforeAdd(entity);
      if (!ok) return Result<V>.Fail(cancelMsg ?? "操作已取消");

      // 3. 执行新增
      var result = await Entity.Insert(entity, UserContext.ClientIp);
      if (!result.Success) return Result<V>.Fail(result.Error);

      // 4. 新增后钩子
      await OnAfterAdd(result.Data!);
      await OnAfterCommitted();

      return Result<V>.Ok(result.Data!);
    }
    catch (Exception ex)
    {
      return Result<V>.Fail($"新增异常：{ex.Message}");
    }
  }

  /// <summary>修改原子方法</summary>
  public virtual async Task<Result<V>> UpdateCore(V entity)
  {
    try
    {
      // 1. 校验
      var (valid, msg) = ValidateEntity(entity);
      if (!valid) return Result<V>.Fail(msg);

      // 2. 修改前钩子（可取消）
      var (ok, cancelMsg) = await OnBeforeUpdate(entity);
      if (!ok) return Result<V>.Fail(cancelMsg ?? "操作已取消");

      // 3. 获取可保存字段（基于 BCFlag）
      var saveableFields = Config.Columns
          .Where(c => c.BCFlag)
          .Select(c => c.FieldName)
          .ToArray();

      // 4. 执行修改
      var result = await Entity.Update(
          entity, UserContext.ClientIp,
          updateFields: saveableFields.Length > 0 ? saveableFields : null);

      if (!result.Success) return Result<V>.Fail(result.Error);

      // 5. 修改后钩子
      await OnAfterUpdate(result.Data!);
      await OnAfterCommitted();

      return Result<V>.Ok(result.Data!);
    }
    catch (Exception ex)
    {
      return Result<V>.Fail($"修改异常：{ex.Message}");
    }
  }

  /// <summary>删除原子方法（按 Code 数组批量删除）</summary>
  public virtual async Task<Result<int>> DeleteCore(params string[] codes)
  {
    try
    {
      if (codes == null || codes.Length == 0)
        return Result<int>.Fail("未指定要删除的记录");

      // 1. 删除前钩子（可取消）
      var (ok, cancelMsg) = await OnBeforeDelete(codes);
      if (!ok) return Result<int>.Fail(cancelMsg ?? "操作已取消");

      // 2. 执行删除
      var result = await Entity.DeleteBatch(codes, hardDelete: HardDelete, clientIp: UserContext.ClientIp);
      if (!result.Success) return Result<int>.Fail(result.Error);

      // 3. 删除后钩子
      await OnAfterDelete(result.Data);
      await OnAfterCommitted();

      return Result<int>.Ok(result.Data);
    }
    catch (Exception ex)
    {
      return Result<int>.Fail($"删除异常：{ex.Message}");
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
  public virtual async Task<Result<(byte[] fileData, string fileName)>> ExportCore(ExportRequest request)
  {
    try
    {
      // 1. 获取数据（不分页，全量导出）
      var result = await BuildExportData(request.Filters);
      if (!result.Success) return Result<(byte[], string)>.Fail(result.Error);
      var data = result.Data;
      if (data == null || data.Count == 0)
        return Result<(byte[], string)>.Fail("没有可导出的数据");

      // 2. 解析 IExcelService（架构层约定，业务项目层注册 EPPlus/NPOI 实现）
      var excelService = ResolveExcelService();
      if (excelService == null)
        return Result<(byte[], string)>.Fail(
            "导出功能未启用：IExcelService 未注册。请在 YzhWebBuilder 中注册 EPPlus/NPOI 实现，" +
            "或子类 override ExportCore 自定义导出。");

      // 3. 列映射：默认取 BCFlag=true 的字段
      var columnMapping = Config.Columns
          .Where(c => c.BCFlag)
          .ToDictionary(c => c.FieldName, c => c.DesName);

      // 4. 生成 Excel
      var bytes = excelService.ExportToExcel(data, columnMapping, Config.Title);
      var fileName = $"{Config.Title}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
      return Result<(byte[], string)>.Ok((bytes, fileName));
    }
    catch (NotImplementedException ex)
    {
      // NullExcelService 抛出的提示
      return Result<(byte[], string)>.Fail(ex.Message);
    }
    catch (Exception ex)
    {
      return Result<(byte[], string)>.Fail($"导出异常：{ex.Message}");
    }
  }

  /// <summary>导入原子方法</summary>
  public virtual async Task<Result<ImportResult>> ImportCore(IFormFile file)
  {
    try
    {
      if (file == null || file.Length == 0)
        return Result<ImportResult>.Fail("未上传文件");

      // 解析 IExcelService（架构层约定，业务项目层注册 EPPlus/NPOI 实现）
      var excelService = ResolveExcelService();
      if (excelService == null)
        return Result<ImportResult>.Fail(
            "导入功能未启用：IExcelService 未注册。请在 YzhWebBuilder 中注册 EPPlus/NPOI 实现，" +
            "或子类 override ImportCore 自定义导入。");

      // 读取文件流
      byte[] fileBytes;
      using (var ms = new MemoryStream())
      {
        await file.CopyToAsync(ms);
        fileBytes = ms.ToArray();
      }

      // 调用 IExcelService 解析（架构层只解析数据，业务校验由子类 override ProcessImport 实现）
      var entities = excelService.ImportFromExcel<V>(fileBytes);

      // 调用子类 ProcessImport（默认逐行 AddCore）
      return await ProcessImport(entities);
    }
    catch (NotImplementedException ex)
    {
      return Result<ImportResult>.Fail(ex.Message);
    }
    catch (Exception ex)
    {
      return Result<ImportResult>.Fail($"导入异常：{ex.Message}");
    }
  }

  /// <summary>
  ///     解析 IExcelService（从 HttpContext.RequestServices 或子类 override 注入）
  ///     子类可 override 此方法直接从构造函数注入的服务返回
  /// </summary>
  protected virtual IExcelService? ResolveExcelService()
  {
    return HttpContext?.RequestServices.GetService<IExcelService>();
  }

  #endregion

  // ========================================================
  // 四、HTTP API 方法（返回 ActionResult&lt;ApiResponse&gt;）
  // ========================================================

  [HttpGet("config")]
  public virtual ActionResult<ApiResponse<EntityConfig>> GetConfig()
  {
    var result = GetConfigCore();
    return RequestResultToActionResult(result.ToApiResponse());
  }

  /// <summary>
  ///     过滤查询（新版推荐）
  ///     前端传 FilterRequest，后端自动解析 FilterItem 为 SQL 条件
  /// </summary>
  [HttpPost("filter")]
  public virtual async Task<ActionResult<ApiResponse<PagedResult<V>>>> Filter([FromBody] FilterRequest request)
  {
    var result = await FilterCore(request);
    return RequestResultToActionResult(result.ToApiResponse());
  }

  [HttpPost("add")]
  public virtual async Task<ActionResult<ApiResponse<V>>> Add([FromBody] V entity)
  {
    var result = await AddCore(entity);
    return RequestResultToActionResult(result.ToApiResponse("创建成功"));
  }

  [HttpPost("update")]
  public virtual async Task<ActionResult<ApiResponse<V>>> Update([FromBody] V entity)
  {
    var result = await UpdateCore(entity);
    return RequestResultToActionResult(result.ToApiResponse("修改成功"));
  }

  /// <summary>批量删除（前端传 Codes 数组）</summary>
  [HttpPost("delete")]
  public virtual async Task<ActionResult<ApiResponse<object?>>> Delete([FromBody] string[] codes)
  {
    var result = await DeleteCore(codes);
    return RequestResultToApiResponse(result.Map(count => ApiResponse<object?>.Ok($"已删除 {count} 条记录")));
  }

  [HttpPost("export")]
  public virtual async Task<IActionResult> Export([FromBody] ExportRequest request)
  {
    var result = await ExportCore(request);
    if (!result.Success) return BadRequest(ApiResponse.Fail(result.Error!));
    // 返回文件流（前端 apiPostAndDownload 接收）
    return File(result.Data.fileData,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        result.Data.fileName);
  }

  [HttpPost("import")]
  public virtual async Task<ActionResult<ApiResponse<ImportResult>>> Import(IFormFile file)
  {
    var result = await ImportCore(file);
    if (!result.Success) return BadRequest(ApiResponse.Fail(result.Error!, result.Code ?? 400));
    return Ok(ApiResponse<ImportResult>.Ok(result.Data!, "导入完成"));
  }

  /// <summary>下载导入模板</summary>
  [HttpGet("import/template")]
  public virtual async Task<IActionResult> DownloadImportTemplate()
  {
    try
    {
      var excelService = ResolveExcelService();
      if (excelService == null)
        return BadRequest(ApiResponse.Fail(
            "模板下载功能未启用：IExcelService 未注册。请在 YzhWebBuilder 中注册 EPPlus/NPOI 实现。"));

      // 默认取 BCFlag=true 的字段名作为模板列
      var fieldNames = Config.Columns
          .Where(c => c.BCFlag)
          .Select(c => c.FieldName)
          .ToList();

      var bytes = excelService.GenerateImportTemplate<V>(fieldNames);
      var fileName = $"{Config.Title}_导入模板.xlsx";
      return File(bytes,
          "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
          fileName);
    }
    catch (NotImplementedException ex)
    {
      return BadRequest(ApiResponse.Fail(ex.Message));
    }
    catch (Exception ex)
    {
      return BadRequest(ApiResponse.Fail($"模板下载异常：{ex.Message}"));
    }
  }

  // ========================================================
  // 五、行操作（委托注册模式）
  // ========================================================

  /// <summary>行操作委托类型</summary>
  protected delegate Task<Result<ApiResponse<object?>>> RowActionHandler(V entity);

  /// <summary>行操作字典</summary>
  protected readonly Dictionary<string, RowActionHandler> _rowActions = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>注册行操作（子类构造函数中调用）</summary>
  protected void RegisterRowAction(string actionName, RowActionHandler handler)
  {
    _rowActions[actionName.ToLowerInvariant()] = handler;
  }

  /// <summary>
  ///     统一行操作入口
  ///     前端通过 EntityConfig.RowButtons.CustomButtons 字典知道按钮→方法名的映射
  ///     URL: POST api/{controller}/action/{methodName}
  /// </summary>
  [HttpPost("action/{methodName}")]
  public virtual async Task<ActionResult<ApiResponse<object?>>> ExecuteAction(string methodName, [FromBody] JsonElement entityData)
  {
    if (string.IsNullOrEmpty(methodName))
      return BadRequest(ApiResponse.Fail("操作名称不能为空"));

    if (!_rowActions.TryGetValue(methodName.ToLowerInvariant(), out var handler))
      return BadRequest(ApiResponse.Fail($"操作 [{methodName}] 未注册，请检查 RegisterRowAction 调用"));

    // 仅传递 Code 属性到处理函数（避免 [Required] 校验失败）
    var code = entityData.TryGetProperty("Code", out var codeProp) ? codeProp.GetString() : null;
    var entity = new V();
    var codePropInfo = typeof(V).GetProperty("Code");
    if (codePropInfo != null && code != null)
      codePropInfo.SetValue(entity, code);

    var result = await handler(entity);
    return RequestResultToApiResponse(result);
  }

  /// <summary>
  ///     切换有效标志（IsValid: 0 ↔ 1）
  ///     URL: POST api/{controller}/toggle-valid
  ///     Body: { "Code": "xxx" }
  ///     返回: { "Code": "xxx", "IsValid": 0/1 }
  /// </summary>
  [HttpPost("toggle-valid")]
  public virtual async Task<ActionResult<ApiResponse<object?>>> ToggleIsValid([FromBody] JsonElement entityData)
  {
    try
    {
      var code = entityData.TryGetProperty("Code", out var codeProp) ? codeProp.GetString() : null;
      if (string.IsNullOrEmpty(code))
        return BadRequest(ApiResponse.Fail("Code 不能为空"));

      // 查询当前实体（不过滤 IsValid）
      var getResult = await Entity.GetByCodeAny(code);
      if (!getResult.Success || getResult.Data == null)
        return BadRequest(ApiResponse.Fail($"记录 {code} 不存在"));

      var entity = getResult.Data;
      var isValidProp = typeof(V).GetProperty("IsValid");
      if (isValidProp == null)
        return BadRequest(ApiResponse.Fail("实体没有 IsValid 字段"));

      var currentVal = (int)(isValidProp.GetValue(entity) ?? 1);
      var newVal = currentVal == 1 ? 0 : 1;
      isValidProp.SetValue(entity, newVal);

      // 执行更新
      var updateResult = await Entity.Update(entity, UserContext.ClientIp);
      if (!updateResult.Success)
        return BadRequest(ApiResponse.Fail(updateResult.Error));

      return Ok(ApiResponse<object?>.Ok(new { Code = code, IsValid = newVal }));
    }
    catch (Exception ex)
    {
      return BadRequest(ApiResponse.Fail($"切换有效标志失败：{ex.Message}"));
    }
  }

  // ========================================================
  // 六、生命周期钩子（子类可覆盖）
  // ========================================================

  #region 查询钩子

  /// <summary>构建过滤条件（覆盖以实现自定义过滤逻辑，/filter 专用）</summary>
  protected virtual List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
  {
    // 默认直接返回，子类可添加全局过滤条件（如租户隔离）
    return filters;
  }

  /// <summary>查询后钩子（字典翻译、字段格式化、脱敏等）</summary>
  protected virtual void OnQueried(PagedResult<V> result) { }

  /// <summary>配置加载后钩子（注入动态配置）</summary>
  protected virtual void OnConfigLoading(EntityConfig config) { }

  #endregion

  #region 新增钩子

  /// <summary>新增前钩子（返回 false 可取消操作）</summary>
  protected virtual Task<(bool ok, string? msg)> OnBeforeAdd(V entity)
      => Task.FromResult<(bool, string?)>((true, null));

  /// <summary>新增后事件（数据已入库，事务内）</summary>
  protected virtual Task OnAfterAdd(V entity) => Task.CompletedTask;

  #endregion

  #region 修改钩子

  /// <summary>修改前钩子（返回 false 可取消操作）</summary>
  protected virtual Task<(bool ok, string? msg)> OnBeforeUpdate(V entity)
      => Task.FromResult<(bool, string?)>((true, null));

  /// <summary>修改后事件（数据已入库，事务内）</summary>
  protected virtual Task OnAfterUpdate(V entity) => Task.CompletedTask;

  #endregion

  #region 删除钩子

  /// <summary>删除前钩子（返回 false 可取消操作）</summary>
  protected virtual Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
      => Task.FromResult<(bool, string?)>((true, null));

  /// <summary>删除后事件</summary>
  protected virtual Task OnAfterDelete(int count) => Task.CompletedTask;

  #endregion

  #region 提交后钩子

  /// <summary>
  ///     提交后钩子（事务已完成，可执行通知/缓存清理等外部操作）
  ///     在 Add/Update/Delete 成功后都会调用
  /// </summary>
  protected virtual Task OnAfterCommitted() => Task.CompletedTask;

  #endregion

  #region 自定义校验

  /// <summary>自定义校验（覆盖以实现复杂业务逻辑校验）</summary>
  protected virtual string? OnCustomValidate(V entity) => null;

  #endregion

  #region 导出/导入钩子

  /// <summary>构建导出数据（覆盖以自定义导出字段和格式）</summary>
  protected virtual async Task<Result<List<V>>> BuildExportData(List<FilterItem> filters)
  {
    try
    {
      var result = await Entity.GetPageAsync(new PagerOptions
      {
        Page = 1,
        PageSize = int.MaxValue,
        Filters = filters
      });

      if (!result.Success) return Result<List<V>>.Fail(result.Error);
      return Result<List<V>>.Ok(result.Data!.Items.ToList());
    }
    catch (Exception ex)
    {
      return Result<List<V>>.Fail($"构建导出数据异常：{ex.Message}");
    }
  }

  /// <summary>处理导入数据（覆盖以自定义解析逻辑）</summary>
  protected virtual async Task<Result<ImportResult>> ProcessImport(List<V> entities)
  {
    var importResult = new ImportResult { TotalRows = entities.Count };
    foreach (var entity in entities)
    {
      var addResult = await AddCore(entity);
      if (addResult.Success) importResult.SuccessRows++;
      else
      {
        importResult.FailedRows++;
        importResult.Errors.Add(new ImportError
        {
          RowIndex = importResult.SuccessRows + importResult.FailedRows,
          Message = addResult.Error ?? "未知错误"
        });
      }
    }
    return Result<ImportResult>.Ok(importResult);
  }

  #endregion

  // ========================================================
  // 七、辅助方法
  // ========================================================

  /// <summary>
  ///     实体校验（基于 EntityConfig 配置的 BCFlag/YXK 自动校验 + 自定义校验）
  ///     返回 (isValid, errorMessage)
  /// </summary>
  protected virtual (bool ok, string? msg) ValidateEntity(V entity)
  {
    foreach (var col in Config.Columns.Where(c => c.BCFlag && !c.YXK))
    {
      var prop = typeof(V).GetProperty(col.FieldName,
          System.Reflection.BindingFlags.IgnoreCase |
          System.Reflection.BindingFlags.Public |
          System.Reflection.BindingFlags.Instance);
      var value = prop?.GetValue(entity);
      if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
        return (false, $"{col.DesName} 不能为空");
    }

    var customErr = OnCustomValidate(entity);
    if (customErr != null) return (false, customErr);

    return (true, null);
  }

  // ==================== ActionResult 转换辅助方法 ====================

  private ActionResult<ApiResponse<TResponse>> RequestResultToActionResult<TResponse>(ApiResponse<TResponse> apiResponse)
  {
    if (apiResponse.Success)
      return Ok(apiResponse);
    return BadRequest(apiResponse);
  }

  private ActionResult<ApiResponse<object?>> RequestResultToApiResponse(Result<ApiResponse<object?>> result)
  {
    if (result.Success)
      return Ok(result.Data!);
    return BadRequest(ApiResponse<object?>.Fail(result.Error!, result.Code ?? 400));
  }
}
