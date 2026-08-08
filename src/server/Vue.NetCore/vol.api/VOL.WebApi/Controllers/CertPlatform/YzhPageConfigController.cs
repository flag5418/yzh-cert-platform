/*
 * YZH V3.0 页面 UI 配置 API
 *
 * 职责：纯 HTTP 适配层（参数解析、状态码返回）
 * 业务逻辑已迁移至 YZH-Framework/YZH.CertPlatform/Services/
 *
 * 路由: /api/yzh-page-config/{pageKey}
 * 前端调用方: YZHConfigLoader（Vue 组件）
 */
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using VOL.Core.Filters;  // JWTAuthorize 所在命名空间
using YZH.CertPlatform.Services;

namespace VOL.WebApi.Controllers.CertPlatform
{
    /// <summary>
    /// YZH V3.0 页面 UI 配置 API 控制器
    /// <para>提供数据库驱动的 UI 配置查询接口，供前端 YZHConfigLoader 调用</para>
    /// <para>安全：需要登录后才能访问（JWT 鉴权）</para>
    /// </summary>
    [Route("api/yzh-page-config")]
    [ApiController]
    [JWTAuthorize]  // 🔒 必须登录才能获取配置信息
    public class YzhPageConfigController : ControllerBase
    {
        private readonly IYzhPageConfigService _service;

        public YzhPageConfigController(IYzhPageConfigService service)
        {
            _service = service;
        }

        /// <summary>
        /// 根据 pageKey 获取完整的页面 UI 配置（页面级 + 字段级）
        /// </summary>
        [HttpGet("{pageKey}")]
        public async Task<IActionResult> GetPageConfig(string pageKey)
        {
            var result = await _service.GetPageConfigAsync(pageKey);

            if (!result.Success)
            {
                return NotFound(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, data = new { pageMeta = result.PageMeta, fieldConfigs = result.FieldConfigs } });
        }

        /// <summary>
        /// 获取所有可用的页面配置列表（用于配置管理页面）
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllPageConfigs()
        {
            var list = await _service.GetAllPageConfigsAsync();
            return Ok(new { success = true, data = list });
        }

        /// <summary>
        /// 批量获取所有页面的完整配置（前端启动时全量加载）
        /// 返回所有活跃页面的 pageMeta + fieldConfigs，用于本地缓存
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllConfigsFull()
        {
            var result = await _service.GetAllConfigsFullAsync();
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }
            return Ok(new
            {
                success = true,
                data = new
                {
                    version = result.Version,
                    configs = result.Configs
                        .Select(kvp => new { key = kvp.Key, value = new { pageMeta = kvp.Value.PageMeta, fieldConfigs = kvp.Value.FieldConfigs } })
                        .ToDictionary(x => x.key, x => x.value)
                }
            });
        }
    }
}
