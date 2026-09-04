using VOL.Core.BaseProvider;
using VOL.Entity.Admin.Platform.Cert;

namespace VOL.CERT.IServices.Admin.Platform
{
    public partial interface ICertStageService : IService<CertStage>
    {
        System.Threading.Tasks.Task<System.Collections.Generic.List<CertStage>> GetActiveStagesAsync();
    }
}
