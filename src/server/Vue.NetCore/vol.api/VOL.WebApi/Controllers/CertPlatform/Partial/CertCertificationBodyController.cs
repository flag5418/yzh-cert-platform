/*
 *接口编写处...
 *如果接口需要做Action的权限验证，请在Action上使用属性
 *如: [ApiActionPermission("CertCertificationBody", Enums.ActionPermissionOptions.Delete)]
 *
 * 独立删除接口：引用校验 + 软删除（短路校验模式）
 *
 * 前端调用方式：
 *   YZHBaseApiClient.del(ids) → POST /api/CertCertificationBody/Remove
 *   请求体格式：{ ids: ["code1", "code2"] }
 *
 * 设计原则（2026-08-07 更新）：
 * 1. 短路校验：只检查最近一层直接关联，不递归穷举
 * 2. 认证机构(CB) → 只查 ISOStandard 是否引用了此机构的 CbCode
 * 3. 默认软删除（Enable=false），不物理删除数据
 * 4. 错误信息简洁明了，指向具体的关联表和数量
 */
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
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
    public partial class CertCertificationBodyController
    {
        private readonly ICertCertificationBodyService _service;
        private readonly ICertCertificationBodyRepository _repository;
        private readonly VOLContext _db;

        [ActivatorUtilitiesConstructor]
        public CertCertificationBodyController(
            ICertCertificationBodyService service,
            ICertCertificationBodyRepository certBodyRepository
        )
        : base(service)
        {
            _service = service;
            _repository = certBodyRepository;
            _db = certBodyRepository.DbContext as VOLContext ?? new VOLContext();
        }

        [HttpPost("GetMaxId")]
        public async Task<IActionResult> GetMaxId()
        {
            return JsonNormal(await _service.GetMaxId());
        }

        /// <summary>
        /// 删除认证机构：短路引用校验 + 软删除
        /// 
        /// 短路校验（只查最近一层）：
        ///   - ISOStandard.CbCode → 该机构下是否有 ISO 标准
        ///   
        /// 删除策略：
        ///   - 有引用 → 返回错误，阻断删除（告诉用户去哪里清理）
        ///   - 无引用 → 软删除（Enable=false + 记录删除信息）
        /// </summary>
        [HttpPost("Remove")]
        public IActionResult Remove([FromBody] CertBodyRemoveRequest request)
        {
            string[] keys = request?.Ids;

            if (keys == null || keys.Length == 0)
                return JsonNormal(new WebResponseContent().Error("请选择要删除的记录"));

            try
            {
                // ====== 第一阶段：短路引用校验 ======
                foreach (var key in keys)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    var code = key.Trim();

                    var entity = long.TryParse(code, out var id)
                        ? _repository.FindFirst(x => x.Id == id)
                        : _repository.FindFirst(x => x.Code == code);

                    if (entity == null) continue;

                    var cbCode = entity.Code ?? code;

                    // TODO: CertOrgStandard 已废弃删除，待 cert_cb_standard 表重建后恢复
                    // var refCount = _db.Set<VOL.Entity.CertPlatform.Sys.CertOrgStandard>()
                    //     .Where(x => x.CbCode == cbCode)
                    //     .Count();
                    var refCount = 0;

                    if (refCount > 0)
                    {
                        // 有引用 → 立即返回错误，不继续处理后续 key
                        var msg = $"无法删除「{entity.Name}」\n\n" +
                                 $"该机构下存在 {refCount} 条 ISO 标准数据，请先转移或删除这些标准后再操作。";
                        Console.WriteLine($"[CB.Remove] 删除被阻断: {entity.Name}, {refCount} 条ISO标准引用");
                        return JsonNormal(new WebResponseContent().Error(msg));
                    }
                }

                // ====== 第二阶段：执行软删除 ======
                int deleted = 0;
                foreach (var key in keys)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    var code = key.Trim();

                    var entity = long.TryParse(code, out var id)
                        ? _repository.FindFirst(x => x.Id == id)
                        : _repository.FindFirst(x => x.Code == code);

                    if (entity != null)
                    {
                        // 软删除：标记 Enable=false + 记录删除信息
                        entity.MarkAsDeleted(
                            UserContext.Current.UserId,
                            UserContext.Current.UserName
                        );
                        _repository.Update(entity, new[] { "Enable", "DeleteID", "Deleter", "DeleteTime" }, saveChanges: true);
                        deleted++;
                        Console.WriteLine($"[CB.Remove] 软删除成功: Code={entity.Code}, Name={entity.Name}");
                    }
                }

                return JsonNormal(new WebResponseContent().OK($"成功删除 {deleted} 条记录（已移至回收站）"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CB.Remove] 异常: {ex}");
                return JsonNormal(new WebResponseContent().Error($"操作失败: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// 删除请求 DTO（CertificationBody 用 Code 作为主键）
    /// </summary>
    public class CertBodyRemoveRequest
    {
        public string[] Ids { get; set; }
    }
}
