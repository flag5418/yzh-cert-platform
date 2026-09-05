using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Rpt;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Wf
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
