using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using VOL.Builder.IRepositories;
using VOL.Builder.IServices;
using VOL.Core.Filters;
using VOL.Entity.DomainModels;

namespace VOL.WebApi.Controllers.Builder
{
    [JWTAuthorize]
    [Route("api/Builder")]
    public class BuilderController : Controller
    {
        private ISys_TableInfoService Service;
        private ISys_TableInfoRepository _repository;
        public BuilderController(ISys_TableInfoService service, ISys_TableInfoRepository repository)
        {
            Service = service;
            _repository = repository;
        }
        [HttpPost]
        [Route("GetTableTree")]
        public async Task<ActionResult> GetTableTree()
        {
            try
            {
                (string, string) builderInfo = await Service.GetTableTree();
                return Json(new { list = builderInfo.Item1, nameSpace = builderInfo.Item2 });
            }
            catch (Exception ex)
            {

                return Json(new { list = ex.Message + ex.StackTrace + ex.Source, nameSpace = ex.InnerException?.Message });
            }
        }

        [Route("CreateVuePage")]
        [ApiActionPermission(ActionRolePermission.SuperAdmin)]
        [HttpPost]
        public ActionResult CreateVuePage([FromBody] Sys_TableInfo sysTableInfo, string vuePath, int tableId, string table)
        {
            return Content(Service.CreateVuePage(sysTableInfo, vuePath, tableId, table));
        }

        [Route("loadOptions")]

        [HttpPost]
        public ActionResult CreateVuePage(int tableId, string table)
        {
            var res = Service.CreateVuePage(null, null, tableId, table);

            return Json(new { status = res.Contains("fun"), content = res });
        }
        [Route("CreateModel")]
        [ApiActionPermission(ActionRolePermission.SuperAdmin)]
        [HttpPost]
        public ActionResult CreateEntityModel([FromBody] Sys_TableInfo tableInfo)
        {
            return Content(Service.CreateEntityModel(tableInfo));
        }
        [Route("Save")]
        [ApiActionPermission(ActionRolePermission.SuperAdmin)]
        [HttpPost]
        public ActionResult SaveEidt([FromBody] Sys_TableInfo tableInfo)
        {
            return Json(Service.SaveEidt(tableInfo));
        }
        [Route("CreateServices")]
        [ApiActionPermission(ActionRolePermission.SuperAdmin)]
        [HttpPost]
        public ActionResult CreateServices(string tableName, string nameSpace, string foldername, bool? partial, bool? api)
        {
            return Content(Service.CreateServices(tableName, nameSpace, foldername, false, true));
        }
        [Route("LoadTableInfo")]
        [HttpPost]
        public ActionResult LoadTable([FromBody] Sys_TableInfo sysTableInfo, int parentId, string tableName, string columnCNName, string nameSpace, string foldername, int table_Id, bool isTreeLoad, string dbServer, bool gengeneric)
        {
            return Json(Service.LoadTable(sysTableInfo, parentId, tableName, columnCNName, nameSpace, foldername, table_Id, isTreeLoad, dbServer));
        }
        [Route("delTree")]
        [ApiActionPermission(ActionRolePermission.SuperAdmin)]
        [HttpPost]
        public async Task<ActionResult> DelTree(int table_Id)
        {
            return Json(await Service.DelTree(table_Id));
        }
        [Route("syncTable")]
        [ApiActionPermission(ActionRolePermission.SuperAdmin)]
        [HttpPost]
        public async Task<ActionResult> SyncTable(string tableName)
        {
            return Json(await Service.SyncTable(tableName));
        }

        [Route("getDyTable")]
        [HttpPost]
        public async Task<ActionResult> GetDyTable()
        {
            await Task.CompletedTask;
            return Json(Array.Empty<object>());
        }
    }
}
