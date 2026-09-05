using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using YZH.Core.Enums;
using YZH.Core.Extensions;
using YZH.Core.ObjectActionValidator;
using YZH.Core.Services;
using YZH.Core.Utilities;

namespace YZH.Core.Filters
{
    public class ActionExecuteFilter : IActionFilter
    {

        public void OnActionExecuting(ActionExecutingContext context)
        {
            //验证方法参数
            context.ActionParamsValidator();
        }
        public void OnActionExecuted(ActionExecutedContext context)
        {

        }
    }
}