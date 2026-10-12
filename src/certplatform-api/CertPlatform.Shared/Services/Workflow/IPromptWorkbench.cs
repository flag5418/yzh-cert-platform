using System.Collections.Generic;
using System.Threading.Tasks;
using CertPlatform.Shared.DocExtraction;

namespace CertPlatform.Shared.Services.Workflow
{
    /// <summary>
    /// ★ 提示词类型常量 —— <b>唯一出处</b>（原 <c>PromptWorkbenchService.Types</c>，2026-10-12 下沉）。
    /// <para>⛔ 端内不得再各写一份字面量：改了这里两端同时生效。</para>
    /// </summary>
    public static class PromptTypes
    {
        /// <summary>元提示词（生成器）</summary>
        public const string Generator = "prompt_generator";
        /// <summary>★ 标题 / 分类提示词（输入文件清单 → 输出类别）</summary>
        public const string Group = "doc_group";
        /// <summary>★ 作用提示词（输入单文件 Markdown → 输出作用）</summary>
        public const string Content = "doc_content";
        /// <summary>★ 语义精要提示词（长文档摘要，输入长 Markdown → 输出精简画像）</summary>
        public const string Essential = "doc_essential";
    }

    /// <summary>
    /// 生效提示词的<b>只读投影</b>（Shared 契约）。
    /// <para><b>为什么不直接返回实体</b>：<c>PromptTemplate</c> 是<b>后台端独占实体</b>
    /// （<c>CertPlatform.Admin/Entities/Wf/</c>）⇒ 透出它等于让专家端重新依赖后台端。
    /// 本 DTO 只带调用方真正要用的 4 个字段。</para>
    /// </summary>
    public sealed class PromptTemplateView
    {
        public string Code { get; init; } = "";
        public int Version { get; init; }
        /// <summary>提示词正文（<c>null</c> 或空白 = 该类型未配生效版本）</summary>
        public string? Template { get; init; }
        /// <summary>模型名快照（为空 = 用平台默认）</summary>
        public string? ModelName { get; init; }
    }

    /// <summary>
    /// ★ 提示词工作台（Shared 契约）。
    /// <para><b>为什么是接口而不是直接引用实现类</b>：实现在
    /// <c>CertPlatform.Admin.Services.Workflow.PromptWorkbenchService</c>（后台端），
    /// 而专家端（<c>Auditor</c>）也要跑语义分析。三方结构铁律（规则文档 §一）
    /// 规定 <c>Auditor ⛔ 引 Admin</c> ⇒ 只能靠<b>接口在 Shared、实现在 Admin、
    /// 单宿主同进程经 DI 解析</b>这一条路。</para>
    /// </summary>
    public interface IPromptWorkbench
    {
        /// <summary>解析「当前生效」的提示词（按 标准级 → 平台级 逐层回退）；无 ⇒ <c>null</c></summary>
        Task<PromptTemplateView?> ResolveActiveAsync(string promptType, string? standardCode);

        /// <summary>队列侧语义分析（按提示词类型跑 LLM，返回已校验的 JSON）</summary>
        Task<AnalyzeForQueueResult> AnalyzeForQueueAsync(
            string promptType, string? standardCode,
            IReadOnlyList<(string FileName, string? Markdown)> markdownByFile,
            string businessRef,
            int maxFiles = 0);
    }

    /// <summary>
    /// 队列语义分析结果（<see cref="AnalyzeForQueueAsync"/>）。
    /// <para>⚠️ 字段名<b>不加前缀</b>，与 <see cref="TestResult"/> 区分靠类名而非字段名 ——
    /// 下游（executor）读的都是 PascalCase，加前缀反而要写两套映射。</para>
    /// </summary>
    public class AnalyzeForQueueResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        /// <summary>模型名（快照落画像的 <c>ModelName</c>）</summary>
        public string Model { get; set; } = "";
        public string PromptCode { get; set; } = "";
        public int PromptVersion { get; set; } = 1;
        /// <summary>经六条校验修复后的 JSON（<b>落库用这个</b>，不是 RawOutput）</summary>
        public string? Json { get; set; }
        /// <summary>LLM 原始返回（仅留档，不落画像）</summary>
        public string? RawOutput { get; set; }
        public long DurationMs { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        /// <summary>后端自动校正明细（写 <c>TagsReason</c> / 返回前端展示）</summary>
        public List<string> ValidationMessages { get; set; } = new();

        public static AnalyzeForQueueResult Fail(string message, LlmInvokeResponse? resp = null, string? raw = null)
            => new()
            {
                Success = false,
                Message = message,
                RawOutput = raw ?? resp?.Content,
                DurationMs = resp?.DurationMs ?? 0,
                PromptTokens = (int)(resp?.PromptTokens ?? 0),
                CompletionTokens = (int)(resp?.CompletionTokens ?? 0),
            };
    }
}
