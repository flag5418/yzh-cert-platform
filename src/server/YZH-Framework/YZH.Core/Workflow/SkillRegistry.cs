using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace YZH.Core.Workflow
{
    /// <summary>
    /// Skill 注册表（V2 静态方法版）。
    /// 从 wf_skill_reflection 表加载 classPath + methodName，
    /// 执行时委托给 SkillExecutor 反射调用静态方法。
    /// </summary>
    public class SkillRegistry : ISkillRegistry
    {
        private readonly SkillExecutor _executor;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SkillRegistry> _logger;

        // 缓存：skillCode → (classPath, methodName)
        private readonly Dictionary<string, (string classPath, string methodName)> _cache = new();
        private readonly object _cacheLock = new();

        public SkillRegistry(
            IServiceProvider serviceProvider,
            SkillExecutor executor,
            ILogger<SkillRegistry> logger)
        {
            _serviceProvider = serviceProvider;
            _executor = executor;
            _logger = logger;
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
            if (string.IsNullOrEmpty(classPath))
                return SkillResult.Fail($"Skill '{skillCode}' 未在数据库中登记反射配置");

            return await _executor.ExecuteAsync(classPath, methodName, context, ct);
        }

        /// <summary>
        /// 从数据库 wf_skill_reflection 表读取 classPath + methodName。
        /// 通过 DI 容器获取 DbContext，避免直接依赖 VOLContext（保持框架独立性）。
        /// </summary>
        private async Task<(string classPath, string methodName)> GetReflectionInfo(string skillCode, CancellationToken ct)
        {
            // 先查缓存
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(skillCode, out var cached))
                    return cached;
            }

            // 从数据库加载
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<VOL.Core.EFDbContext.VOLContext>();
            if (db == null)
            {
                _logger.LogError("无法获取 VOLContext，SkillRegistry 无法加载反射配置");
                return (string.Empty, string.Empty);
            }

            var entity = await db.Set<VOL.Entity.CertPlatform.Wf.WfSkillReflection>()
                .FirstOrDefaultAsync(x => x.SkillCode == skillCode && x.Enable == true, ct);

            if (entity == null || string.IsNullOrWhiteSpace(entity.ClassPath))
            {
                _logger.LogWarning("Skill '{SkillCode}' 未在 wf_skill_reflection 表中登记", skillCode);
                return (string.Empty, string.Empty);
            }

            var methodName = string.IsNullOrWhiteSpace(entity.MethodName) ? "ExecuteAsync" : entity.MethodName;
            var info = (entity.ClassPath, methodName);

            lock (_cacheLock)
            {
                _cache[skillCode] = info;
            }

            return info;
        }
    }
}
