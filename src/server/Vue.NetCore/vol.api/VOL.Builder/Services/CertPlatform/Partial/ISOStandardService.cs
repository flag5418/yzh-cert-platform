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
        /// 重写 GetPageData：查询 v_iso_standard 视图（含字典翻译）
        /// 
        /// T+V 架构：
        /// - T = ISOStandard（实体表，用于增删改）
        /// - V = v_iso_standard（视图，用于显示，含 CategoryName/StatusName）
        /// </summary>
        public override PageGridData<ISOStandard> GetPageData(PageDataOptions options)
        {
            Console.WriteLine($"[ISOStandardGetPageData] ====== 查询视图 v_iso_standard ======");
            
            var db = _repository.DbContext as VOLContext ?? new VOLContext();

            // 查询视图（已包含 CategoryName, StatusName 中文字段）
            IQueryable<ISOStandardView> query = db.Set<ISOStandardView>();

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

            Console.WriteLine($"[ISOStandardGetPageData] ✅ totalCount={totalCount}, 返回{list.Count}条");

            var result = new PageGridData<ISOStandard>();
            result.rows = list.Cast<ISOStandard>().ToList();
            result.total = totalCount;
            return result;
        }
    }
}