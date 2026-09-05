using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using YZH.System.Entities;

namespace YZH.System.Controllers
{
    [Route("api/yzh/sys/dictionaries")]
    [ApiController]
    public class SystemDictionaryController : CrudController<SysDictionary>
    {
        private readonly DomainService<SysDictionaryList> _listSvc;
        public SystemDictionaryController(DomainService<SysDictionary> svc, DomainService<SysDictionaryList> listSvc)
            : base(svc)
        {
            _listSvc = listSvc;
        }

        [HttpPost("Del")]
        public override IActionResult Del([FromBody] List<object> ids)
        {
            var intIds = ids.Select(x => Convert.ToInt32(x)).ToList();
            return DelBy(x => intIds.Contains(x.Dic_ID));
        }

        /// <summary>
        /// 字典明细项（Sys_DictionaryList），按父字典 Dic_ID 过滤
        /// </summary>
        [HttpGet("{dicId}/items")]
        public IActionResult Items(int dicId)
        {
            var items = _listSvc.GetAll()
                .Where(x => x.Dic_ID == dicId)
                .OrderBy(x => x.OrderNo ?? 0)
                .Cast<object>()
                .ToList();
            return Ok(new ApiResult { Rows = items, Total = items.Count });
        }
    }
}
