using VOL.Entity.CertPlatform.Wf;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface ISkillService
    {
        Task<List<Skill>> GetAllAsync();
        Task<Skill> GetByCodeAsync(string skillCode);
        Task<bool> UpdateAsync(Skill skill);
    }
}
