using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Office;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai
{
    /// <summary>
    /// AI 表格填写 —— 给定「<b>表格要什么列</b>」+「已过滤的企业文档」，让模型产出<b>行数组</b>
    /// （39 号 §八），转成 <see cref="TablePayload"/> 供 <c>fill_table</c> 消费。
    ///
    /// <para><b>★★ 为什么列是<b>输入</b>而不是让 AI 自己定</b>（39 号 §8.3）：
    /// 列由<b>模板</b>决定（模板样板行写 <c>{{col:FieldCode}}</c>，由扫描器扫出）。
    /// 若让 AI 定列，它返回的列名与模板列<b>对不上</b>时，<c>fill_table</c> 的列映射层
    /// 会按「缺键 ⇒ 写空」处理 ⇒ <b>整列数据静默消失</b>。</para>
    ///
    /// <para><b>★ 为什么输出是 <see cref="TablePayload"/> 而不是 <see cref="FillValue"/></b>：
    /// 表格不是「一个格子的值」，而是「一个区域的数据」——
    /// 它的落笔方式由 <c>fill_table</c> 按 <c>FileKind</c> 分派（39 号 §11.3），
    /// 本 Skill ⛔ <b>不碰定位参数</b>（那是「泄漏点②」）。</para>
    /// </summary>
    [Skill(
        Code = "src_ai_table",
        Name = "AI 表格填写",
        ReturnType = "json",
        Description = "提示词 + 已过滤企业文档 → 动态分析出表格数据。"
    )]
    public static class SrcAiTableSkill
    {
        /// <summary>本 Skill 编码</summary>
        public const string SkillCode = "src_ai_table";

        /// <summary>执行 —— 解析列定义 → 装配提示词 → 调模型 → 组装 <see cref="TablePayload"/>。</summary>
        /// <param name="table_tag">表格标签（对应模板 <c>{{table:Tag}}</c>，必填）</param>
        /// <param name="columns_json">列定义 JSON（★ 来自模板扫描，⛔ 不是 AI 给的）</param>
        /// <param name="instruction">
        /// 表格要填什么（填写说明）。
        /// <para>⚠️ <b>与 39 号 §8.2 的一处偏离</b>：§8.2 的签名把 <c>instruction</c> 写成必填。
        /// 本实现改为<b>可选</b> —— 表格的「要填什么」<b>已经由列定义表达</b>
        /// （<c>columns_json</c> 的 <c>title</c> 就是中文列名），再加一个必填说明
        /// 只会让「配不出这个参数」变成硬失败。空 ⇒ 退化为「按列名从资料里找」。</para>
        /// </param>
        /// <param name="enterprise_docs">★ 已过滤的企业文档 markdown</param>
        /// <param name="prompt_code">提示词编码；空 ⇒ 用内置默认模板</param>
        /// <param name="max_rows">最多返回行数（<c>&lt;=0</c> = 不限制）。★ 保护：模型偶尔会返回几百行</param>
        /// <param name="org_code">机构编码（空 ⇒ 只命中全局提示词）</param>
        /// <param name="db">数据访问（DI 注入）</param>
        /// <param name="llm">AI 调用器（DI 注入）</param>
        /// <param name="ct">取消令牌</param>
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "表格标签（对应模板 {{table:Tag}}）")]
            string table_tag,

            [SkillParam(Description = "列定义 JSON：[{\"field_code\":\"name\",\"title\":\"姓名\",\"kind\":\"text\"}]",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string columns_json,

            [SkillParam(Description = "表格要填什么（填写说明）", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? instruction = null,

            [SkillParam(Description = "★ 已过滤的企业文档 markdown",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? enterprise_docs = null,

            [SkillParam(Description = "提示词编码 cert_doc_fill_prompt.PromptCode；空=用内置默认模板")]
            string? prompt_code = null,

            [SkillParam(Description = "最多返回行数（保护，0=不限制）")]
            int max_rows = 200,

            [SkillParam(Description = "机构编码（空=只命中全局提示词）")]
            string? org_code = null,

            [FromService] IDbOrm db = null!,
            [FromService] IAiFillInvoker llm = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(table_tag))
                return SkillResult.Fail("table_tag 不能为空");

            if (string.IsNullOrWhiteSpace(columns_json))
                return SkillResult.Fail("columns_json 不能为空");

            // ★ 列定义必须**能解析出列** —— 解析不出就直接 Fail（⛔ 不让 AI 自己定列，见类注释）
            var columns = TablePayloadFactory.ParseColumns(columns_json);
            if (columns.Count == 0)
                return SkillResult.Fail($"columns_json 解析不出任何列：{Truncate(columns_json)}");

            var anchor = new AiFillAnchorSpec
            {
                AnchorCode = table_tag,
                Instruction = instruction ?? string.Empty,
                ValueKind = "table",
                Section = AiFillJsonReader.SectionTables,
                Columns = columns.Select(c => new AiFillColumnSpec
                {
                    FieldCode = c.FieldCode,
                    Title = string.IsNullOrWhiteSpace(c.Title) ? c.FieldCode : c.Title!,
                    ValueKind = string.IsNullOrWhiteSpace(c.ValueKind) ? "text" : c.ValueKind!,
                    NumberFormat = c.NumberFormat,
                }).ToList(),
            };

            var prompt = await AiFillPromptBuilder.BuildSingleAsync(
                db, prompt_code, SkillCode, anchor,
                enterpriseDocs: enterprise_docs, orgCode: org_code, ct: ct);

            var resp = await llm.InvokeAsync(db, prompt, ct);
            if (!resp.Success) return SkillResult.Fail(resp.Error ?? "AI 调用失败");

            // ★ tables.{table_tag} 是**数组**（39 号 §12.4），必须用 ReadSectionRaw ——
            //   ReadObject 只处理对象、ReadSectionValue 只处理标量，用错会得到「取不到」而不报错。
            var node = AiFillJsonReader.ReadSectionRaw(
                resp.Root, AiFillJsonReader.SectionTables, table_tag);

            if (node == null)
                return SkillResult.Fail($"AI 未返回 tables.{table_tag}");

            // ★ 列用**输入的**（模板扫描结果），⛔ 不用 AI 返回里的（见类注释）
            var payload = TablePayloadFactory.FromAiNode(table_tag, columns, node, max_rows);

            if (payload.Rows.Count == 0)
                return SkillResult.Fail($"tables.{table_tag} 没有解析出任何数据行");

            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["table"] = payload,                     // ★ fill_table 的输入
                ["table_tag"] = table_tag,
                ["row_count"] = payload.Rows.Count,
                ["column_count"] = payload.Columns.Count,
                ["hit"] = true,
            });
        }

        private static string Truncate(string s) => s.Length <= 120 ? s : s[..120] + "…";
    }
}
