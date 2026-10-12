using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Workflow.Models;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill
{
    /// <summary>
    /// 表格填写（操作类）—— ★ <b>纯逻辑已下沉到
    /// <see cref="CertPlatform.Shared.Fill.TableRegionBuilder"/></b>（2026-10-12）。
    ///
    /// <para><b>为什么拆</b>：专家端的 <c>DocumentFillOrchestrator</c> 要直接调这段逻辑，
    /// 而它原本长在本类里（后台端工作流 Skill）⇒ 违反三方结构铁律
    /// （<c>Auditor ⛔ 引 Admin</c>）。现本类只剩 <c>[Skill]</c> 壳：
    /// <b>参数绑定 + <c>SkillResult</c> 包装</b>，逻辑唯一出处是 Shared 的构建器。</para>
    ///
    /// <para>⛔ <b>不要在别处再写一份列映射</b> —— 那正是「同一段逻辑两处实现 ⇒ 静默漂移」的入口。</para>
    /// </summary>
    [Skill(
        Code = "fill_table",
        Name = "表格填写",
        ReturnType = "number",
        ResultPort = "row_count",
        Description = "把表格数据装配成区域填充指令（Word 表格 / Excel 区域）。★ 不落盘。"
    )]
    public static class FillTableSkill
    {
        /// <summary>本 Skill 编码</summary>
        public const string SkillCode = "fill_table";

        /// <summary>执行 —— 委托 <see cref="TableRegionBuilder.Append"/>（列映射 → 按文件类型分派 → 追加区域指令）。</summary>
        /// <param name="session">★ 填充会话（编排器直接放对象实例；空 ⇒ 新建）</param>
        /// <param name="table">表格数据</param>
        /// <param name="start_row">Excel 起始行（0-based）。⚠️ &lt;0 时落 0</param>
        /// <param name="start_col">Excel 起始列（0-based）。&lt;0 时落 0</param>
        /// <param name="sheet_name">Excel 工作表名（空 ⇒ 第 1 个工作表）</param>
        /// <param name="ct">取消令牌</param>
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "★ 填充会话（编排器直接放对象实例；空=新建）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            FillSession? session = null,

            [SkillParam(Description = "表格数据：{table_tag, columns, rows, totals}",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            TablePayload? table = null,

            [SkillParam(Description = "Excel 起始行（0-based；<0 落 0）")]
            int start_row = -1,

            [SkillParam(Description = "Excel 起始列（0-based；<0 落 0）")]
            int start_col = -1,

            [SkillParam(Description = "Excel 工作表名（空=第 1 个工作表）")]
            string? sheet_name = null,

            CancellationToken ct = default)
        {
            session ??= new FillSession();

            var (ok, error, errorCode, rowCount) =
                TableRegionBuilder.Append(session, table, start_row, start_col, sheet_name);

            if (!ok)
                return Task.FromResult(SkillResult.Fail(error!, errorCode));

            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
            {
                ["session"] = session,
                ["region_count"] = session.Request.Regions.Count,
                ["row_count"] = rowCount,
                ["location"] = session.Request.Regions[^1].Describe(),
            }));
        }
    }
}
