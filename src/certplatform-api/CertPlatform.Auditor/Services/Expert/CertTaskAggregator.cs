using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Expert;

/// <summary>
/// 认证任务进度汇总回写（46 号 F11）。
///
/// <para><b>为什么必须回写</b>：`cert_task.Status` 是<b>页面锁的开关</b>（§5.5）——
/// 停在 <c>running</c> 就意味着「队列还在跑，人不许动」。如果队列跑完了没人把它改成
/// <c>done</c>，页面就<b>永久锁死</b>。这是 F11 与 F12 的隐藏依赖关系：
/// <b>不回写 = 锁死</b>，不是「进度条不动」这么轻。</para>
///
/// <para><b>为什么从 <c>cert_task_item</c> 汇总而不是从 <c>yzh_queue_task</c></b>：</para>
/// <list type="bullet">
///   <item>业务真相在 <c>cert_task_item</c>（一个检查项跑成什么样），队列只是调度手段；</item>
///   <item>执行器在 <b>返回之前</b> 就把自己的 task_item 置好了，所以「最后一项跑完」那一刻
///         汇总就是准的 —— 而 <c>yzh_queue_task</c> 的状态要等 QueueManager 返回后才更新，
///         用它汇总会永远差最后一项；</item>
///   <item>手工 <c>analyze-item</c>（不走队列）也能被正确计入。</item>
/// </list>
///
/// <para><b>状态口径</b>（与 <c>cert_task.Status</c> 列注释一致）：
/// <c>pending / running / paused / done / partial / failed</c>。</para>
/// </summary>
public static class CertTaskAggregator
{
    /// <summary>视为「已完成」的任务项状态（引擎写 done，人工复核后可能是 analyzed / reviewed）</summary>
    private static readonly string[] DoneStates = { "done", "analyzed", "reviewed" };

    /// <summary>
    /// 重新计算并回写任务进度。
    /// <para>幂等、可高频调用（每跑完一项调一次，行数就是检查项数，成本可忽略）。</para>
    /// </summary>
    public static async Task RefreshAsync(IDbOrm db, string taskCode, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(taskCode)) return;

        try
        {
            var task = (await db.GetOneAsync<CertTask>(t => t.Code == taskCode && !t.IsDeleted)).Data;
            if (task == null) return;

            // ★ 暂停态：Status 由人显式设置，⛔ 汇总不许把它改回 running（否则「暂停」按钮形同虚设）。
            //   但计数还是要刷 —— 暂停那一刻可能有项正在跑并已跑完，不刷的话页面显示
            //   「1 项已完成 / 进度 0」这种自相矛盾的状态（实测踩到过）。
            var isPaused = task.Status == "paused";

            var items = (await db.GetListAsync<CertTaskItem>(i =>
                i.TaskCode == taskCode && !i.IsDeleted)).Data ?? new List<CertTaskItem>();

            var total = items.Count;
            var done = items.Count(i => DoneStates.Contains(i.Status));
            var failed = items.Count(i => i.Status == "failed");
            var skipped = items.Count(i => i.Status == "skipped");
            var pending = total - done - failed - skipped;

            task.ItemTotal = total;
            task.ItemDone = done;
            task.ItemFailed = failed;
            task.ItemSkipped = skipped;

            if (isPaused)
            {
                task.Message = Truncate(
                    $"已暂停：已完成 {done} / 失败 {failed} / 跳过 {skipped}，剩余 {pending}，共 {total}", 500);
            }
            else if (pending > 0)
            {
                // ★ 2026-11-11：只有真正投过队列的任务才允许进入 running。
                //   手工单项分析（analyze-item）也会调本方法刷新计数，但它从不入队；
                //   旧实现无脑写 running ⇒ 页面锁（按 Status=running 判定）在第一次
                //   手工分析后永久锁死，「自动分析」按钮变灰点不动。
                if (!string.IsNullOrWhiteSpace(task.QueueCode))
                {
                    task.Status = "running";
                }
                task.Message = Truncate(
                    $"进行中：已完成 {done} / 失败 {failed} / 跳过 {skipped}，共 {total}", 500);
            }
            else if (done == 0 && failed == 0 && skipped > 0)
            {
                // ★ 全锁场景（§10.3）：⛔ 不报错（勾到已锁定的项是正常误操作），
                //   但也⛔ 不能静默 —— 必须说清"为什么什么都没跑"。
                task.Status = "done";
                task.FinishedAt ??= DateTime.Now;
                task.Message = Truncate(
                    $"选中的 {total} 个检查项均已锁定，本次未执行任何分析", 500);
            }
            else if (failed > 0)
            {
                task.Status = "partial";
                task.FinishedAt ??= DateTime.Now;
                task.Message = Truncate(
                    $"完成 {done} 项、跳过 {skipped} 项、失败 {failed} 项（共 {total}）", 500);
            }
            else
            {
                task.Status = "done";
                task.FinishedAt ??= DateTime.Now;
                task.Message = Truncate(
                    skipped > 0
                        ? $"完成 {done} 项，跳过 {skipped} 项（已锁定）"
                        : $"全部完成，共 {done} 项", 500);
            }

            task.UpdateTime = DateTime.Now;
            await db.UpdateAsync(task);
        }
        catch (Exception ex)
        {
            // ⛔ 汇总失败不能把已落库的分析结果打成失败 —— 只记日志
            logger?.LogError(ex, "[CertTaskAggregator] 汇总回写失败 task={TaskCode}", taskCode);
        }
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
