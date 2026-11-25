using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 手册「0.7 职责权限」→ 企业部门 <c>DeptDuty</c> 的<b>完善器</b>（确定性，零 LLM）。
    ///
    /// <para><b>★ 边界（用户裁定）</b>：只<b>覆盖已有部门的</b> <c>DeptDuty</c>，
    /// ⛔ 不新增部门、⛔ 不改部门名、⛔ 不删除部门 —— 部门由审核员手工建立/对齐，
    /// 本器只把「手册写明的部门职能」补进去；<see cref="ManualIndexParser"/> 已解析出
    /// <c>FrontMatter.DeptDuties</c>（部门名 → 职能条目），此处按 <c>DeptName</c> 精确匹配。</para>
    ///
    /// <para>触发时机：手册章节模式提取成功后（见 <see cref="ClauseExtractExecutor"/>）。
    /// 名称未命中的部门只跳过（返回计数供调用方记日志），⛔ 不报错、不阻断提取。</para>
    /// </summary>
    public static class ManualDeptDutySyncer
    {
        /// <summary>
        /// 按部门名把手册职能写入企业部门的 <c>DeptDuty</c>（手册优先覆盖，值无变化则跳过）。
        /// </summary>
        /// <returns>实际更新的部门数</returns>
        public static async Task<int> SyncAsync(
            IDbOrm db,
            string enterpriseCode,
            ManualIndexParser.ManualIndex? manualIndex,
            CancellationToken ct = default)
        {
            var duties = manualIndex?.FrontMatter?.DeptDuties;
            if (string.IsNullOrWhiteSpace(enterpriseCode) || duties == null || duties.Count == 0)
                return 0;

            // 含禁用部门：审核员可能先把多余部门禁用，但手册里仍有职能描述时也应完善
            var deptResult = await db.GetListAsync<CertEnterpriseDept>(
                x => x.EnterpriseCode == enterpriseCode, includeDisabled: true);
            var depts = deptResult.Data;
            if (depts == null || depts.Count == 0) return 0;

            var byName = new Dictionary<string, CertEnterpriseDept>(StringComparer.Ordinal);
            foreach (var d in depts)
            {
                var name = d.DeptName?.Trim();
                if (!string.IsNullOrEmpty(name) && !byName.ContainsKey(name))
                    byName[name] = d;
            }

            var updated = 0;
            foreach (var duty in duties)
            {
                ct.ThrowIfCancellationRequested();
                var key = duty.Dept?.Trim();
                if (string.IsNullOrEmpty(key) || duty.Duties.Count == 0) continue;
                if (!byName.TryGetValue(key, out var dept)) continue;

                // ★ 存纯文本（一条一行）：DeptDuty 是给审核员在页面上直接看 / 完善的字段，
                //   ⛔ 不存 JSON 数组 —— 那会让人手改字符串字面量（漏引号即坏数据）。
                var text = string.Join("\n", duty.Duties
                    .Select(s => s?.Trim() ?? string.Empty)
                    .Where(s => s.Length > 0));
                if (string.Equals(dept.DeptDuty, text, StringComparison.Ordinal)) continue;

                dept.DeptDuty = text;
                // 只写 DeptDuty 一列：不触碰审计列与其它业务列
                var res = await db.UpdateAsync(dept, nameof(CertEnterpriseDept.DeptDuty));
                if (res.Success) updated++;
            }

            return updated;
        }
    }
}
