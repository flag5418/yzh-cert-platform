using VOL.Core.BaseProvider;
using VOL.Entity.Admin.Platform.Cert;
using System.Threading.Tasks;

namespace VOL.CERT.IServices.Admin.Platform
{
    public partial interface ICertCertificationBodyService : IService<CertificationBody>
    {
        Task<int> GetMaxId();
    }
}
