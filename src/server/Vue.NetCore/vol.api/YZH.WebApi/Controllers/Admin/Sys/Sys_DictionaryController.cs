using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YZH.Core.Controllers.Basic;
using YZH.Core.Extensions;
using YZH.Core.Filters;
using YZH.Sys.IServices;

namespace YZH.Sys.Controllers
{
    [Route("api/Sys_Dictionary")]
    public partial class Sys_DictionaryController : ApiBaseController<ISys_DictionaryService>
    {
        public Sys_DictionaryController(ISys_DictionaryService service)
        : base("System", "System", "Sys_Dictionary", service)
        {
        }
    }
}
