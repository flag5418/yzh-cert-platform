using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace YZH.Core.Workflow
{
    /// <summary>
    /// Skill 注册表（V2 静态方法版 + ISkillNode 回退）。
    /// 优先从 wf_skill_reflection 表加载 classPath + methodName，
    /// 找不到时回退到 DI 容器中注册的 ISkillNode 实例（如 llm_extract）。
    /// </summary>
    public class SkillRegistry : ISkillRegistry
    {
        private readonly SkillExecutor _executor;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SkillRegistry> _logger;
        private readonly Dictionary<string, ISkillNode> _diSkills;

        // 缓存：skillCode → (classPath, methodName)
        private readonly Dictionary<string, (string classPath, string methodName)> _cache = new();
        private readonly object _cacheLock = new();

        public SkillRegistry(
            IServiceProvider serviceProvider,
            SkillExecutor executor,
            IEnumerable<ISkillNode> skills,
            ILogger<SkillRegistry> logger)
        {
            _serviceProvider = serviceProvider;
            _executor = executor;
            _logger = logger;
            _diSkills = skills.ToDictionary(s => s.SkillCode, s => s);
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
