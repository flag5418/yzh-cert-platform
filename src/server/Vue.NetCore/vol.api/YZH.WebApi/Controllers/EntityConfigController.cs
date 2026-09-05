using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Utilities;
using YZH.Entity.Admin.Platform.Base;

namespace YZH.WebApi.Controllers
{
    /// <summary>
    /// 实体配置API
    /// 用于前端获取页面/表格/表单配置
    /// 
    /// 接口说明：
    /// GET /api/entity-config/{entityName} - 获取单个实体的完整页面配置
    /// GET /api/entity-config/list - 获取所有实体配置列表
    /// </summary>
    [Route("api/entity-config")]
    [ApiController]
    public class EntityConfigController : ControllerBase
    {
        /// <summary>
        /// 获取实体页面配置
        /// GET /api/entity-config/{entityName}
        /// 
        /// 返回完整的PageUIConfig，包含：
        /// - PageMeta: 页面元数据（标题、主键、排序、弹窗配置等）
        /// - FieldConfigs: 字段配置列表（表格列、表单字段、搜索条件）
        /// </summary>
        /// <param name="entityName">实体名称（如：CertificationBody）</param>
        /// <returns>页面UI配置</returns>
        [HttpGet("{entityName}")]
        public IActionResult GetPageConfig(string entityName)
        {
            var entityType = FindEntityType(entityName);
            if (entityType == null)
                return NotFound(new { message = $"实体 {entityName} 不存在，或未标记[Page]特性" });

            try
            {
                // 调用泛型方法 Generate<T>()
                var method = typeof(EntityConfigGenerator)
                    .GetMethod("Generate")!
                    .MakeGenericMethod(entityType);

                var config = method.Invoke(null, null);
                return Ok(config);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"生成配置失败: {ex.Message}", detail = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// 获取所有实体配置列表
        /// GET /api/entity-config/list
        /// 
        /// 返回所有标记了[Page]特性的实体列表
        /// </summary>
        /// <returns>实体配置列表</returns>
        [HttpGet("list")]
        public IActionResult GetConfigList()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var entities = assemblies
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .Where(t => t.GetCustomAttribute<PageAttribute>() != null)
                    .Select(t =>
                    {
                        var pageAttr = t.GetCustomAttribute<PageAttribute>();
                        return new
                        {
                            EntityName = t.Name,
                            PageKey = pageAttr?.PageKey,
                            Title = pageAttr?.Title,
                            ControllerName = pageAttr?.ControllerName
                        };
                    })
                    .OrderBy(e => e.EntityName)
                    .ToList();

                return Ok(entities);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"获取配置列表失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 根据实体名查找类型
        /// 支持精确匹配和不区分大小写匹配
        /// </summary>
        private Type? FindEntityType(string entityName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // 精确匹配
            var type = assemblies
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.Name == entityName &&
                                   t.GetCustomAttribute<PageAttribute>() != null);

            if (type != null) return type;

            // 不区分大小写匹配
            return assemblies
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => string.Equals(t.Name, entityName, StringComparison.OrdinalIgnoreCase) &&
                                   t.GetCustomAttribute<PageAttribute>() != null);
        }
    }
}
