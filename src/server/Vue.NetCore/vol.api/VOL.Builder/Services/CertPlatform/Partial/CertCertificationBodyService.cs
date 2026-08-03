/*
 *所有关于CertificationBody类的业务代码应在此处编写
 *可使用repository.调用常用方法，获取EF/Dapper等信息
 *如果需要事务请使用repository.DbContextBeginTransaction
 *也可使用DBServerProvider.手动获取数据库相关信息
 *用户信息、权限、角色等使用UserContext.Current操作
 *CertCertificationBodyService对增、删、改查、导入、导出、审核业务代码扩展参照ServiceFunFilter
 */
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IRepositories.CertPlatform;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using VOL.Entity.DomainModels;

namespace VOL.Builder.Services.CertPlatform
{
    public partial class CertCertificationBodyService
    {
        private readonly ICertCertificationBodyRepository _repository;

        [ActivatorUtilitiesConstructor]
        public CertCertificationBodyService(ICertCertificationBodyRepository dbRepository)
        : base(dbRepository)
        {
            _repository = dbRepository;
            // 显式赋值给基类 field，确保 GetPageData 不报 NPE
            this.repository = dbRepository;
        }

        /// <summary>
        /// 兜底处理：如果框架通过反射或无参构造函数实例化，确保 repository 被赋值
        /// </summary>
        public override PageGridData<CertificationBody> GetPageData(PageDataOptions options)
        {
            if (this.repository == null)
            {
                this.repository = _repository ?? AutofacContainerModule.GetService<ICertCertificationBodyRepository>();
            }
            return base.GetPageData(options);
        }

        /// <summary>
        /// 获取当前最大 ID，用于生成编号
        /// </summary>
        /// <returns></returns>
        public async Task<int> GetMaxId()
        {
            return await _repository.FindAsIQueryable(x => true).CountAsync();
        }

        /// <summary>
        /// 获取所有启用的认证机构（下拉选择用）
        /// </summary>
        // TODO: Phase 2 实现具体业务逻辑时启用此方法
        /*
        public async Task<List<CertificationBody>> GetActiveListAsync()
        {
            return await _repository.FindAsync(x => x.Status == "active")
                .OrderBy(x => x.Name)
                .ToListAsync();
        }
        */
    }
}
