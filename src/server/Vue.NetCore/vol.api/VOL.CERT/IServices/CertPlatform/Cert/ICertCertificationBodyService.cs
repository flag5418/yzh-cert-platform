using VOL.Core.BaseProvider;
using VOL.Entity.CertPlatform.Cert;
using System.Threading.Tasks;

namespace VOL.CERT.IServices.CertPlatform
{
    public partial interface ICertCertificationBodyService : IService<CertificationBody>
    {
        Task<int> GetMaxId();
    }
}
