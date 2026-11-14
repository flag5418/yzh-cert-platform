using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert
{
    // ══════════════════════════════════════════════════════════════════════
    // 请求 / 响应契约
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>入场检查内容行</summary>
    public class CertOnsitePlanItemRow
    {
        public string Code { get; set; } = string.Empty;
        public string CheckItemCode { get; set; } = string.Empty;
        public int SeqNo { get; set; }
        public string CheckWhat { get; set; } = string.Empty;
        public string? CheckMethod { get; set; }
        public string? TargetDept { get; set; }
        public string? SampleRequirement { get; set; }
        public string? ExpectedFinding { get; set; }
        public string? ClauseCodes { get; set; }
        /// <summary>manual / ai</summary>
        public string Source { get; set; } = "manual";
        /// <summary>planned / done / skipped</summary>
        public string Status { get; set; } = "planned";
        public int Sort { get; set; }
        public string? Remark { get; set; }
    }

    /// <summary>保存入场检查内容（Code 有值 = 更新，无值 = 新增）</summary>
    public class CertOnsitePlanSaveRequest
    {
        public string? Code { get; set; }
        public string CheckItemCode { get; set; } = string.Empty;
        public string CheckWhat { get; set; } = string.Empty;
        public string? CheckMethod { get; set; }
        public string? TargetDept { get; set; }
        public string? SampleRequirement { get; set; }
        public string? ExpectedFinding { get; set; }
        public string? ClauseCodes { get; set; }
        public string? Remark { get; set; }
        public int? Sort { get; set; }
    }

    /// <summary>
    /// 入场检查内容服务（认证准备「检查内容」的数据源）
    /// </summary>
    public class CertOnsitePlanService
    {
        private readonly IDbOrm _db;

        public CertOnsitePlanService(IDbOrm db)
        {
            _db = db;
        }

        /// <summary>按检查项查询检查内容（已排序）</summary>
        public async Task<Result<List<CertOnsitePlanItemRow>>> ListAsync(string checkItemCode)
        {
            if (string.IsNullOrWhiteSpace(checkItemCode))
                return Result<List<CertOnsitePlanItemRow>>.Fail("检查项 Code 不能为空");

            var result = await _db.GetListAsync<CertOnsitePlanItem>(i =>
                i.CheckItemCode == checkItemCode && !i.IsDeleted);
            var list = (result.Data ?? new List<CertOnsitePlanItem>())
                .OrderBy(i => i.SeqNo)
                .ThenBy(i => i.Sort)
                .ThenBy(i => i.Code)
                .Select(ToRow)
                .ToList();

            return Result<List<CertOnsitePlanItemRow>>.Ok(list);
        }

        /// <summary>新增 / 更新（唯一业务键 = Code；无 Code 即新增）</summary>
        public async Task<Result<CertOnsitePlanItemRow>> SaveAsync(
            string userCode, CertOnsitePlanSaveRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CheckItemCode))
                return Result<CertOnsitePlanItemRow>.Fail("检查项 Code 不能为空");
            if (string.IsNullOrWhiteSpace(req.CheckWhat))
                return Result<CertOnsitePlanItemRow>.Fail("检查内容不能为空");

            var detailResult = await _db.GetOneAsync<CertAuditPlanDetail>(d =>
                d.Code == req.CheckItemCode && !d.IsDeleted);
            if (detailResult.Data == null)
                return Result<CertOnsitePlanItemRow>.Fail("检查项不存在");

            CertOnsitePlanItem? entity = null;
            if (!string.IsNullOrWhiteSpace(req.Code))
            {
                var existing = await _db.GetOneAsync<CertOnsitePlanItem>(i =>
                    i.Code == req.Code && !i.IsDeleted);
                if (existing.Data == null)
                    return Result<CertOnsitePlanItemRow>.Fail("检查内容不存在");
                entity = existing.Data;
            }

            var now = DateTime.Now;
            if (entity == null)
            {
                // 新增：一次组装完整实体后 Insert（⛔ 不写「先插空行再 Update」）
                var maxSeq = await GetMaxSeqAsync(req.CheckItemCode);
                var created = new CertOnsitePlanItem
                {
                    Code = Guid.NewGuid().ToString("N"),
                    PlanCode = detailResult.Data.PlanCode,
                    CheckItemCode = req.CheckItemCode,
                    SeqNo = maxSeq + 1,
                    Sort = maxSeq + 1,
                    CheckWhat = req.CheckWhat.Trim(),
                    CheckMethod = req.CheckMethod?.Trim(),
                    TargetDept = req.TargetDept?.Trim(),
                    SampleRequirement = req.SampleRequirement?.Trim(),
                    ExpectedFinding = req.ExpectedFinding?.Trim(),
                    ClauseCodes = req.ClauseCodes?.Trim(),
                    Remark = req.Remark?.Trim(),
                    Source = "manual",
                    Status = "planned",
                    CreateBy = userCode,
                    CreateTime = now
                };
                if (req.Sort.HasValue && req.Sort.Value > 0)
                {
                    created.Sort = req.Sort.Value;
                    created.SeqNo = req.Sort.Value;
                }
                await _db.InsertAsync(created);
                return Result<CertOnsitePlanItemRow>.Ok(ToRow(created));
            }

            entity.Source = "manual";
            entity.CheckWhat = req.CheckWhat.Trim();
            entity.CheckMethod = req.CheckMethod?.Trim();
            entity.TargetDept = req.TargetDept?.Trim();
            entity.SampleRequirement = req.SampleRequirement?.Trim();
            entity.ExpectedFinding = req.ExpectedFinding?.Trim();
            entity.ClauseCodes = req.ClauseCodes?.Trim();
            entity.Remark = req.Remark?.Trim();
            if (req.Sort.HasValue && req.Sort.Value > 0)
            {
                entity.Sort = req.Sort.Value;
                entity.SeqNo = req.Sort.Value;
            }
            entity.UpdateBy = userCode;
            entity.UpdateTime = now;
            // R-A：列级写回（全列 UpdateAsync(entity) 会覆盖并发方已写入的其他列）
            await _db.UpdateAsync(entity,
                nameof(CertOnsitePlanItem.Source),
                nameof(CertOnsitePlanItem.CheckWhat),
                nameof(CertOnsitePlanItem.CheckMethod),
                nameof(CertOnsitePlanItem.TargetDept),
                nameof(CertOnsitePlanItem.SampleRequirement),
                nameof(CertOnsitePlanItem.ExpectedFinding),
                nameof(CertOnsitePlanItem.ClauseCodes),
                nameof(CertOnsitePlanItem.Remark),
                nameof(CertOnsitePlanItem.Sort),
                nameof(CertOnsitePlanItem.SeqNo),
                nameof(CertOnsitePlanItem.UpdateBy),
                nameof(CertOnsitePlanItem.UpdateTime));

            return Result<CertOnsitePlanItemRow>.Ok(ToRow(entity));
        }

        /// <summary>删除检查内容（软删）</summary>
        public async Task<Result> DeleteAsync(string userCode, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Result.Fail("检查内容 Code 不能为空");

            var existing = await _db.GetOneAsync<CertOnsitePlanItem>(i =>
                i.Code == code && !i.IsDeleted);
            if (existing.Data == null)
                return Result.Fail("检查内容不存在");

            var entity = existing.Data;
            var now = DateTime.Now;
            entity.IsDeleted = true;
            entity.DeleteBy = userCode;
            entity.DeleteTime = now;
            entity.UpdateBy = userCode;
            entity.UpdateTime = now;
            // R-A：只写软删相关列
            await _db.UpdateAsync(entity,
                nameof(CertOnsitePlanItem.IsDeleted),
                nameof(CertOnsitePlanItem.DeleteBy),
                nameof(CertOnsitePlanItem.DeleteTime),
                nameof(CertOnsitePlanItem.UpdateBy),
                nameof(CertOnsitePlanItem.UpdateTime));
            return Result.Ok();
        }

        // ── 内部 ──

        private async Task<int> GetMaxSeqAsync(string checkItemCode)
        {
            var result = await _db.GetListAsync<CertOnsitePlanItem>(i =>
                i.CheckItemCode == checkItemCode && !i.IsDeleted);
            var list = result.Data ?? new List<CertOnsitePlanItem>();
            return list.Count == 0 ? 0 : list.Max(i => Math.Max(i.SeqNo, i.Sort));
        }

        private static CertOnsitePlanItemRow ToRow(CertOnsitePlanItem i) => new()
        {
            Code = i.Code,
            CheckItemCode = i.CheckItemCode,
            SeqNo = i.SeqNo,
            CheckWhat = i.CheckWhat,
            CheckMethod = i.CheckMethod,
            TargetDept = i.TargetDept,
            SampleRequirement = i.SampleRequirement,
            ExpectedFinding = i.ExpectedFinding,
            ClauseCodes = i.ClauseCodes,
            Source = i.Source,
            Status = i.Status,
            Sort = i.Sort,
            Remark = i.Remark
        };
    }
}
