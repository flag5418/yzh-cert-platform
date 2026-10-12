using System.Threading;
using System.Threading.Tasks;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Shared.Fill.Ai
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
}
