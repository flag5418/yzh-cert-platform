using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// Workflow Skill 服务实现
    /// </summary>
    [Obsolete("请使用 WfSkillService，此类保留仅为向后兼容")]
    public class SkillService : ISkillService, IDependency
    {
        private readonly VOLContext _db;

        public SkillService(VOLContext db)
        {
            _db = db;
        }

        public async Task<List<Skill>> GetAllAsync()
        {
            return await _db.Set<Skill>().ToListAsync();
        }

        public async Task<Skill> GetByCodeAsync(string skillCode)
        {
            return await _db.Set<Skill>().FirstOrDefaultAsync(s => s.SkillCode == skillCode);
        }

        public async Task<bool> UpdateAsync(Skill skill)
        {
            _db.Set<Skill>().Update(skill);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
