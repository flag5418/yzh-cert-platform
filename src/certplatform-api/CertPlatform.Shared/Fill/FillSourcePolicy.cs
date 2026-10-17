using System;

namespace CertPlatform.Shared.Fill
{
    /// <summary>
    ///     <b>锚点来源的企业依赖性判据</b>（`67` 号 §6.2）—— 单一事实来源。
    ///
    ///     <para><b>为什么需要它</b>：文档可以标成「<b>不依赖企业文档</b>」
    ///     （<c>cert_standard_directory_file.Replaceable = false</c>，典型 = 导航页 / 固定描述）。
    ///     这类文档由后台按规则<b>自动生成</b>，企业⛔ 不用交任何东西 ⇒ 它的锚点
    ///     若配了「要从企业资料里取」的来源，<b>必然取不到值</b>。</para>
    ///
    ///     <para>本判据有两个消费点，<b>必须共用同一个函数</b>（⛔ 不许各写一套）：</para>
    ///     <list type="number">
    ///       <item><b>配置期</b>：保存文档属性时给警告（<c>StandardDocContractController.Save</c>）；</item>
    ///       <item><b>运行期</b>：编排器在「不依赖企业文档」的文档上<b>跳过</b>这类来源并写实归因
    ///             （<c>DocumentFillOrchestrator</c>）。</item>
    ///     </list>
    ///
    ///     <para>⚠️ 两处各写一套 = 典型「同一判定两处并存必然漂移」⇒ 会出现
    ///     「配置期没警告、运行期也不报错，只是值一直是空的」这种最坏形态。</para>
    ///
    ///     <para>⚠️ 本类<b>纯函数、零依赖</b>（不碰 DB / 不碰 IO）⇒ 可单测
    ///     （见 <c>CertPlatform.Admin.Tests/Fill/FillSourcePolicyTests.cs</c>）。</para>
    ///
    ///     <para><b>★★ 与「AI 节点 ② 是否依赖企业资料」的区别（⛔ 别把两者混为一谈）</b>：</para>
    ///     <list type="table">
    ///       <listheader><term></term><description>AI 节点 ②（<c>SourceSpecEntry.DependsOnEnterprise</c>）
    ///         vs 文档级（<c>cert_standard_directory_file.Replaceable</c>）</description></listheader>
    ///       <item><term>粒度</term><description>锚点级 / 文档级</description></item>
    ///       <item><term>强度</term>
    ///             <description><b>软</b> —— ② 只是在 AI 指令末尾<b>追加一句话</b>
    ///             （<c>DocumentFillOrchestrator.BuildAiInstructionAsync</c>：
    ///             「本项不依赖企业资料，仅依据上述参考参数与提示词作答。」），
    ///             <b>并不真的把企业文档从提示词里摘掉</b>；
    ///             / <b>硬</b> —— 文档级为 false 时，编排器<b>真的不读画像</b>，
    ///             <c>EnterpriseDocs</c> 恒为空串</description></item>
    ///       <item><term>本判据看谁</term>
    ///             <description>⛔ <b>只看「这个来源靠什么取证」的机制事实</b>（即 kind），
    ///             <b>不看 ② 软开关</b> —— 判据要跟机制对齐，不跟提示词对齐。
    ///             （② 说「不依赖」，但来源本身是「从企业文档抽字段」⇒ 机制上仍需要企业文档。）</description></item>
    ///     </list>
    /// </summary>
    public static class FillSourcePolicy
    {
        /// <summary>「企业档案」表达式命名空间前缀（<c>ReplaceResolver</c> 认领 <c>enterprise.*</c>）</summary>
        private const string EnterpriseNamespace = "enterprise.";

        /// <summary>
        ///     该来源<b>是否依赖企业交上来的资料</b>。
        ///
        ///     <para>判据按「这个来源的取值<b>证据从哪来</b>」分档（⛔ 不按「名字里有没有 ai」猜）：</para>
        ///
        ///     <list type="table">
        ///       <listheader><term>kind</term><description>依赖？· 依据</description></listheader>
        ///       <item><term><c>profile</c></term>
        ///             <description>✅ <b>依赖</b> —— 直接读企业画像（<c>cert_enterprise_doc_profile</c>）</description></item>
        ///       <item><term><c>replace</c> 且 <c>enterprise.*</c></term>
        ///             <description>✅ <b>依赖</b> —— 读企业档案（<c>cert_enterprise</c>）</description></item>
        ///       <item><term><c>ai_field</c></term>
        ///             <description>✅ <b>依赖</b> —— <c>SrcAiFieldSkill</c> 自述「内容不确定（<b>企业有什么</b>）」，
        ///                 且 <c>enterprise_docs</c> 是其<b>唯一证据</b>（<c>AllowEmpty = Deny</c>）</description></item>
        ///       <item><term><c>ai_table</c></term>
        ///             <description>✅ <b>依赖</b> —— 同 <c>ai_field</c>（从企业文档抽表格）</description></item>
        ///       <item><term><c>ai</c>（旧值）</term>
        ///             <description>✅ <b>按依赖处理</b> —— 形态未定，编排器会在
        ///                 <c>ai_semantic</c> / <c>ai_field</c> / <c>ai_table</c> 三者间回落；
        ///                 判不了就<b>宁可提示一次</b>，⛔ 不要静默放过去</description></item>
        ///       <item><term><c>ai_semantic</c></term>
        ///             <description>❌ <b>不依赖</b> —— <c>SrcSemanticSkill</c> 改写的是<b>标准原文</b>
        ///                 （必填参 <c>source_text</c>），与「企业有什么」无关
        ///                 —— <b>这正是本路径的推荐来源</b></description></item>
        ///       <item><term><c>global</c> / <c>manual</c></term>
        ///             <description>❌ <b>不依赖</b> —— 读参数表 / 人工裁决值，都不是企业交上来的文件</description></item>
        ///       <item><term><c>headerFooter</c>（<c>@doc_*</c>）</term>
        ///             <description>❌ <b>不依赖</b> —— 读文档元信息（编号 / 阶段名 / 日期）</description></item>
        ///       <item><term><c>replace</c> 且 <c>org.*</c> / <c>system.*</c></term>
        ///             <description>❌ <b>不依赖</b> —— 机构信息 / 系统变量（日期等）</description></item>
        ///       <item><term>未知 kind</term>
        ///             <description>❌ <b>不依赖</b> —— 保守取「不报」，避免把未来的新来源误判成配错</description></item>
        ///     </list>
        /// </summary>
        /// <param name="kind"><c>SourceSpecEntry.Kind</c>（大小写不敏感；空 ⇒ 不依赖）</param>
        /// <param name="ref"><c>SourceSpecEntry.Ref</c>（仅 <c>replace</c> 用得上：看命名空间）</param>
        public static bool DependsOnEnterpriseMaterial(string? kind, string? @ref)
        {
            if (string.IsNullOrWhiteSpace(kind)) return false;

            var k = kind.Trim().ToLowerInvariant();

            switch (k)
            {
                case "profile":
                case "ai_field":
                case "ai_table":
                    return true;

                // ★ 旧值 `ai`：形态未定（编排器按锚点 section 回落三选一）
                //   ⇒ 判不了就按「可能依赖」处理。代价 = 多一条警告；收益 = 不会静默配错。
                case "ai":
                    return true;

                case "replace":
                    // 只有 `enterprise.*` 是读企业档案；`org.*` / `system.*` 不是
                    return @ref != null
                        && @ref.TrimStart().StartsWith(EnterpriseNamespace, StringComparison.OrdinalIgnoreCase);

                default:
                    return false;
            }
        }

        /// <summary>
        ///     给「配了企业资料类来源、但文档标了不依赖企业文档」的场景写<b>人话归因</b>。
        ///
        ///     <para>⛔ 不要写成「来源链未命中」—— 那句话把人引向「去企业端补资料」，
        ///     而这份文档<b>本来就不该要企业资料</b>（指错方向比不报错更耗时）。</para>
        /// </summary>
        /// <param name="kind">来源类型（原样回显，便于用户去页面上找）</param>
        /// <param name="ref">来源引用（原样回显，可为空）</param>
        public static string DescribeMismatch(string? kind, string? @ref)
        {
            var target = string.IsNullOrWhiteSpace(@ref) ? kind : $"{kind} · {@ref}";
            return $"该文档标记为「不依赖企业文档」，但锚点配了需要企业资料的来源（{target}）"
                 + " —— 请二选一：① 把文档属性改回「依赖企业文档」；② 把该锚点改配为"
                 + "「AI 语义改写（标准原文）」「全局参数」或「人工裁决值」";
        }

        /// <summary>配置期警告（一条来源一条，前端逐条展示）</summary>
        public static string DescribeConfigWarning(string? anchorRef, string? kind, string? @ref)
        {
            var anchor = string.IsNullOrWhiteSpace(anchorRef) ? "(未命名锚点)" : anchorRef;
            var target = string.IsNullOrWhiteSpace(@ref) ? kind : $"{kind} · {@ref}";
            return $"锚点「{anchor}」的来源（{target}）需要企业资料，"
                 + "但本文档已标为「不依赖企业文档」⇒ 运行期该锚点会取不到值";
        }
    }
}
