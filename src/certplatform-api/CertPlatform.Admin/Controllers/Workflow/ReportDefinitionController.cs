using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Shared.Entities.Rpt;

namespace CertPlatform.Admin.Controllers.Workflow
{
    /// <summary>
    /// ★ 体系认证报告章节定义（★2026-09-29 去主表化，D34/D35）
    ///
    /// <para><b>★ 改造背景</b>：原为「cert_report_template（主表：报告名称 + 报表模板）→ rpt_report_section（章节）」两层。
    /// 现已扁平化为一层，<b>与 <c>ValidationRuleController</c>（NC 规则定义）完全对称</b>：
    /// 都是配置层、都按 <c>OrgCode + StandardCode + PhaseCode</c> 三元组定位、都用 <c>IsValid</c> 启用。</para>
    ///
    /// <para><b>★ 已删除的能力</b>（因不做报表系统）：报告名称、报表模板（docx）上传、模板级启停、
    /// 多套模板选默认（<c>IsDefault</c>）、章节顺序整体编排（<c>SectionConfig</c>）。</para>
    ///
    /// <para><b>★ 接口变化</b>：
    /// <list type="bullet">
    /// <item>删除：<c>template/context</c>、<c>template/save</c>、<c>template/upload</c>、<c>template/delete</c></item>
    /// <item>保留：<c>section/list</c>、<c>section/by-context</c>、<c>section/save</c>、<c>section/delete</c></item>
    /// <item><c>section/save</c> 的入参：<c>ReportCode</c> → 三元组 <c>OrgCode/StandardCode/PhaseCode</c></item>
    /// </list></para>
    /// </summary>
    [ApiController]
/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
    [Route("api/Admin/Workflow/ReportDefinition")]
    public class ReportDefinitionController : YzhControllerBase<ReportSection>
    {
        // ★ 基类已持有 Entity（EntityService<ReportSection>）与 UserContext，此处不重复注入
        private EntityService<ReportSection> SectionEntity => Entity;
        private readonly IDbOrm _db;

        public ReportDefinitionController(
            EntityService<ReportSection> entityService,
            IUserContext userContext,
            IDbOrm db)
            : base(entityService, userContext)
        {
            _db = db;
        }

        // =====================================================================
        // YzhControllerBase<ReportSection> 自动提供（★与 ValidationRuleController 对称）：
        //   GET  /config                        实体配置（Cert/ReportSection.json）
        //   POST /filter                        分页查询（FilterRequest）
        //   POST /add | /update | /delete       标准 CRUD
        //   POST /toggle-valid                  启用/禁用（铁律九走 IsValid）
        //   POST /action/{methodName}           行自定义动作
        // =====================================================================

        // ★ 配置来源：显式指定 JSON（与 ValidationRuleController 同款）。
        //   基类默认按 typeof(V).Name 找「ReportSection.json」（根目录），
        //   实际文件在「Cert/ReportSection.json」（参照 ValidationRule 的写法）。
        protected override EntityConfig LoadConfig()
        {
            return EntityConfigHelper.GetConfig("Cert/ReportSection");
        }

        /// <summary>★ 缺 JSON 直接抛错（开发期暴露，不让页面空白上线）</summary>
        protected override bool StrictConfigLoad => true;

        // 行按钮走基类默认 Edit + Delete；启停走基类 POST /toggle-valid（IsValid）。

        /// <summary>★ 搜索字段：只暴露「章节名称」。三元组字段由前端树联动注入，不进搜索区</summary>
        protected override List<YZH.Core.Stand.Models.Config.SearchFieldConfig> GetSearchFields()
        {
            return new List<YZH.Core.Stand.Models.Config.SearchFieldConfig>
            {
                new()
                {
                    Label = "章节名称",
                    Field = "SectionName",
                    Operator = "like",
                    ControlType = "input",
                    Width = 200
                }
            };
        }

        // =====================================================================
        // 章节 CRUD（★唯一保留的能力）
        // =====================================================================

        /// <summary>
        /// ★ 按三元组查询章节列表（★去 JOIN，扁平结构的核心收益）
        /// </summary>
        [HttpGet("section/by-context")]
        public async Task<IActionResult> GetSectionsByContext(
            [FromQuery] string orgCode,
            [FromQuery] string standardCode,
            [FromQuery] string phaseCode)
        {
            if (string.IsNullOrWhiteSpace(orgCode)
                || string.IsNullOrWhiteSpace(standardCode)
                || string.IsNullOrWhiteSpace(phaseCode))
            {
                return Ok(ApiResponse<object?>.Fail("缺少机构 / 标准 / 阶段"));
            }

            var result = await SectionEntity.GetListAsync(x =>
                x.OrgCode == orgCode &&
                x.StandardCode == standardCode &&
                x.PhaseCode == phaseCode);

            var sorted = (result.Data ?? new List<ReportSection>())
                .OrderBy(x => x.SortOrder)
                .ToList();

            return Ok(ApiResponse<object?>.Ok(data: sorted));
        }

        /// <summary>
        /// 按章节 Code 查询单条
        /// </summary>
        [HttpGet("section/detail")]
        public async Task<IActionResult> GetSectionDetail([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Ok(ApiResponse<object?>.Fail("缺少业务键 Code"));

            var byCode = await SectionEntity.GetByCode(code);
            if (byCode.Data == null)
                return Ok(ApiResponse<object?>.Fail("章节不存在", 404));

            return Ok(ApiResponse<object?>.Ok(data: byCode.Data));
        }

        /// <summary>
        /// 创建 / 更新章节
        /// <para>★ 准则 A：新增 vs 更新只看业务键 <c>Code</c>（禁止 <c>Id &gt; 0</c> 分流）</para>
        /// </summary>
        [HttpPost("section/save")]
        public async Task<IActionResult> SaveSection([FromBody] ReportSection entity)
        {
            if (string.IsNullOrWhiteSpace(entity.SectionName))
                return Ok(ApiResponse<object?>.Fail("章节名称不能为空"));

            // ★ 改造：原来校验 ReportCode，现在校验三元组
            if (string.IsNullOrWhiteSpace(entity.OrgCode)
                || string.IsNullOrWhiteSpace(entity.StandardCode)
                || string.IsNullOrWhiteSpace(entity.PhaseCode))
            {
                return Ok(ApiResponse<object?>.Fail("缺少机构 / 标准 / 阶段"));
            }

            // ──── 更新：按 Code 定位 ────
            if (!string.IsNullOrWhiteSpace(entity.Code))
            {
                var byCode = await SectionEntity.GetByCode(entity.Code);
                if (byCode.Data == null)
                    return Ok(ApiResponse<object?>.Fail("章节不存在", 404));

                var target = byCode.Data;
                target.SectionName    = entity.SectionName;
                target.SectionNameEn  = entity.SectionNameEn;
                target.Content        = entity.Content;
                target.SortOrder      = entity.SortOrder;
                target.IsValid        = entity.IsValid;          // ★ 原 IsActive
                target.WorkflowCode   = entity.WorkflowCode;
                target.WorkflowConfig = entity.WorkflowConfig;
                target.LayoutJson     = entity.LayoutJson;
                target.ClauseCode     = entity.ClauseCode;
                target.SectionJson    = entity.SectionJson;
                target.Remark         = entity.Remark;
                target.Status         = entity.Status;
                target.UpdateTime     = DateTime.Now;
                target.UpdateBy       = UserContext.UserCode;

                // 归属三元组：允许改（换阶段/标准），但仍需保持在合法值域
                target.OrgCode      = entity.OrgCode;
                target.StandardCode = entity.StandardCode;
                target.PhaseCode    = entity.PhaseCode;

                var updateResult = await SectionEntity.Update(target);
                return Ok(updateResult.Success
                    ? ApiResponse<object?>.Ok(data: target)
                    : ApiResponse<object?>.Error(updateResult.Error));
            }

            // ──── 新增 ────
            // ★ 同 (OrgCode, StandardCode, PhaseCode, SortOrder) 唯一，
            //   冲突时给出明确提示而不是让 DB 报 1062
            var dup = await SectionEntity.GetOne(x =>
                x.OrgCode == entity.OrgCode &&
                x.StandardCode == entity.StandardCode &&
                x.PhaseCode == entity.PhaseCode &&
                x.SortOrder == entity.SortOrder);

            if (dup.Data != null)
            {
                return Ok(ApiResponse<object?>.Fail(
                    $"该阶段下第 {entity.SortOrder} 章已被「{dup.Data.SectionName}」占用，请调整排序号"));
            }

            entity.Code      = Guid.NewGuid().ToString("N");
            entity.CreateBy  = UserContext.UserCode;
            entity.CreateTime = DateTime.Now;
            entity.IsValid   = entity.IsValid == 0 ? 0 : 1;
            entity.IsDeleted = false;

            var addResult = await SectionEntity.Insert(entity);
            return Ok(addResult.Success
                ? ApiResponse<object?>.Ok(data: entity)
                : ApiResponse<object?>.Error(addResult.Error));
        }

        /// <summary>
        /// 删除章节（软删）
        /// </summary>
        [HttpPost("section/delete")]
        public async Task<IActionResult> DeleteSection([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Ok(ApiResponse<object?>.Fail("缺少业务键 Code"));

            // ★ 软删（ISoftDelete），不物理删除
            // ★★ 不用 EntityService.DeleteByCode：它内部走 GetOneAsync 受 IsValid=1 过滤，
            //    章节被停用（IsValid=0）后会报「记录不存在或已被删除」→ 停用的章节删不掉。
            //    故用 GetOneIgnoreValidAsync 绕过 IsValid 过滤（仅过滤软删除）。
            var found = await _db.GetOneIgnoreValidAsync<ReportSection>(x => x.Code == code);
            if (!found.Success || found.Data == null)
                return Ok(ApiResponse<object?>.Fail("章节不存在或已被删除", 404));

            // ★ 直接走 SqlSugar Updateable（IDbOrm 无 Update；EntityService.Update 会重查受 IsValid 过滤）
            var affected = await _db.Client.Updateable<ReportSection>()
                .SetColumns(x => new ReportSection
                {
                    IsDeleted  = true,
                    DeleteBy   = UserContext.UserCode,
                    DeleteTime = DateTime.Now,
                    UpdateBy   = UserContext.UserCode,
                    UpdateTime = DateTime.Now
                })
                .Where(x => x.Code == code)
                .ExecuteCommandAsync();

            var result = affected > 0
                ? Result<bool>.Ok(true)
                : Result<bool>.Fail("软删失败：未命中任何行");
            return Ok(result.Success ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Error(result.Error));
        }

        /// <summary>
        /// ★ 批量启停（替代原「模板级启停」能力，D34 删除主表后的等价手段）
        /// </summary>
        [HttpPost("section/batch-toggle")]
        public async Task<IActionResult> BatchToggle(
            [FromQuery] string orgCode,
            [FromQuery] string standardCode,
            [FromQuery] string phaseCode,
            [FromQuery] int isValid)
        {
            if (isValid != 0 && isValid != 1)
                return Ok(ApiResponse<object?>.Fail("isValid 只能是 0 或 1"));

            var list = await SectionEntity.GetListAsync(
                x => x.OrgCode == orgCode &&
                     x.StandardCode == standardCode &&
                     x.PhaseCode == phaseCode);

            var rows = list.Data ?? new List<ReportSection>();
            foreach (var row in rows)
            {
                row.IsValid    = isValid;
                row.UpdateBy   = UserContext.UserCode;
                row.UpdateTime = DateTime.Now;
            }

            if (rows.Count == 0)
                return Ok(ApiResponse<object?>.Ok(data: new { Affected = 0 }));

            // ★ 逐条更新（EntityService 无 UpdateRange）
            var failed = 0;
            foreach (var row in rows)
            {
                var r = await SectionEntity.Update(row, UserContext.ClientIp);
                if (!r.Success) failed++;
            }
            if (failed > 0)
                return Ok(ApiResponse<object?>.Fail($"批量启停部分失败：{failed}/{rows.Count}"));

            return Ok(ApiResponse<object?>.Ok(data: new { Affected = rows.Count }));
        }
    }
}
