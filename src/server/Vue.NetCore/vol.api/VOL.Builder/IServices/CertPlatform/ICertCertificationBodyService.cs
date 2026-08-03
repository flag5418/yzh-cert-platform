using VOL.Core.BaseProvider;
using VOL.Entity.CertPlatform.Cert;
using System.Threading.Tasks;

namespace VOL.Builder.IServices.CertPlatform
{
    public partial interface ICertCertificationBodyService : IService<CertificationBody>
    {
        Task<int> GetMaxId();
    }
}
