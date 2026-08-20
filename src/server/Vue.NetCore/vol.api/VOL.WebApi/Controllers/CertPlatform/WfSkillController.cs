using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.BaseProvider;
using VOL.Entity.CertPlatform.Wf;
using VOL.Entity.DomainModels;
using VOL.Builder.IServices.CertPlatform;
using YZH.Core.Workflow;

namespace VOL.WebApi.Controllers.CertPlatform
{
    /// <summary>
    /// Skill 管理（V2 静态方法版）
    /// </summary>
    [Route("api/skill")]
    [Authorize]
    public class WfSkillController : ControllerBase
    {
        private readonly IWfSkillService _service;
        private readonly SkillExecutor _executor;

        public WfSkillController(IWfSkillService service, SkillExecutor executor)
        {
            _service = service;
            _executor = executor;
        }

        [HttpPost("page")]
        public async Task<IActionResult> GetPage([FromBody] PageDataOptions options,
            [FromQuery] string keyword = null,
            [FromQuery] string category = null)
        {
            var result = await _service.GetPageDataAsync(options, keyword, category);
            return Ok(new { status = true, data = result });
        }

        // ===== 固定路径端点（必须在 {skillCode} 之前） =====

        [HttpGet("list-active")]
        public async Task<IActionResult> GetActiveSkills()
        {
            var list = await _service.GetActiveSkillsAsync();
            return Ok(new { status = true, data = list });
        }

        [HttpGet("query-nodes")]
        public async Task<IActionResult> GetNodeCatalog()
        {
            var catalog = await _service.GetCatalogAsync();
            return Ok(new { status = true, data = catalog });
        }

        /// <summary>
        /// 反射验证接口：填入 classPath + methodName 后反射提取端口信息。
        /// 用于管理页面"验证"按钮——人工核实反射分析结果无误后再保存。
        /// </summary>
        [HttpPost("analyze")]
        public IActionResult Analyze([FromBody] AnalyzeRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ClassPath))
                return Ok(new { status = false, message = "请填写实现类全名" });

            var methodName = string.IsNullOrWhiteSpace(req.MethodName) ? "ExecuteAsync" : req.MethodName;
            var metadata = _executor.Analyze(req.ClassPath, methodName);

            if (metadata == null)
                return Ok(new { status = false, message = $"反射失败：找不到类型 {req.ClassPath} 或方法 {methodName}，或缺少 [Skill] 特性" });

            return Ok(new { status = true, data = metadata });
        }

        // ===== 动态路径端点 =====

        [HttpGet("{skillCode}")]
        public async Task<IActionResult> GetDetail(string skillCode)
        {
            var detail = await _service.GetDetailAsync(skillCode);
            if (detail == null) return Ok(new { status = false, message = "Skill 不存在" });
            return Ok(new { status = true, data = detail });
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SkillDetailDto dto)
        {
            var (ok, message) = await _service.SaveAsync(dto);
            return Ok(new { status = ok, message });
        }

        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return Ok(new { status = result, message = result ? "删除成功" : "删除失败" });
        }

        [HttpPost("toggle-active/{id}")]
        public async Task<IActionResult> ToggleActive(long id)
        {
            var result = await _service.ToggleActiveAsync(id);
            return Ok(new { status = result, message = result ? "操作成功" : "操作失败" });
        }
    }

    public class AnalyzeRequest
    {
        public string ClassPath { get; set; } = string.Empty;
        public string MethodName { get; set; } = "ExecuteAsync";
    }
}
