using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 确定性比较 Skill：compare。
    /// 支持三种形态：① 数值比较（value + operator + threshold）；② 日期差（date_a + date_b + unit）；③ 非空判断（operator=not_empty）。
    /// 纯函数（SideEffect=false），输出 result（数值模式为 boolean，日期模式为 number）。
    /// </summary>
    public class CompareSkill : SkillBase
    {
        public override string SkillCode => "compare";
        public override string SkillName => "值比较";
        public override string Category => "data_process";
        public override bool SideEffect => false;
        public override string ReturnType => "boolean";

        public override IReadOnlyList<SkillParam> InputDecls { get; } = new[]
        {
            new SkillParam { Name = "value", Type = "json", Required = false, Description = "待比较值（数值/字符串）" },
            new SkillParam { Name = "operator", Type = "string", Required = false, Description = "比较运算符：> >= < <= == != equals not_equals not_empty" },
            new SkillParam { Name = "threshold", Type = "json", Required = false, Description = "比较阈值" },
            new SkillParam { Name = "date_a", Type = "date", Required = false, Description = "日期 A（日期差模式）" },
            new SkillParam { Name = "date_b", Type = "date", Required = false, Description = "日期 B（日期差模式）" },
            new SkillParam { Name = "unit", Type = "string", Required = false, Description = "日期差单位：day/month/year" }
        };

        public override IReadOnlyList<SkillParam> OutputDecls { get; } = new[]
        {
            new SkillParam { Name = "result", Type = "json", Required = true, Description = "比较结果（boolean，日期模式为 number）" }
        };

        protected override Task<SkillResult> ExecuteCoreAsync(SkillContext context, CancellationToken ct)
        {
            var value = Get(context, "value");
            var op = GetString(context, "operator");
            var threshold = Get(context, "threshold");
            var dateA = Get(context, "date_a");
            var dateB = Get(context, "date_b");
            var unit = string.IsNullOrWhiteSpace(GetString(context, "unit")) ? "day" : GetString(context, "unit");

            object result;

            if (dateA != null && dateB != null)
            {
                if (DateTime.TryParse(dateA.ToString() ?? string.Empty, out var da2) &&
                    DateTime.TryParse(dateB.ToString() ?? string.Empty, out var db2))
                {
                    var diff = da2 - db2;
                    result = unit.ToLowerInvariant() switch
                    {
                        "day" => (double)diff.TotalDays,
                        "month" => diff.TotalDays / 30.0,
                        "year" => diff.TotalDays / 365.0,
                        _ => (double)diff.TotalDays
                    };
                }
                else
                {
                    return Task.FromResult(SkillResult.Fail("date_a/date_b 无法解析为日期"));
                }
            }
            else if (op == "not_empty")
            {
                result = !string.IsNullOrWhiteSpace(value?.ToString());
            }
            else if (threshold != null && value != null)
            {
                if (double.TryParse(value.ToString(), out var val) && double.TryParse(threshold.ToString(), out var thr))
                {
                    result = op switch
                    {
                        ">" => val > thr,
                        ">=" => val >= thr,
                        "<" => val < thr,
                        "<=" => val <= thr,
                        "==" or "equals" => val == thr,
                        "!=" or "not_equals" => val != thr,
                        _ => throw new InvalidOperationException($"未知比较运算符: {op}")
                    };
                }
                else
                {
                    return Task.FromResult(SkillResult.Fail("value 或 threshold 无法解析为数字"));
                }
            }
            else
            {
                return Task.FromResult(SkillResult.Fail("compare 需要 value+operator 或 date_a+date_b 入参"));
            }

            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object> { ["result"] = result }));
        }

        private static object? Get(SkillContext context, string key)
            => context.Inputs.TryGetValue(key, out var v) ? v : null;

        private static string GetString(SkillContext context, string key)
            => context.Inputs.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
    }
}
