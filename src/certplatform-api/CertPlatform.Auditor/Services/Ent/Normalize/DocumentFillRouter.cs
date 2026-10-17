using System;

namespace CertPlatform.Auditor.Services.Ent.Normalize
{
    /// <summary>
    ///     <b>规范化四路分流</b>（`67` 号 §6.1）—— 由**两个正交维度**决定走哪条路。
    ///
    ///     <para>维度① <c>DocCategory</c> —— 这份文档<b>要不要被重新编写</b>（fixed / editable）</para>
    ///     <para>维度② <c>Replaceable</c> —— 内容<b>是否依赖企业文档</b>（false = 标准自带，企业不用交）</para>
    ///
    ///     <para><b>为什么抽成独立纯函数</b>：分流判定是整条链路的<b>第一个岔口</b>，
    ///     判错 ⇒ 后面全白做；而它本身<b>零依赖</b>（只读两个字段）。
    ///     抽出来后可以<b>不起 DB / 不起对象存储</b>直接单测
    ///     （见 <c>CertPlatform.Admin.Tests/Fill/DocumentFillRouterTests.cs</c>）。</para>
    /// </summary>
    public enum DocumentFillRoute
    {
        /// <summary>依赖企业文档 × <c>fixed</c> ⇒ ② 文件匹配（读画像里已算好的匹配结论）</summary>
        FixedMatch,

        /// <summary>不依赖企业文档 × <c>fixed</c> ⇒ ① 快归档（标准自带原件按目录结构归档）</summary>
        FixedArchive,

        /// <summary>依赖企业文档 × <c>editable</c> ⇒ 七步填充链（取企业资料 → Office 落笔）</summary>
        FillWithEnterprise,

        /// <summary>不依赖企业文档 × <c>editable</c> ⇒ 七步填充链，⛔ <b>不读企业资料</b></summary>
        FillStandardOnly,
    }

    /// <summary>四路分流的判定（纯函数，无副作用，可单测）</summary>
    public static class DocumentFillRouter
    {
        /// <summary>
        ///     按「文档分类 × 是否依赖企业文档」判定路由。
        ///
        ///     <para>⚠️ <b>未知分类一律按 <c>editable</c> 处理</b>（含 <c>hybrid</c>）——
        ///     与旧实现口径一致：旧代码只判 <c>category == "fixed"</c>，其余全走填充链。
        ///     ⛔ 不要在这里把未知值判成「跳过」，那会让历史脏数据整批静默不生成。</para>
        ///
        ///     <para>⚠️ 分类比较**忽略大小写并 Trim** —— 库里曾有 <c>"Fixed"</c> / <c>" fixed "</c>
        ///     这类人工录入值（旧代码先 <c>ToLowerInvariant</c> 再比，行为等价）。</para>
        /// </summary>
        /// <param name="docCategory"><c>cert_standard_directory_file.DocCategory</c>（可空）</param>
        /// <param name="dependsOnEnterprise">
        ///     <c>cert_standard_directory_file.Replaceable</c>：<c>true</c> = 依赖企业文档 /
        ///     <c>false</c> = 不依赖（标准自带）。
        /// </param>
        public static DocumentFillRoute Of(string? docCategory, bool dependsOnEnterprise)
        {
            var isFixed = string.Equals(
                (docCategory ?? "editable").Trim(), "fixed", StringComparison.OrdinalIgnoreCase);

            if (isFixed)
            {
                return dependsOnEnterprise
                    ? DocumentFillRoute.FixedMatch
                    : DocumentFillRoute.FixedArchive;
            }

            return dependsOnEnterprise
                ? DocumentFillRoute.FillWithEnterprise
                : DocumentFillRoute.FillStandardOnly;
        }

        /// <summary>该路由是否走「七步填充链」（即非 fixed 分支）</summary>
        public static bool IsFillChain(DocumentFillRoute route)
            => route is DocumentFillRoute.FillWithEnterprise or DocumentFillRoute.FillStandardOnly;
    }
}
