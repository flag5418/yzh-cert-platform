/*
 * 机构-标准 / 机构-阶段 关联管理 Service 实现
 * 
 * 职责：
 *   1. SyncOrgStandards — 批量同步机构-标准关联（勾选即保存）
 *   2. GetOrgStdIds — 查询某机构已关联的标准 ID 列表
 *   3. SyncOrgStages — 批量同步机构-阶段关联（勾选即保存）
 *   4. GetOrgStageIds — 查询某机构已关联的阶段 ID 列表
 * 
 * 设计原则（2026-08-08 确认）：
 * - 使用 EF Core 操作，确保类型安全和事务一致性
 * - 关联表删除时进行引用检查，防止误删
 */
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.CertPlatform.Sys;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public class OrgLinkService : IOrgLinkService
    {
        private readonly VOLContext _db;

        public OrgLinkService(VOLContext db)
        {
            _db = db;
        }

        // ============================================================
        // 机构-标准 关联 API
        // ============================================================

        /// <summary>
        /// 同步机构-标准关联
        /// </summary>
        public WebResponseContent SyncOrgStandards(string cbCode, long[] addIds, long[] removeIds)
        {
            if (string.IsNullOrEmpty(cbCode))
                return new WebResponseContent().Error("缺少机构编码");

            int added = 0, removed = 0;
            var rejectedRemoves = new List<object>();

            // ====== 新增关联 ======
            if (addIds?.Length > 0)
            {
                var existingStdIds = _db.Set<CertOrgStandard>()
                    .Where(x => x.CbCode == cbCode && addIds.Contains(x.StdId))
                    .Select(x => x.StdId)
                    .ToHashSet();

                var toAdd = addIds.Where(id => !existingStdIds.Contains(id)).ToList();
                foreach (var stdId in toAdd)
                {
                    var std = _db.Set<ISOStandard>().FirstOrDefault(x => x.Id == stdId);
                    if (std == null) continue;

                    _db.Set<CertOrgStandard>().Add(new CertOrgStandard
                    {
                        CbCode = cbCode,
                        StdId = stdId,
                        StdCode = std.StandardCode,
                        EnabledAt = DateTime.Now,
                    });
                    added++;
                }
            }

            // ====== 删除关联（带引用检查）======
            if (removeIds?.Length > 0)
            {
                foreach (var stdId in removeIds)
                {
                    var std = _db.Set<ISOStandard>().FirstOrDefault(x => x.Id == stdId);
                    if (std == null) continue;

                    // 引用检查：ISOClause 是否引用了此标准
                    var refCount = _db.Set<ISOClause>()
                        .Count(x => x.StandardCode == std.StandardCode);

                    if (refCount > 0)
                    {
                        rejectedRemoves.Add(new {
                            StdId = stdId,
                            StandardCode = std.StandardCode,
                            StandardName = std.StandardName,
                            RefCount = refCount,
                            Reason = $"该标准下存在 {refCount} 条标准条款"
                        });
                    }
                    else
                    {
                        var toRemove = _db.Set<CertOrgStandard>()
                            .Where(x => x.CbCode == cbCode && x.StdId == stdId)
                            .ToList();
                        _db.Set<CertOrgStandard>().RemoveRange(toRemove);
                        removed += toRemove.Count;
                    }
                }
            }

            _db.SaveChanges();

            var msg = $"保存完成：新增 {added} 条";
            if (removed > 0) msg += $", 移除 {removed} 条";
            if (rejectedRemoves.Count > 0) msg += $", {rejectedRemoves.Count} 条因引用无法移除";

            return new WebResponseContent().OK(msg,
                new { added, removed, rejectedRemoves });
        }

        /// <summary>
        /// 获取机构已关联的标准 ID 列表
        /// </summary>
        public object GetOrgStdIds(string cbCode)
        {
            if (string.IsNullOrEmpty(cbCode))
                return new WebResponseContent().Error("缺少机构编码");

            var ids = _db.Set<CertOrgStandard>()
                .Where(x => x.CbCode == cbCode)
                .Select(x => x.StdId)
                .ToList();

            return new WebResponseContent().OK(null, ids);
        }

        // ============================================================
        // 机构-阶段 关联 API
        // ============================================================

        /// <summary>
        /// 同步机构-阶段关联
        /// </summary>
        public WebResponseContent SyncOrgStages(string cbCode, long[] addIds, long[] removeIds)
        {
            if (string.IsNullOrEmpty(cbCode))
                return new WebResponseContent().Error("缺少机构编码");

            int added = 0, removed = 0;

            // ====== 新增关联 ======
            if (addIds?.Length > 0)
            {
                var existingStageIds = _db.Set<CertOrgStage>()
                    .Where(x => x.CbCode == cbCode && addIds.Contains(x.StageId))
                    .Select(x => x.StageId)
                    .ToHashSet();

                var toAdd = addIds.Where(id => !existingStageIds.Contains(id)).ToList();
                foreach (var stageId in toAdd)
                {
                    var stage = _db.Set<CertStage>().FirstOrDefault(x => x.Id == stageId);
                    _db.Set<CertOrgStage>().Add(new CertOrgStage
                    {
                        CbCode = cbCode,
                        StageId = stageId,
                        StageCode = stage?.StageCode ?? "",
                        EnabledAt = DateTime.Now,
                    });
                    added++;
                }
            }

            // ====== 删除关联 ======
            if (removeIds?.Length > 0)
            {
                var toRemove = _db.Set<CertOrgStage>()
                    .Where(x => x.CbCode == cbCode && removeIds.Contains(x.StageId))
                    .ToList();
                _db.Set<CertOrgStage>().RemoveRange(toRemove);
                removed = toRemove.Count;
            }

            _db.SaveChanges();

            return new WebResponseContent().OK($"同步完成：新增 {added} 条，移除 {removed} 条",
                new { added, removed });
        }

        /// <summary>
        /// 获取机构已关联的阶段 ID 列表
        /// </summary>
        public object GetOrgStageIds(string cbCode)
        {
            if (string.IsNullOrEmpty(cbCode))
                return new WebResponseContent().Error("缺少机构编码");

            var ids = _db.Set<CertOrgStage>()
                .Where(x => x.CbCode == cbCode)
                .Select(x => x.StageId)
                .ToList();

            return new WebResponseContent().OK(null, ids);
        }
    }
}
