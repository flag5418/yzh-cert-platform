using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.Workflow.Skills;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// Skill 注册表（V2 静态方法版 + ISkillNode 回退）
    /// <para>移植自：旧 CertSkillRegistry.cs（EF VOLContext → IDbOrm 原生 SQL 查询 wf_skill_reflection）</para>
    /// <para>优先从 wf_skill_reflection 表加载 classPath + methodName 反射执行，</para>
    /// <para>找不到时回退到 DI 容器中注册的 ISkillNode 实例。</para>
    /// <para>缓存策略：5 分钟过期（IMemoryCache），运行期修改 wf_skill_reflection 后自动刷新</para>
    /// </summary>
    public class CertSkillRegistry : ISkillRegistry
    {
        private readonly SkillExecutor _executor;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDbOrm _db;
        private readonly ILogger<CertSkillRegistry> _logger;
        private readonly Dictionary<string, ISkillNode> _diSkills;
        private readonly IMemoryCache _cache;

        // 缓存过期时间（分钟）：Skill 反射信息变更频率低，但必须支持运行期刷新
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

        public CertSkillRegistry(
            IServiceProvider serviceProvider,
            SkillExecutor executor,
            IEnumerable<ISkillNode> skills,
            IDbOrm db,
            ILogger<CertSkillRegistry> logger,
            IMemoryCache cache)
        {
            _serviceProvider = serviceProvider;
            _executor = executor;
            _db = db;
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

            // 回退：从 DI 容器查找 ISkillNode 实例
            if (_diSkills.TryGetValue(skillCode, out var skillNode))
            {
                _logger.LogInformation("Skill '{SkillCode}' 从 DI 容器执行（非反射模式）", skillCode);
                return await skillNode.ExecuteAsync(context, ct);
            }

            return SkillResult.Fail($"Skill '{skillCode}' 未在数据库中登记反射配置，也未在 DI 容器中注册");
        }

        /// <summary>
        /// 从数据库 wf_skill_reflection 表读取 classPath + methodName。
        /// 迁移改写：EF DbContext → IDbOrm 原生 SQL（列名 snake_case，规避实体注解映射歧义，与执行表同策略）
        /// </summary>
        private async Task<(string classPath, string methodName)> GetReflectionInfo(string skillCode, CancellationToken ct)
        {
            // 先查缓存（IMemoryCache 线程安全，无需 lock）
            if (_cache.TryGetValue(skillCode, out var cached) && cached is (string cp, string mn))
                return (cp, mn);

            var row = (await _db.QueryFirstOrDefaultAsync<ReflectionRow>(
                "SELECT class_path AS ClassPath, method_name AS MethodName FROM wf_skill_reflection " +
                "WHERE skill_code = @skillCode AND enable = 1 AND IsDeleted = 0",
                new { skillCode })).Data;

            if (row == null || string.IsNullOrWhiteSpace(row.ClassPath))
            {
                _logger.LogWarning("Skill '{SkillCode}' 未在 wf_skill_reflection 表中登记", skillCode);
                return (string.Empty, string.Empty);
            }

            var methodName = string.IsNullOrWhiteSpace(row.MethodName) ? "ExecuteAsync" : row.MethodName;
            var info = (row.ClassPath, methodName);

            // 写入缓存，设 5 分钟过期
            _cache.Set(skillCode, info, CacheExpiration);

            return info;
        }

        private class ReflectionRow
        {
            public string ClassPath { get; set; } = "";
            public string MethodName { get; set; } = "";
        }

        /// <summary>
        /// 手动失效缓存（Skill 配置变更后刷新）
        /// </summary>
        public void InvalidateCache(string skillCode)
        {
            _cache.Remove(skillCode);
            _logger.LogInformation("Skill '{SkillCode}' 缓存已失效", skillCode);
        }
    }
}
