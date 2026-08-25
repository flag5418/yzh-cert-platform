using VOL.Core.BaseProvider;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.CERT.IServices.CertPlatform
{
    public partial interface ICertStageService : IService<CertStage>
    {
        System.Threading.Tasks.Task<System.Collections.Generic.List<CertStage>> GetActiveStagesAsync();
    }
}
