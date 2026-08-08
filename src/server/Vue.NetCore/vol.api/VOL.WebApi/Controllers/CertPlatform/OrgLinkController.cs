/*
 * 机构-标准 / 机构-阶段 关联管理 API
 *
 * 职责：
 *   1. SyncOrgStandards — 批量同步机构-标准关联（勾选即保存）
 *   2. SyncOrgStages — 批量同步机构-阶段关联（勾选即保存）
 *   3. GetOrgStdIds — 查询某机构已关联的标准 ID 列表
 *   4. GetOrgStageIds — 查询某机构已关联的阶段 ID 列表
 *
 * 调用方式（前端 YzhTreeCheckboxTable）：
 *   - 切换树节点时调用 Get*Ids 加载已勾选状态
 *   - 用户勾选/取消 checkbox 时调用 Sync* 实时保存
 *
 * 设计原则（2026-08-07 确认）：
 * - 新建机构时自动在 cert_org_stage 中插入全部阶段记录
 * - 勾选模式：A = 自动保存（每次 checkbox 变化立即写 DB）
 * - 使用 EF Core 操作，确保类型安全和事务一致性
 */
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.Utilities;
using VOL.Core.EFDbContext;
using VOL.Core.Filters;
using VOL.Core.ManageUser;
using VOL.Core.Controllers.Basic;
using VOL.Entity.CertPlatform.Sys;
using VOL.Entity.CertPlatform.Cert;
using System.Collections.Generic;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/org-link")]
    [ApiController]
    [JWTAuthorize]
    public class OrgLinkController : ApiBaseController<object>
    {
        private readonly VOLContext _db;

        public OrgLinkController(VOLContext db)
        {
            _db = db;
        }

        // ============================================================
        // 机构-标准 关联 API
        // ============================================================

        [HttpPost("SyncOrgStandards")]
        public IActionResult SyncOrgStandards([FromBody] SyncLinkRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            try
            {
                int added = 0, removed = 0;
                var rejectedRemoves = new List<object>();

                // ====== 新增关联 ======
                if (request.AddStdIds?.Length > 0)
                {
                    var existingStdIds = _db.Set<CertOrgStandard>()
                        .Where(x => x.CbCode == request.CbCode && request.AddStdIds.Contains(x.StdId))
                        .Select(x => x.StdId)
                        .ToHashSet();

                    var toAdd = request.AddStdIds.Where(id => !existingStdIds.Contains(id)).ToList();
                    foreach (var stdId in toAdd)
                    {
                        var std = _db.Set<ISOStandard>().FirstOrDefault(x => x.Id == stdId);
                        if (std == null) continue;

                        _db.Set<CertOrgStandard>().Add(new CertOrgStandard
                        {
                            CbCode = request.CbCode,
                            StdId = stdId,
                            StdCode = std.StandardCode,
                            EnabledAt = DateTime.Now,
                        });
                        added++;
                    }
                }

                // ====== 删除关联（带引用检查）======
                if (request.RemoveStdIds?.Length > 0)
                {
                    foreach (var stdId in request.RemoveStdIds)
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
                            Console.WriteLine($"[OrgLink.SyncStd] ❌ 删除被阻断: {std.StandardName}, {refCount} 条条款引用");
                        }
                        else
                        {
                            var toRemove = _db.Set<CertOrgStandard>()
                                .Where(x => x.CbCode == request.CbCode && x.StdId == stdId)
                                .ToList();
                            _db.Set<CertOrgStandard>().RemoveRange(toRemove);
                            removed += toRemove.Count;
                        }
                    }
                }

                _db.SaveChanges();

                Console.WriteLine($"[OrgLink.SyncStd] CbCode={request.CbCode}, added={added}, removed={removed}, rejected={rejectedRemoves.Count}");
                
                var msg = $"保存完成：新增 {added} 条";
                if (removed > 0) msg += $", 移除 {removed} 条";
                if (rejectedRemoves.Count > 0) msg += $", {rejectedRemoves.Count} 条因引用无法移除";
                
                return JsonNormal(new WebResponseContent().OK(msg,
                    new { added, removed, rejectedRemoves }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrgLink.SyncStd] 异常: {ex}");
                return JsonNormal(new WebResponseContent().Error($"同步失败: {ex.Message}"));
            }
        }

        [HttpGet("GetOrgStdIds/{cbCode}")]
        public IActionResult GetOrgStdIds(string cbCode)
        {
            if (string.IsNullOrEmpty(cbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            var ids = _db.Set<CertOrgStandard>()
                .Where(x => x.CbCode == cbCode)
                .Select(x => x.StdId)
                .ToList();

            return JsonNormal(new WebResponseContent().OK(null, ids));
        }

        // ============================================================
        // 机构-阶段 关联 API
        // ============================================================

        [HttpPost("SyncOrgStages")]
        public IActionResult SyncOrgStages([FromBody] SyncLinkRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            try
            {
                int added = 0, removed = 0;

                if (request.AddStageIds?.Length > 0)
                {
                    var existingStageIds = _db.Set<CertOrgStage>()
                        .Where(x => x.CbCode == request.CbCode && request.AddStageIds.Contains(x.StageId))
                        .Select(x => x.StageId)
                        .ToHashSet();

                    var toAdd = request.AddStageIds.Where(id => !existingStageIds.Contains(id)).ToList();
                    foreach (var stageId in toAdd)
                    {
                        var stage = _db.Set<CertStage>().FirstOrDefault(x => x.Id == stageId);
                        _db.Set<CertOrgStage>().Add(new CertOrgStage
                        {
                            CbCode = request.CbCode,
                            StageId = stageId,
                            StageCode = stage?.StageCode ?? "",
                            EnabledAt = DateTime.Now,
                        });
                        added++;
                    }
                }

                if (request.RemoveStageIds?.Length > 0)
                {
                    var toRemove = _db.Set<CertOrgStage>()
                        .Where(x => x.CbCode == request.CbCode && request.RemoveStageIds.Contains(x.StageId))
                        .ToList();
                    _db.Set<CertOrgStage>().RemoveRange(toRemove);
                    removed = toRemove.Count;
                }

                _db.SaveChanges();

                Console.WriteLine($"[OrgLink.SyncStage] CbCode={request.CbCode}, added={added}, removed={removed}");
                return JsonNormal(new WebResponseContent().OK($"同步完成：新增 {added} 条，移除 {removed} 条",
                    new { added, removed }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrgLink.SyncStage] 异常: {ex}");
                return JsonNormal(new WebResponseContent().Error($"同步失败: {ex.Message}"));
            }
        }

        [HttpGet("GetOrgStageIds/{cbCode}")]
        public IActionResult GetOrgStageIds(string cbCode)
        {
            if (string.IsNullOrEmpty(cbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            var ids = _db.Set<CertOrgStage>()
                .Where(x => x.CbCode == cbCode)
                .Select(x => x.StageId)
                .ToList();

            return JsonNormal(new WebResponseContent().OK(null, ids));
        }

        // ============================================================
        // 初始化：为新建机构分配全部阶段
        // ============================================================

        [HttpPost("InitOrgStages")]
        public IActionResult InitOrgStages([FromBody] InitOrgRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            try
            {
                var allStages = _db.Set<CertStage>()
                    .ToList();

                var existingStageIds = _db.Set<CertOrgStage>()
                    .Where(x => x.CbCode == request.CbCode)
                    .Select(x => x.StageId)
                    .ToHashSet();

                var toAdd = allStages.Where(s => !existingStageIds.Contains(s.Id));
                foreach (var stage in toAdd)
                {
                    _db.Set<CertOrgStage>().Add(new CertOrgStage
                    {
                        CbCode = request.CbCode,
                        StageId = stage.Id,
                        StageCode = stage.StageCode,
                        EnabledAt = DateTime.Now,
                    });
                }

                _db.SaveChanges();

                Console.WriteLine($"[OrgLink.InitStage] CbCode={request.CbCode}, 初始化 {toAdd.Count()} 个阶段");
                return JsonNormal(new WebResponseContent().OK($"初始化完成，分配 {toAdd.Count()} 个阶段"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrgLink.InitStage] 异常: {ex}");
                return JsonNormal(new WebResponseContent().Error($"初始化失败: {ex.Message}"));
            }
        }
    }

    // ============================================================
    // 请求 DTO
    // ============================================================

    public class SyncLinkRequest
    {
        public string CbCode { get; set; }
        public long[] AddStdIds { get; set; }
        public long[] RemoveStdIds { get; set; }
        public long[] AddStageIds { get; set; }
        public long[] RemoveStageIds { get; set; }
    }

    public class InitOrgRequest
    {
        public string CbCode { get; set; }
    }
}
