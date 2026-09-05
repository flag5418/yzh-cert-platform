using YZH.Core.BaseProvider;
using YZH.Entity.Admin.Platform.Cert;
using System.Threading.Tasks;

namespace Cert.Platform.IServices.Admin.Platform
{
    public partial interface ICertCertificationBodyService : IService<CertificationBody>
    {
        Task<int> GetMaxId();
    }
}
