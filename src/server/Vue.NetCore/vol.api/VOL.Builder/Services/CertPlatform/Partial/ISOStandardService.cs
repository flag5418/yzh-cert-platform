using VOL.Core.BaseProvider;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;
using VOL.Entity.DomainModels;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Builder.Services.CertPlatform
{
    public partial class ISOStandardService
    {
        private readonly IISOStandardRepository _repository;

        [ActivatorUtilitiesConstructor]
        public ISOStandardService(IISOStandardRepository dbRepository)
        : base(dbRepository)
        {
            _repository = dbRepository;
            // 显式赋值给基类 field，确保 GetPageData 不报 NPE
            this.repository = dbRepository;
        }

        /// <summary>
        /// 兜底处理：如果框架通过反射或无参构造函数实例化，确保 repository 被赋值
        /// </summary>
        public override PageGridData<ISOStandard> GetPageData(PageDataOptions options)
        {
            if (this.repository == null)
            {
                this.repository = _repository ?? AutofacContainerModule.GetService<IISOStandardRepository>();
            }
            return base.GetPageData(options);
        }
    }
}