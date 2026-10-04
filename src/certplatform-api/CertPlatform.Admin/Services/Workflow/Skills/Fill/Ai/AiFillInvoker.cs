using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Shared.DocExtraction;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai
{
    /// <summary>
    /// <b>AI 填充调用器</b> —— 三个 AI Skill（<c>src_semantic</c> / <c>src_ai_field</c> / <c>src_ai_table</c>）
    /// 共用的<b>唯一</b>模型调用入口（39 号 §12.1）。
    ///
    /// <para><b>★ 为什么必须唯一</b>：<c>LlmExtractSkill</c> 踩过的 5 个坑
    /// （重试、<c>MaxTokens</c> 截断、<c>JsonElement</c> 不转换、六键注入、围栏剥离）若各 Skill 各写一遍，
    /// 必然出现「A Skill 能跑通、B Skill 静默返回空」。本类把 5 项<b>一次性收口</b>。</para>
    ///
    /// <para><b>★ 重试策略（刻意与 <c>LlmExtractSkill</c> 有 1 处不同）</b>：
    /// <list type="bullet">
    ///   <item><b>JSON 解析失败</b> ⇒ 重试 1 次（第 2 次追加「仅输出符合 Schema 的 JSON」补正）
    ///         —— 模型首次带 markdown 围栏是常态；</item>
    ///   <item><b>配置 / 网络 / 超时错误</b> ⇒ <b>⛔ 不重试</b>，立刻返回。
    ///         理由：重试只会让用户多等一个 180s 超时，且错误原因不变。</item>
    /// </list></para>
    ///
    /// <para>⚠️ 本类<b>不读提示词</b>（那是 <c>AiFillPromptBuilder</c> 的职责），
    /// <b>不解析业务字段</b>（那是 <c>AiFillJsonReader</c> 的职责）。</para>
    /// </summary>
    public interface IAiFillInvoker
    {
        /// <summary>调用模型一次（含 1 次 JSON 补正重试）</summary>
        /// <param name="db">数据访问（读 <c>cert_sys_config</c> 六键）</param>
        /// <param name="request">已渲染好的提示词 + 期望结构</param>
        /// <param name="ct">取消令牌</param>
        Task<AiFillInvokeResult> InvokeAsync(IDbOrm db, AiFillInvokeRequest request, CancellationToken ct = default);
    }

    /// <inheritdoc cref="IAiFillInvoker"/>
    public sealed class AiFillInvoker : IAiFillInvoker
    {
        /// <summary>★ 填充输出比提取更长，4096 会在字符串中间截断 ⇒ JSON 解析失败</summary>
        public const int DefaultMaxTokens = 8192;

        private readonly LlmInvokeService _llm;
        private readonly ILogger<AiFillInvoker>? _logger;

        public AiFillInvoker(LlmInvokeService llm, ILogger<AiFillInvoker>? logger = null)
        {
            _llm = llm;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<AiFillInvokeResult> InvokeAsync(
            IDbOrm db, AiFillInvokeRequest request, CancellationToken ct = default)
        {
            var result = new AiFillInvokeResult();

            if (request == null || string.IsNullOrWhiteSpace(request.UserPrompt))
                return Fail(result, "提示词为空（UserPrompt 不能为空）");

            var settings = await AiFillSettings.ReadAsync(db, ct);
            if (!settings.IsUsable)
                return Fail(result, "AI 未配置 —— 请先到「系统参数」配置 ai_api_key / ai_base_url");

            result.Model = settings.Model;

            // ★ 提示词里带上期望结构：模型不知道 Schema 就一定会自由发挥
            var prompt = request.UserPrompt;
            if (!string.IsNullOrWhiteSpace(request.OutputSchema))
                prompt += "\n\n## 输出结构\n" + request.OutputSchema;

            var maxTokens = request.MaxTokens > 0 ? request.MaxTokens : DefaultMaxTokens;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var attemptPrompt = attempt == 0
                    ? prompt
                    : prompt + "\n\n注意：上一轮输出存在 JSON 格式错误。请仅输出符合上面结构的 JSON，" +
                               "不要包含任何解释文字或 Markdown 围栏。";

                LlmInvokeResponse resp;
                try
                {
                    resp = await _llm.CompleteAsync(new LlmInvokeRequest
                    {
                        BaseUrl = settings.BaseUrl,
                        ApiKey = settings.ApiKey,
                        Model = settings.Model,
                        // ★ 低温：填充是「取事实」不是「创作」，同一份资料跑两次应得同样的值
                        Temperature = (float)request.Temperature,
                        MaxTokens = maxTokens,
                        SystemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt) ? null : request.SystemPrompt,
                        Prompt = attemptPrompt,
                        DocumentContent = string.Empty,
                        ForceJson = true,
                    });
                }
                catch (Exception ex)
                {
                    // LlmInvokeService 自己吞异常，这里只是防御（DI 未注册 / 取消）
                    return Fail(result, $"AI 调用异常：{ex.Message}");
                }

                result.DurationMs += resp.DurationMs;
                result.PromptTokens = resp.PromptTokens;
                result.CompletionTokens = resp.CompletionTokens;
                result.RawText = resp.Content;

                if (resp.Success && resp.Json != null)
                {
                    using var doc = resp.Json;
                    var root = AiFillJsonReader.NormalizeRoot(doc);
                    if (root == null)
                    {
                        // JSON 合法但不是对象 ⇒ 不值得重试（模型理解错了任务）
                        return Fail(result, "AI 返回的 JSON 根不是对象");
                    }

                    result.Success = true;
                    result.Root = root;
                    return result;
                }

                // ★ 配置 / 网络类错误 ⇒ ⛔ 不重试（重试只是多等一个 180s 超时）
                if (!resp.Success && IsTransportError(resp.Message))
                    return Fail(result, resp.Message);

                _logger?.LogWarning("[AI_FILL] 第 {Attempt} 次输出无法解析为 JSON：{Msg}", attempt + 1, resp.Message);
            }

            return Fail(result, "AI 返回内容两次都无法解析为 JSON");
        }

        /// <summary>是否为「重试也没用」的错误（配置 / 传输 / 超时）</summary>
        private static bool IsTransportError(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            var m = message!;
            return m.Contains("未配置", StringComparison.Ordinal)
                || m.Contains("调用失败", StringComparison.Ordinal)
                || m.Contains("调用超时", StringComparison.Ordinal)
                || m.Contains("调用异常", StringComparison.Ordinal)
                || m.Contains("Prompt 为空", StringComparison.Ordinal);
        }

        private static AiFillInvokeResult Fail(AiFillInvokeResult r, string error)
        {
            r.Success = false;
            r.Error = error;
            return r;
        }
    }
}
