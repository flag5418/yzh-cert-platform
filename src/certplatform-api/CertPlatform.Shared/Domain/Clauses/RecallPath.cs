using System.Collections.Generic;

namespace CertPlatform.Shared.Domain.Clauses
{
    /// <summary>
    /// 召回方式（<b>6 种确定性轴 + RM7 语义轴</b>，跨标准不变）。见
    /// <c>docs/80-v4/15-章作用与召回路径模型-V1.md</c> §三 与
    /// <c>docs/80-v4/16-条款释义匹配方案-V1.md</c> §三（16 号 §八 已把「只此 6 种」修订为 6 + RM7）。
    ///
    /// <para>与 12 号"规则 RL1–RL5"区分：<b>路线用 RM</b>、<b>规则用 RL</b>（§九命名修订）。</para>
    /// </summary>
    public static class RecallAxis
    {
        /// <summary>RM1 条款轴：文档 <c>PrimaryClauses[]</c> ∩ 检查项条款（手册/程序/指导书）。</summary>
        public const string Clause = "RM1";

        /// <summary>RM2 过程轴：条款 <c>Process[]</c> ∩ 文档 <c>ProcessArea</c>（记录类<b>主轴</b>）。</summary>
        public const string Process = "RM2";

        /// <summary>RM3 部门轴：检查项 <c>DeptNames</c> ∩ 文档 <c>OwnerDept</c>（记录类主轴）。</summary>
        public const string Dept = "RM3";

        /// <summary>RM4 编号轴：记录编号 → 程序 → 条款（<b>仅</b>服务 7.5.x 文件控制）。</summary>
        public const string DocNo = "RM4";

        /// <summary>RM5 现场：标识/环境/设备状态等必须现场看。</summary>
        public const string Site = "RM5";

        /// <summary>RM6 访谈：承诺类条款（5.1 / 5.2.1）两段式获取。</summary>
        public const string Interview = "RM6";

        /// <summary>
        /// RM7 语义轴：无确定性键可用时，用条款 <c>ClauseText</c> 与文档画像的 BM25 相似度**反查**。
        /// <para>由条款作用类型 <c>ClauseCategory ∈ {策划,支持,运行,改进}</c> 驱动（非开关）；RM1–RM6 恒优先、RM7 仅补召回。</para>
        /// </summary>
        public const string Semantic = "RM7";
    }

    /// <summary>
    /// 一条召回路径 = 轴(RM) × 键(K) × 体裁过滤(章) × 提取方式(E) × 章作用(Role)。
    ///
    /// <para>由 <see cref="RecallPathResolver"/> 从条款属性自动生成；
    /// 落到 <c>cert_clause_attr</c> 时序列化为 <c>RecallRoutes</c> 属性（JSON 数组）。</para>
    ///
    /// <para>字段名保持 PascalCase（列名铁律），JsonSerializer 默认即此（⛔ 不配置 camelCase）。</para>
    /// </summary>
    public sealed class RecallPath
    {
        /// <summary>召回方式：RM1–RM6。</summary>
        public string Axis { get; set; } = string.Empty;

        /// <summary>轴键：RM1=条款号 / RM2=过程键(逗号分隔) / 其余为 null（运行时补，如部门、编号）。</summary>
        public string? Key { get; set; }

        /// <summary>体裁白名单（由章作用 Role 决定）：召回到的文档 <c>DocType</c> 必须落在其中。</summary>
        public List<string> DocTypes { get; set; } = new();

        /// <summary>默认提取方式（E1 条款摘录 / E2 表头抽行 / E3 摘要引用 / E4 两段式现场 / E5 现场观察）。</summary>
        public string Extract { get; set; } = string.Empty;

        /// <summary>章作用（r1–r7，= 国标 4–10 章语义）。</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>为什么选这条轴（写进 <c>InputSnapshotJson</c> 做路径记账，让"缺证据"可解释）。</summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 解析器入参：一条条款的属性三元组（+ 过程键）。
    /// 字段名与 <c>cert_clause_attr.AttrCode</c> 一致，便于装配入口直接映射。
    /// </summary>
    public sealed class ClauseRecallInput
    {
        /// <summary>条款号（4.4 / 7.1.5.1）。</summary>
        public string ClauseNumber { get; set; } = string.Empty;

        /// <summary>证据族：f1 制度族 / f2 执行族 / f1f2 混合族。</summary>
        public string? EvidenceFamily { get; set; }

        /// <summary>证据形态：cite / sample / site / interview。</summary>
        public string? EvidenceForm { get; set; }

        /// <summary>章作用：r1–r7（= 国标 4–10 章）。⛔ 直接读属性池，<b>不由条款号推导</b>（13485 章号≠Role）。</summary>
        public string? Role { get; set; }

        /// <summary>过程键（<c>cert_clause_attr.Process</c> 抽出的 purchase/inspection/…）。</summary>
        public List<string> ProcessKeys { get; set; } = new();
    }
}
