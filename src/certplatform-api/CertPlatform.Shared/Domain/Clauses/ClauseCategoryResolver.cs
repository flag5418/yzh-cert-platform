using System;

namespace CertPlatform.Shared.Domain.Clauses
{
    /// <summary>
    /// 条款作用类型（<c>ClauseCategory</c>）判定器 —— RM7 语义召回的**启用判据**。
    ///
    /// <para>见 <c>docs/80-v4/16-条款释义匹配方案-V1.md</c> §三.a：对 9001 / 14001 / 45001
    /// 全部条款号横向归纳得 6 类作用类型，<b>跨标准分类冲突 = 0</b>（只由条款号域决定，标准无关）。</para>
    ///
    /// <para><b>实现上不新造属性</b>：由 <c>Role</c>（r1–r7，= 国标 4–10 章功能角色）+ 条款号二级规则派生；
    /// 仅两处比 Role 更细：① 4 章拆「框架(章级/4.4) / 策划(4.1–4.3)」；② 9、10 章合并「改进」。</para>
    ///
    /// <para><b>纯函数</b>：不查库、可单测；⛔ 端内不得另写一份判据（守 A-R6「同一判据第二处即违规」）。</para>
    /// </summary>
    public static class ClauseCategoryResolver
    {
        public const string Framework = "框架";
        public const string Commitment = "承诺";
        public const string Planning = "策划";
        public const string Support = "支持";
        public const string Operation = "运行";
        public const string Improvement = "改进";

        /// <summary>判定条款作用类型；无法判定时返回 <see cref="Planning"/>（保守放行 RM7）。</summary>
        public static string Classify(string? role, string? clauseNumber)
        {
            var r = (role ?? string.Empty).Trim().ToLowerInvariant();
            var n = (clauseNumber ?? string.Empty).Trim();
            if (n.Length == 0) return Planning;

            return r switch
            {
                "r2" => Commitment,                                  // 5 章：方针 / 承诺 / 协商
                "r3" => Planning,                                    // 6 章：策划
                "r4" => Support,                                     // 7 章：支持
                "r5" => Operation,                                   // 8 章：运行
                "r6" or "r7" => Improvement,                         // 9/10 章：合并为改进
                "r1" => IsFrameworkNumber(n) ? Framework : Planning, // 4 章：细分框架 / 策划
                _ => Planning
            };
        }

        /// <summary>
        /// 是否需要追加 RM7 语义轴：<c>{策划, 支持, 运行, 改进}</c> 需要；<c>{框架, 承诺}</c> 不需要。
        /// </summary>
        public static bool NeedsSemantic(string? role, string? clauseNumber)
        {
            var category = Classify(role, clauseNumber);
            return category is Planning or Support or Operation or Improvement;
        }

        /// <summary>4 章的「框架」条款：章级（无“.”）或 4.4（含 4.4.x）。</summary>
        private static bool IsFrameworkNumber(string n)
            => n.IndexOf('.') < 0 || n == "4.4" || n.StartsWith("4.4.", StringComparison.Ordinal);
    }
}
