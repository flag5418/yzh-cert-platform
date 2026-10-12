using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Models;
using CertPlatform.Admin.Entities.Dir;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ 判断某个标准目录的文件是否缺失
    ///
    /// <para><b>解决的问题</b>：认证审核里常出现「企业根本没提交某个必需文件」
    /// （如未交内审报告、未交管理评审记录）。这与「文件交了但内容为空」是两回事，
    /// 判定依据和处置方式都不同。</para>
    ///
    /// <para><b>数据源</b>：<c>cert_standard_directory_file</c>（标准目录的文件槽位定义）
    /// —— 有 <c>StandardFileCode</c>（文件要求编码）、<c>IsRequired</c>（是否必需）、
    /// <c>VersionNumber</c>（当前版本）。</para>
    ///
    /// <para><b>四态</b>（比字段/表格多一态，因为要区分"必需"与"可选"）：
    ///   · <c>uploaded</c>       —— 文件已上传（有实际存储路径）
    ///   · <c>not_required</c>   —— 该文件对本企业非必需
    ///   · <c>placeholder_only</c>—— ★ 有槽位行但未上传（目录里是个空占位）
    ///   · <c>not_configured</c> —— ★ 标准目录里根本没配这个文件要求（配置缺失，≠企业缺失）</para>
    /// </summary>
    [Skill(
        Code = "is_std_file_missing",
        Name = "标准目录文件是否缺失",
        ReturnType = "boolean",
        ResultPort = "result",
        Description = "★ 判断某标准目录要求的企业文件是否已上传：返回四态 uploaded/not_required/placeholder_only/not_configured，★ 区分「企业没交」与「目录没配」"
    )]
    public static class IsStdFileMissingSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            // ★ AllowEmpty = SkillParamEmptyPolicy.Deny：这是本节点的**判定对象**，没有它算不出任何结论。
            //   ⚠️ 它由 NodeExecutor.ExecuteStdFileMissingAsync 从**节点 config** 注入
            //   （⛔ 不是上游连线）—— 配置缺失时那边会先抛 CONFIG_ERROR，这里只是第二道闸。
            [SkillParam(Description = "★标准文件要求编码（cert_standard_directory_file.StandardFileCode，如 SF-9001-7-5）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? standard_file_code,

            [SkillParam(Description = "企业编码",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? enterprise_code,

            // ★ 2026-10-11 修：这两个参数描述写着「可选」，却**没有默认值** ⇒
            //   SkillRegistry 按 ParameterInfo.HasDefaultValue 判必填 ⇒ 调用方不传就报
            //   「缺少必填入参: standard_code, stage_code」，把「可选」变成了「必填」。
            //   加 `= null` 让语义与描述一致（空 ⇒ 该维度不过滤，见下方 Where）。
            // ★ 2026-10-12：再显式声明 AllowEmpty = SkillParamEmptyPolicy.Allow —— 即使将来有人去掉 `= null`，
            //   语义也不会被「无默认值 = 必填」这条旧判据悄悄改掉。
            [SkillParam(Description = "标准编码（可选，用于校验配置归属；空 = 不校验）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? standard_code = null,

            [SkillParam(Description = "阶段编码（可选；空 = 不校验）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? stage_code = null,

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(standard_file_code))
                return SkillResult.Fail("standard_file_code 不能为空", WorkflowErrorCodes.PreconditionEmpty);
            if (string.IsNullOrWhiteSpace(enterprise_code))
                return SkillResult.Fail("enterprise_code 不能为空", WorkflowErrorCodes.PreconditionEmpty);

            var ent = enterprise_code!;
            var sfc = standard_file_code!;

            // ── ① 查该企业在此标准目录下的文件行 ──
            var all = await db.GetListAsync<StandardDirectoryFile>(
                x => x.EnterpriseCode == ent, includeDisabled: true);
            var rows = (all.Data ?? new List<StandardDirectoryFile>())
                .Where(r => string.Equals(r.StandardFileCode, sfc, StringComparison.OrdinalIgnoreCase))
                .Where(r => string.IsNullOrWhiteSpace(standard_code)
                            || string.Equals(r.StandardCode, standard_code, StringComparison.OrdinalIgnoreCase))
                .Where(r => string.IsNullOrWhiteSpace(stage_code)
                            || string.Equals(r.StageCode, stage_code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var outputs = new Dictionary<string, object>();
            outputs["standard_file_code"] = sfc;
            outputs["record_count"] = rows.Count;

            // ── ② 目录里没配这个文件要求 ──
            if (rows.Count == 0)
            {
                // ★ 这是【配置缺失】，不是企业没交 —— 判定依据完全不同，不能混
                outputs["result"]         = true;      // 视为「不可用」（保守）
                outputs["is_missing"]     = true;
                outputs["is_uploaded"]    = false;
                outputs["is_required"]    = false;
                outputs["state"]          = "not_configured";
                outputs["message"] =
                    $"★标准目录未配置文件要求 {sfc}（企业={ent}）—— 这是【配置缺失】而非【企业未提交】，" +
                    "请先在标准目录管理中确认该文件要求是否应存在";
                return SkillResult.Ok(outputs, 1.0);
            }

            var row = rows.OrderByDescending(r => r.VersionNumber).First();
            var isRequired = row.IsRequired;   // ★ 实测：实体是 bool，不是 int
            var fileName   = row.FileName ?? string.Empty;
            var fileSize   = row.FileSize ?? 0;

            outputs["file_name"]   = fileName;
            outputs["is_required"] = isRequired;
            outputs["file_size"]   = fileSize;
            outputs["version"]     = row.VersionNumber;
            outputs["file_type"]   = row.FileType ?? string.Empty;

            // ── ③ 判断是否真的上传了 ──
            // 判定依据：★ 文件名 + 大小 + 版本
            //   目录里建了槽位行（FileName 已填）但 FileSize=0 → 只是占位，没真上传
            //   ⚠️ 实测 VersionNumber 默认值为 1（实体 :53），不能拿它判是否上传
            var uploaded =
                !EmptyJudge.IsBlankText(fileName)
                && fileSize > 0;

            if (uploaded)
            {
                outputs["result"]      = false;
                outputs["is_missing"]  = false;
                outputs["is_uploaded"] = true;
                outputs["state"]       = "uploaded";
                outputs["message"]     = $"{fileName}（{(double)fileSize / 1024:F0} KB，第 {row.VersionNumber} 版）已上传";
                return SkillResult.Ok(outputs, 1.0);
            }

            // ── ④ 有槽位但没上传 ──
            if (isRequired)
            {
                // ★ 必需文件缺失 —— 这是 NC 检查的重要判据
                outputs["result"]      = true;
                outputs["is_missing"]  = true;
                outputs["is_uploaded"] = false;
                outputs["state"]       = "placeholder_only";
                outputs["message"]     =
                    $"★【必需文件缺失】{sfc}（{fileName}）企业未上传" +
                    "—— 可作为不符合项的判据";
                return SkillResult.Ok(outputs, 0.0);   // ★ 置信度 0：确证缺失
            }

            // ── ⑤ 可选文件没上传 ──
            outputs["result"]      = false;
            outputs["is_missing"]  = false;
            outputs["is_uploaded"] = false;
            outputs["state"]       = "not_required";
            outputs["message"]     = $"{sfc}（{fileName}）为可选文件，未上传不构成问题";
            return SkillResult.Ok(outputs, 1.0);
        }
    }
}
