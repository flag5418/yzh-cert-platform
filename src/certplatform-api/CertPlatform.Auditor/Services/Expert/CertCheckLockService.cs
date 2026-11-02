using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert;

/// <summary>
/// 检查项锁服务 —— 锁的唯一读写出口（46 号 §10）。
///
/// <para><b>用户口径</b>：「一旦一个检查项被锁定了，即使选择了这个检查项，
/// 但我们队列执行的时候，也要忽略该检查项」。</para>
///
/// <para><b>锁住的是什么</b>：不是"禁止查看"，而是<b>禁止任何队列执行覆盖它</b>。
/// 结果、证据链、疑点、审核员的修改全部冻结。</para>
///
/// <para><b>三道闸门（⛔ 少一道就有漏洞）</b>：</para>
/// <list type="number">
///   <item><b>① 入队</b>（<see cref="GetLockedCodesAsync"/>）：建 cert_task_item 时过滤，
///         锁定的项仍建 item 但 Status='skipped'，⛔ 不投队列 ——
///         防白建队列项、白占并发位、白打 LLM 配额。</item>
///   <item><b>② 执行</b>（<see cref="IsLockedAsync"/>）：执行器第一行再查，
///         防「入队后、执行前被锁上」的时间窗 —— ⛔ 不调 LLM、不写结果。</item>
///   <item><b>③ 写入</b>（<see cref="AssertNotLockedAsync"/>）：落 cert_check_result 前断言，
///         防极端漏判（代码路径绕过前两道）。</item>
/// </list>
///
/// <para><b>锁与指纹解耦</b>（§10.4）：<c>IsLocked=1</c> ⇒ <b>无条件跳过</b>。
/// 指纹不再参与"跳过"判断，改用于<b>过期告警</b>（<c>cert_check_analysis.Status='stale'</c> + 界面橙标）。
/// 没有任何 <c>RunMode</c> 能绕过锁（包括 <c>full</c>）。</para>
/// </summary>
public class CertCheckLockService
{
    private readonly IDbOrm _db;
    private readonly ILogger<CertCheckLockService> _logger;

    public CertCheckLockService(IDbOrm db, ILogger<CertCheckLockService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>锁粒度常量</summary>
    public static class StageKinds
    {
        /// <summary>整项锁死（默认）—— 准备和检查两个分支都不跑</summary>
        public const string All = "all";

        /// <summary>只锁准备分支 —— 确认了"资料齐不齐"，但现场还要去</summary>
        public const string Prep = "prep";

        /// <summary>只锁检查分支 —— 最终判定已定，不再重跑检查</summary>
        public const string Onsite = "onsite";
    }

    // ══════════════════════════════════════════════════════════════════
    // 闸门② / ③：单检查项判定
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 该检查项在指定分支上是否被锁（闸门② 与 ③ 共用）。
    /// <para><paramref name="stageKind"/> = <c>prep</c> / <c>onsite</c>；
    /// 命中条件：存在 <c>StageKind IN ('all', stageKind)</c> 且 <c>IsActive=1</c> 的锁。</para>
    /// </summary>
    public async Task<bool> IsLockedAsync(string checkItemCode, string stageKind)
    {
        if (string.IsNullOrWhiteSpace(checkItemCode)) return false;

        var stage = NormalizeStage(stageKind);

        // 先查最宽松的 all（绝大多数场景走这条，命中即返回，省一次查询）
        var hitAll = await _db.ExistsAsync<CertCheckLock>(l =>
            l.CheckItemCode == checkItemCode
            && l.StageKind == StageKinds.All
            && l.IsActive
            && !l.IsDeleted);
        if (hitAll.Data) return true;

        var hit = await _db.ExistsAsync<CertCheckLock>(l =>
            l.CheckItemCode == checkItemCode
            && l.StageKind == stage
            && l.IsActive
            && !l.IsDeleted);
        return hit.Data;
    }

    /// <summary>
    /// 闸门③：写入前断言。<b>已锁定 ⇒ 抛异常</b>（不是返回 false）。
    /// <para>为什么抛而不是返回：这是"不该发生"的路径（前两道都过了还被写），
    /// 静默 return 会让人以为分析成功但结果没落库 —— 抛出去让整条链明显失败。</para>
    /// </summary>
    public async Task AssertNotLockedAsync(string checkItemCode, string stageKind)
    {
        if (await IsLockedAsync(checkItemCode, stageKind))
            throw new InvalidOperationException(
                $"检查项已锁定，拒绝写入（CheckItemCode={checkItemCode}, StageKind={stageKind}）");
    }

    // ══════════════════════════════════════════════════════════════════
    // 闸门①：批量判定（建任务时用）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 闸门①：从一批检查项里筛出"已锁定"的 Code 集合。
    /// <para>用于建 <c>cert_task_item</c> 时：命中的项仍建 item 但 <c>Status='skipped'</c>，
    /// ⛔ 不投队列。</para>
    /// <para>⚠️ 一次查询解决，⛔ 不要循环调 <see cref="IsLockedAsync"/>（N 项 = N 次往返）。</para>
    /// </summary>
    public async Task<HashSet<string>> GetLockedCodesAsync(
        IEnumerable<string> checkItemCodes, string stageKind)
    {
        var codes = (checkItemCodes ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        if (codes.Count == 0) return new HashSet<string>();

        var stage = NormalizeStage(stageKind);

        var rows = (await _db.GetListAsync<CertCheckLock>(l =>
            codes.Contains(l.CheckItemCode)
            && (l.StageKind == StageKinds.All || l.StageKind == stage)
            && l.IsActive
            && !l.IsDeleted)).Data ?? new List<CertCheckLock>();

        return rows.Select(r => r.CheckItemCode).ToHashSet();
    }

    /// <summary>该检查项的所有生效锁（界面展示"为什么不能跑"）</summary>
    public async Task<List<CertCheckLock>> ListActiveAsync(string checkItemCode)
    {
        if (string.IsNullOrWhiteSpace(checkItemCode)) return new List<CertCheckLock>();

        return (await _db.GetListAsync<CertCheckLock>(l =>
            l.CheckItemCode == checkItemCode
            && l.IsActive
            && !l.IsDeleted)).Data ?? new List<CertCheckLock>();
    }

    // ══════════════════════════════════════════════════════════════════
    // 加锁 / 解锁
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 锁定检查项。
    /// <para>幂等：同一 <c>(CheckItemCode, StageKind)</c> 已有行 ⇒ 复活它（<c>IsActive=1</c>）而不是插新行
    /// （唯一键 <c>uk_item_stage</c> 不允许两条）。</para>
    /// </summary>
    public async Task<Result> LockAsync(
        string userCode, string checkItemCode, string stageKind = StageKinds.All,
        string? lockReason = null, string lockSource = "manual")
    {
        if (string.IsNullOrWhiteSpace(checkItemCode))
            return Result.Fail("检查项 Code 不能为空");

        var stage = NormalizeStage(stageKind, allowAll: true);
        var now = DateTime.Now;

        var existing = await _db.GetOneAsync<CertCheckLock>(l =>
            l.CheckItemCode == checkItemCode
            && l.StageKind == stage
            && !l.IsDeleted);

        if (existing.Data != null)
        {
            var row = existing.Data;
            if (row.IsActive) return Result.Ok();   // 已锁，幂等返回

            row.IsActive = true;
            row.LockedBy = userCode;
            row.LockedAt = now;
            row.LockReason = lockReason ?? "manual_confirm";
            row.LockSource = lockSource;
            row.UnlockedBy = null;
            row.UnlockedAt = null;
            row.UpdateBy = userCode;
            row.UpdateTime = now;
            await _db.UpdateAsync(row);
            return Result.Ok();
        }

        await _db.InsertAsync(new CertCheckLock
        {
            Code = Guid.NewGuid().ToString("N"),
            CheckItemCode = checkItemCode,
            StageKind = stage,
            LockedBy = userCode,
            LockedAt = now,
            LockReason = lockReason ?? "manual_confirm",
            LockSource = lockSource,
            IsActive = true,
            CreateBy = userCode,
            CreateTime = now
        });

        _logger.LogInformation("[CertCheckLock] 已锁定 item={Item} stage={Stage} by={User}",
            checkItemCode, stage, userCode);
        return Result.Ok();
    }

    /// <summary>
    /// 解锁检查项（锁的<b>唯一出口</b>）。
    /// <para>留痕不删行：<c>IsActive=0</c> + <c>UnlockedBy/At</c>，这样"谁在什么时候解过锁"查得到。</para>
    /// </summary>
    public async Task<Result> UnlockAsync(
        string userCode, string checkItemCode, string stageKind = StageKinds.All)
    {
        if (string.IsNullOrWhiteSpace(checkItemCode))
            return Result.Fail("检查项 Code 不能为空");

        var stage = NormalizeStage(stageKind, allowAll: true);

        var existing = await _db.GetOneAsync<CertCheckLock>(l =>
            l.CheckItemCode == checkItemCode
            && l.StageKind == stage
            && !l.IsDeleted);

        if (existing.Data == null)
            return Result.Fail("该检查项在此粒度上没有锁记录");

        var row = existing.Data;
        if (!row.IsActive) return Result.Ok();   // 已解锁，幂等返回

        row.IsActive = false;
        row.UnlockedBy = userCode;
        row.UnlockedAt = DateTime.Now;
        row.UpdateBy = userCode;
        row.UpdateTime = DateTime.Now;
        await _db.UpdateAsync(row);

        _logger.LogInformation("[CertCheckLock] 已解锁 item={Item} stage={Stage} by={User}",
            checkItemCode, stage, userCode);
        return Result.Ok();
    }

    // ══════════════════════════════════════════════════════════════════
    // 辅助
    // ══════════════════════════════════════════════════════════════════

    /// <summary>分支名归一：空 / 未知值一律按 onsite 处理（保守：宁可按检查分支算，不漏锁）</summary>
    private static string NormalizeStage(string? stageKind, bool allowAll = false)
    {
        if (allowAll && string.Equals(stageKind, StageKinds.All, StringComparison.OrdinalIgnoreCase))
            return StageKinds.All;
        if (string.Equals(stageKind, StageKinds.Prep, StringComparison.OrdinalIgnoreCase))
            return StageKinds.Prep;
        return StageKinds.Onsite;
    }
}
