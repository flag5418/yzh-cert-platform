using YZH.Core.BaseProvider;
using YZH.Core.Extensions;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;
using YZH.Entity.DomainModels;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Cert;
using YZH.Core.EFDbContext;
using System;
using System.Linq;

namespace Cert.Platform.Services.Admin.Platform
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
        /// 重写 GetPageData：自动路由到 v_iso_standard 视图（如果配置了 ViewName）
        /// 视图提供 CategoryName 等 JOIN 翻译字段。
        /// SaveChanges 仍然操作 cert_iso_standard 表。
        /// </summary>
        public override PageGridData<ISOStandard> GetPageData(PageDataOptions options)
        {
            var db = _repository.DbContext as VOLContext ?? new VOLContext();

            // YZH ViewName 路由：实体配置 [ViewName("v_iso_standard")] 自动生效
            // 等效 SQL: SELECT * FROM (SELECT * FROM v_iso_standard) AS x WHERE ... ORDER BY ... LIMIT n
            var query = db.Set<ISOStandard>().UseViewIfExists();

            int totalCount = query.Count();

            // 排序
            string sortField = options.Sort ?? "CreateDate";
            bool isAsc = options.Order?.ToLower() == "asc";
            query = sortField.ToUpper() switch
            {
                "STANDARDCODE" => isAsc ? query.OrderBy(x => x.StandardCode) : query.OrderByDescending(x => x.StandardCode),
                "STANDARDNAME" => isAsc ? query.OrderBy(x => x.StandardName) : query.OrderByDescending(x => x.StandardName),
                "CATEGORYNAME" => isAsc ? query.OrderBy(x => x.CategoryName) : query.OrderByDescending(x => x.CategoryName),
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
