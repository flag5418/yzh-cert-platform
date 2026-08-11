using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Core.Filters;
using VOL.Core.Utilities;
using VOL.Builder.IServices.CertPlatform;
using VOL.Entity.CertPlatform.Wf;

namespace VOL.WebApi.Controllers.CertPlatform
{
    /// <summary>
    /// Prompt 模板管理接口
    /// </summary>
    [Route("api/prompt-template")]
    [ApiController]
    [JWTAuthorize]
    public class PromptTemplateController : ApiBaseController<object>
    {
        private readonly IPromptTemplateService _service;

        public PromptTemplateController(IPromptTemplateService service)
        {
            _service = service;
        }

        /// <summary>获取提示词列表（可按类型/技能筛选）</summary>
        [HttpGet]
        public async Task<IActionResult> GetList(
            [FromQuery] string? promptType = null,
            [FromQuery] string? skillTarget = null)
        {
            var result = await _service.GetListAsync(promptType, skillTarget);
            return JsonNormal(result);
        }

        /// <summary>根据编码获取单条提示词</summary>
        [HttpGet("{promptCode}")]
        public async Task<IActionResult> GetByCode(string promptCode)
        {
            var entity = await _service.GetByCodeAsync(promptCode);
            if (entity == null)
                return JsonNormal(new { success = false, message = "提示词不存在" });
            return JsonNormal(new { success = true, data = entity });
        }

        /// <summary>获取指定类型当前生效的提示词</summary>
        [HttpGet("active/{promptType}")]
        public async Task<IActionResult> GetActive(string promptType, [FromQuery] string? skillTarget = null)
        {
            var entity = await _service.GetActiveAsync(promptType, skillTarget);
            if (entity == null)
                return JsonNormal(new { success = false, message = "未找到生效的提示词" });
            return JsonNormal(new { success = true, data = entity });
        }

        /// <summary>创建或更新提示词</summary>
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] PromptTemplate entity)
        {
            if (string.IsNullOrWhiteSpace(entity.PromptCode))
                return JsonNormal(new { success = false, message = "prompt_code 不能为空" });
            if (string.IsNullOrWhiteSpace(entity.PromptType))
                return JsonNormal(new { success = false, message = "prompt_type 不能为空" });
            if (string.IsNullOrWhiteSpace(entity.Template))
                return JsonNormal(new { success = false, message = "template 不能为空" });

            var success = await _service.SaveAsync(entity);
            return JsonNormal(new { success, message = success ? "保存成功" : "保存失败" });
        }

        /// <summary>删除提示词</summary>
        [HttpDelete("{promptCode}")]
        public async Task<IActionResult> Delete(string promptCode)
        {
            var success = await _service.DeleteAsync(promptCode);
            return JsonNormal(new { success, message = success ? "删除成功" : "删除失败" });
        }

        /// <summary>激活提示词（同类型其他版本设为不活跃）</summary>
        [HttpPost("{promptCode}/activate")]
        public async Task<IActionResult> Activate(string promptCode)
        {
            var success = await _service.ActivateAsync(promptCode);
            return JsonNormal(new { success, message = success ? "已激活" : "激活失败" });
        }
    }
}
