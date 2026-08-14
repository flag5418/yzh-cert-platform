using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Ent;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 企业管理服务实现
    /// </summary>
    public class EnterpriseService : IEnterpriseService
    {
        private readonly VOLContext _db;

        public EnterpriseService(VOLContext dbContext)
        {
            _db = dbContext;
        }

        /// <summary>
        /// 获取企业列表（分页）
        /// </summary>
        public async Task<(List<Enterprise> items, int total)> GetListAsync(string orgCode, int page, int rows)
        {
            var query = _db.Set<Enterprise>().Where(x => x.Enable == true);

            if (!string.IsNullOrEmpty(orgCode))
                query = query.Where(x => x.OrgCode == orgCode);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.Id)
                .Skip((page - 1) * rows)
                .Take(rows)
                .ToListAsync();

            return (items, total);
        }

        /// <summary>
        /// 获取企业详情
        /// </summary>
        public async Task<Enterprise> GetDetailAsync(string code)
        {
            return await _db.Set<Enterprise>()
                .FirstOrDefaultAsync(x => x.Code == code && x.Enable == true);
        }

        /// <summary>
        /// 创建企业（自动生成 EnterpriseNo，初始化企业阶段）
        /// </summary>
        public async Task<WebResponseContent> CreateAsync(Enterprise entity)
        {
            if (string.IsNullOrEmpty(entity.Name))
                return new WebResponseContent().Error("企业名称不能为空");

            // 生成企业编码
            entity.EnterpriseNo = await GenerateEnterpriseNoAsync();
            entity.Code = Guid.NewGuid().ToString("N");
            entity.Status = "active";
            entity.Enable = true;

            // 填充创建信息
            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            entity.FillCreateInfo(userId, userName, entity.OrgCode);

            _db.Set<Enterprise>().Add(entity);
            await _db.SaveChangesAsync();

            return new WebResponseContent().OK("创建成功", entity.Code);
        }

        /// <summary>
        /// 更新企业信息
        /// </summary>
        public async Task<WebResponseContent> UpdateAsync(Enterprise entity)
        {
            if (string.IsNullOrEmpty(entity.Code))
                return new WebResponseContent().Error("企业编码不能为空");

            var existing = await _db.Set<Enterprise>()
                .FirstOrDefaultAsync(x => x.Code == entity.Code && x.Enable == true);
            if (existing == null)
                return new WebResponseContent().Error("企业不存在");

            // 更新可编辑字段
            existing.Name = entity.Name;
            existing.ShortName = entity.ShortName;
            existing.CreditCode = entity.CreditCode;
            existing.LegalPerson = entity.LegalPerson;
            existing.Province = entity.Province;
            existing.City = entity.City;
            existing.Address = entity.Address;
            existing.IndustryType = entity.IndustryType;
            existing.EmployeeCount = entity.EmployeeCount;
            existing.CertScope = entity.CertScope;
            existing.ContactName = entity.ContactName;
            existing.ContactPhone = entity.ContactPhone;
            existing.ContactEmail = entity.ContactEmail;

            // 填充修改信息
            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            existing.FillModifyInfo(userId, userName);

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("更新成功");
        }

        /// <summary>
        /// 删除企业（软删除）
        /// </summary>
        public async Task<WebResponseContent> DeleteAsync(string code)
        {
            var entity = await _db.Set<Enterprise>()
                .FirstOrDefaultAsync(x => x.Code == code);
            if (entity == null)
                return new WebResponseContent().Error("企业不存在");

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            entity.MarkAsDeleted(userId, userName);

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("删除成功");
        }

        /// <summary>
        /// 生成企业短编码（ENT-2026-0001 格式）
        /// </summary>
        public async Task<string> GenerateEnterpriseNoAsync()
        {
            var year = DateTime.Now.Year;
            var prefix = $"ENT-{year}-";

            var lastNo = await _db.Set<Enterprise>()
                .Where(x => x.EnterpriseNo != null && x.EnterpriseNo.StartsWith(prefix))
                .OrderByDescending(x => x.EnterpriseNo)
                .Select(x => x.EnterpriseNo)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(lastNo))
                return $"{prefix}0001";

            // 解析序号并递增
            var parts = lastNo.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var seq))
                return $"{prefix}{(seq + 1):D4}";

            return $"{prefix}0001";
        }
    }
}
