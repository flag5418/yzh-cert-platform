using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using YZH.System.Entities;

namespace YZH.System
{
    /// <summary>
    /// YZH 通用 CRUD 控制器基类（替代 VOL ApiBaseController 反射式通用逻辑）。
    /// 暴露与 BaseApiClient 兼容的信封：GetPageData / Add / Update / Del。
    /// </summary>
    public abstract class CrudController<T> : ControllerBase where T : class
    {
        protected readonly DomainService<T> _svc;

        protected CrudController(DomainService<T> svc)
        {
            _svc = svc;
        }

        [HttpPost("GetPageData")]
        public virtual IActionResult GetPageData([FromBody] PageQuery q)
        {
            var (rows, total) = _svc.GetPage(q);
            return Ok(new ApiResult
            {
                Rows = rows.Cast<object>().ToList(),
                Total = total
            });
        }

        [HttpPost("Add")]
        public virtual IActionResult Add([FromBody] SaveModel<T> m)
        {
            try
            {
                _svc.Add(m.MainData);
                return Ok(new ApiResult());
            }
            catch (Exception ex)
            {
                return Ok(new ApiResult { Status = false, Msg = ex.Message });
            }
        }

        [HttpPost("Update")]
        public virtual IActionResult Update([FromBody] SaveModel<T> m)
        {
            try
            {
                _svc.Update(m.MainData);
                return Ok(new ApiResult());
            }
            catch (Exception ex)
            {
                return Ok(new ApiResult { Status = false, Msg = ex.Message });
            }
        }

        [HttpPost("Del")]
        public abstract IActionResult Del([FromBody] List<object> ids);

        /// <summary>
        /// 通用批量删除：根据表达式谓词匹配主键。
        /// </summary>
        protected IActionResult DelBy(Expression<Func<T, bool>> predicate)
        {
            try
            {
                _svc.Delete(predicate);
                return Ok(new ApiResult());
            }
            catch (Exception ex)
            {
                return Ok(new ApiResult { Status = false, Msg = ex.Message });
            }
        }
    }
}
