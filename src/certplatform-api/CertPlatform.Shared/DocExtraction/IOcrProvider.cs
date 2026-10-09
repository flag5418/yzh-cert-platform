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
        /// ★ <b>PDF 走视觉 OCR 时的渲染参数</b>（2026-10-09）。
        ///
        /// <para><b>为什么由 Provider 提供</b>：页数上限与渲染 DPI 属于<b>视觉能力配置</b>
        /// （存在 <c>ai_vision_config</c> 那一行 JSON 里），而读该配置需要 <c>IDbOrm</c> ——
        /// 只有 Admin 层的真实实现拿得到（<c>Shared</c> 不引 <c>Yzh.Core.DataBase</c>，分层约束）。
        /// 故由 Provider 转出，⛔ 调用方不得自行写死。</para>
        ///
        /// <para><b>为什么必须逐页渲染</b>：视觉模型<b>不接受 PDF 字节</b>走 <c>image_url</c>
        /// （实测 <c>data:image/png</c> 与 <c>data:application/pdf</c> 均 400
        /// <c>The image format is illegal and cannot be opened</c>）。</para>
        /// </summary>
        PdfOcrOptions PdfOptions { get; }

        /// <summary>
        /// 把图片 / 扫描件转换为 Markdown
        /// </summary>
        /// <param name="fileName">原始文件名（含扩展名，供 Provider 判断类型）</param>
        /// <param name="content">原始文件字节（<b>图片</b>；PDF 请先经 <c>DocumentConvertClient.RenderPdfPagesAsync</c> 渲染）</param>
        /// <returns>转换结果；未接入时返回 <see cref="OcrResult.NotAvailable"/></returns>
        Task<OcrResult> ToMarkdownAsync(string fileName, byte[] content);

        /// <summary>
        /// ★ 图片 / PDF「分类 + 作用 + 关键词」识别（§2 路径② 两层过滤的输入，2026-10-07）。
        /// <para>与 <see cref="ToMarkdownAsync"/> 共用同一视觉模型 + 同一行 <c>ai_vision_config</c>
        /// （底层核心能力不版本管理、一份配置服务所有文档）。⛔ 必须与 <see cref="ToMarkdownAsync"/>
        /// 成对实现：真实能力在 Admin <c>VisionOcrProvider</c>；空壳 <c>NullOcrProvider</c> 返回
        /// <see cref="DocPurposeJudge.NotAvailable"/>（能力边界显式声明，不抛异常）。</para>
        /// </summary>
        /// <param name="fileName">原始文件名（含扩展名，供 Provider 判断类型）</param>
        /// <param name="content">原始文件字节（图片 / PDF）</param>
        /// <returns>识别结果；未接入时返回 <see cref="DocPurposeJudge.NotAvailable"/></returns>
        Task<DocPurposeJudge> JudgeDocPurposeAsync(string fileName, byte[] content);
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

    /// <summary>
    /// PDF 走视觉 OCR 的渲染参数（2026-10-09）。
    /// <para><b>取值依据（2026-10-09 实测）</b>：A4 在 150 dpi 下约 1240×1754，
    /// 单页 ≈ <b>2147 image tokens</b>（qwen3-vl-flash）。50 页 ≈ 10.7 万 tokens，
    /// 是「既不把单份文档做废、又不把上下文打满」的折中。</para>
    /// <para>⚠️ 超限<b>不是静默截断</b>：由 <c>FileConvertCore</c> 把「共 N 页，仅识别前 M 页」
    /// 写进 <c>MarkdownMessage</c> 如实告知。</para>
    /// </summary>
    public sealed record PdfOcrOptions(int MaxPages, int Dpi)
    {
        /// <summary>缺省：50 页 / 150 dpi</summary>
        public static readonly PdfOcrOptions Default = new(50, 150);

        /// <summary>归一化（配置写 0 / 负数时回落到缺省，⛔ 不让坏配置把链路炸掉）</summary>
        public PdfOcrOptions Normalized()
            => new(MaxPages > 0 ? MaxPages : Default.MaxPages,
                   Dpi > 0 ? Dpi : Default.Dpi);
    }
}
