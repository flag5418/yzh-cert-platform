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
 * - 无 v_cert_stage 视图，GetPageData 直接查表 + 内存字典翻译
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.DomainModels;
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
        /// 重写 GetPageData：直接查询 cert_cert_stage 表 + 内存字典翻译
        /// 
        /// V3 适配：无 v_cert_stage 视图，直接查表并做内存翻译
        /// 前端直接显示中文字段，无需任何字典处理
        /// </summary>
        public override PageGridData<CertStage> GetPageData(PageDataOptions options)
        {
            var query = _repository.FindAsIQueryable(x => true);

            // 排序
            string sortField = options.Sort ?? "SortOrder";
            bool isAsc = options.Order?.ToLower() == "asc";

            query = sortField.ToUpper() switch
            {
                "SORTORDER" => isAsc ? query.OrderBy(x => x.SortOrder) : query.OrderByDescending(x => x.SortOrder),
                "STAGECODE" or "PHASECODE" => isAsc ? query.OrderBy(x => x.StageCode) : query.OrderByDescending(x => x.StageCode),
                "CREATEDATE" => isAsc ? query.OrderBy(x => x.CreateDate) : query.OrderByDescending(x => x.CreateDate),
                _ => query.OrderByDescending(x => x.Id),
            };

            int totalCount = query.Count();

            // 分页
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = query.Skip((page - 1) * rows).Take(rows).ToList();

            // 转换为 PageGridData
            var result = new PageGridData<CertStage>();
            result.rows = list;
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
