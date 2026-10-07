using CertPlatform.Shared.Entities.Cert;
using SharedFill = CertPlatform.Shared.Fill;

namespace CertPlatform.Shared.Services.Fill;

/// <summary>
/// 企业实体（<see cref="Enterprise"/>）→ 填充引擎快照（<see cref="SharedFill.EnterpriseInfo"/>）的
/// <b>★ 全项目唯一映射</b>。
///
/// <para><b>为什么必须有这一个类</b>：填充引擎（<c>Shared/Fill/</c>、<c>Shared/Office/</c>）是
/// <b>纯函数资产</b> —— 它只认 <see cref="SharedFill.EnterpriseInfo"/> 这个「扁平快照」，
/// 不认识 ORM 实体。于是每个「从库里读到企业档案 → 交给引擎取值」的调用点，
/// 都要写一遍 16 个字段的逐字段搬运。</para>
///
/// <para>实测（2026-10-06，<c>59</c> 复核）：这段搬运被<b>手写了两份</b>，且都在
/// <c>CertPlatform.Admin</c> 内 ——
/// ① <c>Workflow/Skills/Fill/SrcGlobalParamSkill.MapToInfo</c>
/// ② <c>Ent/Executors/EnterpriseDocNormalizationExecutor.MapToInfo</c>。
/// 成因是当时 <c>Shared</c> 不引 <c>YZH.Core.DataBase</c>，放不进共用层（自设约束）。</para>
///
/// <para><b>★ 两份副本的真实代价不是「重复」而是「漂移」</b>：两份都<b>只映射了 16 个字段</b>，
/// 而 <see cref="SharedFill.EnterpriseInfo.Get"/> 支持的属性更多。于是当某天有人往
/// <c>EnterpriseInfo</c> 加一个字段（例如「企业人数」之外的「注册资本」），
/// 只改其中一份的后果是：<b>同一个参数、两条调用链取到的值不同，且两边都不报错</b> ——
/// 页面显示一个值、文档里填另一个值。这正是 <c>58</c> 要消灭的 D6。</para>
///
/// <para><b>★ 约束</b>：本类只做「字段搬运」，⛔ 不查库、⛔ 不判断、⛔ 不做业务裁剪。
/// 要新增可取值属性时，<b>同时</b>改 <see cref="SharedFill.EnterpriseInfo.Get"/> 的 switch
/// 与本方法（守卫：<c>58</c> §7 的 D6 出口门 —— <c>grep "MapToInfo" CertPlatform.Admin</c> 必须为 0）。</para>
/// </summary>
public static class EnterpriseInfoMapper
{
    /// <summary>
    /// 把企业实体映射为引擎快照。<paramref name="e"/> 为 <c>null</c> 时返回空快照
    /// （⛔ 不抛异常：调用方在「企业不存在」时的正确行为是「取不到值」，
    /// 由 <c>ParamValueResolver.Resolve</c> 产出「待补齐」决策，而不是让整个任务崩掉）。
    /// </summary>
    public static SharedFill.EnterpriseInfo ToInfo(Enterprise? e)
    {
        if (e == null) return new SharedFill.EnterpriseInfo();

        return new SharedFill.EnterpriseInfo
        {
            Code = e.Code ?? string.Empty,
            Name = e.Name ?? string.Empty,
            ShortName = e.ShortName,
            CreditCode = e.CreditCode,
            LegalPerson = e.LegalPerson,
            Province = e.Province,
            City = e.City,
            Address = e.Address,
            IndustryType = e.IndustryType,
            EmployeeCount = e.EmployeeCount,
            CertScope = e.CertScope,
            ContactName = e.ContactName,
            ContactPhone = e.ContactPhone,
            ContactEmail = e.ContactEmail,
            EnterpriseNo = e.EnterpriseNo,
            ArchiveDate = e.ArchiveDate,
        };
    }
}
