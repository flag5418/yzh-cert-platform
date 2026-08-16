using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.CertPlatform.Sys;
using VOL.Entity.CertPlatform.Wf;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public interface ICertPlatformTreeService
    {
        Task<object> GetOrgStandardPhaseTreeAsync();
        Task<List<ISOStandard>> GetStandardsAsync(string orgCode = null);
        Task<List<PhaseDefinition>> GetPhasesAsync();
        Task<List<CertificationBody>> GetOrgsAsync();
    }

    public class CertPlatformTreeService : ICertPlatformTreeService, VOL.Core.Extensions.AutofacManager.IDependency
    {
        private readonly VOLContext _db;

        public CertPlatformTreeService(VOLContext db)
        {
            _db = db;
        }

        /// <summary>
        /// 构建机构→标准→阶段→规则数 的树形结构
        /// </summary>
        public async Task<object> GetOrgStandardPhaseTreeAsync()
        {
            // 加载所有关联数据
            var orgs = await _db.Set<CertificationBody>().Where(x => x.Enable).ToListAsync();
            var standards = await _db.Set<ISOStandard>().Where(x => x.Enable).ToListAsync();
            var phases = await _db.Set<PhaseDefinition>().Where(x => x.Enable).ToListAsync();
            var rules = await _db.Set<ValidationRule>().Where(x => x.Enable).ToListAsync();
            var orgStandards = await _db.Set<CertOrgStandard>().Where(x => x.Enable).ToListAsync();

            // 按机构 → 标准 → 阶段 分组规则数
            var ruleCounts = new Dictionary<string, int>();
            foreach (var r in rules)
            {
                var key = $"{r.OrgCode}|{r.StandardCode}|{r.PhaseCode}";
                if (!ruleCounts.ContainsKey(key)) ruleCounts[key] = 0;
                ruleCounts[key]++;
            }

            // 构建树
            var result = new List<dynamic>();
            foreach (var org in orgs)
            {
                var orgCode = org.Code;
                var orgStandardsList = orgStandards.Where(os => os.OrgCode == orgCode).Select(os => os.StdCode).ToList();

                var stdNodes = new List<dynamic>();
                foreach (var std in standards)
                {
                    if (!orgStandardsList.Contains(std.Code)) continue;
                    var stdCode = std.StandardCode;

                    var phaseNodes = new List<dynamic>();
                    foreach (var phase in phases)
                    {
                        var key = $"{orgCode}|{stdCode}|{phase.PhaseCode}";
                        var count = ruleCounts.ContainsKey(key) ? ruleCounts[key] : 0;
                        phaseNodes.Add(new
                        {
                            key = $"phase_{orgCode}_{stdCode}_{phase.PhaseCode}",
                            label = $"{phase.PhaseCode} {phase.PhaseName}",
                            icon = "Document",
                            color = "#e6a23c",
                            ruleCount = count,
                            filter = new { orgCode, standardCode = stdCode, phaseCode = phase.PhaseCode }
                        });
                    }

                    var stdCount = phaseNodes.Sum(p => (int)p.ruleCount);
                    stdNodes.Add(new
                    {
                        key = $"std_{orgCode}_{stdCode}",
                        label = std.StandardName,
                        icon = "Folder",
                        color = "#67c23a",
                        ruleCount = stdCount,
                        children = phaseNodes
                    });
                }

                var orgCount = stdNodes.Sum(s => (int)s.ruleCount);
                result.Add(new
                {
                    key = $"org_{orgCode}",
                    label = org.Name,
                    icon = "OfficeBuilding",
                    color = "#409eff",
                    ruleCount = orgCount,
                    children = stdNodes
                });
            }

            return result;
        }

        public async Task<List<ISOStandard>> GetStandardsAsync(string orgCode = null)
        {
            var query = _db.Set<ISOStandard>().Where(x => x.Enable);
            if (!string.IsNullOrWhiteSpace(orgCode))
            {
                var orgStdCodes = await _db.Set<CertOrgStandard>()
                    .Where(os => os.OrgCode == orgCode && os.Enable)
                    .Select(os => os.StdCode)
                    .ToListAsync();
                query = query.Where(s => orgStdCodes.Contains(s.StandardCode));
            }
            return await query.OrderBy(x => x.StandardCode).ToListAsync();
        }

        public async Task<List<PhaseDefinition>> GetPhasesAsync()
        {
            return await _db.Set<PhaseDefinition>()
                .Where(x => x.Enable)
                .OrderBy(x => x.SequenceOrder)
                .ToListAsync();
        }

        public async Task<List<CertificationBody>> GetOrgsAsync()
        {
            return await _db.Set<CertificationBody>()
                .Where(x => x.Enable)
                .OrderBy(x => x.Code)
                .ToListAsync();
        }
    }
}
