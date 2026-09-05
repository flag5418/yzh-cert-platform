using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YZH.Core.BaseProvider;
using YZH.Core.Extensions;
using Cert.Platform.IRepositories.Admin.Platform;
using YZH.Entity.Admin.Platform.Cert;
using YZH.Entity.DomainModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Services.Admin.Platform
{
    public partial class CertificationBodyService
    {
        private readonly ICertCertificationBodyRepository _repository;

        [ActivatorUtilitiesConstructor]
        public CertificationBodyService(ICertCertificationBodyRepository dbRepository)
            : base(dbRepository)
        {
            _repository = dbRepository;
            this.repository = dbRepository;
        }

        /// <summary>
        /// 重写 GetPageData：自动路由到 v_certification_body 视图
        /// </summary>
        public override PageGridData<CertificationBody> GetPageData(PageDataOptions options)
        {
            var db = _repository.DbContext;

            var query = db.Set<CertificationBody>().UseViewIfExists();

            int totalCount = query.Count();

            // 排序
            string sortField = options.Sort ?? "Id";
            bool isAsc = options.Order?.ToLower() == "asc";
            query = sortField.ToUpper() switch
            {
                "NAME" => isAsc ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
                "CBCODE" => isAsc ? query.OrderBy(x => x.CbCode) : query.OrderByDescending(x => x.CbCode),
                "SHORTNAME" => isAsc ? query.OrderBy(x => x.ShortName) : query.OrderByDescending(x => x.ShortName),
                _ => query.OrderByDescending(x => x.Id),
            };

            // 分页
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = query.Skip((page - 1) * rows).Take(rows).ToList();

            var result = new PageGridData<CertificationBody>();
            result.rows = list;
            result.total = totalCount;
            return result;
        }

        /// <summary>
        /// 获取最大 ID
        /// </summary>
        public async Task<int> GetMaxId()
        {
            return await _repository.DbContext.Set<CertificationBody>().MaxAsync(x => (int?)x.Id) ?? 0;
        }
    }
}
