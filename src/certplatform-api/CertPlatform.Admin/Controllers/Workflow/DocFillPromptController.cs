using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Doc;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 全文填写提示词控制器（「标准文档填写规则」页面的「全文规则」Tab）
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Workflow/DocFillPrompt</c></para>
/// <para><b>数据库</b>：<c>cert_doc_fill_prompt</c></para>
///
/// <para><b>业务定位</b>：与 <c>DocTemplateAnchor</c>（逐锚点规则）<b>并列的第二条填写通路</b>。
/// 锚点规则管「这个格子填什么」；本表管「整篇文档怎么组织、口吻怎么写」。
/// <c>cert_doc_template.FillPromptCode</c> 引用本表 <c>PromptCode</c>（空 = 不走全文规则）。</para>
///
/// <para><b>★ 版本语义（不覆盖）</b>：<c>uk_org_prompt_ver(OrgCode, PromptCode, Version)</c> ⇒
/// 改提示词<b>不更新旧行，而是新增一版</b>（<c>Version = max + 1</c>），留痕以支撑
/// 「当时为什么这么写」的追溯。所以：</para>
/// <list type="bullet">
/// <item><c>POST add</c> = 新增一版（<c>Version</c> 未显式给出时自动 +1）</item>
/// <item><c>POST update</c> = 改本版内容（<c>Version</c> 不变）</item>
/// </list>
///
/// <para><b>★ <c>IsDefault</c> 排他</b>：同一 <c>(OrgCode, PromptCode)</c> 下只能有一条为 1。
/// DB 只有普通索引 <c>idx_default</c>（⛔ 不是唯一索引）⇒ <b>由本控制器保证</b>：
/// 置 1 前先把同 Code 的其它行清零。</para>
/// </summary>
[ApiController]
[Route("api/Admin/Workflow/DocFillPrompt")]
public class DocFillPromptController : YzhControllerBase<DocFillPrompt>
{
    private readonly IDbOrm _db;
    private readonly ILogger<DocFillPromptController> _logger;

    public DocFillPromptController(
        EntityService<DocFillPrompt> entityService,
        IUserContext userContext,
        IDbOrm db,
        ILogger<DocFillPromptController> logger)
        : base(entityService, userContext)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>★ 开发期强制暴露缺配置问题（缺 JSON 直接 throw，不静默空白）</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>加载 EntityConfig 配置</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<DocFillPrompt>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、写入覆写（版本自增 + IsDefault 排他 + 含已删查重）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 新增一版：<c>Version &lt;= 0</c> 时自动取 <c>max(Version) + 1</c>；
    /// <c>IsDefault = true</c> 时先清掉同 <c>(OrgCode, PromptCode)</c> 其它行的默认位。
    /// </summary>
    public override async Task<Result<DocFillPrompt>> AddCore(DocFillPrompt entity)
    {
        Normalize(entity);

        // ★ 版本自增必须早于 Validate（Validate 要求 Version > 0，而「未指定」时它是 0）
        var siblings = await LoadSiblingsAsync(entity.OrgCode, entity.PromptCode);
        if (entity.Version <= 0)
            entity.Version = siblings.Count == 0 ? 1 : siblings.Max(s => s.Version) + 1;

        var err = Validate(entity);
        if (err != null) return Result<DocFillPrompt>.Fail(err);

        // uk_org_prompt_ver 不含 IsDeleted ⇒ 含已删查重（同版本号已存在 ⇒ 就地复活）
        var sameVersion = siblings.FirstOrDefault(s => s.Version == entity.Version);
        if (sameVersion != null && !sameVersion.IsDeleted)
            return Result<DocFillPrompt>.Fail(
                $"「{entity.PromptCode}」已存在版本 {entity.Version}（{sameVersion.PromptName}）。"
                + "若要改内容请编辑该版本，若要出新版请留空版本号由系统自增。");

        if (sameVersion != null)
        {
            // 复活已删的同版本行（沿用同 Code，避免产生第二行撞唯一键）
            entity.Code = sameVersion.Code;
            entity.Id = sameVersion.Id;
            entity.CreateTime = sameVersion.CreateTime;
            entity.CreateBy = sameVersion.CreateBy;
            entity.IsDeleted = false;
            entity.DeleteBy = null;
            entity.DeleteTime = null;
            entity.UpdateTime = DateTime.Now;

            var upd = await _db.UpdateAsync(entity);
            if (!upd.Success) return Result<DocFillPrompt>.Fail(upd.Error ?? "复活提示词版本失败");

            _logger.LogInformation("[DocFillPrompt] 复活已软删版本：{PromptCode} v{Ver}", entity.PromptCode, entity.Version);

            // ★ 排他放在写成功之后 —— 顺序反了会在写失败时留下「一个默认都没有」的不一致态
            if (entity.IsDefault)
                await ClearDefaultAsync(entity.OrgCode, entity.PromptCode, exceptCode: entity.Code);

            return Result<DocFillPrompt>.Ok(entity);
        }

        var added = await base.AddCore(entity);
        if (!added.Success) return added;

        if (entity.IsDefault)
            await ClearDefaultAsync(entity.OrgCode, entity.PromptCode, exceptCode: added.Data?.Code);

        return added;
    }

    /// <summary>修改本版内容（<c>Version</c> 不变；改 <c>IsDefault</c> 时同步排他）</summary>
    public override async Task<Result<DocFillPrompt>> UpdateCore(DocFillPrompt entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return Result<DocFillPrompt>.Fail("更新失败：缺少业务键 Code");

        var current = await _db.GetOneAsync<DocFillPrompt>(p => p.Code == entity.Code);
        if (current.Data == null)
            return Result<DocFillPrompt>.Fail("该提示词版本不存在或已删除");

        Normalize(entity);

        // ★ 版本号不可改（它是唯一键成员，改了等于换行；要出新版请用「新增」）
        entity.Version = current.Data.Version;

        var err = Validate(entity);
        if (err != null) return Result<DocFillPrompt>.Fail(err);

        var updated = await base.UpdateCore(entity);
        if (!updated.Success) return updated;

        // ★ 排他放在写成功之后（同 AddCore）
        if (entity.IsDefault)
            await ClearDefaultAsync(entity.OrgCode, entity.PromptCode, exceptCode: entity.Code);

        return updated;
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、自定义端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>取生效提示词</b>：给定 <c>PromptCode</c> + <c>OrgCode</c>，返回填充引擎<b>实际会用</b>的那一版。
    /// <para><b>选取口径（全项目唯一实现，⛔ 别处不得另写一份）</b>：
    /// ① 机构专属（<c>OrgCode</c> 命中）优先于全局（<c>OrgCode = ''</c>）；
    /// ② 同层级内 <c>IsDefault = 1</c> 优先；
    /// ③ 再取 <c>Version</c> 最大者。</para>
    /// <para>返回 <c>Found=false</c> 时，填充引擎应按「不走全文规则」处理（不报错）。</para>
    /// </summary>
    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery] string promptCode, [FromQuery] string? orgCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode))
            return Ok(ApiResponse<object>.Fail("请指定 PromptCode"));

        var org = (orgCode ?? string.Empty).Trim();

        var candidates = await _db.Client.Queryable<DocFillPrompt>()
            .Where(p => p.PromptCode == promptCode
                        && (p.OrgCode == string.Empty || p.OrgCode == org)
                        && p.IsDeleted == false
                        && p.IsValid == 1)
            .ToListAsync();

        if (candidates.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { Found = false, PromptCode = promptCode, OrgCode = org }));

        var picked = candidates
            .OrderByDescending(p => org.Length > 0 && p.OrgCode == org)   // ① 更具体优先
            .ThenByDescending(p => p.IsDefault)                          // ② 默认优先
            .ThenByDescending(p => p.Version)                            // ③ 版本新优先
            .First();

        return Ok(ApiResponse<object>.Ok(new
        {
            Found = true,
            PromptCode = promptCode,
            OrgCode = org,
            Picked = new
            {
                picked.Code,
                picked.OrgCode,
                picked.PromptCode,
                picked.PromptName,
                picked.SystemPrompt,
                picked.UserTemplate,
                picked.OutputSchema,
                picked.Model,
                picked.Temperature,
                picked.MaxTokens,
                picked.Version,
                picked.IsDefault,
                picked.Status,
            },
            CandidateCount = candidates.Count,
            Candidates = candidates
                .OrderByDescending(p => p.Version)
                .Select(p => new { p.Code, p.OrgCode, p.Version, p.IsDefault, p.Status }),
        }));
    }

    /// <summary><b>版本历史</b>：同一 <c>PromptCode</c> 的全部版本（含已软删），按版本降序。</summary>
    [HttpGet("versions")]
    public async Task<IActionResult> Versions([FromQuery] string promptCode, [FromQuery] string? orgCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode))
            return Ok(ApiResponse<object>.Fail("请指定 PromptCode"));

        var org = (orgCode ?? string.Empty).Trim();

        var rows = await _db.Client.Queryable<DocFillPrompt>()
            .Where(p => p.PromptCode == promptCode && p.OrgCode == org)
            .OrderByDescending(p => p.Version)
            .Select(p => new
            {
                p.Code,
                p.OrgCode,
                p.PromptCode,
                p.PromptName,
                p.Model,
                p.Temperature,
                p.MaxTokens,
                p.Version,
                p.IsDefault,
                p.Status,
                p.Sort,
                p.IsValid,
                p.IsDeleted,
                p.UpdateTime,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            PromptCode = promptCode,
            OrgCode = org,
            Total = rows.Count,
            Items = rows,
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、私有：规范化 / 校验 / 版本 / 排他
    // ════════════════════════════════════════════════════════════════════

    /// <summary>规范化：<c>OrgCode</c> NOT NULL（空 = 全局），数值列落合法区间</summary>
    private static void Normalize(DocFillPrompt p)
    {
        p.OrgCode = (p.OrgCode ?? string.Empty).Trim();
        p.PromptCode = (p.PromptCode ?? string.Empty).Trim();
        p.PromptName = (p.PromptName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(p.Status)) p.Status = "draft";
        else p.Status = p.Status.Trim();

        if (p.MaxTokens <= 0) p.MaxTokens = 4000;
        if (p.Temperature < 0m) p.Temperature = 0m;
        if (p.Temperature > 2m) p.Temperature = 2m;
    }

    /// <summary>L3 提交期校验</summary>
    private static string? Validate(DocFillPrompt p)
    {
        if (string.IsNullOrWhiteSpace(p.PromptCode)) return "Prompt 编码（PromptCode）不能为空";
        if (string.IsNullOrWhiteSpace(p.PromptName)) return "名称（PromptName）不能为空";
        if (p.MaxTokens <= 0) return "最大 Token 必须大于 0";
        if (p.Version <= 0) return "版本号必须大于 0";

        var status = p.Status ?? string.Empty;
        if (status.Length > 0 && status != "draft" && status != "active" && status != "archived")
            return $"状态「{status}」不合法（可选：draft / active / archived）";

        return null;
    }

    /// <summary>取同 <c>(OrgCode, PromptCode)</c> 的全部版本，<b>含已软删</b>（唯一键查重必需）</summary>
    private async Task<List<DocFillPrompt>> LoadSiblingsAsync(string orgCode, string promptCode)
    {
        return await _db.Client.Queryable<DocFillPrompt>()
            .Where(p => p.OrgCode == orgCode && p.PromptCode == promptCode)
            .ToListAsync();
    }

    /// <summary>
    /// 清掉同 <c>(OrgCode, PromptCode)</c> 下其它行的 <c>IsDefault</c>（<b>列级写入</b>，只动这一列）。
    /// <para>DB 只有普通索引 ⇒ 唯一性由本方法保证；⛔ 不要依赖数据库报错来发现两条默认。</para>
    /// </summary>
    private async Task ClearDefaultAsync(string orgCode, string promptCode, string? exceptCode)
    {
        var others = await _db.Client.Queryable<DocFillPrompt>()
            .Where(p => p.OrgCode == orgCode && p.PromptCode == promptCode
                        && p.IsDefault == true && p.IsDeleted == false)
            .ToListAsync();

        foreach (var o in others)
        {
            if (!string.IsNullOrEmpty(exceptCode)
                && string.Equals(o.Code, exceptCode, StringComparison.Ordinal))
                continue;

            o.IsDefault = false;
            o.UpdateTime = DateTime.Now;

            var upd = await _db.UpdateAsync(o, nameof(DocFillPrompt.IsDefault), nameof(DocFillPrompt.UpdateTime));
            if (!upd.Success)
            {
                // 不中断主流程（默认位不唯一不会导致填充失败，只会让 resolve 有歧义），但必须留痕
                _logger.LogWarning("[DocFillPrompt] 清除旧默认位失败：{Code} → {Err}", o.Code, upd.Error);
            }
        }
    }
}
