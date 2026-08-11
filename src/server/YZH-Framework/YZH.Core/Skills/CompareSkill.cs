using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 确定性比较 Skill：compare / date_diff。
    /// SkillCode = "compare"（date_diff 作为别名通过同一实例处理）。
    /// </summary>
    public class CompareSkill : ISkillNode
    {
        public string SkillCode => "compare";

        public Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            var value = context.Inputs.TryGetValue("value", out var v) ? v : null;
            var op = context.Inputs.TryGetValue("operator", out var o) ? o?.ToString() : string.Empty;
            var threshold = context.Inputs.TryGetValue("threshold", out var t) ? t : null;
            var dateA = context.Inputs.TryGetValue("date_a", out var da) ? da : null;
            var dateB = context.Inputs.TryGetValue("date_b", out var db) ? db : null;
            var unit = context.Inputs.TryGetValue("unit", out var u) ? u?.ToString() : "day";

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
                    return Task.FromResult(new SkillResult { Success = false, Error = "date_a/date_b 无法解析为日期" });
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
                    return Task.FromResult(new SkillResult { Success = false, Error = "value 或 threshold 无法解析为数字" });
                }
            }
            else
            {
                return Task.FromResult(new SkillResult { Success = false, Error = "compare 需要 value+operator 或 date_a+date_b 入参" });
            }

            return Task.FromResult(new SkillResult { Success = true, Outputs = new Dictionary<string, object> { ["result"] = result } });
        }
    }
}
