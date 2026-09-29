using System.Threading.Tasks;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// OCR 默认实现：**不提供能力**（当前阶段的能力边界声明）
    ///
    /// <para>返回 <see cref="OcrResult.NotAvailable"/>，转换链据此把 <c>MarkdownStatus</c> 置为
    /// <c>failed</c> 并写入明确原因。**上传流程照常完成**（文件已入库、PDF 预览产物已生成），
    /// 提取规则照常可配置，只是提取结果需人工填写。</para>
    ///
    /// <para>★ 将来接入 OCR / 视觉模型的完整步骤（**已实测验证可行，2026-09-26**）：</para>
    /// <list type="number">
    ///   <item>新建 <c>XxxOcrProvider : IOcrProvider</c>（例：走 DashScope 视觉模型 <c>qwen-vl-max</c>，
    ///         同一 <c>ai_base_url</c> + <c>ai_api_key</c> 即可，实测可用）。</item>
    ///   <item>若走视觉模型，需先补两处基础设施：
    ///         ① <c>LlmInvokeService</c> 增加图片输入（当前 <c>content</c> 被拼成纯字符串，不支持 OpenAI 多模态数组）；
    ///         ② 新增独立配置键 <c>ai_vision_model_name</c>（默认 <c>qwen-vl-max</c>），
    ///            **不要复用 <c>ai_model_name</c>** —— 否则提取主流程会全线按视觉模型计价。</item>
    ///   <item>若需处理多页扫描件 PDF，还需逐页栅格化：
    ///         <c>docker/libreoffice/Dockerfile</c> 加 <c>apk add --no-cache poppler-utils</c>
    ///         （<c>soffice --convert-to png</c> **只能出第 1 页**，实测不可用）。</item>
    ///   <item><b>只改 DI 注册一行</b>（<c>AddSingleton&lt;IOcrProvider, XxxOcrProvider&gt;()</c>）→
    ///         转换链、提取链、前端零改动。</item>
    /// </list>
    /// </summary>
    public class DefaultOcrProvider : IOcrProvider
    {
        /// <summary>恒为 false —— 当前不具备 OCR 能力</summary>
        public bool IsAvailable => false;

        public Task<OcrResult> ToMarkdownAsync(string fileName, byte[] content)
        {
            // 明确返回"未接入"，不伪造内容、不抛异常。
            // 调用方（OfficeConvertService）会把 Message 写入 MarkdownMessage，
            // 前端据此引导用户走「手工定义字段 + 人工填写」。
            return Task.FromResult(OcrResult.NotAvailable());
        }
    }
}
