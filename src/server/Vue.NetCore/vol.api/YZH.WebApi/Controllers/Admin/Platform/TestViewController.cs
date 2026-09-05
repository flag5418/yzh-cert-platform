using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions;
using YZH.Core.Utilities;
using YZH.Entity.Admin.Platform.Cert;

namespace YZH.WebApi.Controllers.Admin.Platform
{
    [Route("api/TestView")]
    [ApiController]
    public class TestViewController : ControllerBase
    {
        [HttpGet("ISOStandard"), AllowAnonymous]
        public object GetISOStandards(int rows = 5)
        {
            try
            {
                using var db = new VOLContext();
                var query = db.Set<ISOStandard>().UseViewIfExists();
                return new { total = query.Count(), data = query.Take(rows).ToList() };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }

        [HttpGet("CertStage"), AllowAnonymous]
        public object GetCertStages(int rows = 5)
        {
            try
            {
                using var db = new VOLContext();
                var query = db.Set<CertStage>().UseViewIfExists();
                return new { total = query.Count(), data = query.Take(rows).ToList() };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }

        [HttpGet("Workflow"), AllowAnonymous]
        public object GetWorkflows(int rows = 5)
        {
            try
            {
                using var db = new VOLContext();
                var query = db.Set<YZH.Entity.Admin.Platform.Wf.WorkflowDefinition>().UseViewIfExists();
                return new { total = query.Count(), data = query.Take(rows).ToList() };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }
}
