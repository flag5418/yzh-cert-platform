using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Cert;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Wf
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
