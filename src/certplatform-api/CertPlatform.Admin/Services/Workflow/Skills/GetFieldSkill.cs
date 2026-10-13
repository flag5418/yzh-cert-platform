
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.Workflow.Models;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Workflow.Skills;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// 获取字段值：按 field_code + enterprise_code 查询提取结果。
    /// <para>移植自：旧 YZH.Builder .../Skills/GetFieldSkill.cs</para>
    /// <para>迁移改写：EF VOLContext → IDbOrm.GetListAsync（SqlSugar 强类型查询，与 NodeExecutor docField 同数据源）</para>
    ///
    /// <para><b>★★ 2026-10-12 两处修正</b>：</para>
    /// <list type="number">
    /// <item><b>「取不到」不再 Fail，改为返回空值</b>（<c>success:true</c> + <c>result:null</c>）——
    ///       与 <c>docField</c> 节点<b>同口径</b>（2026-10-11 用户裁定：「取数」与「判空」正交）。
    ///       修复前它 <c>Fail("未找到 field_code=…")</c>，而 <c>SkillExecutor</c> 又会把失败吞掉，
    ///       下游拿到的是 <c>{success:false, result:{}}</c> —— 既没中断路径，也没法判空，
    ///       实测导致 <c>get_field → is_empty</c> <b>恒判「非空」</b>。
    ///       改为返回空值后，正确图例 = <c>get_field → is_empty → branch</c>。</item>
    /// <item><b><c>ResultPort = "field_value"</c></b> —— 连线下游靠 <c>result</c> 拿到<b>那一个字段值</b>，
    ///       ⛔ 不再是整包字典（<c>{field_code, field_name, field_value, …}</c>）。</item>
    /// </list>
    /// </summary>
    [Skill(
        Code = "get_field",
        Name = "获取字段值",
        ReturnType = "string",
        ResultPort = "field_value",
        Description = "按字段编码和企业编码查询已提取的文档字段值"
    )]
    public static class GetFieldSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "字段编码，如 iso9001.ent_base.biz_lic.Name",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string field_code,
            // ★ AllowEmpty = SkillParamEmptyPolicy.Allow：描述写着「空 = 标准样例企业」⇒ 空是**合法语义**，
            //   ⛔ 不能判成缺参（修复前它无 C# 默认值 ⇒ 被当必填，与描述矛盾）。
            [SkillParam(Description = "企业编码（空 = 平台标准级样例域）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string enterprise_code,
            [SkillParam(Description = "文件编码，可选，文件级过滤",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? file_code = null,
            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(field_code))
                return SkillResult.Fail("field_code 不能为空", WorkflowErrorCodes.PreconditionEmpty);

            // 企业编码兜底（与 NodeExecutor docField 节点一致）
            var entCode = string.IsNullOrWhiteSpace(enterprise_code) ? "" : enterprise_code;

            var fields = (await db.GetListAsync<CertPlatform.Shared.Entities.Doc.ExtractionResult>(x =>
                x.FieldCode == field_code && x.EnterpriseCode == entCode)).Data ?? new();

            if (string.IsNullOrWhiteSpace(file_code))
            {
                var picked = fields.OrderByDescending(x => x.ExtractedAt).FirstOrDefault();
                if (picked == null)
                    return EmptyResult(field_code, null, EmptyJudge.StateNotFound,
                        $"★未找到 field_code={field_code}, enterprise_code={entCode} 的提取记录" +
                        "（可能未上传对应文件，或未配置该字段的提取规则）");

                var confidence = (double?)(picked.Confidence ?? 0m);
                return SkillResult.Ok(new Dictionary<string, object>
                {
                    ["field_code"] = picked.FieldCode,
                    ["field_name"] = picked.FieldName ?? string.Empty,
                    ["field_value"] = picked.ExtractedValue ?? string.Empty,
                    ["confidence"] = confidence!,
                    ["is_manual_edited"] = picked.IsManualEdited
                }, confidence);
            }

            // 文件级过滤
            var byFile = fields.Where(x => x.FileCode == file_code)
                .OrderByDescending(x => x.ExtractedAt).FirstOrDefault();
            if (byFile == null)
                return EmptyResult(field_code, file_code, EmptyJudge.StateNotFound,
                    $"★未找到 field_code={field_code}, enterprise_code={entCode}, file_code={file_code} 的提取记录");

            var conf = (double?)(byFile.Confidence ?? 0m);
            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["field_code"] = byFile.FieldCode,
                ["field_name"] = byFile.FieldName ?? string.Empty,
                ["field_value"] = byFile.ExtractedValue ?? string.Empty,
                ["confidence"] = conf!,
                ["is_manual_edited"] = byFile.IsManualEdited
            }, conf);
        }

        /// <summary>
        /// ★ 「取不到」的统一出口（与 <c>NodeExecutor.ExecuteDocFieldAsync</c> 同口径）：
        /// <b>成功 + 空值</b>，并把「为什么空」显式暴露给下游与排障。
        /// <para>⛔ 不得改成 <c>Fail</c>：那样一次字段缺失就会中断整条路径，
        /// 规则作者就<b>没法</b>用 <c>branch</c> 分流（例如「字段空 ⇒ 判 NC 不合规」）。</para>
        /// </summary>
        private static SkillResult EmptyResult(
            string fieldCode, string? fileCode, string dataState, string message)
            => SkillResult.Ok(new Dictionary<string, object>
            {
                ["field_code"] = fieldCode,
                ["field_name"] = string.Empty,
                // ★ 必须是 null —— 不能透出占位符原文（"-" / "待填写" …），
                //   IsEmptySkill 的判空口径不认占位符 ⇒ 透出会让它误判「有值」。
                ["field_value"] = null!,
                ["has_value"] = false,
                ["data_state"] = dataState,
                ["file_code"] = fileCode ?? string.Empty,
                ["is_manual_edited"] = false,
                ["message"] = message
            }, 0.0);
    }
}
