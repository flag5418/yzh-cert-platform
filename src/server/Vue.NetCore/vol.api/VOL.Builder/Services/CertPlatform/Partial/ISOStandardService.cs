using VOL.Core.BaseProvider;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;
using VOL.Entity.DomainModels;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Core.EFDbContext;
using System;
using System.Linq;

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
            this.repository = dbRepository;
        }

        /// <summary>
        /// 重写 GetPageData：直接查询 cert_iso_standard 表
        /// 
        /// 注意：v_iso_standard 视图已 DROP，后续按需重建视图或用 EF Core 投影替代
        /// </summary>
        public override PageGridData<ISOStandard> GetPageData(PageDataOptions options)
        {
            var db = _repository.DbContext as VOLContext ?? new VOLContext();
            var query = db.Set<ISOStandard>().AsQueryable();

            int totalCount = query.Count();

            // 排序
            string sortField = options.Sort ?? "CreateDate";
            bool isAsc = options.Order?.ToLower() == "asc";
            query = sortField.ToUpper() switch
            {
                "STANDARDCODE" => isAsc ? query.OrderBy(x => x.StandardCode) : query.OrderByDescending(x => x.StandardCode),
                "STANDARDNAME" => isAsc ? query.OrderBy(x => x.StandardName) : query.OrderByDescending(x => x.StandardName),
                _ => query.OrderByDescending(x => x.Id),
            };

            // 分页
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = query.Skip((page - 1) * rows).Take(rows).ToList();

            var result = new PageGridData<ISOStandard>();
            result.rows = list;
            result.total = totalCount;
            return result;
        }
    }
}
