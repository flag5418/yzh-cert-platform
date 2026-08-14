/*
 * 所有关于 CertStage 类的业务代码应在此处编写
 * 可使用 repository.调用常用方法，获取 EF/Dapper 等信息
 * 如果需要事务请使用 repository.DbContextBeginTransaction
 * 也可使用 DBServerProvider.手动获取数据库相关信息
 * 用户信息、权限、角色等使用 UserContext.Current 操作
 *
 * 认证阶段管理（全局基础资料）
 * 基于 ISO/IEC 17021-1:2015 规定的认证流程阶段
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
        /// 
        /// 问题根因：
        ///   Vol 基类 Del() 的 ValidationValueForDbType 对 long 主键处理有问题
        ///   且只设置 DeleteTime 不设置 Enable=false，导致刷新后数据还在
        ///
        /// 解决方案：
        ///   直接用 Id 查出实体 → repository.Delete() 物理删除
        /// </summary>
        public override WebResponseContent Del(object[] keys, bool delList = true)
        {
            Console.WriteLine($"[CertStageDel] ====== 删除请求开始 ======");
            Console.WriteLine($"[CertStageDel] keys is null: {keys == null}");
            if (keys != null)
            {
                Console.WriteLine($"[CertStageDel] keys.Length: {keys.Length}");
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = keys[i];
                    Console.WriteLine($"[CertStageDel]   keys[{i}]: type={k?.GetType().Name ?? "null"}, value={k?.ToString() ?? "null"}");
                }
            }

            if (keys == null || keys.Length == 0)
            {
                Console.WriteLine($"[CertStageDel] ====== 拒绝：keys 为空 ======");
                return new WebResponseContent().Error("请选择要删除的记录");
            }

            try
            {
                int deletedCount = 0;
                foreach (var key in keys)
                {
                    if (key == null) continue;

                    // 尝试解析为 long Id
                    string keyStr = key.ToString().Trim();
                    if (!long.TryParse(keyStr, out long idValue))
                    {
                        Console.WriteLine($"[CertStageDel] 跳过无效Id: {keyStr}");
                        continue;
                    }

                    // 用 Id 查找实体
                    var entity = _repository.FindFirst(x => x.Id == idValue);
                    if (entity != null)
                    {
                        Console.WriteLine($"[CertStageDel] 找到实体 Id={idValue}, StageCode={entity.StageCode}, 正在删除...");
                        repository.Delete(entity, true); // true = 立即 SaveChanges
                        deletedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"[CertStageDel] 未找到 Id={idValue} 的实体");
                    }
                }

                Console.WriteLine($"[CertStageDel] ====== 删除完成: {deletedCount} 条 ======");
                return new WebResponseContent().OK($"成功删除 {deletedCount} 条记录");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CertStageDel] 异常: {ex}");
                return new WebResponseContent().Error($"删除失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重写 GetPageData：查询 v_cert_stage 视图（含字典翻译）
        /// 
        /// T+V 架构：
        /// - T = CertStage（实体表，用于 Add/Update/Del）
        /// - V = v_cert_stage（视图，用于 GetPageData，含 CategoryName/StatusName）
        /// 
        /// 前端直接显示中文字段，无需任何字典处理
        /// </summary>
        public override PageGridData<CertStage> GetPageData(PageDataOptions options)
        {
            Console.WriteLine($"[CertStageGetPageData] ====== 查询视图 v_cert_stage ======");
            
            var db = _repository.DbContext as VOL.Core.EFDbContext.VOLContext
                ?? new VOL.Core.EFDbContext.VOLContext();

            // 使用原生 SQL 查询视图（避免 EF Core 继承映射问题）
            var sql = "SELECT * FROM v_cert_stage";
            var viewData = db.Set<CertStage>().FromSqlRaw(sql).ToList();
            
            // 内存中排序和分页
            string sortField = options.Sort ?? "SortOrder";
            bool isAsc = options.Order?.ToLower() == "asc";
            
            viewData = sortField.ToUpper() switch
            {
                "SORTORDER" => isAsc ? viewData.OrderBy(x => x.SortOrder).ToList() : viewData.OrderByDescending(x => x.SortOrder).ToList(),
                "STAGECODE" => isAsc ? viewData.OrderBy(x => x.StageCode).ToList() : viewData.OrderByDescending(x => x.StageCode).ToList(),
                "CREATEDATE" => isAsc ? viewData.OrderBy(x => x.CreateDate).ToList() : viewData.OrderByDescending(x => x.CreateDate).ToList(),
                _ => viewData.OrderByDescending(x => x.Id).ToList(),
            };
            
            int totalCount = viewData.Count;
            
            // 分页
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = viewData.Skip((page - 1) * rows).Take(rows).ToList();

            Console.WriteLine($"[CertStageGetPageData] ✅ totalCount={totalCount}, 返回{list.Count}条");

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
            var db = _repository.DbContext as VOL.Core.EFDbContext.VOLContext
                ?? new VOL.Core.EFDbContext.VOLContext();
            return await db.Set<CertStage>()
                .Where(x => x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
        }

        // TODO: CertOrgStage 已废弃删除，待 cert_cb_stage 表重建后恢复
        // /// <summary>
        // /// 获取指定机构已关联的阶段 ID 列表
        // /// </summary>
        // public async Task<List<long>> GetOrgStageIdsAsync(string cbCode)
        // {
        //     if (string.IsNullOrEmpty(cbCode)) return new List<long>();
        //
        //     var db = _repository.DbContext as VOL.Core.EFDbContext.VOLContext
        //         ?? new VOL.Core.EFDbContext.VOLContext();
        //
        //     return await db.Set<VOL.Entity.CertPlatform.Sys.CertOrgStage>()
        //         .Where(x => x.CbCode == cbCode)
        //         .Select(x => x.StageId)
        //         .ToListAsync();
        // }
    }
}
