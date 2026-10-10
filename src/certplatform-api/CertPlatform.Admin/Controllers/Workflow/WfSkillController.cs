
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
                    dtos.Add(new SkillMetadataDto
                    {
                        Code = skill.Code,
                        SkillCode = skill.SkillCode,
                        Name = skill.Name,
                        Description = skill.Description ?? string.Empty,
                        InputPorts = await ResolveInputPortsAsync(skill.SkillCode, ct)
                    });
                }
                return Ok(ApiResponse<List<SkillMetadataDto>>.Ok(dtos));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<List<SkillMetadataDto>>.Fail($"读取技能元数据失败：{ex.Message}", 500));
            }
        }

        private async Task<List<SkillPortDto>> ResolveInputPortsAsync(string skillCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return new List<SkillPortDto>();

            SkillMetadata? meta = null;
            if (SkillIndex.Value.TryGetValue(skillCode, out var loc))
                meta = _skillExecutor.Analyze(loc.ClassPath, loc.MethodName);

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
        public string? DefaultValue { get; set; }
        public string Description { get; set; } = string.Empty;
        /// <summary>Link / LinkOrConstant / Enum</summary>
        public string BindMode { get; set; } = "LinkOrConstant";
        public string? EnumSource { get; set; }
    }
}
