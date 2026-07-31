using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IRepositories.CertPlatform;

namespace VOL.Builder.Repositories.CertPlatform
{
    public class CertCertificationBodyRepository : RepositoryBase<CertificationBody>, ICertCertificationBodyRepository, IDependency
    {
        // 使用默认无参构造函数，框架会自动注入 DbContext
    }
}
