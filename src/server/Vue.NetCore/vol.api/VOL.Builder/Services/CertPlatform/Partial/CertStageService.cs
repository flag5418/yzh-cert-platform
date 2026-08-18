/*
 * 所有关于 CertStage 类的业务代码应在此处编写
 * 可使用 repository.调用常用方法，获取 EF/Dapper 等信息
 * 如果需要事务请使用 repository.DbContextBeginTransaction
 * 也可使用 DBServerProvider.手动获取数据库相关信息
 * 用户信息、权限、角色等使用 UserContext.Current 操作
 *
 * 认证阶段管理（全局基础资料）
 * 基于 ISO/IEC 17021-1:2015 规定的认证流程阶段
 * 
 * V3 适配说明：
 * - 数据库表 cert_cert_stage 使用 phase_code/phase_name 列名
 * - GetPageData 使用原生 SQL 查询 v_cert_stage 视图（含字典翻译后的 CategoryName/StatusName）
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.DomainModels;
using VOL.Core.DBManager;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Services.CertPlatform
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
        /// 重写 Del 方法：用 Id 定位记录并物理删除
        /// </summary>
        public override WebResponseContent Del(object[] keys, bool delList = true)
        {
            if (keys == null || keys.Length == 0)
            {
                return new WebResponseContent().Error("请选择要删除的记录");
            }

            try
            {
                int deletedCount = 0;
                foreach (var key in keys)
                {
                    if (key == null) continue;

                    string keyStr = key.ToString().Trim();
                    if (!long.TryParse(keyStr, out long idValue))
                    {
                        continue;
                    }

                    var entity = _repository.FindFirst(x => x.Id == idValue);
                    if (entity != null)
                    {
                        repository.Delete(entity, true);
                        deletedCount++;
                    }
                }

                return new WebResponseContent().OK($"成功删除 {deletedCount} 条记录");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"删除失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重写 GetPageData：原生 SQL 查询 v_cert_stage 视图，直接返回含中文字段的 CertStageView
        /// 
        /// 视图 v_cert_stage 已通过 LEFT JOIN Sys_DictionaryList 完成字典翻译，
        /// 返回 CategoryName/StatusName 中文字段，前端无需做任何字典拼接。
        /// </summary>
        public override PageGridData<CertStage> GetPageData(PageDataOptions options)
        {
            // 排序字段映射（前端传 PascalCase → 视图列名已统一为 PascalCase）
            string sortField = options.Sort ?? "SortOrder";
            bool isAsc = options.Order?.ToLower() == "asc";
            string orderClause = sortField.ToUpper() switch
            {
                "SORTORDER" => isAsc ? "SortOrder ASC" : "SortOrder DESC",
                "STAGECODE" => isAsc ? "StageCode ASC" : "StageCode DESC",
                "CREATEDATE" => isAsc ? "CreateDate ASC" : "CreateDate DESC",
                _ => "Id DESC",
            };

            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            int offset = (page - 1) * rows;

            // 原生 SQL：查视图总数 + 分页数据
            string countSql = "SELECT COUNT(*) FROM v_cert_stage";
            string dataSql = $@"
                SELECT Id, Code, StageCode, StageName, Description, SortOrder,
                       Category, CategoryName, Status, StatusName,
                       Remark, Enable, CreateID, Creator, CreateDate,
                       ModifyID, Modifier, ModifyDate, DeleteID, Deleter, DeleteTime
                FROM v_cert_stage
                ORDER BY {orderClause}
                LIMIT {offset}, {rows}";

            var dapper = DBServerProvider.SqlDapper;

            int totalCount = dapper.ExecuteScalar(countSql, null) as int? ?? 0;

            // Dapper 查出的 CertStageView（继承 CertStage）可直接放入 PageGridData<CertStage>
            var viewList = dapper.QueryList<CertStageView>(dataSql, null)
                .Cast<CertStage>().ToList();

            var result = new PageGridData<CertStage>();
            result.rows = viewList;
            result.total = totalCount;
            return result;
        }

        /// <summary>
        /// 获取所有启用的认证阶段（按 SortOrder 排序）
        /// </summary>
        public async Task<List<CertStage>> GetActiveStagesAsync()
        {
            return await _repository.FindAsIQueryable(x => x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
        }
    }
}
