using System.Threading.Tasks;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// 空实现：**恒定不提供 OCR 能力**（能力边界的显式声明，不是 bug）。
    ///
    /// <para><b>为什么保留它</b>：<c>IOcrProvider</c> 是 <c>FileConvertCore</c> 的兜底分支 ——
    /// 当视觉配置缺失 / 模型配错 / 厂商不可达时，DI 可退到本实现，让链路
    /// <b>「如实报不支持」</b>而不是抛异常。</para>
    ///
    /// <para>真实实现在 <c>CertPlatform.Admin/Services/DocExtraction/VisionOcrProvider.cs</c>，
    /// ⛔ <b>必须放在 Admin 层</b>：它要读系统参数（需 <c>IDbOrm</c>），
    /// 而 <c>CertPlatform.Shared</c> <b>不引用</b> <c>YZH.Core.DataBase</c>（分层约束，不能破坏）。</para>
    /// </summary>
    public class NullOcrProvider : IOcrProvider
    {
        /// <summary>恒为 false —— 本实现不具备 OCR 能力</summary>
        public bool IsAvailable => false;

        public Task<OcrResult> ToMarkdownAsync(string fileName, byte[] content)
        {
            // 明确返回「未接入」，不伪造内容、不抛异常。
            // 调用方会把 Message 写入 MarkdownMessage，前端据此引导用户走「手工定义 + 人工填写」。
            return Task.FromResult(OcrResult.NotAvailable());
        }

        /// <summary>
        /// 空壳实现：与 <see cref="ToMarkdownAsync"/> 同为能力边界显式声明（⛔ 不抛异常）。
        /// 真实能力在 Admin <c>VisionOcrProvider.JudgeDocPurposeAsync</c>（读 <c>ai_vision_config</c>）。
        /// </summary>
        public Task<DocPurposeJudge> JudgeDocPurposeAsync(string fileName, byte[] content)
        {
            return Task.FromResult(DocPurposeJudge.NotAvailable("视觉模型未接入，无法识别文档分类/作用"));
        }
    }

    /// <summary>
    /// 图片预处理（省 token）。
    ///
    /// <para><b>为什么值得单独一个类</b>：图片按 <b>token</b> 计费，实测 1520×1240 的图
    /// ≈ <b>1874 image_tokens</b>（2026-10-03 实测）；压到长边 1600 大约省一半。
    /// 图片量少时无所谓，但企业一次上传几十份扫描件时差距就出来了。</para>
    ///
    /// <para><b>⛔ 当前不真正缩放</b>：本项目部署在容器里，缩图需要
    /// <c>System.Drawing</c>（Linux 需 <c>libgdiplus</c>）或 <c>ImageSharp</c>（多一个依赖）。
    /// 这里保留钩子 + 兜底原样返回（<b>宁可大一点也不失败</b>），将来接库时只改这一处。</para>
    /// </summary>
    public static class ImagePreprocess
    {
        /// <summary>小于此值直接原样返回（不值得折腾）</summary>
        private const int SkipBelowBytes = 300 * 1024;

        /// <summary>
        /// 尽力缩到长边 ≤ <paramref name="maxEdge"/>；无缩放能力时原样返回。
        /// </summary>
        /// <param name="mime">按<b>魔数</b>判定的真实 MIME（⛔ 不信文件名，可伪造）</param>
        public static byte[] ScaleDown(byte[] data, int maxEdge, out string mime)
        {
            mime = DetectMime(data);
            if (data.Length <= SkipBelowBytes) return data;
            // ⛔ 待接缩放库（ImageSharp 等）。接上后这里返回压缩字节，调用方零改动。
            return data;
        }

        /// <summary>按<b>魔数</b>判 MIME</summary>
        public static string DetectMime(byte[] d)
        {
            if (d == null || d.Length < 12) return "image/png";

            // PNG：89 50 4E 47
            if (d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) return "image/png";
            // JPEG：FF D8 FF
            if (d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
            // GIF：47 49 46
            if (d[0] == 0x47 && d[1] == 0x49 && d[2] == 0x46) return "image/gif";
            // WEBP：52 49 46 46 … 57 45 42 50
            if (d[0] == 0x52 && d[1] == 0x49 && d[2] == 0x46 && d[3] == 0x46
                && d[8] == 0x57 && d[9] == 0x45 && d[10] == 0x42 && d[11] == 0x50) return "image/webp";
            // BMP：42 4D
            if (d[0] == 0x42 && d[1] == 0x4D) return "image/bmp";

            return "image/png";
        }

        /// <summary>剥掉模型常加的 ``` 围栏（否则整段 Markdown 会被下游当成代码块）</summary>
        public static string StripCodeFence(string? content)
        {
            var t = (content ?? "").Trim();
            if (!t.StartsWith("```", System.StringComparison.Ordinal)) return t;
            var firstNl = t.IndexOf('\n');
            if (firstNl > 0) t = t[(firstNl + 1)..].Trim();
            if (t.EndsWith("```", System.StringComparison.Ordinal)) t = t[..^3].Trim();
            return t;
        }
    }
}