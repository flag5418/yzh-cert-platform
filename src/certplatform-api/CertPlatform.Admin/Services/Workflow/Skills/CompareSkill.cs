using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Workflow.Models;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// 值比较：接收两个字符串值 + 运算符，执行确定性比较。纯函数，无副作用。
    /// 日期/数值/字符串类型在函数内部自动判断和转换，无需调用方区分。
    /// <para>移植自：旧 YZH.Core.Skills.CompareSkill.cs（零改动，仅 namespace 调整）</para>
    ///
    /// <para><b>★★ 2026-10-12 用户点名要收紧的节点</b>：「比较节点（2 个字段的比较）……
    /// 如果上游节点值为空，都需要终止整个流程，因为路径没跑通很大可能影响整个结果」。
    /// ⇒ 三个入参全部 <c>AllowEmpty = SkillParamEmptyPolicy.Deny</c>：任一侧没值 ⇒ 中断当前检查项 + 落补录缺口，
    /// ⛔ 不再「拿着空串比出一个结果」。</para>
    ///
    /// <para><b>★ 输出契约</b>：<c>result</c>（bool）= 主输出 ——
    /// <c>ExecuteBranch</c> 只认这个键 ⇒ 本节点可直连 <c>branch</c> 的 <c>condition</c> 端口。</para>
    /// </summary>
    [Skill(
        Code = "compare",
        Name = "值比较",
        ReturnType = "boolean",
        ResultPort = "result",
        Description = "确定性比较：支持数值比较（> >= < <= == !=）和日期比较（自动解析日期格式，按天计算差值）"
    )]
    public static class CompareSkill
    {
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "比较值 A（数值/日期/字符串）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? value_a = null,

            [SkillParam(Description = "比较值 B（数值/日期/字符串）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? value_b = null,

            [SkillParam(Description = "运算符", BindMode = SkillParamBindMode.Enum,
                        EnumSource = "compare_operator", AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? @operator = null,

            CancellationToken ct = default)
        {
            // ⚠️ 下面这段是「逃生舱」：正常经工作流引擎调用时，
            //    SkillExecutor 的入参校验（AllowEmpty=false）会先一步拦下并返回
            //    PRECONDITION_EMPTY ⇒ 根本走不到这里。保留它是为了直接调用方
            //    （单测 / 编排器 / 将来的非引擎路径）也能得到明确失败。
            if (string.IsNullOrWhiteSpace(value_a) || string.IsNullOrWhiteSpace(value_b))
                return Task.FromResult(SkillResult.Fail(
                    "value_a 和 value_b 均不能为空", WorkflowErrorCodes.PreconditionEmpty));

            if (string.IsNullOrWhiteSpace(@operator))
                return Task.FromResult(SkillResult.Fail(
                    "operator 不能为空", WorkflowErrorCodes.PreconditionEmpty));

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
                    ["result"] = result,
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
                    ["result"] = result,
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
                ["result"] = strResult,
                ["compare_result"] = strResult
            }));
        }
    }
}
