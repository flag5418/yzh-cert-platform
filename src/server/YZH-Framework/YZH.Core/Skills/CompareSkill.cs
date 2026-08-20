using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 值比较：接收两个字符串值 + 运算符，执行确定性比较。纯函数，无副作用。
    /// 日期/数值/字符串类型在函数内部自动判断和转换，无需调用方区分。
    /// </summary>
    [Skill(
        Code = "compare",
        Name = "值比较",
        ReturnType = "boolean",
        Description = "确定性比较：支持数值比较（> >= < <= == !=）和日期比较（自动解析日期格式，按天计算差值）"
    )]
    public static class CompareSkill
    {
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "比较值 A（数值/日期/字符串）", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? value_a = null,

            [SkillParam(Description = "比较值 B（数值/日期/字符串）", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? value_b = null,

            [SkillParam(Description = "运算符", BindMode = SkillParamBindMode.Enum, EnumSource = "compare_operator")]
            string? @operator = null,

            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(value_a) || string.IsNullOrWhiteSpace(value_b))
                return Task.FromResult(SkillResult.Fail("value_a 和 value_b 均不能为空"));

            if (string.IsNullOrWhiteSpace(@operator))
                return Task.FromResult(SkillResult.Fail("operator 不能为空"));

            var op = @operator!.Trim();

            // 尝试数值比较
            if (double.TryParse(value_a, out var va) && double.TryParse(value_b, out var vb))
            {
                var result = op switch
                {
                    ">" => va > vb,
                    ">=" => va >= vb,
                    "<" => va < vb,
                    "<=" => va <= vb,
                    "==" => Math.Abs(va - vb) < 0.000001,
                    "!=" => Math.Abs(va - vb) >= 0.000001,
                    _ => throw new InvalidOperationException($"未知运算符: {op}")
                };
                return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
                {
                    ["compare_result"] = result
                }));
            }

            // 尝试日期比较
            if (DateTime.TryParse(value_a, out var da) && DateTime.TryParse(value_b, out var db))
            {
                var diffDays = (da - db).TotalDays;
                var result = op switch
                {
                    ">" => diffDays > 0,
                    ">=" => diffDays >= 0,
                    "<" => diffDays < 0,
                    "<=" => diffDays <= 0,
                    "==" => Math.Abs(diffDays) < 1, // 同一天视为相等
                    "!=" => Math.Abs(diffDays) >= 1,
                    _ => throw new InvalidOperationException($"未知运算符: {op}")
                };
                return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
                {
                    ["compare_result"] = result,
                    ["diff_days"] = diffDays
                }));
            }

            // 字符串比较
            var strResult = op switch
            {
                "==" => value_a == value_b,
                "!=" => value_a != value_b,
                _ => throw new InvalidOperationException($"运算符 {op} 不支持字符串比较，仅支持 == 和 !=")
            };
            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
            {
                ["compare_result"] = strResult
            }));
        }
    }
}
