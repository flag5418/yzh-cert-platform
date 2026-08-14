/*
 * 机构-标准 / 机构-阶段 关联管理 Service 实现
 * 
 * V3 适配说明：
 * - cbCode 参数 = CertificationBody.Code（如 CB001-CODE）
 * - cert_org_stage 表结构：org_code + phase_code + standard_code
 * - cert_org_standard 表结构：org_code + standard_code
 * - 前端传 addIds/removeIds 是 CertStage.Id / ISOStandard.Id
 * - 后端需要通过 Id 查出对应的 phase_code / standard_code，再操作关联表
 * - GetOrgStageIds 返回 CertStage.Id 列表（前端用 Id 匹配 checkbox）
 * 
 * 设计原则：
 * - 使用 EF Core 查询，删除使用原生 SQL 避免 EF Core 继承映射问题
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
                // 通过 Id 查出标准的 standard_code
                var stds = _db.Set<ISOStandard>()
                    .AsNoTracking()
                    .Where(x => addIds.Contains(x.Id))
                    .ToList();

                // 查询已存在的关联
                var existingStdCodes = _db.Set<CertOrgStandard>()
                    .AsNoTracking()
                    .Where(x => x.OrgCode == cbCode)
                    .Select(x => x.StdCode)
                    .ToHashSet();

                foreach (var std in stds)
                {
                    if (existingStdCodes.Contains(std.StandardCode))
                        continue;

                    _db.Set<CertOrgStandard>().Add(new CertOrgStandard
                    {
                        OrgCode = cbCode,
                        StdCode = std.StandardCode,
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
                        rejectedRemoves.Add(new
                        {
                            StdId = stdId,
                            StandardCode = std.StandardCode,
                            StandardName = std.StandardName,
                            RefCount = refCount,
                            Reason = $"该标准下存在 {refCount} 条标准条款"
                        });
                    }
                    else
                    {
                        // 使用原生 SQL 执行删除
                        var sql = "DELETE FROM cert_org_standard WHERE org_code = {0} AND standard_code = {1}";
                        removed += _db.Database.ExecuteSqlRaw(sql, cbCode, std.StandardCode);
                    }
                }
            }

            if (added > 0)
            {
                _db.SaveChanges();
            }

            var msg = $"保存完成：新增 {added} 条";
            if (removed > 0) msg += $", 移除 {removed} 条";
            if (rejectedRemoves.Count > 0) msg += $", {rejectedRemoves.Count} 条因引用无法移除";

            return new WebResponseContent().OK(msg,
                new { added, removed, rejectedRemoves });
        }

        /// <summary>
        /// 获取机构已关联的标准 ID 列表
        /// 前端用 Id 匹配 checkbox，所以需要通过 standard_code JOIN 回 ISOStandard 获取 Id
        /// </summary>
        public object GetOrgStdIds(string cbCode)
        {
            if (string.IsNullOrEmpty(cbCode))
                return new WebResponseContent().Error("缺少机构编码");

            // cert_org_standard.standard_code → ISOStandard.standard_code → ISOStandard.Id
            var stdCodes = _db.Set<CertOrgStandard>()
                .AsNoTracking()
                .Where(x => x.OrgCode == cbCode)
                .Select(x => x.StdCode)
                .ToList();

            var ids = _db.Set<ISOStandard>()
                .AsNoTracking()
                .Where(x => stdCodes.Contains(x.StandardCode))
                .Select(x => x.Id)
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
                // 通过 Id 查出阶段的 phase_code
                var stages = _db.Set<CertStage>()
                    .AsNoTracking()
                    .Where(x => addIds.Contains(x.Id))
                    .ToList();

                // 查询已存在的关联
                var existingPhaseCodes = _db.Set<CertOrgStage>()
                    .AsNoTracking()
                    .Where(x => x.OrgCode == cbCode)
                    .Select(x => x.StageCode)
                    .ToHashSet();

                foreach (var stage in stages)
                {
                    if (existingPhaseCodes.Contains(stage.StageCode))
                        continue;

                    _db.Set<CertOrgStage>().Add(new CertOrgStage
                    {
                        OrgCode = cbCode,
                        StageCode = stage.StageCode,
                    });
                    added++;
                }
            }

            // ====== 删除关联（使用原生SQL避免视图冲突）======
            if (removeIds?.Length > 0)
            {
                // 通过 Id 查出阶段的 phase_code，再删除
                var stagesToRemove = _db.Set<CertStage>()
                    .AsNoTracking()
                    .Where(x => removeIds.Contains(x.Id))
                    .Select(x => x.StageCode)
                    .ToList();

                foreach (var phaseCode in stagesToRemove)
                {
                    var sql = "DELETE FROM cert_org_stage WHERE org_code = {0} AND phase_code = {1}";
                    removed += _db.Database.ExecuteSqlRaw(sql, cbCode, phaseCode);
                }
            }

            if (added > 0)
            {
                _db.SaveChanges();
            }

            return new WebResponseContent().OK($"同步完成：新增 {added} 条，移除 {removed} 条",
                new { added, removed });
        }

        /// <summary>
        /// 获取机构已关联的阶段 ID 列表
        /// 前端用 Id 匹配 checkbox，所以需要通过 phase_code JOIN 回 cert_cert_stage 获取 Id
        /// </summary>
        public object GetOrgStageIds(string cbCode)
        {
            if (string.IsNullOrEmpty(cbCode))
                return new WebResponseContent().Error("缺少机构编码");

            // cert_org_stage.phase_code → cert_cert_stage.phase_code → cert_cert_stage.Id
            var phaseCodes = _db.Set<CertOrgStage>()
                .AsNoTracking()
                .Where(x => x.OrgCode == cbCode)
                .Select(x => x.StageCode)
                .ToList();

            var ids = _db.Set<CertStage>()
                .AsNoTracking()
                .Where(x => phaseCodes.Contains(x.StageCode))
                .Select(x => x.Id)
                .ToList();

            return new WebResponseContent().OK(null, ids);
        }
    }
}
