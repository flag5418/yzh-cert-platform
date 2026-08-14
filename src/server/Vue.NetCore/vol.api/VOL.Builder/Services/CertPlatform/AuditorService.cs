using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.CacheManager;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 审核员管理服务实现
    /// </summary>
    public class AuditorService : IAuditorService
    {
        private readonly VOLContext _db;
        private readonly ICacheService _cache;

        public AuditorService(VOLContext dbContext, ICacheService cache)
        {
            _db = dbContext;
            _cache = cache;
        }

        /// <summary>
        /// 发送手机验证码
        /// </summary>
        public async Task<WebResponseContent> SendSmsCodeAsync(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length != 11)
                return new WebResponseContent().Error("手机号格式不正确");

            // 检查手机号是否已注册
            var exists = await _db.Set<AuditorProfile>()
                .AnyAsync(x => x.Phone == phone && x.Enable == true);
            if (exists)
                return new WebResponseContent().Error("该手机号已注册");

            // 生成 6 位验证码
            var code = new Random().Next(100000, 999999).ToString();
            var cacheKey = $"auditor_sms_{phone}";
            _cache.Add(cacheKey, code, 5); // 5 分钟过期

            // TODO: 接入短信服务发送验证码
            Console.WriteLine($"[AuditorService] 验证码: {phone} → {code}");

            return new WebResponseContent().OK("验证码已发送");
        }

        /// <summary>
        /// 审核员注册（手机号+验证码 → 写入 Sys_User + cert_auditor_profile）
        /// </summary>
        public async Task<WebResponseContent> RegisterAsync(string phone, string smsCode, string auditorName, string orgCode)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length != 11)
                return new WebResponseContent().Error("手机号格式不正确");
            if (string.IsNullOrEmpty(smsCode))
                return new WebResponseContent().Error("验证码不能为空");
            if (string.IsNullOrEmpty(auditorName))
                return new WebResponseContent().Error("审核员姓名不能为空");
            if (string.IsNullOrEmpty(orgCode))
                return new WebResponseContent().Error("机构编码不能为空");

            // 验证验证码
            var cacheKey = $"auditor_sms_{phone}";
            var cachedCode = _cache.Get<string>(cacheKey);
            if (string.IsNullOrEmpty(cachedCode) || cachedCode != smsCode)
                return new WebResponseContent().Error("验证码错误或已过期");

            // 检查手机号是否已注册
            var exists = await _db.Set<AuditorProfile>()
                .AnyAsync(x => x.Phone == phone && x.Enable == true);
            if (exists)
                return new WebResponseContent().Error("该手机号已注册");

            // TODO: 写入 Sys_User（需要密码加密等逻辑）
            // 目前先直接创建 AuditorProfile，userId 后续补充
            var profile = new AuditorProfile
            {
                Code = Guid.NewGuid().ToString("N"),
                UserId = 0, // 待 Sys_User 创建后回填
                AuditorNo = $"AUD-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                AuditorName = auditorName,
                Phone = phone,
                Enable = true,
                Status = "active",
                OrgCode = orgCode
            };

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            profile.FillCreateInfo(userId, userName, orgCode);

            _db.Set<AuditorProfile>().Add(profile);
            await _db.SaveChangesAsync();

            // 清除验证码
            _cache.Remove(cacheKey);

            return new WebResponseContent().OK("注册成功", profile.Code);
        }

        /// <summary>
        /// 获取审核员列表（分页，按机构过滤）
        /// </summary>
        public async Task<(List<object> items, int total)> GetListAsync(string orgCode, int page, int rows)
        {
            var query = _db.Set<AuditorProfile>()
                .Where(x => x.Enable == true);

            if (!string.IsNullOrEmpty(orgCode))
                query = query.Where(x => x.OrgCode == orgCode);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.Id)
                .Skip((page - 1) * rows)
                .Take(rows)
                .Select(x => new
                {
                    x.Code,
                    x.UserId,
                    x.AuditorNo,
                    x.AuditorName,
                    x.Phone,
                    x.Email,
                    x.Qualification,
                    x.ExpertiseAreas,
                    x.Status,
                    x.OrgCode,
                    x.CreateDate
                })
                .ToListAsync();

            return (items.Cast<object>().ToList(), total);
        }

        /// <summary>
        /// 获取审核员详情
        /// </summary>
        public async Task<AuditorProfile> GetDetailAsync(long userId)
        {
            return await _db.Set<AuditorProfile>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Enable == true);
        }

        /// <summary>
        /// 更新审核员资质信息
        /// </summary>
        public async Task<WebResponseContent> UpdateProfileAsync(AuditorProfile profile)
        {
            if (string.IsNullOrEmpty(profile.Code))
                return new WebResponseContent().Error("审核员编码不能为空");

            var existing = await _db.Set<AuditorProfile>()
                .FirstOrDefaultAsync(x => x.Code == profile.Code);
            if (existing == null)
                return new WebResponseContent().Error("审核员不存在");

            existing.AuditorName = profile.AuditorName;
            existing.Email = profile.Email;
            existing.Qualification = profile.Qualification;
            existing.ExpertiseAreas = profile.ExpertiseAreas;

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            existing.FillModifyInfo(userId, userName);

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("更新成功");
        }
    }
}
