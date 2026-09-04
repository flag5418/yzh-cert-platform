using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Rpt;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.CertPlatform
{
    public partial class ReportSectionRepository : RepositoryBase<ReportSection>, IReportSectionRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public ReportSectionRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static IReportSectionRepository Instance
        {
            get { return AutofacContainerModule.GetService<IReportSectionRepository>(); }
        }
    }
}
