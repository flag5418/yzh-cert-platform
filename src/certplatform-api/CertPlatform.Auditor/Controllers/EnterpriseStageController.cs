using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Shared.Entities.Cert;

using Ent = CertPlatform.Shared.Entities.Cert.Enterprise;
using Stage = CertPlatform.Shared.Entities.Cert.CertStage;
using Std = CertPlatform.Shared.Entities.Cert.ISOStandard;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 企业-阶段-标准关联控制器（专家端 —— 专家平台第 2 个业务功能）
///
/// <para><b>交互形态 = core 的「关联型（勾选授权）」范式（AS-1 / AS-2）</b>：
/// 左树 = 本工作区的企业；右侧 = 「阶段 → 标准」勾选树表，<b>勾选即关联、取消即解除</b>。
/// 前端对应 <c>cert-auditor/src/pages/enterprise-stages/</c>
/// （<c>CheckTreeCore</c> + <c>YzhTree</c> + <c>YzhTreeTableCheckSelector</c>），
/// 与 <c>pages/system/role-api/</c> 同构。</para>
///
/// <para><b>端点契约</b>（逐字对齐 <c>TreeTableControllerBase</c> 的「四、树形表格选择器」一节，
/// 亦即前端 <c>AssociationApi</c> 的 6 个方法）：</para>
/// <list type="table">
///   <item><term>POST tree/root</term><description>左树根节点（本工作区全部有效企业）</description></item>
///   <item><term>POST tree/children</term><description>左树子节点 —— 本页为<b>扁平树</b>，恒返回空</description></item>
///   <item><term>POST checkTree</term><description>右侧「阶段→标准」<b>扁平</b>列表（含 ParentCode / CheckFlag）</description></item>
///   <item><term>POST check/add</term><description>批量建关联（幂等；既有无效行走「复活」而非 INSERT）</description></item>
///   <item><term>POST check/remove</term><description>批量解除关联（<b>物理删除</b>）</description></item>
///   <item><term>POST check/all</term><description>本工作区全部关联对（前端本地缓存 / 左树 badge）</description></item>
/// </list>
///
/// <para><b>为什么继承 <see cref="YzhControllerBase{T}"/> 而不是
/// <c>TreeTableControllerBase&lt;T, V&gt;</c></b>：后者的左树实体 <c>T</c> 必须实现
/// <c>ITreeEntity</c>（含 <c>ParentCode</c>），而左树实体是 <see cref="Ent"/> —— 一张
/// <b>扁平业务表</b>，没有 <c>ParentCode</c>。若为了套基类给它加接口 + 忽略列，
/// 会牵动已在运行的「企业档案」页，且可能触发启动期命名规则校验。
/// 故此处<b>按行为契约手写</b>同名同形的 5 个端点（判据见
/// <c>docs/10-YZH架构/样板页面指南-V1.md</c> §7.1：合规看行为契约，不看 <c>:</c> 后面写了什么）。</para>
///
/// <para><b>两条硬约束</b>：</para>
/// <list type="number">
///   <item><b>工作区隔离</b>：本表无 <c>OrgCode</c> 列，隔离走「本工作区的企业集合」——
///         见 <see cref="OnBuildingFilter"/>；每个写端点先校验企业归属。</item>
///   <item><b>解除关联 = 物理删除</b>：实体标注
///         <c>[YZHDeleteStrategy(Mode = DeleteMode.Hard)]</c>，
///         由 <c>EntityService.DeleteByCode</c> 读取该特性后走硬删。</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class EnterpriseStageController : YzhControllerBase<CertEnterpriseStage>
{
    /// <summary>工作区无企业时的「不可能命中」哨兵值（避免 IN () 语法错误）</summary>
    private const string NoEnterpriseSentinel = "__NO_ENTERPRISE__";

    /// <summary>阶段节点 Code 前缀</summary>
    private const string StagePrefix = "STAGE:";

    /// <summary>标准节点 Code 前缀</summary>
    private const string StdPrefix = "STD:";

    /// <summary>「阶段 × 标准」合成键的分隔符</summary>
    private const char CodeSeparator = '|';

    /// <summary>右侧节点类型：阶段（分组，不可勾选）</summary>
    private const string NodeTypeStage = "stage";

    /// <summary>右侧节点类型：标准（叶子，可勾选）</summary>
    private const string NodeTypeStandard = "standard";

    private readonly EntityService<Ent> _entService;
    private readonly EntityService<Stage> _stageService;
    private readonly EntityService<Std> _stdService;
    private readonly IDbOrm _db;
    private readonly WorkspaceContextService _workspace;

    public EnterpriseStageController(
        EntityService<CertEnterpriseStage> entityService,
        EntityService<Ent> entService,
        EntityService<Stage> stageService,
        EntityService<Std> stdService,
        IDbOrm db,
        IUserContext userContext,
        WorkspaceContextService workspace)
        : base(entityService, userContext)
    {
        _entService = entService;
        _stageService = stageService;
        _stdService = stdService;
        _db = db;
        _workspace = workspace;
    }

    /// <summary>缺 EntityConfig 时直接抛错（开发期暴露，避免「页面空白且零报错」）</summary>
    protected override bool StrictConfigLoad => true;

    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<CertEnterpriseStage>();
    }

    // ========================================================
    // 一、工作区隔离（基类 filter/export 等端点共用）
    // ========================================================

    /// <summary>
    /// 构建过滤条件：收敛到「本工作区的企业」。
    ///
    /// <para><b>为什么不是 <c>OrgCode</c> 等值</b>：<c>cert_enterprise_stage</c> 没有
    /// <c>OrgCode</c> 列 —— 归属信息在 <c>cert_enterprise.OrgCode</c> 上。故先取本工作区
    /// 的企业 Code 集合，再用 <c>in</c> 收敛。</para>
    ///
    /// <para>解析失败时**不静默放行** —— 抛错，避免越权看到全部工作区的数据。</para>
    /// </summary>
    protected override List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
    {
        filters = base.OnBuildingFilter(filters);

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            throw new InvalidOperationException(ws.Error ?? "无法定位当前工作区");

        filters.RemoveAll(f => f.Field == "EnterpriseCode");
        filters.Add(new FilterItem
        {
            Field = "EnterpriseCode",
            Operator = "in",
            Value = WorkspaceEnterpriseCodes(ws.Data.Code)
        });

        return filters;
    }

    // ========================================================
    // 二、左树（企业）
    // ========================================================

    /// <summary>
    /// 左树根节点 —— 本工作区全部有效企业。
    /// <para>POST <c>api/Auditor/EnterpriseStage/tree/root</c></para>
    /// <para>返回 <c>TreeNode</c> 形状（<c>Code / Name / ParentCode / NodeType / IsLeaf / Extra</c>），
    /// 直接喂给 <c>YzhTree</c> 与 <c>AssociationTreeCore.treeData</c>。</para>
    /// </summary>
    [HttpPost("tree/root")]
    public async Task<ActionResult<ApiResponse<object?>>> TreeRoot()
    {
        try
        {
            var ws = _workspace.Resolve(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse.Fail(ws.Error ?? "无法定位当前工作区"));

            var list = await _entService.GetListAsync(p =>
                p.OrgCode == ws.Data.Code && p.IsValid == 1 && !p.IsDeleted);
            if (!list.Success)
                return Ok(ApiResponse.Fail(list.Error));

            var nodes = list.Data!
                .OrderBy(p => p.Sort)
                .ThenBy(p => p.EnterpriseNo)
                .Select(p => new
                {
                    Code = p.Code,
                    Name = p.Name,
                    ParentCode = (string?)null,
                    NodeType = "enterprise",
                    IsLeaf = true,
                    Extra = new { EnterpriseNo = p.EnterpriseNo }
                })
                .ToList();

            return Ok(ApiResponse<object?>.Ok(nodes));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 左树子节点 —— 本页左树是**扁平**的企业列表（<c>IsLeaf = true</c>），
    /// 前端 <c>:lazy="false"</c> 不会调用。保留端点以完整实现 <c>AssociationApi</c> 契约，
    /// 恒返回空数组（而非 404），避免将来误开懒加载时「静默无数据」。
    /// </summary>
    [HttpPost("tree/children")]
    public ActionResult<ApiResponse<object?>> TreeChildren()
    {
        return Ok(ApiResponse<object?>.Ok(new List<object>()));
    }

    // ========================================================
    // 三、右侧「阶段 → 标准」勾选树
    // ========================================================

    /// <summary>
    /// 右侧「阶段 → 标准」**扁平**列表（含 <c>ParentCode</c> / <c>CheckFlag</c>）。
    ///
    /// <para><b>为什么是扁平而不是嵌套</b>：<c>YzhTreeTableCheckSelector</c> 内部自己
    /// <c>flatToTree</c>，且差集勾选以 <c>Code</c> 为键 —— 扁平 + <c>ParentCode</c> 是组件契约。</para>
    ///
    /// <para><b>叶子 Code = <c>STAGE:{阶段码}|STD:{标准Code}</c></b>（前端合成键，<b>非 DB 主键</b>）：
    /// 勾选链路只会回传 <c>{ Code, NodeType }</c>（见 <c>AssociationTreeCore.buildSelections</c>），
    /// 所以「阶段 × 标准」这个二元组必须编码进 <c>Code</c>。</para>
    ///
    /// <para>展示字段（标准编号 / 版本年 / 类别）放在 <c>Extra</c> 内 ——
    /// 前端 <c>handleNodeSelect</c> 会把 <c>Extra</c> 平铺到节点上，列可直接绑 <c>StandardNo</c> 等。</para>
    ///
    /// <para>POST <c>api/Auditor/EnterpriseStage/checkTree</c>，body <c>{ ContextCode }</c>（= 企业 Code）</para>
    /// </summary>
    [HttpPost("checkTree")]
    public async Task<ActionResult<ApiResponse<object?>>> CheckTree([FromBody] ContextRequest request)
    {
        try
        {
            var ws = _workspace.Resolve(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse.Fail(ws.Error ?? "无法定位当前工作区"));

            var err = await EnsureOwnedEnterpriseAsync(request.ContextCode, ws.Data.Code);
            if (err != null)
                return Ok(ApiResponse.Fail(err));

            // 阶段（统一源：cert_cert_stage，与「认证阶段定义」页同源）
            var stages = await _stageService.GetListAsync(p => p.IsValid == 1 && !p.IsDeleted);
            if (!stages.Success)
                return Ok(ApiResponse.Fail(stages.Error));

            // 标准（cert_iso_standard）
            var stds = await _stdService.GetListAsync(p => p.IsValid == 1 && !p.IsDeleted);
            if (!stds.Success)
                return Ok(ApiResponse.Fail(stds.Error));

            // 该企业已关联的「阶段 × 标准」组合
            var linked = await Entity.GetListAsync(p =>
                p.EnterpriseCode == request.ContextCode && p.IsValid == 1 && !p.IsDeleted);
            var linkedKeys = new HashSet<string>(
                (linked.Data ?? new List<CertEnterpriseStage>())
                    .Select(p => BuildStandardNodeCode(p.StageCode, p.StandardCode)));

            var stdList = stds.Data!
                .OrderBy(p => p.Sort)
                .ThenBy(p => p.StandardCode)
                .ToList();

            var nodes = new List<object>();

            foreach (var st in stages.Data!.OrderBy(p => p.SortOrder).ThenBy(p => p.StageCode))
            {
                var stageNodeCode = BuildStageNodeCode(st.StageCode);

                var children = stdList.Select(sd =>
                {
                    var leafCode = BuildStandardNodeCode(st.StageCode, sd.Code);
                    return new
                    {
                        Code = leafCode,
                        Name = sd.StandardName,
                        ParentCode = (string?)stageNodeCode,
                        NodeType = NodeTypeStandard,
                        CheckFlag = linkedKeys.Contains(leafCode),
                        Extra = new
                        {
                            StageCode = st.StageCode,
                            StageName = st.StageName,
                            StandardCode = sd.Code,
                            StandardNo = sd.StandardCode,
                            StandardName = sd.StandardName,
                            VersionYear = sd.VersionYear,
                            Category = sd.Category
                        }
                    };
                }).ToList();

                // 阶段行只是分组：本身不可勾选（前端 selectableNodeTypes=['standard']），
                // CheckFlag 由前端 afterAssociationsLoaded 按子级回填
                nodes.Add(new
                {
                    Code = stageNodeCode,
                    Name = st.StageName,
                    ParentCode = (string?)null,
                    NodeType = NodeTypeStage,
                    CheckFlag = false,
                    Extra = new
                    {
                        StageCode = st.StageCode,
                        StageName = st.StageName,
                        SortOrder = st.SortOrder,
                        ChildCount = children.Count
                    }
                });

                nodes.AddRange(children);
            }

            return Ok(ApiResponse<object?>.Ok(nodes));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 四、勾选增删（批量）
    // ========================================================

    /// <summary>
    /// 批量建关联（幂等）。
    ///
    /// <para>POST <c>api/Auditor/EnterpriseStage/check/add</c>，
    /// body <c>{ ContextCode, Selections: [{ Code, NodeType }] }</c></para>
    ///
    /// <para>返回 <c>{ Updated, Applied }</c> —— <c>Applied</c> 是服务端确认「现已关联」的节点 Code 集合，
    /// 供前端局部更新本地缓存（与 <c>RoleApiController</c> 同形）。</para>
    /// </summary>
    [HttpPost("check/add")]
    public async Task<ActionResult<ApiResponse<object?>>> CheckAdd([FromBody] CheckActionRequest request)
    {
        try
        {
            var ws = _workspace.Resolve(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse.Fail(ws.Error ?? "无法定位当前工作区"));

            var err = await EnsureOwnedEnterpriseAsync(request.ContextCode, ws.Data.Code);
            if (err != null)
                return Ok(ApiResponse.Fail(err));

            var parsed = ParseSelections(request.Selections);
            if (parsed.Count == 0)
                return Ok(ApiResponse.Fail("未指定有效的关联项"));

            // 阶段 / 标准存在性校验（防悬空关联）。
            // 两张基础表都很小 → 全量取回建 HashSet，避免在 SqlSugar 表达式里写 Contains。
            var stages = await _stageService.GetListAsync(p => p.IsValid == 1 && !p.IsDeleted);
            if (!stages.Success)
                return Ok(ApiResponse.Fail(stages.Error));
            var validStages = new HashSet<string>(stages.Data!.Select(p => p.StageCode));

            var stds = await _stdService.GetListAsync(p => p.IsValid == 1 && !p.IsDeleted);
            if (!stds.Success)
                return Ok(ApiResponse.Fail(stds.Error));
            var validStds = new HashSet<string>(stds.Data!.Select(p => p.Code));

            var applied = new List<string>();
            var inserted = 0;

            foreach (var item in parsed)
            {
                if (!validStages.Contains(item.StageCode)) continue;
                if (!validStds.Contains(item.StandardCode)) continue;

                var nodeCode = BuildStandardNodeCode(item.StageCode, item.StandardCode);

                // ★ 必须 includeDisabled: true —— GetListAsync 默认 includeDisabled=false
                // 会自动追加 IsValid=1（SqlSugarDbOrm.cs:81）。表上有唯一键 uk_ent_stage_std，
                // 若已存在一行 IsValid=0 却查不出来 → 落到 INSERT → 撞唯一键报错（ERROR 1062）。
                var existing = await Entity.GetListAsync(p =>
                    p.EnterpriseCode == request.ContextCode
                    && p.StageCode == item.StageCode
                    && p.StandardCode == item.StandardCode, includeDisabled: true);

                if (existing.Success && existing.Data != null && existing.Data.Count > 0)
                {
                    // 已存在（可能被 toggle-valid 置为无效）→ 复活该行，而不是重复插入
                    foreach (var row in existing.Data.Where(p => p.IsValid != 1))
                    {
                        row.IsValid = 1;
                        row.UpdateBy = UserContext.UserCode;
                        row.UpdateTime = DateTime.UtcNow;
                        var revived = await Entity.Update(row, UserContext.ClientIp);
                        if (!revived.Success)
                            return Ok(ApiResponse.Fail(revived.Error));
                    }

                    applied.Add(nodeCode);
                    continue;
                }

                var entity = new CertEnterpriseStage
                {
                    Code = Guid.NewGuid().ToString("N"),
                    EnterpriseCode = request.ContextCode,
                    StageCode = item.StageCode,
                    StandardCode = item.StandardCode,
                    Status = "none",
                    IsValid = 1,
                    CreateBy = UserContext.UserCode,
                    CreateTime = DateTime.UtcNow
                };

                var ins = await Entity.Insert(entity, UserContext.ClientIp);
                if (!ins.Success)
                    return Ok(ApiResponse.Fail(ins.Error));

                inserted++;
                applied.Add(nodeCode);
            }

            return Ok(ApiResponse<object?>.Ok(new { Updated = inserted, Applied = applied }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 批量解除关联（**物理删除** —— 实体带 <c>[YZHDeleteStrategy(Mode = DeleteMode.Hard)]</c>）。
    ///
    /// <para>POST <c>api/Auditor/EnterpriseStage/check/remove</c>，
    /// body <c>{ ContextCode, Selections: [{ Code, NodeType }] }</c></para>
    ///
    /// <para>此处**不校验阶段/标准是否存在** —— 阶段或标准被停用后，既有脏关联仍须能被清理。</para>
    /// </summary>
    [HttpPost("check/remove")]
    public async Task<ActionResult<ApiResponse<object?>>> CheckRemove([FromBody] CheckActionRequest request)
    {
        try
        {
            var ws = _workspace.Resolve(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse.Fail(ws.Error ?? "无法定位当前工作区"));

            var err = await EnsureOwnedEnterpriseAsync(request.ContextCode, ws.Data.Code);
            if (err != null)
                return Ok(ApiResponse.Fail(err));

            var parsed = ParseSelections(request.Selections);
            if (parsed.Count == 0)
                return Ok(ApiResponse<object?>.Ok(new { Updated = 0 }));

            var removed = 0;

            foreach (var item in parsed)
            {
                // includeDisabled: true —— 禁用态的脏关联也必须能被清理
                var rows = await Entity.GetListAsync(p =>
                    p.EnterpriseCode == request.ContextCode
                    && p.StageCode == item.StageCode
                    && p.StandardCode == item.StandardCode, includeDisabled: true);
                if (!rows.Success || rows.Data == null) continue;

                foreach (var row in rows.Data)
                {
                    var deleted = await Entity.DeleteByCode(row.Code, UserContext.ClientIp);
                    if (!deleted.Success)
                        return Ok(ApiResponse.Fail(deleted.Error));
                    removed++;
                }
            }

            return Ok(ApiResponse<object?>.Ok(new { Updated = removed }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 本工作区全部关联对（前端本地缓存初始化 → 左树 badge 计数、切换企业时无需重取）。
    ///
    /// <para>POST <c>api/Auditor/EnterpriseStage/check/all</c>（无 body）</para>
    ///
    /// <para>返回 <c>[{ ContextCode = 企业 Code, TargetCode = 叶子合成键, NodeType }]</c>。</para>
    /// </summary>
    [HttpPost("check/all")]
    public ActionResult<ApiResponse<object?>> CheckAll()
    {
        try
        {
            var ws = _workspace.Resolve(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse.Fail(ws.Error ?? "无法定位当前工作区"));

            var codes = WorkspaceEnterpriseCodes(ws.Data.Code);

            var rows = _db.Client.Queryable<CertEnterpriseStage>()
                .Where(p => codes.Contains(p.EnterpriseCode) && p.IsValid == 1 && !p.IsDeleted)
                .ToList();

            var dtos = rows.Select(p => new
            {
                ContextCode = p.EnterpriseCode,
                TargetCode = BuildStandardNodeCode(p.StageCode, p.StandardCode),
                NodeType = NodeTypeStandard
            }).ToList();

            return Ok(ApiResponse<object?>.Ok(dtos));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 五、私有辅助
    // ========================================================

    /// <summary>阶段节点合成键：<c>STAGE:{阶段码}</c></summary>
    private static string BuildStageNodeCode(string stageCode) => $"{StagePrefix}{stageCode}";

    /// <summary>标准叶子合成键：<c>STAGE:{阶段码}|STD:{标准Code}</c></summary>
    private static string BuildStandardNodeCode(string stageCode, string standardCode) =>
        $"{StagePrefix}{stageCode}{CodeSeparator}{StdPrefix}{standardCode}";

    /// <summary>
    /// 解析前端合成键 → (阶段码, 标准Code)。
    ///
    /// <para>非法格式**直接丢弃**（不抛错）—— 避免一个坏 Code 让整批提交失败。</para>
    /// </summary>
    private static List<(string StageCode, string StandardCode)> ParseSelections(
        List<NodeSelection>? selections)
    {
        var result = new List<(string, string)>();
        if (selections == null) return result;

        foreach (var sel in selections)
        {
            if (string.IsNullOrWhiteSpace(sel.Code)) continue;

            // 只接受叶子节点（阶段行不可勾选，此处防御性过滤）
            if (!string.IsNullOrEmpty(sel.NodeType) && sel.NodeType != NodeTypeStandard) continue;

            var code = sel.Code;
            var sepIndex = code.IndexOf(CodeSeparator);
            if (sepIndex <= 0) continue;

            var left = code[..sepIndex];
            var right = code[(sepIndex + 1)..];

            if (!left.StartsWith(StagePrefix, StringComparison.Ordinal)) continue;
            if (!right.StartsWith(StdPrefix, StringComparison.Ordinal)) continue;

            var stageCode = left[StagePrefix.Length..];
            var standardCode = right[StdPrefix.Length..];
            if (string.IsNullOrEmpty(stageCode) || string.IsNullOrEmpty(standardCode)) continue;

            result.Add((stageCode, standardCode));
        }

        return result;
    }

    /// <summary>校验「企业存在且属于当前工作区」；失败返回错误文案，成功返回 null</summary>
    private async Task<string?> EnsureOwnedEnterpriseAsync(string? enterpriseCode, string workspaceCode)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode))
            return "企业编码不能为空";

        var owned = await _entService.ExistsAsync(p =>
            p.Code == enterpriseCode && p.OrgCode == workspaceCode);

        return owned.Data ? null : "企业不存在或不属于当前工作区";
    }

    /// <summary>取某工作区下全部有效企业的 Code 集合（同步 —— 供同步钩子复用）</summary>
    private List<string> WorkspaceEnterpriseCodes(string workspaceCode)
    {
        var codes = _db.Client.Queryable<Ent>()
            .Where(p => p.OrgCode == workspaceCode && p.IsValid == 1 && !p.IsDeleted)
            .Select(p => p.Code)
            .ToList();

        // 空集合 → IN () 是语法错误，用哨兵值代替（结果等价于空集）
        if (codes.Count == 0) codes.Add(NoEnterpriseSentinel);
        return codes;
    }

    // ========================================================
    // 六、请求模型
    // ========================================================

    /// <summary>上下文请求（<c>ContextCode</c> = 左树节点 Code = 企业 Code）</summary>
    public class ContextRequest
    {
        public string ContextCode { get; set; } = string.Empty;
    }

    /// <summary>勾选提交项（对齐前端 <c>AssociationSelection</c>）</summary>
    public class NodeSelection
    {
        public string Code { get; set; } = string.Empty;
        public string? NodeType { get; set; }
    }

    /// <summary>勾选增删请求（对齐 <c>TreeTableControllerBase.CheckActionRequest</c>）</summary>
    public class CheckActionRequest
    {
        public string ContextCode { get; set; } = string.Empty;
        public List<NodeSelection>? Selections { get; set; }
    }
}
