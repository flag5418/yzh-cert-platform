
using System.Reflection;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Admin.Services.Workflow.Skills;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Controllers.Workflow
{
    /// <summary>
    /// 技能管理 - 单表 CRUD
    /// <para>路由前缀：/api/Workflow/WfSkill</para>
    /// <para>继承 YzhControllerBase&lt;Skill&gt;，自动获得：</para>
    /// <para>  POST /filter        分页查询</para>
    /// <para>  POST /add           新增</para>
    /// <para>  POST /update        修改</para>
    /// <para>  POST /delete        删除</para>
    /// <para>  POST /toggle-valid  启用/禁用（IsValid 0↔1）</para>
    /// <para>  GET  /metadata      Skill 反射元数据（输入端口）—— 画布参数面板数据源</para>
    /// </summary>
    [ApiController]
/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。</para>
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
    [Route("api/Admin/Workflow/[controller]")]
    public class WfSkillController : YzhControllerBase<CertPlatform.Admin.Entities.Wf.Skill>
    {
        private readonly ISkillRegistry _skillRegistry;
        private readonly SkillExecutor _skillExecutor;

        /// <summary>
        /// [Skill] 特性静态索引：skillCode → (ClassPath, MethodName)。
        /// 代码即数据源：只要 Skill 类标了 [Skill(Code=...)]，即使 wf_skill_reflection
        /// 漏登记也能算出输入端口（DB 登记只作回退）。
        /// </summary>
        private static readonly Lazy<IReadOnlyDictionary<string, SkillLoc>> SkillIndex =
            new(BuildSkillIndex, isThreadSafe: true);

        private readonly record struct SkillLoc(string ClassPath, string MethodName);

        public WfSkillController(
            EntityService<CertPlatform.Admin.Entities.Wf.Skill> entityService,
            IUserContext userContext,
            ISkillRegistry skillRegistry,
            SkillExecutor skillExecutor)
            : base(entityService, userContext)
        {
            _skillRegistry = skillRegistry;
            _skillExecutor = skillExecutor;
        }

        protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
            CertPlatform.Admin.Entities.Wf.Skill entity)
        {
            var result = await Entity.GetByCodeAny(entity.Code);
            if (result.Success && result.Data != null)
                return (false, $"编码 {entity.Code} 已存在");
            return (true, null);
        }

        protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
            CertPlatform.Admin.Entities.Wf.Skill entity)
        {
            var result = await Entity.GetByCodeAny(entity.Code);
            if (result.Success && result.Data != null && result.Data.Code != entity.Code)
                return (false, $"编码 {entity.Code} 已被其他记录使用");
            return (true, null);
        }

        #region 元数据（画布输入端口）

        /// <summary>
        /// 全量 Skill 输入端口元数据 —— 工作流画布「参数配置」面板的唯一数据源。
        /// <para>★ 方案 A（反射元数据，2026-10-10）：端口由代码里的 [SkillParam] 反射得出，</para>
        /// <para>  不依赖 wf_skill_input 数据行（该表仅 6 行、19 个 Skill 中 17 个无行）。</para>
        /// <para>  权威来源 = <c>SkillExecutor.Analyze</c>（docs/10-YZH架构/24-后端实体归属清单-V1.md
        ///  已定 wf_skill_input=规划中、SkillDetailDto=待删）。</para>
        /// <para>解析顺序：[Skill] 特性索引（零 DB 依赖）→ wf_skill_reflection（DI 型 Skill 回退）。</para>
        /// <para>返回的 <c>Code</c> = wf_skill.Code（画布节点 skillCode 取值），<c>SkillCode</c> = 执行/反射键。</para>
        /// <para>★ 2026-10-14：wf_skill 双业务键已统一为单一 <c>Code</c>（2026-10-13 迁移删 SkillCode 列），
        ///   DTO 的 <c>SkillCode</c> 字段保留并回填 <c>Code</c> 值 —— 前端双键索引（s.SkillCode || s.Code）与
        ///   历史画布节点存量 skillCode 不受影响。</para>
        /// </summary>
        [HttpGet("metadata")]
        public async Task<IActionResult> GetMetadata(CancellationToken ct)
        {
            try
            {
                var result = await Entity.GetListAsync(null, includeDisabled: true);
                if (!result.Success || result.Data == null)
                    return Ok(ApiResponse<List<SkillMetadataDto>>.Fail(result.Error ?? "读取技能列表失败"));

                var dtos = new List<SkillMetadataDto>(result.Data.Count);
                foreach (var skill in result.Data)
                {
                    // ★ 2026-10-15：Description 改走反射（代码优先），见 ResolveDescription 注释。
                    var meta = ResolveMeta(skill.Code);

                    dtos.Add(new SkillMetadataDto
                    {
                        Code = skill.Code,
                        SkillCode = skill.Code,
                        Name = skill.Name,
                        Description = ResolveDescription(meta, skill.Description),
                        // ★ 用途开关（2026-10-11）：节点库按当前设计器场景过滤，默认 0 = 不出现
                        IsUseForNcConfig = skill.IsUseForNcConfig,
                        IsUseForReport = skill.IsUseForReport,
                        InputPorts = await ResolveInputPortsAsync(skill.Code, ct)
                    });
                }
                return Ok(ApiResponse<List<SkillMetadataDto>>.Ok(dtos));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<List<SkillMetadataDto>>.Fail($"读取技能元数据失败：{ex.Message}", 500));
            }
        }

        /// <summary>
        /// 取 Skill 的反射元数据（<c>[Skill]</c> 静态索引优先，零 DB 依赖）。
        /// <para>返回 <c>null</c> = 该 code 不在代码索引里（如纯 DI 型 Skill，只有 wf_skill_reflection 登记）。</para>
        /// </summary>
        private SkillMetadata? ResolveMeta(string? skillCode)
            => !string.IsNullOrWhiteSpace(skillCode) && SkillIndex.Value.TryGetValue(skillCode!, out var loc)
                ? _skillExecutor.Analyze(loc.ClassPath, loc.MethodName)
                : null;

        /// <summary>
        /// ★ 节点说明的取数口径（2026-10-15 修正）：<b>代码优先，DB 兜底</b>。
        ///
        /// <para><b>为什么必须改</b>：属性面板顶部的「!」悬浮读的就是本字段，而它此前<b>只读
        /// <c>wf_skill.Description</c></b>。实测 <c>stamp_judge_level</c> / <c>nc_conclusion_v2</c> /
        /// <c>collect_judges</c> 三个 v2 节点的该列为<b>空</b> ⇒ 用户在画布上<b>根本看不到「!」</b>，
        /// 而代码 <c>[Skill(Description=…)]</c> 里那份说明（含「三种接法」「怎么配」）<b>写了却到不了界面</b>。</para>
        ///
        /// <para><b>为什么是「代码优先」</b>：与端口元数据（<see cref="ResolveInputPortsAsync"/>）
        /// 同一口径 —— 本项目已定「代码即数据源，DB 登记只作回退」（见本文件
        /// <c>SkillIndex</c> 注释）。若反过来的话，改说明要同时改代码 + 改 DB，必然分叉。</para>
        /// </summary>
        private static string ResolveDescription(SkillMetadata? meta, string? dbDescription)
        {
            if (meta != null && !string.IsNullOrWhiteSpace(meta.Description))
                return meta.Description;
            return dbDescription ?? string.Empty;
        }

        /// <summary>端口 DTO 投影（把反射端口翻译成对外契约）。</summary>
        private async Task<List<SkillPortDto>> ResolveInputPortsAsync(string skillCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return new List<SkillPortDto>();

            var meta = ResolveMeta(skillCode);

            if (meta == null)
                meta = await _skillRegistry.LoadAsync(skillCode, ct);

            if (meta?.InputPorts == null || meta.InputPorts.Count == 0)
                return new List<SkillPortDto>();

            return meta.InputPorts
                .Select(p => new SkillPortDto
                {
                    Name = p.Name,
                    // 画布端口显示名：SkillParamAttribute.Description 即参数中文描述，
                    // 回退参数名（英文）保证端口永不为空白
                    Label = string.IsNullOrWhiteSpace(p.Description) ? p.Name : p.Description,
                    Type = p.Type,
                    Required = p.Required,
                    AllowEmpty = p.AllowEmpty,
                    Description = p.Description,
                    DefaultValue = p.DefaultValue,
                    BindMode = p.BindMode,
                    EnumSource = p.EnumSource
                })
                .ToList();
        }

        /// <summary>扫描程序集里所有 [Skill] 静态类，建 skillCode → 反射位置索引（进程内一次性）。</summary>
        private static IReadOnlyDictionary<string, SkillLoc> BuildSkillIndex()
        {
            var map = new Dictionary<string, SkillLoc>(StringComparer.Ordinal);
            try
            {
                foreach (var type in typeof(SkillAttribute).Assembly.GetTypes())
                {
                    var attr = type.GetCustomAttribute<SkillAttribute>();
                    if (attr == null || string.IsNullOrWhiteSpace(attr.Code)) continue;
                    var method = type.GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Static);
                    if (method == null) continue;
                    map[attr.Code] = new SkillLoc(type.FullName ?? string.Empty, method.Name);
                }
            }
            catch
            {
                // 反射失败时退化为空索引：LoadAsync（DB 反射）路径仍可工作
            }
            return map;
        }

        #endregion
    }

    /// <summary>Skill 反射元数据 DTO（画布节点输入端口）。</summary>
    public class SkillMetadataDto
    {
        /// <summary>wf_skill.Code —— 画布 node.skillCode 取值</summary>
        public string Code { get; set; } = string.Empty;
        /// <summary>wf_skill.SkillCode —— 执行期 wf_skill_reflection 键</summary>
        public string SkillCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// ★ 是否可用于 NC 规则配置（wf_skill.IsUseForNcConfig，2026-10-11）
        /// <para>设计器按 <c>workflowType</c> 取对应开关过滤节点库；默认 <c>false</c>。</para>
        /// </summary>
        public bool IsUseForNcConfig { get; set; }
        /// <summary>★ 是否可用于报告规则配置（wf_skill.IsUseForReport，2026-10-11）</summary>
        public bool IsUseForReport { get; set; }
        public List<SkillPortDto> InputPorts { get; set; } = new();
    }

    /// <summary>单个输入端口（与前端 PortDef 字段对齐）。</summary>
    public class SkillPortDto
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        /// <summary>string / number / boolean / date / json</summary>
        public string Type { get; set; } = "json";
        public bool Required { get; set; }
        /// <summary>
        /// ★ <b>该参数是否允许为空</b>（2026-10-12）—— <c>Required == !AllowEmpty</c>，两者<b>同源</b>
        /// （唯一判据 = <c>SkillExecutor.AllowsEmpty</c>）。
        ///
        /// <para><b>为什么与 <c>Required</c> 冗余下发</b>：前端可直接用「允许空 / 不允许空」写提示文案，
        /// 不必反推 <c>!Required</c>；且二者将来若需分叉（如「界面必填」≠「执行期必须非空」）各有字段。</para>
        ///
        /// <para>语义：<c>false</c> = skill 开发者显式声明「本参数不允许为空」⇒
        /// 上游给空值时该节点以 <c>PRECONDITION_EMPTY</c> 失败（全局中断当前检查项 + 落补录）。</para>
        /// </summary>
        public bool AllowEmpty { get; set; } = true;
        public string? DefaultValue { get; set; }
        public string Description { get; set; } = string.Empty;
        /// <summary>Link / LinkOrConstant / Enum</summary>
        public string BindMode { get; set; } = "LinkOrConstant";
        public string? EnumSource { get; set; }
    }
}
