using YZH.Core.BaseProvider;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;
using YZH.Entity.DomainModels;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Audit;

namespace Cert.Platform.Services.Admin.Platform
{
    public partial class AuditTaskService
    {
        private readonly IAuditTaskRepository _repository;

        [ActivatorUtilitiesConstructor]
        public AuditTaskService(IAuditTaskRepository dbRepository)
        : base(dbRepository)
        {
            _repository = dbRepository;
            // 显式赋值给基类 field，确保 GetPageData 不报 NPE
            this.repository = dbRepository;
        }

        /// <summary>
        /// 兜底处理：如果框架通过反射或无参构造函数实例化，确保 repository 被赋值
        /// </summary>
        public override PageGridData<AuditTask> GetPageData(PageDataOptions options)
        {
            if (this.repository == null)
            {
                this.repository = _repository ?? AutofacContainerModule.GetService<IAuditTaskRepository>();
            }
            return base.GetPageData(options);
        }
    }
}