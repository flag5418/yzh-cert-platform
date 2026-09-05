using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.Workflow;

namespace Cert.Platform.Services.Admin.Platform.WorkflowEngine
{
    /// <summary>
    /// Skill 注册表（V2 静态方法版 + ISkillNode 回退）。
    /// 优先从 wf_skill_reflection 表加载 classPath + methodName，
    /// 找不到时回退到 DI 容器中注册的 ISkillNode 实例（如 llm_extract）。
    /// 项目独有：依赖认证平台的 WfSkillReflection 实体。
    /// </summary>
    public class CertSkillRegistry : ISkillRegistry
    {
        private readonly SkillExecutor _executor;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CertSkillRegistry> _logger;
        private readonly Dictionary<string, ISkillNode> _diSkills;
        private readonly IMemoryCache _cache;

        // 缓存过期时间（分钟）：Skill 反射信息变更频率低，但必须支持运行期刷新
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

        public CertSkillRegistry(
            IServiceProvider serviceProvider,
            SkillExecutor executor,
            IEnumerable<ISkillNode> skills,
            ILogger<CertSkillRegistry> logger,
            IMemoryCache cache)
        {
            _serviceProvider = serviceProvider;
            _executor = executor;
            _logger = logger;
            _diSkills = skills.ToDictionary(s => s.SkillCode, s => s);
            _cache = cache;
        }

        public async Task<SkillMetadata?> LoadAsync(string skillCode, CancellationToken ct = default)
        {
            var (classPath, methodName) = await GetReflectionInfo(skillCode, ct);
            if (string.IsNullOrEmpty(classPath)) return null;
            return _executor.Analyze(classPath, methodName);
        }

        public async Task<SkillResult> ExecuteAsync(string skillCode, SkillContext context, CancellationToken ct = default)
        {
            var (classPath, methodName) = await GetReflectionInfo(skillCode, ct);
            if (!string.IsNullOrEmpty(classPath))
                return await _executor.ExecuteAsync(classPath, methodName, context, ct);

            // 回退：从 DI 容器查找 ISkillNode 实例（如 llm_extract）
            if (_diSkills.TryGetValue(skillCode, out var skillNode))
            {
                _logger.LogInformation("Skill '{SkillCode}' 从 DI 容器执行（非反射模式）", skillCode);
                return await skillNode.ExecuteAsync(context, ct);
            }

            return SkillResult.Fail($"Skill '{skillCode}' 未在数据库中登记反射配置，也未在 DI 容器中注册");
        }

        /// <summary>
        /// 从数据库 wf_skill_reflection 表读取 classPath + methodName。
        /// 通过 DI 容器获取 DbContext，避免直接依赖 VOLContext（保持框架独立性）。
        /// 缓存策略：5 分钟过期，运行期修改 wf_skill_reflection 后可调用 InvalidateCache 刷新。
        /// </summary>
        private async Task<(string classPath, string methodName)> GetReflectionInfo(string skillCode, CancellationToken ct)
        {
            // 先查缓存（IMemoryCache 线程安全，无需 lock）
            if (_cache.TryGetValue(skillCode, out var cached) && cached is (string cp, string mn))
                return (cp, mn);

            // 从数据库加载
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<YZH.Core.EFDbContext.VOLContext>();
            if (db == null)
            {
                _logger.LogError("无法获取 VOLContext，CertSkillRegistry 无法加载反射配置");
                return (string.Empty, string.Empty);
            }

            var entity = await db.Set<YZH.Entity.Admin.Platform.Wf.WfSkillReflection>()
                .FirstOrDefaultAsync(x => x.SkillCode == skillCode && x.Enable == true, ct);

            if (entity == null || string.IsNullOrWhiteSpace(entity.ClassPath))
            {
                _logger.LogWarning("Skill '{SkillCode}' 未在 wf_skill_reflection 表中登记", skillCode);
                return (string.Empty, string.Empty);
            }

            var methodName = string.IsNullOrWhiteSpace(entity.MethodName) ? "ExecuteAsync" : entity.MethodName;
            var info = (entity.ClassPath, methodName);

            // 写入缓存，设 5 分钟过期
            _cache.Set(skillCode, info, CacheExpiration);

            return info;
        }

        /// <summary>
        /// 手动失效缓存（Skill 配置变更后刷新）
        /// </summary>
        public void InvalidateCache(string skillCode)
        {
            _cache.Remove(skillCode);
            _logger.LogInformation("Skill '{SkillCode}' 缓存已失效", skillCode);
        }

        /// <summary>
        /// 清空全部缓存
        /// </summary>
        public void InvalidateAll()
        {
            if (_cache is MemoryCache memCache)
                memCache.Compact(1.0); // 100% 清除
            _logger.LogInformation("CertSkillRegistry 全部缓存已清空");
        }
    }
}
