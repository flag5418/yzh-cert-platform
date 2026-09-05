using YZH.Core.Extensions;
using System;
using YZH.Core.Configuration;

namespace YZH.Core.Extensions.AutofacManager
{
    public class AutofacContainerModule
    {
        public static TService GetService<TService>() where TService:class
        {
            return typeof(TService).GetService() as TService;
        }
    }
}
