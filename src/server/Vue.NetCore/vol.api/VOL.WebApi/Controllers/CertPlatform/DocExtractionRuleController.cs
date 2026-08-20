using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.Filters;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Core.Enums;
using Microsoft.EntityFrameworkCore;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.DocExtraction;
using VOL.Entity.CertPlatform.DocExtraction.DTOs;
// using VOL.Entity.CertPlatform.Dir; // 废弃，待企业文件实体重建后恢复
using VOL.Builder.IServices.CertPlatform;
using YZH.Core.Extractor.Models;

namespace VOL.WebApi.Controllers.CertPlatform
{
    /// <summary>
    /// 文档提取规则管理
    /// </summary>
    [Route("api/DocExtractionRule")]
    [ApiController]
    [JWTAuthorize]
    public class DocExtractionRuleController : ApiBaseController<object>
    {
        private readonly IDocExtractionRuleService _service;

        public DocExtractionRuleController(IDocExtractionRuleService service)
        {
            _service = service;
        }

        /// <summary>
        /// AI自动分析文档
        /// </summary>
        [HttpPost, Route("analyze")]
        public async Task<IActionResult> AIAnalyze([FromBody] AIAnalyzeRequest request)
        {
            var result = await _service.AIAnalyzeAsync(request);
            return JsonNormal(result);
        }

        /// <summary>
        /// 生成提取Prompt
        /// </summary>
        [HttpPost, Route("generate-prompt")]
        public async Task<IActionResult> GeneratePrompt([FromBody] GeneratePromptRequest request)
        {
            var prompt = await _service.GeneratePromptAsync(request);
            return JsonNormal(new { success = true, prompt });
        }

        /// <summary>
        /// 验证Prompt
        /// </summary>
        [HttpPost, Route("verify")]
        public async Task<IActionResult> VerifyPrompt([FromBody] VerifyPromptRequest request)
        {
            var result = await _service.VerifyPromptAsync(request);
            return JsonNormal(result);
        }

        /// <summary>
        /// 保存提取规则
        /// </summary>
        [HttpPost, Route("save")]
        public async Task<IActionResult> SaveExtractionRule([FromBody] SaveExtractionRuleRequest request)
        {
            var success = await _service.SaveExtractionRuleAsync(request);
            return JsonNormal(new { success, message = success ? "保存成功" : "保存失败" });
        }

        /// <summary>
        /// 获取已配置提取规则的文档列表（供工作流配置页面选择文档）
        /// 注意：放在 {standardFileCode} 之前，避免路由参数拦截
        /// </summary>
        [HttpGet, Route("configured-rules")]
        public async Task<IActionResult> GetConfiguredRules()
        {
            var rules = await _service.GetConfiguredRulesAsync();
            return Ok(new { status = true, data = rules });
        }

        /// <summary>
        /// 获取规则详情
        /// </summary>
        [HttpGet, Route("{standardFileCode}")]
        public async Task<IActionResult> GetRuleDetail(string standardFileCode)
        {
            var result = await _service.GetRuleDetailAsync(standardFileCode);
            if (result == null)
                return JsonNormal(new { success = false, message = "规则不存在" });
            return JsonNormal(new { success = true, data = result });
        }

        /// <summary>
        /// 获取规则的字段和表格定义（供 docField/docTable 节点选择）
        /// 注意：放在 {standardFileCode} 之后是安全的，因为 {ruleCode}/fields-tables 有子路径
        /// </summary>
        [HttpGet, Route("{ruleCode}/fields-tables")]
        public async Task<IActionResult> GetFieldsAndTables(string ruleCode)
        {
            var result = await _service.GetFieldsAndTablesAsync(ruleCode);
            return Ok(new { status = true, data = result });
        }

        /// <summary>
        /// 删除规则
        /// </summary>
        [HttpPost, Route("{standardFileCode}/delete")]
        public async Task<IActionResult> DeleteRule(string standardFileCode)
        {
            var success = await _service.DeleteRuleAsync(standardFileCode);
            return JsonNormal(new { success, message = success ? "删除成功" : "删除失败" });
        }

        /// <summary>
        /// 获取AI配置
        /// </summary>
        [HttpGet, Route("ai-config")]
        public async Task<IActionResult> GetAIConfig()
        {
            var config = await _service.GetAIConfigAsync();
            return JsonNormal(new { success = true, data = config });
        }

        /// <summary>
        /// 更新AI配置
        /// </summary>
        [HttpPost, Route("ai-config")]
        public async Task<IActionResult> UpdateAIConfig([FromBody] AIConfigDto config)
        {
            var success = await _service.UpdateAIConfigAsync(config);
            return JsonNormal(new { success, message = success ? "保存成功" : "保存失败" });
        }

        /// <summary>
        /// 获取可用技能列表
        /// </summary>
        [HttpGet, Route("skills")]
        public IActionResult GetSkills()
        {
            var skills = _service.GetSkills();
            return JsonNormal(new { success = true, data = skills });
        }

        // TODO: 以下两个端点依赖废弃的 StandardDirectoryFile/IStandardDirectoryService，待企业文件服务重建后恢复
        // /// <summary>
        // /// 获取标准目录文件树（按目录编码）。
        // /// </summary>
        // [HttpGet, Route("files/tree")]
        // public IActionResult GetFileTree([FromQuery] string directoryCode)
        // {
        //     var dirService = AutofacContainerModule.GetService<VOL.Builder.IServices.CertPlatform.IStandardDirectoryService>();
        //     if (dirService == null)
        //         return JsonNormal(new { success = false, message = "标准目录服务不可用" });
        //     var tree = dirService.GetStageFileTree(directoryCode);
        //     return JsonNormal(new { success = true, data = tree });
        // }

        // /// <summary>
        // /// 获取文件全文内容（经 IFileExtractor 提取）。
        // /// </summary>
        // [HttpGet, Route("files/{fileCode}/content")]
        // public async Task<IActionResult> GetFileContent(string fileCode)
        // {
        //     var volContext = AutofacContainerModule.GetService<VOL.Core.EFDbContext.VOLContext>();
        //     var stdFile = await volContext.Set<StandardDirectoryFile>()
        //         .FirstOrDefaultAsync(x => x.FileCode == fileCode);
        //     if (stdFile == null)
        //         return JsonNormal(new { success = false, message = "文件不存在" });
        //
        //     var extractor = AutofacContainerModule.GetService<YZH.Core.Extractor.IFileExtractor>();
        //     if (extractor == null)
        //         return JsonNormal(new { success = false, message = "文件提取器不可用" });
        //
        //     var filePath = stdFile.ConvertedStoragePath ?? stdFile.StoragePath;
        //     if (string.IsNullOrWhiteSpace(filePath))
        //         return JsonNormal(new { success = false, message = "文件路径为空" });
        //
        //     // StoragePath 是 MinIO 对象路径，下载到内存流后调用流式提取
        //     var minio = AutofacContainerModule.GetService<VOL.Builder.IServices.CertPlatform.IMinIOHelper>();
        //     FileExtractionResult result;
        //     if (minio != null)
        //     {
        //         var (stream, _) = await minio.DownloadAsync(filePath);
        //         using (stream)
        //         {
        //             result = await extractor.ExtractAsync(stream, stdFile.FileName);
        //         }
        //     }
        //     else
        //     {
        //         result = await extractor.ExtractAsync(filePath);
        //     }
        //     return JsonNormal(new {
        //         success = true,
        //         data = new {
        //             fullText = result.FullText ?? string.Empty,
        //             fileName = stdFile.FileName,
        //             sourceType = result.SourceType.ToString(),
        //             status = result.Status.ToString(),
        //             message = result.Message,
        //             errorMessage = result.ErrorMessage,
        //             fieldCount = result.Fields.Count,
        //             tableCount = result.Tables.Count
        //         }
        //     });
        // }

        /// <summary>
        /// 测试字段提取（配置期验证）
        /// </summary>
        [HttpPost, Route("test-field")]
        public async Task<IActionResult> TestField([FromBody] TestFieldRequest request)
        {
            var result = await _service.TestFieldAsync(request.RuleCode, request.FieldCode, request.DocType);
            return Ok(new { status = true, data = result });
        }

        /// <summary>
        /// 测试表格提取（配置期验证）
        /// </summary>
        [HttpPost, Route("test-table")]
        public async Task<IActionResult> TestTable([FromBody] TestTableRequest request)
        {
            var result = await _service.TestTableAsync(request.RuleCode, request.TableCode, request.DocType);
            return Ok(new { status = true, data = result });
        }

        /// <summary>
        /// 获取规则列表（分页）
        /// </summary>
        [HttpPost, Route("list")]
        public async Task<IActionResult> GetList([FromBody] object options)
        {
            // TODO: 实现分页查询
            return JsonNormal(new { success = true, data = new List<object>() });
        }
    }
}
