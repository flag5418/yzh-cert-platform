using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.Utilities;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 审核员管理服务接口
    /// 职责：审核员注册（手机号+验证码）、审核员列表、详情、资质更新
    /// </summary>
    public interface IAuditorService : IDependency
    {
        /// <summary>
        /// 发送手机验证码
        /// </summary>
        Task<WebResponseContent> SendSmsCodeAsync(string phone);

        /// <summary>
        /// 审核员注册（手机号+验证码 → 写入 Sys_User + cert_auditor_profile）
        /// </summary>
        Task<WebResponseContent> RegisterAsync(string phone, string smsCode, string auditorName, string orgCode);

        /// <summary>
        /// 获取审核员列表（分页，按机构过滤）
        /// </summary>
        Task<(List<object> items, int total)> GetListAsync(string orgCode, int page, int rows);

        /// <summary>
        /// 获取审核员详情
        /// </summary>
        Task<AuditorProfile> GetDetailAsync(long userId);

        /// <summary>
        /// 更新审核员资质信息
        /// </summary>
        Task<WebResponseContent> UpdateProfileAsync(AuditorProfile profile);
    }
}
