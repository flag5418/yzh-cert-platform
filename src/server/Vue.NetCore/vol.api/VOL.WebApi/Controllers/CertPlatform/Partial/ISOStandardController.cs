/*
 *接口编写处...
 *如果接口需要做Action的权限验证，请在Action上使用属性
 *如: [ApiActionPermission("ISOStandard", Enums.ActionPermissionOptions.Delete)]
 *
 * 独立删除接口：短路引用校验 + 软删除
 *
 * 前端调用方式：
 *   YZHBaseApiClient.del(ids) → POST /api/ISOStandard/Remove
 *
 * 设计原则（2026-08-07）：
 * 1. 短路校验：只检查最近一层直接关联
 * 2. ISOStandard → 只查 ISOClause 是否引用了此标准的 StandardCode
 * 3. 默认软删除（Enable=false），不物理删除数据
 */
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IServices.CertPlatform;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Core.Utilities;
using VOL.Core.EFDbContext;
using VOL.Core.ManageUser;

namespace VOL.WebApi.Controllers.CertPlatform
{
    public partial class ISOStandardController
    {
        private readonly IISOStandardService _service;
        private readonly IISOStandardRepository _repository;
        private readonly VOLContext _db;

        [ActivatorUtilitiesConstructor]
        public ISOStandardController(
            IISOStandardService service,
            IISOStandardRepository isoStandardRepository
        )
        : base(service)
        {
            _service = service;
            _repository = isoStandardRepository;
            _db = isoStandardRepository.DbContext as VOLContext ?? new VOLContext();
        }

        /// <summary>
        /// 删除 ISO 标准：短路引用校验 + 软删除
        /// 
        /// 短路校验（只查最近一层）：
        ///   - ISOClause.StandardCode → 此标准下是否有条款
        ///   
        /// 删除策略：
        ///   - 有引用 → 返回错误，阻断删除
        ///   - 无引用 → 软删除（Enable=false）
        /// </summary>
        [HttpPost("Remove")]
        public IActionResult Remove([FromBody] IsoStandardRemoveRequest request)
        {
            var keys = request?.Ids;

            if (keys == null || keys.Length == 0)
                return JsonNormal(new WebResponseContent().Error("请选择要删除的记录"));

            try
            {
                // ====== 第一阶段：短路引用校验 ======
                foreach (var key in keys)
                {
                    if (key == null) continue;

                    // ISOStandard 用 Id 作为主键
                    var id = Convert.ToInt64(key);
                    var entity = _repository.FindFirst(x => x.Id == id);

                    if (entity == null) continue;

                    var standardCode = entity.Code;

                    // 【短路】只查 ISOClause 这一张直接关联表
                    var refCount = _db.Set<ISOClause>()
                        .Where(x => x.StandardCode == standardCode && x.Enable == true)
                        .Count();

                    if (refCount > 0)
                    {
                        var msg = $"无法删除「{entity.StandardName}({entity.StandardCode})」\n\n" +
                                 $"该标准下存在 {refCount} 条标准条款数据，请先转移或删除这些条款后再操作。";
                        Console.WriteLine($"[ISO.Remove] 删除被阻断: {entity.StandardName}, {refCount} 条条款引用");
                        return JsonNormal(new WebResponseContent().Error(msg));
                    }
                }

                // ====== 第二阶段：执行软删除 ======
                int deleted = 0;
                foreach (var key in keys)
                {
                    if (key == null) continue;

                    var id = Convert.ToInt64(key);
                    var entity = _repository.FindFirst(x => x.Id == id);

                    if (entity != null)
                    {
                        entity.MarkAsDeleted(
                            UserContext.Current.UserId,
                            UserContext.Current.UserName
                        );
                        _repository.Update(entity, new[] { "Enable", "DeleteID", "Deleter", "DeleteTime" }, saveChanges: true);
                        deleted++;
                        Console.WriteLine($"[ISO.Remove] 软删除成功: Id={id}, StandardCode={entity.StandardCode}");
                    }
                }

                return JsonNormal(new WebResponseContent().OK($"成功删除 {deleted} 条记录（已移至回收站）"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ISO.Remove] 异常: {ex}");
                return JsonNormal(new WebResponseContent().Error($"操作失败: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// 删除请求 DTO（ISOStandard 用 Id 作为主键）
    /// </summary>
    public class IsoStandardRemoveRequest
    {
        public object[] Ids { get; set; }
    }
}
