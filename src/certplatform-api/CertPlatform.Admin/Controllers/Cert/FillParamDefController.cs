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
/// 企业资料参数字典控制器（后台管理）
///
/// <para><b>业务定位（2026-10-06 重定义，见 26-核心菜单功能设计 §3.1 裁决）</b>：
/// 「企业资料参数」是一个<b>按标准管理的简单字典</b> —— 定义专家端可完善、
/// 审核员可补充的企业资料字段，供未来标准文档填充使用。</para>
///
/// <para><b>数据模型绑定制</b>：左树 = [通用] + 各ISO标准（只读，树数据源非本实体，
/// 前端从 <c>/api/Admin/Foundation/ISOStandardTreeTable/tree/root</c> 取并拼通用根）；
/// 每条参数唯一归属一节点（<c>StandardCode=''</c>=通用 或 ISOStandard.Code）；
/// <c>OrgCode</c> / <c>StageCode</c> 服务端强制恒空串（不分机构、不分阶段）；
/// 行级 <c>IsValid</c> 即启用/禁用开关。</para>
///
/// <para><b>为什么不是 TreeTableControllerBase</b>：树数据源是 ISO 标准表（另一实体），
/// 本控制器的树增删改无业务意义 —— 左树只读，右表走基类 <c>/filter</c>（前端注入
/// <c>StandardCode eq node.Code</c> 单条件），故保持 <c>YzhControllerBase</c>。</para>
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Cert/FillParamDef</c>
/// <b>数据库</b>：<c>cert_fill_param_def</c></para>
///
/// <para><b>★ 端点变化（2026-10-06）</b>：原自定义端点 <c>scopes</c> / <c>enterprise-attrs</c> /
/// <c>effective</c> 仅被旧页面调用，随作用域模型一并删除 —— ApiCode 集合变化，
/// 部署后须重跑 ApiSync 并重关联角色-接口。</para>
/// </summary>
[ApiController]
/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ 既有 ApiCode 不变。</para>
[Route("api/Admin/Cert/[controller]")]
public class FillParamDefController : YzhControllerBase<FillParamDef>
{
    private readonly IDbOrm _db;

    public FillParamDefController(
        EntityService<FillParamDef> entityService,
        IUserContext userContext,
        IDbOrm db)
        : base(entityService, userContext)
    {
        _db = db;
    }

    /// <summary>★ 开发期强制暴露缺配置问题（缺 JSON 直接 throw，不静默空白）</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>加载 EntityConfig 配置</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<FillParamDef>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、CRUD 校验钩子
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 新增原子方法覆写：<b>「已删行复活」必须改走更新通路</b>（2026-10-06 冒烟实测修复）。
    ///
    /// <para>框架 <c>YzhControllerBase.AddCore</c>（YzhControllerBase.cs:252）在 <c>OnBeforeAdd</c>
    /// 之后<b>无条件 Insert</b> —— 在钩子里把 <c>entity.Code</c> 指到已删行也拦不住 1062
    /// （<c>uk_org_std_stage_param</c> 不含 <c>IsDeleted</c>，唯一约束冲突 = 「新增失败：数据已存在」）。
    /// 故复活判定提前到本覆写：命中已删行 → 沿用 Code/Id 走 <c>Entity.Update</c>
    /// （IgnoreColumns = Code/Id/CreateTime/CreateBy，<c>IsDeleted</c> 可写 ⇒ 复活落库），
    /// 否则交回基类正常校验 + Insert。</para>
    /// </summary>
    public override async Task<Result<FillParamDef>> AddCore(FillParamDef entity)
    {
        // 归一前置（与 OnBeforeAdd 同款、幂等）—— 复活查行依赖归一后的三键 + trim 后的 ParamCode
        entity.OrgCode = string.Empty;
        entity.StageCode = string.Empty;
        entity.SourceExpr = null;
        entity.MaintainMode = "manual";
        entity.StandardCode ??= string.Empty;
        if (string.IsNullOrWhiteSpace(entity.ParamCode))
            return Result<FillParamDef>.Fail("参数编码不能为空");
        entity.ParamCode = entity.ParamCode.Trim();

        // 含已删查行（uk 不含 IsDeleted ⇒ 必须在 Insert 前接管）
        var dead = await _db.Client.Queryable<FillParamDef>()
            .Where(p => p.IsDeleted
                        && p.OrgCode == entity.OrgCode
                        && p.StandardCode == entity.StandardCode
                        && p.StageCode == entity.StageCode
                        && p.ParamCode == entity.ParamCode)
            .FirstAsync();

        if (dead == null)
            return await base.AddCore(entity);

        var (valid, vmsg) = ValidateEntity(entity);
        if (!valid) return Result<FillParamDef>.Fail(vmsg);

        // 复活 = 把已删行当活行更新：沿用 Code/Id/CreateTime，清删除标记
        entity.Code = dead.Code;
        entity.Id = dead.Id;
        entity.IsDeleted = false;
        entity.DeleteBy = null;
        entity.DeleteTime = null;
        entity.CreateTime = dead.CreateTime;
        entity.CreateBy = dead.CreateBy;

        var upd = await Entity.Update(entity, UserContext.ClientIp);
        if (!upd.Success || upd.Data == null)
            return Result<FillParamDef>.Fail(upd.Error ?? "复活失败");

        await OnAfterUpdate(upd.Data);
        await OnAfterCommitted();
        return Result<FillParamDef>.Ok(upd.Data);
    }

    /// <summary>新增前：三键服务端强制归一 + ParamCode 必填 + 同一标准下唯一（含已删，复活在 AddCore）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(FillParamDef entity)
    {
        // ★ 关键字段不分机构、不分阶段（2026-10-06 裁决）—— 服务端强制，不信任客户端传值
        entity.OrgCode = string.Empty;
        entity.StageCode = string.Empty;
        entity.SourceExpr = null;
        entity.MaintainMode = "manual";
        // StandardCode 是唯一允许客户端决定的归属键（前端从左树选中节点注入），仅防空
        entity.StandardCode ??= string.Empty;

        if (string.IsNullOrWhiteSpace(entity.ParamCode))
            return (false, "参数编码不能为空");
        entity.ParamCode = entity.ParamCode.Trim();

        // ★ 查重「含已删」（DB 唯一索引不含 IsDeleted）。
        // 已删行正常情况下已在 AddCore 覆写里被接管复活 —— 走到这里说明是两查之间的
        // 并发竞态，响亮拒绝让调用方重试（重试即走复活通路），绝不放进 Insert 撞 1062。
        var existing = await _GetOneIgnoreValid(entity);
        if (existing != null)
            return (false, $"同一标准下参数编码【{entity.ParamCode}】已存在");

        return (true, null);
    }

    /// <summary>修改前：Code 业务键 + 三键归一 + 同一标准下唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(FillParamDef entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        // ★ 与 OnBeforeAdd 同款强制归一：客户端不传 / 传脏都以服务端为准
        entity.OrgCode = string.Empty;
        entity.StageCode = string.Empty;
        entity.SourceExpr = null;
        entity.MaintainMode = "manual";
        entity.StandardCode ??= string.Empty;

        // 查重范围 = 含禁用、不含已删（DB 唯一索引不含 IsValid；已删行由新增复活路径接管）
        var dup = await _db.Client.Queryable<FillParamDef>()
            .Where(p => p.Code != entity.Code
                        && p.IsDeleted == false
                        && p.OrgCode == entity.OrgCode
                        && p.StandardCode == entity.StandardCode
                        && p.StageCode == entity.StageCode
                        && p.ParamCode == entity.ParamCode)
            .FirstAsync();

        if (dup != null)
            return (false, $"同一标准下参数编码【{entity.ParamCode}】已存在");

        // ★ 内置参数的 ParamCode 不允许改（改了会断裂已配置的文档锚点引用）
        var current = await Entity.GetOne(p => p.Code == entity.Code);
        if (current.Success && current.Data != null && current.Data.IsBuiltin)
        {
            if (!string.Equals(current.Data.ParamCode, entity.ParamCode, StringComparison.Ordinal))
                return (false, "内置参数的「参数编码」不可修改（已配置的文档锚点依赖它）");
        }

        return (true, null);
    }

    /// <summary>删除前：内置参数不可删除（含已禁用行）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return (false, "未选择要删除的记录");

        // includeDisabled: true —— 已禁用的内置参数同样不可删
        var builtin = await Entity.GetListAsync(
            p => codes.Contains(p.Code) && p.IsBuiltin, includeDisabled: true);

        if (builtin.Success && builtin.Data != null && builtin.Data.Count > 0)
        {
            var names = string.Join("、", builtin.Data.Select(x => x.ParamName));
            return (false, $"内置参数不可删除（{names}）。如需停用，请把「状态」改为禁用。");
        }

        return (true, null);
    }

    /// <summary>
    /// 按三键查行（<b>含已删</b>、不过滤 IsValid）—— 新增复活判定用。
    /// ⛔ 不能用 <c>GetOneIgnoreValidAsync</c>：它仍过滤软删除（SqlSugarDbOrm.cs:64），
    /// 命中不了已删行，复活逻辑会静默失效；而 DB 唯一索引 uk_org_std_stage_param
    /// <b>不含 IsDeleted</b> → 复活失效时重复插入直接撞 1062。故此处裸查 Client。
    /// </summary>
    private async Task<FillParamDef?> _GetOneIgnoreValid(FillParamDef entity)
    {
        var r = await _db.Client.Queryable<FillParamDef>()
            .Where(p => p.OrgCode == entity.OrgCode
                        && p.StandardCode == entity.StandardCode
                        && p.StageCode == entity.StageCode
                        && p.ParamCode == entity.ParamCode)
            .FirstAsync();
        return r;
    }
}
