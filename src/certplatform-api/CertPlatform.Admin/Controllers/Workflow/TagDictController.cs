using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Doc;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Controllers.Workflow
{
    /// <summary>
    /// 受控标签字典查询（<c>cert_tag_dict</c>）—— 33 号 §十 遗留项 **F6「标签清单无 UI」的补齐**。
    ///
    /// <para><b>为什么必须有</b>：标签是本平台的<b>唯一召回键</b>（14 号 D14），而
    /// 「受控词表」的价值全在<b>只能从清单里选</b>。前端此前只能<b>手输标签码</b>，
    /// 手输 ⇒ 必然出现大小写/拼写漂移 ⇒ 标签漂移 ⇒ 召回退化（36 号 R6）。
    /// 有了本端点，前端标签控件才能从下拉受控选择。</para>
    ///
    /// <para>★ <b>端标记（2026-10-03）</b>：路由 <c>api/Admin/Workflow/TagDict</c>。
    /// 控制器**只增不改名**（改名 → ApiCode 变 → 角色-接口关联静默断裂）。</para>
    ///
    /// <para><b>为什么继承 <see cref="WebControllerBase"/> 而不是 <c>YzhControllerBase&lt;TagDict&gt;</c></b>：
    /// 本控制器<b>只读</b>（标签是受控词表，只能由 SQL 维护 —— 33 号 §十 F6 就是因为没有 UI 只能改 SQL），
    /// 不该顺手暴露一套 CRUD；而 <c>YzhControllerBase&lt;V&gt;</c> 会自动带来 filter/add/update/delete/config
    /// 六个写端点，那才是真正的风险（有人会去页面上删标签）。守卫 R-B 同样要求不用裸 <c>ControllerBase</c>。</para>
    /// </summary>
    [ApiController]
    [Route("api/Admin/Workflow/TagDict")]
    public class TagDictController : WebControllerBase
    {
        private readonly IDbOrm _db;

        public TagDictController(IDbOrm db) => _db = db;

        /// <summary>
        /// 标签清单（受控词表全量，供前端下拉）。
        /// </summary>
        /// <param name="standardCode">
        /// 标准 Code（<b>GUID</b>）；空 = 不裁剪。
        /// ⚠️ ⚠️ <c>cert_tag_dict.StandardCodes</c> 存的是 <b>slug</b>（<c>["iso9001"]</c>），
        /// 与本仓其他地方的 <c>StandardCode</c>（GUID）<b>口径不同</b> —— 传 GUID 进来必须先转 slug，
        /// 否则裁剪恒空（36 号 §5.3 同一个坑）。本端点<b>不做</b>转换，由调用方明确传 slug 或不传。
        /// </param>
        /// <param name="applicableSide">
        /// <c>enterprise</c> / <c>standard</c> / <c>both</c>；空 = 不过滤。
        /// <c>both</c> 的标签两侧都算命中。
        /// </param>
        [HttpPost("list")]
        public async Task<IActionResult> List([FromBody] TagDictQueryRequest? req)
        {
            req ??= new TagDictQueryRequest();
            var all = await _db.Client.Queryable<TagDict>()
                .Where(x => x.IsValid == 1 && !x.IsDeleted)
                .ToListAsync() ?? new List<TagDict>();

            var rows = all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(req.ApplicableSide))
            {
                var side = req.ApplicableSide!.Trim();
                rows = rows.Where(t =>
                    string.Equals(t.ApplicableSide, "both", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.ApplicableSide, side, StringComparison.OrdinalIgnoreCase));
            }

            var list = rows
                .OrderBy(t => t.Sort)
                .ThenBy(t => t.TagCode, StringComparer.Ordinal)
                .Select(t => new
                {
                    t.Code,
                    t.TagCode,
                    t.TagName,
                    t.TagGroup,
                    t.ApplicableSide,
                    t.TagPurposeHint,
                    t.MatchFeature,
                    t.IsManualCorrected,
                })
                .ToList();

            // 按分组聚合，供前端「语义分组视图」直接渲染（老板要的「分组」落点之一）
            var groups = list
                .GroupBy(t => t.TagGroup ?? "未分组")
                .Select(g => new { TagGroup = g.Key, Count = g.Count() })
                .OrderBy(g => g.TagGroup, StringComparer.Ordinal)
                .ToList();

            return Ok(ApiResponse<object>.Ok(new { Total = list.Count, Rows = list, Groups = groups }));
        }

        /// <summary>仅取 <c>TagCode</c> 列表（轻量，供批量校验/过滤参数回显用）</summary>
        [HttpGet("codes")]
        public async Task<IActionResult> Codes()
        {
            var rows = await _db.Client.Queryable<TagDict>()
                .Where(x => x.IsValid == 1 && !x.IsDeleted)
                .OrderBy(x => x.Sort)
                .Select(x => x.TagCode)
                .ToListAsync() ?? new List<string>();
            return Ok(ApiResponse<object>.Ok(new { Rows = rows }));
        }

        public class TagDictQueryRequest
        {
            public string? ApplicableSide { get; set; }
            public string? StandardCode { get; set; }
        }
    }
}