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
    public partial class CertStageService
    {
        private readonly ICertStageRepository _repository;

        [ActivatorUtilitiesConstructor]
        public CertStageService(ICertStageRepository dbRepository)
            : base(dbRepository)
        {
            _repository = dbRepository;
            this.repository = dbRepository;
        }

        /// <summary>
        /// 重写 GetPageData：自动路由到 v_cert_stage 视图
        /// </summary>
        public override PageGridData<CertStage> GetPageData(PageDataOptions options)
        {
            var db = _repository.DbContext;

            var query = db.Set<CertStage>().UseViewIfExists();

            int totalCount = query.Count();

            string sortField = options.Sort ?? "SortOrder";
            bool isAsc = options.Order?.ToLower() == "asc";
            query = sortField.ToUpper() switch
            {
                "SORTORDER" => isAsc ? query.OrderBy(x => x.SortOrder) : query.OrderByDescending(x => x.SortOrder),
                "STAGECODE" => isAsc ? query.OrderBy(x => x.StageCode) : query.OrderByDescending(x => x.StageCode),
                "STAGENAME" => isAsc ? query.OrderBy(x => x.StageName) : query.OrderByDescending(x => x.StageName),
                _ => query.OrderByDescending(x => x.Id),
            };

            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = query.Skip((page - 1) * rows).Take(rows).ToList();

            var result = new PageGridData<CertStage>();
            result.rows = list;
            result.total = totalCount;
            return result;
        }

        /// <summary>
        /// 获取所有启用的认证阶段
        /// </summary>
        public async Task<List<CertStage>> GetActiveStagesAsync()
        {
            return await _repository.FindAsIQueryable(x => x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
        }
    }
}
