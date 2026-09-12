using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Helpers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Api.Controllers;

/// <summary>
///     左树右表统一控制器基类
///
///     继承体系：
///     ControllerBase (ASP.NET)
///         └── YzhControllerBase&lt;V&gt;        ← 单表 CRUD
///                 └── TreeTableControllerBase&lt;T, V&gt;  ← 左树右表
///
///     泛型：
///     T = 树节点实体（必须实现 ITreeEntity 接口：Code + ParentCode）
///     V = 表格实体（与树节点可以是同一实体，也可以是不同实体）
///
///     核心能力：
///     1. 继承获得全部单表 CRUD（/config /filter /add /update /delete /export /import /action）
///     2. 新增树能力（/tree/root /tree/children /tree/add /tree/update /tree/delete /tree/action）
///     3. 树→表格联动（选中树节点后自动注入过滤条件到 /filter）
///     4. 配置覆盖（/config 返回 TreeTableConfig，包含 TableConfig + TreeConfig）
///     5. 生命周期钩子（单表 6 个 + 树节点 6 个）
///
///     V1 版本：统一 API、TreeItemDto 标准、配置驱动 UI、Split 增量更新
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class TreeTableControllerBase<T, V> : YzhControllerBase<V>
    where T : class, ITreeEntity, new()
    where V : class, new()
{
    // ========================================================
    // 一、属性定义
    // ========================================================

    /// <summary>树节点服务</summary>
    protected EntityService<T> TreeEntity { get; }

    /// <summary>树配置</summary>
    protected TreeConfig TreeConfig { get; set; } = new();

    /// <summary>树节点表单配置文件名（子类设置后自动加载到 TreeTableConfig.TreeFormConfig）</summary>
    protected string? TreeFormConfigName { get; set; }

    // ========================================================
    // 二、构造函数
    // ========================================================

    protected TreeTableControllerBase(
        EntityService<T> treeEntityService,
        EntityService<V> tableEntityService,
        IUserContext userContext)
        : base(tableEntityService, userContext)
    {
        TreeEntity = treeEntityService;
    }

    // ========================================================
    // 三、树数据加载
    // ========================================================

    /// <summary>加载根节点</summary>
    [HttpPost("tree/root")]
    public virtual async Task<ActionResult<ApiResponse<TreeItemDto[]>>> GetRootNodes()
    {
        try
        {
            var items = await TreeEntity.GetRootNodes();
            var dtos = new List<TreeItemDto>();

            foreach (var item in items)
            {
                var dto = MapToTreeItem(item, 0);
                dtos.Add(dto);
            }

            // 批量计算 isLeaf
            await FillIsLeafBatch(dtos);

            return Ok(ApiResponse<TreeItemDto[]>.Ok(dtos.ToArray()));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<TreeItemDto[]>.Fail($"加载根节点失败：{ex.Message}"));
        }
    }

    /// <summary>懒加载子节点（ParentCode 为空时回退加载根节点）</summary>
    [HttpPost("tree/children")]
    public virtual async Task<ActionResult<ApiResponse<TreeItemDto[]>>> GetChildren(
        [FromBody] TreeChildrenRequest request)
    {
        try
        {
            List<T> items;

            if (string.IsNullOrEmpty(request.ParentCode))
            {
                // 容错：ParentCode 为空时回退到加载根节点
                items = await TreeEntity.GetRootNodes();
            }
            else
            {
                items = await TreeEntity.GetChildren(request.ParentCode);
            }

            var dtos = new List<TreeItemDto>();
            var baseLevel = string.IsNullOrEmpty(request.ParentCode) ? 0 : request.Level + 1;

            foreach (var item in items)
            {
                dtos.Add(MapToTreeItem(item, baseLevel));
            }

            // 批量计算 isLeaf
            await FillIsLeafBatch(dtos);

            return Ok(ApiResponse<TreeItemDto[]>.Ok(dtos.ToArray()));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<TreeItemDto[]>.Fail($"加载子节点失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 四、树节点 CRUD
    // ========================================================

    /// <summary>新增树节点</summary>
    [HttpPost("tree/add")]
    public virtual async Task<ActionResult<ApiResponse<TreeItemDto>>> AddTreeNode([FromBody] T entity)
    {
        try
        {
            // 1. 校验
            if (string.IsNullOrEmpty(entity.Code))
                entity.Code = Guid.NewGuid().ToString("N");

            // 2. 新增前钩子（可取消）
            var (ok, cancelMsg) = await OnBeforeAddTree(entity);
            if (!ok) return BadRequest(ApiResponse.Fail(cancelMsg ?? "操作已取消"));

            // 3. 校验父节点存在性
            if (!string.IsNullOrEmpty(entity.ParentCode))
            {
                var parentResult = await TreeEntity.GetByCode(entity.ParentCode);
                if (!parentResult.Success || parentResult.Data == null)
                    return BadRequest(ApiResponse.Fail($"父节点 {entity.ParentCode} 不存在"));
            }

            // 4. 执行新增
            var result = await TreeEntity.Insert(entity, UserContext.ClientIp);
            if (!result.Success) return BadRequest(ApiResponse.Fail(result.Error));

            // 5. 新增后钩子
            await OnAfterAddTree(result.Data!);
            await OnAfterCommitted();

            // 6. 返回 DTO
            return Ok(ApiResponse<TreeItemDto>.Ok(MapToTreeItem(result.Data!, 0), "创建成功"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"新增树节点失败：{ex.Message}"));
        }
    }

    /// <summary>修改树节点</summary>
    [HttpPost("tree/update")]
    public virtual async Task<ActionResult<ApiResponse<TreeItemDto>>> UpdateTreeNode([FromBody] T entity)
    {
        try
        {
            // 1. 修改前钩子（可取消）
            var (ok, cancelMsg) = await OnBeforeUpdateTree(entity);
            if (!ok) return BadRequest(ApiResponse.Fail(cancelMsg ?? "操作已取消"));

            // 2. 防环校验：不能将 parentCode 改为自身或后代
            if (!string.IsNullOrEmpty(entity.ParentCode))
            {
                if (entity.ParentCode == entity.Code)
                    return BadRequest(ApiResponse.Fail("不能将父节点设为自己"));

                var descendants = await GetDescendantCodes(entity.Code);
                if (descendants.Contains(entity.ParentCode))
                    return BadRequest(ApiResponse.Fail("不能将父节点设为自己的后代（会形成循环）"));
            }

            // 3. 执行修改
            var result = await TreeEntity.Update(entity, UserContext.ClientIp);
            if (!result.Success) return BadRequest(ApiResponse.Fail(result.Error));

            // 4. 修改后钩子
            await OnAfterUpdateTree(result.Data!);
            await OnAfterCommitted();

            return Ok(ApiResponse<TreeItemDto>.Ok(MapToTreeItem(result.Data!, 0), "修改成功"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"修改树节点失败：{ex.Message}"));
        }
    }

    /// <summary>删除树节点</summary>
    [HttpPost("tree/delete")]
    public virtual async Task<ActionResult<ApiResponse<string>>> DeleteTreeNode([FromBody] string[] codes)
    {
        try
        {
            if (codes == null || codes.Length == 0)
                return BadRequest(ApiResponse.Fail("未指定要删除的节点"));

            // 1. 删除前钩子（可取消）
            var (ok, cancelMsg) = await OnBeforeDeleteTree(codes);
            if (!ok) return BadRequest(ApiResponse.Fail(cancelMsg ?? "操作已取消"));

            // 2. 检查是否有子节点
            if (!TreeConfig.AllowDeleteWithChildren)
            {
                foreach (var code in codes)
                {
                    var childCount = await TreeEntity.GetChildrenCount(code);
                    if (childCount > 0)
                        return BadRequest(ApiResponse.Fail($"该节点下有 {childCount} 个子节点，请先删除子节点"));
                }
            }

            // 3. 收集所有子孙 code（级联删除）
            var allCodes = new List<string>(codes);
            foreach (var code in codes)
            {
                var descendants = await GetDescendantCodes(code);
                allCodes.AddRange(descendants);
            }
            allCodes = allCodes.Distinct().ToList();

            // 4. 执行删除
            var result = await TreeEntity.DeleteBatch(allCodes, hardDelete: HardDelete, clientIp: UserContext.ClientIp);
            if (!result.Success) return BadRequest(ApiResponse.Fail(result.Error));

            // 5. 删除后钩子
            await OnAfterDeleteTree(result.Data);
            await OnAfterCommitted();

            return Ok(ApiResponse<string>.Ok($"已删除 {result.Data} 个节点（含子节点）"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"删除树节点失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 五、树节点自定义操作
    // ========================================================

    /// <summary>
    ///     树节点操作注册字典
    ///     Key: 方法名（小写）
    ///     Value: 处理函数（接收树节点实体，返回操作结果）
    /// </summary>
    private readonly Dictionary<string, Func<T, Task<object?>>> _treeActions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册树节点自定义操作</summary>
    protected void RegisterTreeAction(string methodName, Func<T, Task<object?>> handler)
    {
        _treeActions[methodName.ToLowerInvariant()] = handler;
    }

    /// <summary>统一树节点操作入口</summary>
    [HttpPost("tree/action/{methodName}")]
    public virtual async Task<ActionResult<ApiResponse<object?>>> ExecuteTreeAction(
        string methodName, [FromBody] JsonElement entityData)
    {
        if (string.IsNullOrEmpty(methodName))
            return BadRequest(ApiResponse.Fail("操作名称不能为空"));

        if (!_treeActions.TryGetValue(methodName.ToLowerInvariant(), out var handler))
            return BadRequest(ApiResponse.Fail($"树操作 [{methodName}] 未注册，请检查 RegisterTreeAction 调用"));

        // 仅传递 Code 属性到处理函数（避免 [Required] 校验失败）
        var code = entityData.TryGetProperty("Code", out var codeProp) ? codeProp.GetString() : null;
        var entity = new T();
        var codePropInfo = typeof(T).GetProperty("Code");
        if (codePropInfo != null && code != null)
            codePropInfo.SetValue(entity, code);

        var result = await handler(entity);
        return Ok(ApiResponse<object?>.Ok(result));
    }

    /// <summary>
    ///     切换树节点有效标志（IsValid: 0 ↔ 1）
    ///     URL: POST api/{controller}/tree/toggle-valid
    ///     Body: { "Code": "xxx" }
    ///     返回: { "Code": "xxx", "IsValid": 0/1 }
    /// </summary>
    [HttpPost("tree/toggle-valid")]
    public virtual async Task<ActionResult<ApiResponse<object?>>> ToggleTreeNodeIsValid([FromBody] JsonElement entityData)
    {
        try
        {
            var code = entityData.TryGetProperty("Code", out var codeProp) ? codeProp.GetString() : null;
            if (string.IsNullOrEmpty(code))
                return BadRequest(ApiResponse.Fail("Code 不能为空"));

            // 查询当前实体（不过滤 IsValid）
            var getResult = await TreeEntity.GetByCodeAny(code);
            if (!getResult.Success || getResult.Data == null)
                return BadRequest(ApiResponse.Fail($"节点 {code} 不存在"));

            var entity = getResult.Data;
            var isValidProp = typeof(T).GetProperty("IsValid");
            if (isValidProp == null)
                return BadRequest(ApiResponse.Fail("树节点实体没有 IsValid 字段"));

            var currentVal = (int)(isValidProp.GetValue(entity) ?? 1);
            var newVal = currentVal == 1 ? 0 : 1;
            isValidProp.SetValue(entity, newVal);

            // 执行更新
            var updateResult = await TreeEntity.Update(entity, UserContext.ClientIp);
            if (!updateResult.Success)
                return BadRequest(ApiResponse.Fail(updateResult.Error));

            return Ok(ApiResponse<object?>.Ok(new { Code = code, IsValid = newVal }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"切换树节点有效标志失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 六、树→表格联动
    // ========================================================

    /// <summary>覆盖 Filter → 自动注入树节点过滤条件</summary>
    [NonAction]
    public override async Task<Result<PagedResult<V>>> FilterCore(FilterRequest request)
    {
        // 从前端传入的条件中查找树关联字段
        var treeFilter = request.Filters?
            .FirstOrDefault(f => f.Field == TreeConfig.RelateField);

        if (treeFilter != null)
        {
            // 选中节点 → 注入过滤条件（替换或添加）
            var otherFilters = request.Filters?
                .Where(f => f.Field != TreeConfig.RelateField)
                .ToList() ?? new List<FilterItem>();

            // 确保树过滤条件存在
            if (!otherFilters.Any(f => f.Field == TreeConfig.RelateField))
            {
                otherFilters.Add(new FilterItem
                {
                    Field = TreeConfig.RelateField,
                    Operator = "eq",
                    Value = treeFilter.Value
                });
            }

            request.Filters = otherFilters;
        }
        else
        {
            // 未选中节点 → 按 NoSelectionBehavior 决定
            if (TreeConfig.NoSelectionBehavior == "empty")
            {
                return Result<PagedResult<V>>.Ok(new PagedResult<V>(
                    Array.Empty<V>(), 0, request.Page, request.PageSize));
            }
            // "all" → 不过滤，返回全部
        }

        return await base.FilterCore(request);
    }

    // ========================================================
    // 七、配置获取
    // ========================================================

    /// <summary>覆盖 /config → 返回 TreeTableConfigDto（前端 DTO，camelCase）</summary>
    /// <remarks>
    /// 注意：不能使用 [HttpGet("config")]，因为与基类 virtual 方法冲突，
    /// ASP.NET Core 会优先选择基类的 virtual 方法。
    /// 改用 [ActionName] + 独立路由，前端通过 /api/{controller}/treepconfig 访问。
    /// </remarks>
    [HttpGet("treepconfig")]
    [ActionName("treepconfig")]
    public ActionResult<ApiResponse<TreeTableConfigDto>> GetTreeTableConfig()
    {
        var tableConfig = GetConfigCore();
        if (!tableConfig.Success)
            return BadRequest(ApiResponse<TreeTableConfigDto>.Fail(tableConfig.Error!));

        var treeFormConfig = LoadTreeFormConfig();

        // 自动注入已注册的 RowActions 到 TableConfig.RowButtons
        var tableConfigDto = ConvertToDto(tableConfig.Data!);
        InjectRowActionsToConfig(tableConfigDto);

        // 自动注入已注册的 TreeActions 到 TreeConfig.CustomActions
        var treeConfigDto = ConvertTreeConfigToDto(TreeConfig);
        InjectTreeActionsToConfig(treeConfigDto);

        var result = new TreeTableConfigDto
        {
            TableConfig = tableConfigDto,
            TreeConfig = treeConfigDto,
            TreeFormConfig = treeFormConfig != null ? ConvertToDto(treeFormConfig) : null
        };

        return Ok(ApiResponse<TreeTableConfigDto>.Ok(result));
    }

    /// <summary>自动注入已注册的 RowActions 到 Config.RowButtons.CustomButtons</summary>
    private void InjectRowActionsToConfig(EntityConfigDto dto)
    {
        if (_rowActions.Count == 0) return;

        dto.RowButtons ??= new RowButtonConfigDto();
        if (dto.RowButtons.CustomButtons == null)
            dto.RowButtons.CustomButtons = new Dictionary<string, string>();

        // 内置按钮映射
        var builtInMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "disable", "禁用" },
            { "enable", "启用" }
        };

        foreach (var actionName in _rowActions.Keys)
        {
            if (!dto.RowButtons.CustomButtons.ContainsKey(actionName))
            {
                dto.RowButtons.CustomButtons[actionName] =
                    builtInMap.GetValueOrDefault(actionName, actionName);
            }
        }
    }

    /// <summary>自动注入已注册的 TreeActions 到 TreeConfig.CustomActions</summary>
    private void InjectTreeActionsToConfig(TreeBehaviorConfigDto dto)
    {
        if (_treeActions.Count == 0) return;

        dto.CustomActions = new Dictionary<string, string>();

        // 内置按钮映射
        var builtInMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "disable", "禁用" },
            { "enable", "启用" }
        };

        foreach (var actionName in _treeActions.Keys)
        {
            dto.CustomActions[actionName] =
                builtInMap.GetValueOrDefault(actionName, actionName);
        }
    }

    /// <summary>将后端 EntityConfig 转换为前端 EntityConfigDto</summary>
    private EntityConfigDto ConvertToDto(EntityConfig config)
    {
        var dto = new EntityConfigDto
        {
            Title = config.Title,
            FillMode = config.FillMode.ToString(),
            Columns = config.Columns?.Select(c => new ColumnConfigDto
            {
                FieldName = c.FieldName,
                DesName = c.DesName,
                Type = c.Type.ToString(),
                XsFlag = c.XSFlag,
                BcFlag = c.BCFlag,
                Yxk = c.YXK,
                Enable = c.Enable,
                Sortable = c.Sortable,
                Width = c.Width > 0 ? (int)c.Width : null,
                Fixed = c.Fixed,
                Align = c.Align,
                DictCode = c.DictCode,
                Format = c.Format,
                Row = c.Row,
                Col = c.Col,
                RowSpan = c.RowSpan,
                ColSpan = c.ColSpan,
                Mrz = c.MRZ
            }).ToList() ?? new(),
            NewEntity = config.NewEntity,
            Schema = config.Schema,
            EnableField = config.EnableField
        };
        return dto;
    }

    /// <summary>将后端 TreeConfig 转换为前端 TreeBehaviorConfigDto</summary>
    private TreeBehaviorConfigDto ConvertTreeConfigToDto(TreeConfig config)
    {
        return new TreeBehaviorConfigDto
        {
            Lazy = config.Lazy,
            AllowEdit = config.AllowEdit,
            AllowDelete = config.AllowDelete,
            AllowRename = config.AllowRename,
            RootParentCode = config.RootParentCode,
            NameField = config.NameField,
            CodeField = config.CodeField,
            ParentCodeField = config.ParentCodeField,
            RelateField = config.RelateField,
            NoSelectionBehavior = config.NoSelectionBehavior,
            MaxLevel = config.MaxLevel,
            AllowDeleteWithChildren = config.AllowDeleteWithChildren,
            EnableField = config.EnableField
        };
    }

    /// <summary>加载树节点表单配置（如果子类指定了文件名）</summary>
    private EntityConfig? LoadTreeFormConfig()
    {
        if (string.IsNullOrEmpty(TreeFormConfigName))
            return null;
        var config = EntityConfigHelper.GetConfig(TreeFormConfigName);
        // 反射树节点实体类型 T，注入空实体模板
        config.NewEntity = EntitySchemaHelper.GetEmptyEntity<T>();
        config.Schema = EntitySchemaHelper.GetSchema<T>();
        return config;
    }

    // ========================================================
    // 八、生命周期钩子（子类可覆盖）
    // ========================================================

    #region 树节点钩子

    /// <summary>树节点新增前（校验/数据填充/可取消）</summary>
    protected virtual Task<(bool ok, string? msg)> OnBeforeAddTree(T entity)
        => Task.FromResult<(bool, string?)>((true, null));

    /// <summary>树节点新增后（数据已入库，事务内）</summary>
    protected virtual Task OnAfterAddTree(T entity) => Task.CompletedTask;

    /// <summary>树节点修改前（校验/可取消）</summary>
    protected virtual Task<(bool ok, string? msg)> OnBeforeUpdateTree(T entity)
        => Task.FromResult<(bool, string?)>((true, null));

    /// <summary>树节点修改后（数据已入库，事务内）</summary>
    protected virtual Task OnAfterUpdateTree(T entity) => Task.CompletedTask;

    /// <summary>树节点删除前（可检查关联数据/级联/可取消）</summary>
    protected virtual Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
        => Task.FromResult<(bool, string?)>((true, null));

    /// <summary>树节点删除后</summary>
    protected virtual Task OnAfterDeleteTree(int count) => Task.CompletedTask;

    #endregion

    // ========================================================
    // 九、辅助方法
    // ========================================================

    /// <summary>实体 → TreeItemDto 映射（使用共享 TreeMapper 工具）</summary>
    protected virtual TreeItemDto MapToTreeItem(T entity, int level)
    {
        var mapping = TreeFieldMapping.FromTreeConfig(TreeConfig);
        return Helpers.TreeMapper.MapToTreeItem(entity, level, mapping);
    }

    /// <summary>批量填充 isLeaf 字段</summary>
    protected virtual async Task FillIsLeafBatch(List<TreeItemDto> dtos)
    {
        if (dtos == null || dtos.Count == 0) return;

        try
        {
            var codes = dtos.Select(d => d.Code).Distinct().ToList();
            var childrenCount = await TreeEntity.GetChildrenCountBatch(codes);

            foreach (var dto in dtos)
            {
                dto.IsLeaf = !childrenCount.ContainsKey(dto.Code) || childrenCount[dto.Code] == 0;
            }
        }
        catch
        {
            foreach (var dto in dtos)
            {
                dto.IsLeaf = false;
            }
        }
    }

    /// <summary>获取指定节点的所有子孙 code（递归）</summary>
    protected virtual async Task<List<string>> GetDescendantCodes(string code)
    {
        var result = new List<string>();
        await CollectDescendants(code, result);
        return result;
    }

    private async Task CollectDescendants(string parentCode, List<string> result)
    {
        try
        {
            var children = await TreeEntity.GetChildren(parentCode);
            foreach (var child in children)
            {
                result.Add(child.Code);
                await CollectDescendants(child.Code, result);
            }
        }
        catch { /* 忽略递归错误 */ }
    }

    // ========================================================
    // 四、树形表格选择器（通用虚方法）
    //     适用场景：左侧选树节点，右侧勾选关联实体
    //     子类重写这 3 个方法即可实现角色-用户、角色-菜单等场景
    // ========================================================

    /// <summary>
    /// 获取混合树数据（含勾选状态）
    /// 子类重写：根据业务场景返回不同的混合树节点
    /// </summary>
    [HttpPost("checkTree")]
    public virtual async Task<ActionResult<ApiResponse<CheckTreeNodeDto[]>>> GetCheckTree(
        [FromBody] CheckTreeRequest request)
    {
        return NotFound(ApiResponse<CheckTreeNodeDto[]>.Fail("当前控制器未实现 GetCheckTree"));
    }

    /// <summary>
    /// 勾选保存（增加关联）
    /// 子类重写：根据业务场景处理关联写入
    /// </summary>
    [HttpPost("check/add")]
    public virtual async Task<ActionResult<ApiResponse<object?>>> CheckAdd(
        [FromBody] CheckActionRequest request)
    {
        return NotFound(ApiResponse<object?>.Fail("当前控制器未实现 CheckAdd"));
    }

    /// <summary>
    /// 取消勾选保存（移除关联）
    /// 子类重写：根据业务场景处理关联删除
    /// </summary>
    [HttpPost("check/remove")]
    public virtual async Task<ActionResult<ApiResponse<object?>>> CheckRemove(
        [FromBody] CheckActionRequest request)
    {
        return NotFound(ApiResponse<object?>.Fail("当前控制器未实现 CheckRemove"));
    }

    /// <summary>
    /// 获取所有关联关系（用于前端本地缓存，减少切换时的网络请求）
    /// 子类重写：返回当前控制器的全部关联对（如角色-用户、角色-菜单等）
    /// 前端页面加载时调用一次，后续切换左侧节点时直接从本地缓存计算 CheckFlag
    /// </summary>
    [HttpPost("check/all")]
    public virtual async Task<ActionResult<ApiResponse<AssociationDto[]>>> GetAllAssociations()
    {
        return NotFound(ApiResponse<AssociationDto[]>.Fail("当前控制器未实现 GetAllAssociations"));
    }

}
