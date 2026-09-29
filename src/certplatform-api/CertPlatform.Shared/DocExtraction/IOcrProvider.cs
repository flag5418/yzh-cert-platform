using System;
using System.Threading.Tasks;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// OCR 能力接口（图片 / 扫描件 → Markdown）
    ///
    /// <para>★ 设计定位（2026-09-26 用户裁定）：</para>
    /// <para><b>这不是一个必须实现的能力</b>。产品哲学是「用有限的精力，做差异化的产品」——
    /// 核心目标是<b>把大部分 Office 文件处理成所需的字段和表格</b>。图片 / 扫描件因为质量不可控
    /// （手写、受潮、不清晰），<b>不追求自动提取</b>，而是允许用户手工定义字段与表格后由人工填写。</para>
    ///
    /// <para>因此本接口的作用是<b>预留扩展缝</b>：</para>
    /// <list type="number">
    ///   <item>当前：<see cref="DefaultOcrProvider"/> 返回 <see cref="OcrResult.NotAvailable"/>，
    ///         转换链把 MarkdownStatus 置为 failed 并写入明确原因（**不伪造内容**），
    ///         上传流程照常完成、规则照常可配。</item>
    ///   <item>将来：接入第三方 OCR / 视觉模型时，**只改 DI 注册一行**，
    ///         转换链、提取链、前端全部无需改动。</item>
    /// </list>
    ///
    /// <para>⚠️ 为什么不返回「默认成功」：那会让占位文本以 <c>MarkdownStatus=completed</c> 的形态
    /// 流入提取链 → 被当成真文档喂给 LLM → LLM 编出一份格式正确的错误结果，**零报错**。
    /// 这是典型的静默失败，必须避免。「上传流程完整」应当靠「失败态清晰可读」达成，而不是靠伪造成功。</para>
    /// </summary>
    public interface IOcrProvider
    {
        /// <summary>当前是否具备可用的 OCR 能力（false 时调用方应直接走"人工填写"分支）</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 把图片 / 扫描件转换为 Markdown
        /// </summary>
        /// <param name="fileName">原始文件名（含扩展名，供 Provider 判断类型）</param>
        /// <param name="content">原始文件字节</param>
        /// <returns>转换结果；未接入时返回 <see cref="OcrResult.NotAvailable"/></returns>
        Task<OcrResult> ToMarkdownAsync(string fileName, byte[] content);
    }

    /// <summary>OCR 转换结果</summary>
    public class OcrResult
    {
        /// <summary>是否成功产出 Markdown</summary>
        public bool Success { get; set; }

        /// <summary>Markdown 文本（Success=true 时非空）</summary>
        public string? Markdown { get; set; }

        /// <summary>成功/失败说明。失败时写入 MarkdownMessage，供前端与人工判断</summary>
        public string Message { get; set; } = "";

        /// <summary>产物 Markdown 的字节（便于直接落 MinIO）</summary>
        public byte[]? Content { get; set; }

        public static OcrResult Ok(string markdown, byte[] content)
            => new() { Success = true, Markdown = markdown, Content = content, Message = "" };

        /// <summary>未接入 OCR：不是错误，是当前能力边界</summary>
        public static OcrResult NotAvailable(string message = DefaultNotAvailableMessage)
            => new() { Success = false, Message = message };

        public static OcrResult Fail(string message)
            => new() { Success = false, Message = message };

        /// <summary>未接入提示（前端据此引导用户「手工定义字段 + 人工填写」）</summary>
        public const string DefaultNotAvailableMessage =
            "图片/扫描件暂不支持自动提取内容。可在本页手工定义字段与表格，提取结果由人工填写";
    }
}
