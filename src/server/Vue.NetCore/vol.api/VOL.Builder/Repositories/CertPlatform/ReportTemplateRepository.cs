using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Repositories.CertPlatform
{
    public partial class ReportTemplateRepository : RepositoryBase<ReportTemplate>, IReportTemplateRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public ReportTemplateRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static IReportTemplateRepository Instance
        {
            get { return AutofacContainerModule.GetService<IReportTemplateRepository>(); }
        }
    }
}
