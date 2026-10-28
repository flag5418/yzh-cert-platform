using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Cert;

namespace CertPlatform.Admin.Controllers.Cert;

/// <summary>
/// 条款属性池控制器（L0 · 后台管理）
///
/// <para><b>业务定位</b>（<c>docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md</c> §三）：
/// 「属性全集」—— 跨标准跨条款复用的属性定义。<b>池子大、配置小</b>：
/// 这里只管"世界上有哪些属性"，某条款用哪几条由 <see cref="ClauseAttr"/> 决定。</para>
///
/// <para><b>为什么 Code 是人可读的驼峰而不是 GUID</b>：属性池是**受控枚举**，
/// 它的 Code 会被写进 L1 的 <c>AttrCode</c>、被提示词引用、被人排查。
/// 用 GUID 等于让所有人对着乱码猜。⇒ 新增时 Code 由用户填（必填），⛔ 框架不会自动生成。</para>
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Cert/ClauseAttrDef</c>
/// <b>数据库</b>：<c>cert_clause_attr_def</c></para>
/// </summary>
[ApiController]
[Route("api/Admin/Cert/[controller]")]
public class ClauseAttrDefController : YzhControllerBase<ClauseAttrDef>
{
    private readonly IDbOrm _db;

    /// <summary>允许的值类型 —— <b>只这 5 种</b>（24 号 §3.2）。⛔ 没有 int / date / *_multi</summary>
    private static readonly HashSet<string> AllowedValueTypes =
        new(StringComparer.Ordinal) { "text", "dict", "bool", "json", "prompt" };

    /// <summary>需要字典的值类型（<c>dict</c> 单值 / <c>json</c> 受控多选）</summary>
    private static readonly HashSet<string> TypesNeedDict =
        new(StringComparer.Ordinal) { "dict", "json" };

    /// <summary>属性码格式：字母开头，只含字母数字下划线（要能安全写进 JSON / 提示词）</summary>
    private static readonly Regex CodePattern = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public ClauseAttrDefController(
        EntityService<ClauseAttrDef> entityService,
        IUserContext userContext,
        IDbOrm db)
        : base(entityService, userContext)
    {
        _db = db;
    }

    /// <summary>开发期强制暴露缺配置问题（缺 JSON 直接 throw，不静默空白）</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>加载 EntityConfig（<c>Assets/EntityConfigs/Cert/ClauseAttrDef.json</c>）</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<ClauseAttrDef>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、校验钩子
    // ════════════════════════════════════════════════════════════════════

    /// <summary>新增前：Code 必填且合法 + ValueType 白名单 + DictCode 条件必填 + 唯一（含已删）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(ClauseAttrDef entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "属性码不能为空（如 Artifacts）");
        entity.Code = entity.Code.Trim();

        var basic = ValidateShape(entity);
        if (!basic.ok) return basic;

        // ⚠️ 唯一索引 uk_code 不含 IsDeleted ⇒ 已删行仍占坑，必须含已删一起判，
        //    否则走到 Insert 只会撞 1062 得到一句「数据已存在」，用户不知道该改什么。
        var dead = await _db.Client.Queryable<ClauseAttrDef>()
            .Where(p => p.Code == entity.Code)
            .FirstAsync();

        if (dead != null)
            return (false, dead.IsDeleted
                ? $"属性码【{entity.Code}】曾被删除（记录仍在库中），请换一个码，或先在数据库恢复该行"
                : $"属性码【{entity.Code}】已存在");

        if (entity.Sort == 0)
        {
            var maxSort = await _db.Client.Queryable<ClauseAttrDef>()
                .Where(p => !p.IsDeleted)
                .MaxAsync(p => (int?)p.Sort) ?? 0;
            entity.Sort = maxSort + 10;
        }

        return (true, null);
    }

    /// <summary>修改前：Code 不可改（L1 的 AttrCode 引用它）+ 同款形状校验 + 名称唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(ClauseAttrDef entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        var current = await Entity.GetOne(p => p.Code == entity.Code);
        if (current.Success && current.Data != null)
        {
            // Code 是 L1 的外键值，改了会让已配好的条款属性集体失联（且不报错）
            if (!string.Equals(current.Data.Code, entity.Code, StringComparison.Ordinal))
                return (false, "属性码不可修改（已配置的条款属性依赖它）");

            // 已灌数据的属性不许换值类型：换了 ⇒ 存量值按新类型解析必炸
            if (!string.Equals(current.Data.ValueType, entity.ValueType, StringComparison.Ordinal))
            {
                var used = await _db.Client.Queryable<ClauseAttr>()
                    .Where(p => p.AttrCode == entity.Code && !p.IsDeleted)
                    .CountAsync();
                if (used > 0)
                    return (false, $"已有 {used} 条条款属性在用【{entity.Code}】，不允许改值类型（会与存量值冲突）");
            }
        }

        return ValidateShape(entity);
    }

    /// <summary>删除前：已被条款属性引用的属性不可删（否则 L1 变成悬空外键）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return (false, "未选择要删除的记录");

        var usedCodes = await _db.Client.Queryable<ClauseAttr>()
            .Where(p => codes.Contains(p.AttrCode) && !p.IsDeleted)
            .Select(p => p.AttrCode)
            .Distinct()
            .ToListAsync();

        if (usedCodes.Count > 0)
            return (false,
                $"属性【{string.Join("、", usedCodes)}】已被条款属性配置引用，不能删除。如需停用，请把「状态」改为禁用。");

        return (true, null);
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、自定义端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 属性下拉选项（给「条款属性配置」页的 AttrCode 选择器用）。
    /// <para>只返回启用的属性，按 Sort 排；带 ValueType / DictCode，前端据此切换控件形态。</para>
    /// </summary>
    [HttpGet("options")]
    public async Task<IActionResult> GetOptions()
    {
        var list = await _db.Client.Queryable<ClauseAttrDef>()
            .Where(p => !p.IsDeleted && p.IsValid == 1)
            .OrderBy(p => p.Sort)
            .Select(p => new
            {
                p.Code,
                p.AttrName,
                p.ValueType,
                p.DictCode,
                p.Remark
            })
            .ToListAsync();

        return Ok(ApiResponse<List<object>>.Ok(list.Cast<object>().ToList()));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、私有校验
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 形状校验（新增/修改共用，⛔ 不得各写一份）：
    /// ① Code 格式 ② ValueType 白名单 ③ 需要字典的类型必须给 DictCode
    /// </summary>
    private static (bool ok, string? msg) ValidateShape(ClauseAttrDef entity)
    {
        if (!CodePattern.IsMatch(entity.Code ?? string.Empty))
            return (false, "属性码只能字母开头、由字母/数字/下划线组成（如 Artifacts、EvidenceChain）");

        if (string.IsNullOrWhiteSpace(entity.AttrName))
            return (false, "属性名不能为空");

        var vt = (entity.ValueType ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedValueTypes.Contains(vt))
            return (false, $"值类型【{entity.ValueType}】不合法，只能是 text / dict / bool / json / prompt");
        entity.ValueType = vt;

        var dictCode = (entity.DictCode ?? string.Empty).Trim();
        if (TypesNeedDict.Contains(vt))
        {
            if (dictCode.Length == 0)
                return (false, $"值类型为 {vt} 时必须指定「字典编码」（如 clause_obligation / process_domain）");
        }
        else
        {
            // 非字典类型清空，避免"留着上一版的字典码"造成下游误判
            dictCode = string.Empty;
        }
        entity.DictCode = dictCode.Length == 0 ? null : dictCode;

        return (true, null);
    }
}
