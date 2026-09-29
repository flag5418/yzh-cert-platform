using System;
using System.Collections.Generic;
using System.Linq;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>
    /// 文档库（MinIO 顶层前缀）—— 区分「标准目录」与「企业文档」两套上传体系。
    ///
    /// <para>★ 为什么需要这个抽象（2026-09-26 用户第 6 点）：</para>
    /// 企业资料上传与标准目录上传**过程一致**（同样是「文件夹 + 文件 + 转 PDF + 转 Markdown」），
    /// 因此转换链、预览链、提取链应当**共用同一套实现**，仅靠**输入路径前缀**区分归属。
    /// 本类型就是那个「前缀判据」的唯一权威源。
    ///
    /// <para>⚠️ 当前阶段只启用 <see cref="StandardDirectory"/>；<see cref="EnterpriseDocuments"/>
    /// 为已预留的第二个库（代码中 <c>DeriveOrgCodeFromPath</c> 早已识别该前缀）。
    /// 启用时**只需在 <see cref="Prefixes"/> 之外新增数据来源**，转换/预览/提取链零改动。</para>
    ///
    /// <para>★ MinIO 语义提醒：这里的「库」只是 **key 的前缀段**，不是真目录 ——
    /// MinIO/S3 是扁平 key 存储，控制台按 <c>/</c> 分组**显示**成目录。
    /// 因此「建库/建文件夹」**不需要任何 API 调用**，路径在代码里算好即可。</para>
    /// </summary>
    public enum DocumentLibrary
    {
        /// <summary>未知/不归属任何库（路径非法或不在白名单内）</summary>
        Unknown = 0,

        /// <summary>标准目录库：<c>/standard-directory/...</c></summary>
        StandardDirectory = 1,

        /// <summary>企业文档库：<c>/enterprise-documents/...</c>（已预留，尚未启用）</summary>
        EnterpriseDocuments = 2
    }

    /// <summary>文档库路径工具（前缀解析 + 白名单校验的唯一权威源）</summary>
    public static class DocumentLibraryPath
    {
        /// <summary>标准目录库前缀</summary>
        public const string StandardDirectoryPrefix = "standard-directory";

        /// <summary>企业文档库前缀</summary>
        public const string EnterpriseDocumentsPrefix = "enterprise-documents";

        /// <summary>全部合法库前缀（白名单）</summary>
        public static readonly IReadOnlyList<string> Prefixes = new[]
        {
            StandardDirectoryPrefix,
            EnterpriseDocumentsPrefix
        };

        /// <summary>取库对应的前缀段</summary>
        public static string PrefixOf(DocumentLibrary library) => library switch
        {
            DocumentLibrary.StandardDirectory => StandardDirectoryPrefix,
            DocumentLibrary.EnterpriseDocuments => EnterpriseDocumentsPrefix,
            _ => ""
        };

        /// <summary>
        /// 把路径规范化成「无前导斜杠、反斜杠转正斜杠、去空白」的形态，便于前缀比较。
        /// </summary>
        public static string Normalize(string? path)
            => (path ?? "").Replace('\\', '/').Trim().TrimStart('/');

        /// <summary>
        /// 解析路径所属文档库。
        /// <para>⚠️ 只按**首段**匹配（避免 <c>foo/standard-directory/x</c> 这种伪造路径被放行）。</para>
        /// </summary>
        public static DocumentLibrary Resolve(string? path)
        {
            var p = Normalize(path);
            if (p.Length == 0) return DocumentLibrary.Unknown;

            var firstSeg = p.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (firstSeg.Equals(StandardDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
                return DocumentLibrary.StandardDirectory;
            if (firstSeg.Equals(EnterpriseDocumentsPrefix, StringComparison.OrdinalIgnoreCase))
                return DocumentLibrary.EnterpriseDocuments;

            return DocumentLibrary.Unknown;
        }

        /// <summary>
        /// 存储路径白名单校验：必须位于**已知文档库**之下，且不含穿越片段与空段。
        ///
        /// <para>★ 替代原 <c>ControllerSafetyExtensions.IsAllowedStoragePath</c> 的硬编码
        /// <c>p.StartsWith("standard-directory/")</c> —— 那会让企业文档库的
        /// <c>file-preview</c>/<c>file-markdown</c>/<c>download</c> 请求**静默被拒**
        /// （业务失败恒 HTTP 200，前端只看到「未找到文件」，无线索指向白名单）。</para>
        /// </summary>
        public static bool IsAllowedStoragePath(string? path)
        {
            var p = Normalize(path);
            if (p.Length == 0) return false;

            var segs = p.Split('/');
            // 禁止穿越片段与空段
            if (segs.Any(seg => seg == ".." || seg == "." || seg.Length == 0)) return false;

            return Resolve(p) != DocumentLibrary.Unknown;
        }

        /// <summary>
        /// 判断给定路径是否位于某文档库之下（用于按库过滤查询）。
        /// </summary>
        public static bool IsUnder(string? path, DocumentLibrary library)
        {
            if (library == DocumentLibrary.Unknown) return false;
            return Resolve(path) == library;
        }
    }
}
