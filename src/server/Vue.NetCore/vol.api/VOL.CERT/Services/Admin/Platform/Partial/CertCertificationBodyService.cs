/*
 *所有关于CertificationBody类的业务代码应在此处编写
 *可使用repository.调用常用方法，获取EF/Dapper等信息
 *如果需要事务请使用repository.DbContextBeginTransaction
 *也可使用DBServerProvider.手动获取数据库相关信息
 *用户信息、权限、角色等使用UserContext.Current操作
 *CertCertificationBodyService对增、删、改查、导入、导出、审核业务代码扩展参照ServiceFunFilter
 *
 * 核心设计：Code 作为业务主键
 * - 前端新增时生成 Code（CB + 年月日时分 + 3位随机数）
 * - 编辑时通过 Code 定位真实实体，修正 Id 后交由框架处理
 * - 彻底解决 EF Core "expected 1 row but affected 0" 并发异常
 *
 * 唯一性校验：通过 [UniqueField] 特性 + 通用扩展方法，无需硬编码
 */

using System;
using System.Collections.Generic;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Cert;
using VOL.CERT.IRepositories.Admin.Platform;
using VOL.CERT.Extensions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels;
using YZH.Core.Exceptions;

namespace VOL.CERT.Services.Admin.Platform
{
    public partial class CertCertificationBodyService
    {
        private readonly ICertCertificationBodyRepository _repository;

        [ActivatorUtilitiesConstructor]
        public CertCertificationBodyService(ICertCertificationBodyRepository dbRepository)
            : base(dbRepository)
        {
            _repository = dbRepository;
            this.repository = dbRepository;
        }

        /// <summary>
        /// 重写 Update 方法：用 Code（业务主键）定位真实实体，修正 Id
        /// 
        /// 唯一性校验和异常脱敏由 YZHServiceBase.Update 自动处理，
        /// 此处仅修正 Id 后委托给 base.Update(saveDataModel)。
        /// </summary>
        public override WebResponseContent Update(SaveModel saveDataModel)
        {
            if (saveDataModel?.MainData == null)
                return new WebResponseContent().Error("提交数据为空");

            var mainData = saveDataModel.MainData;

            // ====== 关键：用 Code 修正 Id ======
            if (mainData.ContainsKey("Code"))
            {
                string code = mainData["Code"]?.ToString();
                if (!string.IsNullOrEmpty(code))
                {
                    var dbEntity = _repository.FindFirst(x => x.Code == code);
                    if (dbEntity != null)
                    {
                        mainData["Id"] = dbEntity.Id;
                    }
                }
            }

            // ====== 委托给 YZHServiceBase.Update（含自动校验 + 异常脱敏） ======
            return base.Update(saveDataModel);
        }

        /// <summary>
        /// 重写 Add 方法：确保 Code 有值
        /// 
        /// 唯一性校验和异常脱敏由 YZHServiceBase.Add 自动处理，
        /// 此处仅补充 Code 后委托给 base.Add(saveDataModel)。
        /// </summary>
        public override WebResponseContent Add(SaveModel saveDataModel)
        {
            if (saveDataModel?.MainData == null)
                return new WebResponseContent().Error("提交数据为空");

            var mainData = saveDataModel.MainData;

            // ====== 如果前端没传 Code，自动生成一个 ======
            if (!mainData.ContainsKey("Code") ||
                string.IsNullOrEmpty(mainData["Code"]?.ToString()))
            {
                mainData["Code"] =
                    $"CB{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(100, 999)}";
            }

            // ====== 委托给 YZHServiceBase.Add（含自动校验 + 异常脱敏） ======
            return base.Add(saveDataModel);
        }

        /// <summary>
        /// 重写 Del 方法：直接用 Code 删除，最简单可靠
        ///
        /// 不再依赖框架的 base.Del()（需要 long 类型的 Id 数组 + ValidationValueForDbType 校验）
        /// 改为直接用 Code 定位记录并删除，一步到位
        /// </summary>
        public override WebResponseContent Del(object[] keys, bool delList = true)
        {
            // 超详细调试日志
            Console.WriteLine($"[Del] ====== 删除请求开始 ======");
            Console.WriteLine($"[Del] keys is null: {keys == null}");
            if (keys != null)
            {
                Console.WriteLine($"[Del] keys.Length: {keys.Length}");
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = keys[i];
                    Console.WriteLine($"[Del]   keys[{i}]: type={k?.GetType().Name ?? "null"}, value={k?.ToString() ?? "null"}, json={System.Text.Json.JsonSerializer.Serialize(k)}");
                }
            }

            // 前置守卫
            if (keys == null || keys.Length == 0)
            {
                Console.WriteLine($"[Del] ====== 拒绝：keys 为空 ======");
                return new WebResponseContent().Error("请选择要删除的记录");
            }

            try
            {
                foreach (var key in keys)
                {
                    if (key == null || string.IsNullOrWhiteSpace(key.ToString()))
                        continue;

                    string code = key.ToString().Trim();

                    // 用 Code 查出实体（支持 Code 或 Id 两种格式）
                    CertificationBody entity = null;
                    if (long.TryParse(code, out long idValue))
                    {
                        entity = _repository.FindFirst(x => x.Id == idValue);
                    }
                    else
                    {
                        entity = _repository.FindFirst(x => x.Code == code);
                    }

                    // 找到就删除，没找到跳过
                    if (entity != null)
                    {
                        repository.Delete(entity, true);  // true = 立即 SaveChanges
                    }
                }

                return new WebResponseContent().OK("删除成功");
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "删除");
            }
        }

        /// <summary>
        /// 兜底处理：确保 repository 可用
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
        /// 获取当前记录数（用于前端生成编号）
        /// </summary>
        public async Task<int> GetMaxId()
        {
            return await _repository.FindAsIQueryable(x => true).CountAsync();
        }

        /// <summary>
        /// 根据 Code 获取实体（支持前端通过业务编码定位）
        /// </summary>
        public async Task<CertificationBody> GetByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            return await _repository.FindFirstAsync(x => x.Code == code);
        }

        /// <summary>
        /// 获取所有启用的认证机构（下拉选择用）
        /// TODO: Phase 2 实现具体业务逻辑时启用此方法
        /// </summary>
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
