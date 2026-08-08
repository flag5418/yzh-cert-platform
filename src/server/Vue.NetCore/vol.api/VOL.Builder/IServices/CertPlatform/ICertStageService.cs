using VOL.Core.BaseProvider;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Builder.IServices.CertPlatform
{
    public partial interface ICertStageService : IService<CertStage>
    {
        System.Threading.Tasks.Task<System.Collections.Generic.List<CertStage>> GetActiveStagesAsync();
        System.Threading.Tasks.Task<System.Collections.Generic.List<long>> GetOrgStageIdsAsync(string cbCode);
    }
}
