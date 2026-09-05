using YZH.Core.BaseProvider;
using YZH.Entity.Admin.Platform.Cert;

namespace Cert.Platform.IServices.Admin.Platform
{
    public partial interface ICertStageService : IService<CertStage>
    {
        System.Threading.Tasks.Task<System.Collections.Generic.List<CertStage>> GetActiveStagesAsync();
    }
}
