using System;
using System.Collections.Generic;
using System.Linq;

namespace CertPlatform.Shared.Storage
{
    /// <summary>
    /// ★★ <b>上传文件类型契约</b>（全平台后端唯一切面，2026-10-03 用户裁决）
    ///
    /// <para><b>为什么要有这个类</b>：修之前「哪些文件能上传」散落在各处 ——
    /// 前端 3 处各自硬编码 <c>accept</c>，后端只有 <c>EnterpriseOriginalService</c> 有一份私有白名单，
    /// 而标准目录 / 资料库的上传端点<b>根本不校验后缀</b>（任何后缀都能传）。
    /// 结果就是「发现一个堵一个」：先是 <c>.exe</c>，再是 <c>.zip</c>，再是 <c>.DS_Store</c>……</para>
    ///
    /// <para><b>规则（与前端 <c>cert-share/src/constants/upload-file-policy.ts</c> 逐字对齐）</b>：</para>
    /// <list type="number">
    ///   <item><b>系统噪声静默剔除</b>：<c>.DS_Store</c> / <c>Thumbs.db</c> / <c>~$xx.doc</c> /
    ///         <c>._xx</c> / 一切 <c>.</c> 开头。⇒ <b>不报给用户、不阻塞上传</b>
    ///         （它们本来就在每个目录里，用户从没打算上传；报「格式不支持」是错的）。</item>
    ///   <item><b>白名单</b>：文档 / 图片 / PDF 三类。⛔ 采白名单不采黑名单 —— 格式数不清，
    ///         黑名单必然漏。</item>
    ///   <item><b>永久黑名单</b>：可执行文件 / DLL / 脚本（html/svg 可内嵌脚本 = XSS 面）。
    ///         即使白名单写错，这些也永远拒。</item>
    /// </list>
    ///
    /// <para><b>⚠️ 前端过滤只是体验，本类是权威</b>：前端 <c>accept</c> 可绕过（改属性 / 直接调 API），
    /// 所有<b>写入对象的端点</b>都必须过 <see cref="Check"/>。</para>
    ///
    /// <para><b>PDF 的特殊之处</b>：不在本类，而在转换链 —— PDF <b>先试文本层直解</b>，
    /// 只有解不出才走 AI 视觉（见 <c>FileConvertCore.ConvertToMarkdownAtAsync</c> → OCR 兜底）。
    /// 白名单里 PDF 与其它格式同级，<b>判定顺序</b>由转换链负责，不在白名单里区分。</para>
    /// </summary>
    public static class UploadFilePolicy
    {
        #region 类别定义

        /// <summary>办公文档（Word/Excel/PPT/文本/RTF/OpenDocument）</summary>
        public static readonly IReadOnlyList<string> DocumentExtensions = new[]
        {
            ".doc", ".docx",
            ".xls", ".xlsx",
            ".ppt", ".pptx",
            ".rtf",
            ".odt", ".ods", ".odp",
            ".txt", ".md", ".csv",
        };

        /// <summary>图片（证件扫描件 / 现场照片）</summary>
        public static readonly IReadOnlyList<string> ImageExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        };

        /// <summary>PDF（文本层直解优先，失败才 AI）</summary>
        public static readonly IReadOnlyList<string> PdfExtensions = new[] { ".pdf" };

        /// <summary>
        /// 压缩包 —— <b>默认不开放</b>。
        /// <para>⛔ 不给开口子的理由：压缩包无法做内容抽取 / 语义分析 / 预览，
        /// 后续所有处理链都不支持它；放进来只会占 MinIO 与数据库。
        /// 将来若要「打包上传并自动解压」，正确做法是<b>新增解压任务</b>，不是放宽白名单。</para>
        /// </summary>
        public static readonly IReadOnlyList<string> ArchiveExtensions = new[]
        {
            ".zip", ".rar", ".7z", ".tar", ".gz",
        };

        #endregion

        #region 白名单 / 黑名单

        /// <summary>
        /// 默认允许的后缀（小写，含点）。<b>不含压缩包</b>。
        /// <para>⚠️ 与前端 <c>UPLOAD_ALLOWED_EXTENSIONS</c> 逐字一致 —— 改一处必须改另一处，
        /// 否则出现「前端能选、后端拒」或反之的错位。</para>
        /// </summary>
        public static readonly IReadOnlyList<string> AllowedExtensions =
            DocumentExtensions.Concat(ImageExtensions).Concat(PdfExtensions).ToList();

        /// <summary>永久黑名单：可执行文件 / 动态库 / 脚本执行器</summary>
        public static readonly IReadOnlyList<string> NeverExtensions = new[]
        {
            ".exe", ".dll", ".so", ".dylib", ".bat", ".cmd", ".sh", ".bash",
            ".msi", ".apk", ".deb", ".rpm",
        };

        /// <summary>
        /// 永久黑名单：HTML / SVG / XML —— 可内嵌脚本，放行等于给存储开 XSS 面。
        /// <para>⚠️ 本项目 Markdown 会被前端直接渲染（提示词工作台），SVG 内嵌
        /// <c>&lt;script&gt;</c> 时会执行。</para>
        /// </summary>
        public static readonly IReadOnlyList<string> ScriptExtensions = new[]
        {
            ".html", ".htm", ".xhtml", ".svg", ".xml",
        };

        /// <summary>操作系统 / 编辑器垃圾文件名（小写）</summary>
        private static readonly HashSet<string> SystemNoiseNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "thumbs.db", "ehthumb.db", "desktop.ini", "icon",
        };

        private static readonly HashSet<string> AllowedSet =
            new(AllowedExtensions, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> NeverSet =
            new(NeverExtensions, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ScriptSet =
            new(ScriptExtensions, StringComparer.OrdinalIgnoreCase);

        #endregion

        /// <summary>判定结果分类</summary>
        public enum PolicyReason
        {
            /// <summary>允许</summary>
            Allow = 0,

            /// <summary>系统噪声（静默剔除，不报给用户）</summary>
            SystemNoise = 1,

            /// <summary>后缀不在白名单 / 在永久黑名单</summary>
            BadExtension = 2,
        }

        /// <summary>判定结果</summary>
        /// <param name="Ok">是否允许上传</param>
        /// <param name="Reason">分类（<see cref="PolicyReason.Allow"/> 时无意义）</param>
        /// <param name="Extension">小写扩展名（含点）；无扩展名时为空串</param>
        /// <param name="Message">可直接展示给用户的中文说明</param>
        public readonly record struct Verdict(
            bool Ok,
            PolicyReason Reason,
            string Extension,
            string Message)
        {
            /// <summary>是否系统噪声（前端据此选「已自动忽略」而非「不支持」）</summary>
            public bool IsSystemNoise => Reason == PolicyReason.SystemNoise;
        }

        #region 判定

        /// <summary>扩展名（小写，含点）；无扩展名时返回空串</summary>
        public static string ExtOf(string? fileName)
        {
            var n = fileName ?? "";
            var i = n.LastIndexOf('.');
            // ⚠️ 点号不能是最后一个字符（`.gitignore` 的 i>0 判定），也不能在开头（隐藏文件）
            return i > 0 && i < n.Length - 1 ? n[i..].ToLowerInvariant() : "";
        }

        /// <summary>
        /// ★ 是否为系统噪声（操作系统 / 编辑器垃圾）。
        /// <para><b>用前缀规则而不是穷举黑名单</b>：macOS 会生成 <c>.DS_Store</c> / <c>.Spotlight-V100</c> /
        /// <c>.Trashes</c> / <c>.fseventsd</c>，Windows 有 <c>Thumbs.db</c> / <c>desktop.ini</c>，
        /// Office/WPS 会生成 <c>~$xx.doc</c> ⇒ 穷举永远列不全。</para>
        /// </summary>
        public static bool IsSystemNoise(string? fileName)
        {
            var n = (fileName ?? "").Trim();
            if (n.Length == 0) return true;
            if (n.StartsWith(".", StringComparison.Ordinal)) return true;
            if (n.StartsWith("~$", StringComparison.Ordinal)) return true;
            return SystemNoiseNames.Contains(n);
        }

        /// <summary>是否在默认白名单内</summary>
        public static bool IsAllowedExtension(string? extension)
            => extension.Length > 0 && AllowedSet.Contains(extension);

        /// <summary>
        /// ★ 判定单个文件能否上传。<b>所有写入对象的端点都必须调用它。</b>
        /// </summary>
        public static Verdict Check(string? fileName)
        {
            var name = (fileName ?? "").Trim();

            if (IsSystemNoise(name))
                return new Verdict(false, PolicyReason.SystemNoise, "",
                    $"「{name}」是系统文件，已自动忽略");

            var ext = ExtOf(name);
            if (ext.Length == 0)
                return new Verdict(false, PolicyReason.BadExtension, "",
                    $"「{name}」没有扩展名，无法判断类型");

            if (NeverSet.Contains(ext))
                return new Verdict(false, PolicyReason.BadExtension, ext,
                    $"「{name}」是程序文件，不允许上传");

            if (ScriptSet.Contains(ext))
                return new Verdict(false, PolicyReason.BadExtension, ext,
                    $"「{name}」是网页/脚本文件，不允许上传（存在脚本执行风险）");

            if (!AllowedSet.Contains(ext))
                return new Verdict(false, PolicyReason.BadExtension, ext,
                    $"「{name}」不是支持的资料格式（支持：{string.Join(" ", AllowedExtensions)}）");

            return new Verdict(true, PolicyReason.Allow, ext, "");
        }

        /// <summary>是否需要走 AI 视觉解析（PDF 无文本层 / 图片）—— <b>判定顺序由转换链负责，此处只回答「类型是否需要 AI」</b></summary>
        public static bool MayNeedVision(string? fileName)
        {
            var v = Check(fileName);
            if (!v.Ok) return false;
            return ImageExtensions.Contains(v.Extension, StringComparer.OrdinalIgnoreCase)
                   || PdfExtensions.Contains(v.Extension, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ★ 该<b>扩展名</b>是否为「图片 或 PDF」（§2 路径② 需走视觉模型 分类+作用+Markdown 的判定，2026-10-07）。
        /// <para>复用已有 <see cref="ImageExtensions"/> / <see cref="PdfExtensions"/>，⛔ 不另写清单；
        /// 接受带点（<c>.jpg</c>）或不带点（<c>jpg</c>），归一化后比对。</para>
        /// </summary>
        public static bool IsImageOrPdf(string? extension)
        {
            var ext = (extension ?? "").Trim().ToLowerInvariant();
            if (ext.Length == 0) return false;
            if (!ext.StartsWith(".", StringComparison.Ordinal)) ext = "." + ext;
            return ImageExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase))
                   || PdfExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}