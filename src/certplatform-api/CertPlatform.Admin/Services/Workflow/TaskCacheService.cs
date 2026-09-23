using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.Workflow.Models;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 任务级缓存服务 — 管理工作流执行期间的任务级缓存
    /// <para>移植自：旧 TaskCacheService.cs（CSRedis → IMemoryCache，决策 D-2）</para>
    /// <para>生命周期：任务开始 → 预热缓存 → 执行期间读取 → 任务结束 → 清理缓存</para>
    /// <para>键结构（与旧 Redis 版一致，为将来分布式升级预留）：</para>
    /// <para>  wf:task:{taskCode}:doc:field:{ruleCode}:{fieldCode}  → 标准文档字段值</para>
    /// <para>  wf:task:{taskCode}:doc:table:{ruleCode}:{tableCode}  → 标准文档表格数据</para>
    /// <para>  wf:task:{taskCode}:ent:field:{entCode}:{ruleCode}:{fieldCode}  → 企业文档字段值</para>
    /// <para>  wf:task:{taskCode}:ent:table:{entCode}:{ruleCode}:{tableCode}  → 企业文档表格数据</para>
    /// <para>  wf:task:{taskCode}:skill:{skillCode}  → Skill 配置</para>
    ///
    /// <para><b>2026-09-22 阶段二：日志收敛</b> —— 原先本类用 <c>ILogger</c> 自行拼
    /// <c>[CACHE_WARMUP_START]</c> / <c>[CACHE_WARMUP_DONE]</c> / <c>[CACHE_CLEANUP]</c>，
    /// 造成两个问题：① <see cref="WorkflowLogger.CacheWarmupStart"/> / <c>CacheWarmupDone</c>
    /// 声明了却从无调用点（死方法）；② <c>[CACHE_CLEANUP]</c> 与
    /// <c>WfExecutionTaskService.FinishRunAsync</c> 各打一次，**每次执行重复两行**。
    /// 现统一走 <see cref="WorkflowLogger"/> —— 它是全引擎日志格式的唯一出口。</para>
    /// </summary>
    public class TaskCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly WorkflowLogger _wfLogger;
        private readonly ILogger<TaskCacheService> _logger;

        /// <summary>缓存键前缀</summary>
        private const string KeyPrefix = "wf:task:";

        public TaskCacheService(
            IMemoryCache cache,
            WorkflowLogger wfLogger,
            ILogger<TaskCacheService> logger)
        {
            _cache = cache;
            _wfLogger = wfLogger;
            _logger = logger;
        }

        // ── 缓存键生成 ──

        /// <summary>标准文档字段缓存键</summary>
        public string DocFieldKey(string taskCode, string ruleCode, string fieldCode)
            => $"{KeyPrefix}{taskCode}:doc:field:{ruleCode}:{fieldCode}";

        /// <summary>标准文档表格缓存键</summary>
        public string DocTableKey(string taskCode, string ruleCode, string tableCode)
            => $"{KeyPrefix}{taskCode}:doc:table:{ruleCode}:{tableCode}";

        /// <summary>企业文档字段缓存键</summary>
        public string EntFieldKey(string taskCode, string enterpriseCode, string ruleCode, string fieldCode)
            => $"{KeyPrefix}{taskCode}:ent:field:{enterpriseCode}:{ruleCode}:{fieldCode}";

        /// <summary>企业文档表格缓存键</summary>
        public string EntTableKey(string taskCode, string enterpriseCode, string ruleCode, string tableCode)
            => $"{KeyPrefix}{taskCode}:ent:table:{enterpriseCode}:{ruleCode}:{tableCode}";

        /// <summary>Skill 配置缓存键</summary>
        public string SkillKey(string taskCode, string skillCode)
            => $"{KeyPrefix}{taskCode}:skill:{skillCode}";

        // ── 写入 ──

        /// <summary>
        /// 写入缓存
        /// </summary>
        public Task SetAsync<T>(string key, T value, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                _cache.Set(key, json, TimeSpan.FromHours(1)); // 任务级缓存，1 小时兜底过期
                _logger.LogDebug("[CACHE_SET] key={Key}, valueLength={Length}", key, json.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE_SET] 失败, key={Key}", key);
            }
            return Task.CompletedTask;
        }

        // ── 读取 ──

        /// <summary>
        /// 读取缓存（泛型反序列化）
        /// </summary>
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            try
            {
                if (!_cache.TryGetValue(key, out var raw) || raw is not string json || string.IsNullOrEmpty(json))
                {
                    _logger.LogDebug("[CACHE_MISS] key={Key}", key);
                    return Task.FromResult(default(T));
                }

                _logger.LogDebug("[CACHE_HIT] key={Key}", key);
                return Task.FromResult(JsonSerializer.Deserialize<T>(json));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE_GET] 失败, key={Key}", key);
                return Task.FromResult(default(T));
            }
        }

        /// <summary>
        /// 读取缓存（原始 JSON 字符串）
        /// </summary>
        public Task<string?> GetRawAsync(string key, CancellationToken ct = default)
        {
            try
            {
                if (!_cache.TryGetValue(key, out var raw) || raw is not string json || string.IsNullOrEmpty(json))
                {
                    _logger.LogDebug("[CACHE_MISS] key={Key}", key);
                    return Task.FromResult<string?>(null);
                }

                _logger.LogDebug("[CACHE_HIT] key={Key}", key);
                return Task.FromResult<string?>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE_GET] 失败, key={Key}", key);
                return Task.FromResult<string?>(null);
            }
        }

        // ── 批量预热 ──

        /// <summary>
        /// 预热缓存
        /// <para>文档：V3 §9.1 — 解析 nodes → 识别需要哪些文档数据 → 批量预热</para>
        /// <para>当前阶段：与旧版一致保持模拟预热（真实数据加载在 NodeExecutor docField/docTable 直查落位后，</para>
        /// <para>预热收益主要在 NC_CHECK 正式执行场景，TEST 场景影响有限）</para>
        /// </summary>
        public async Task<(int cachedCount, List<string> cacheKeys)> WarmUpAsync(
            string taskCode,
            ParsedWorkflow parsed,
            string enterpriseCode,
            CancellationToken ct = default)
        {
            _wfLogger.CacheWarmupStart(taskCode, parsed.NodeMap.Count);

            var cacheKeys = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int cachedCount = 0;

            foreach (var node in parsed.NodeMap.Values)
            {
                ct.ThrowIfCancellationRequested();

                var config = node.Config ?? new();
                var nodeType = node.NodeType?.ToLowerInvariant() ?? "";

                string? cacheKey = null;
                string? cacheValue = null;

                switch (nodeType)
                {
                    case "docfield":
                        {
                            var docType = config.GetValueOrDefault("docType")?.ToString() ?? "standard";
                            var ruleCode = config.GetValueOrDefault("ruleCode")?.ToString() ?? "";
                            var fieldCode = config.GetValueOrDefault("fieldCode")?.ToString() ?? "";

                            if (docType == "enterprise")
                                cacheKey = EntFieldKey(taskCode, enterpriseCode ?? "", ruleCode, fieldCode);
                            else
                                cacheKey = DocFieldKey(taskCode, ruleCode, fieldCode);

                            // TODO: 从 ent_extraction_result 加载真实数据（与 NodeExecutor 直查同源）
                            cacheValue = JsonSerializer.Serialize(new
                            {
                                fieldValue = "预热字段值",
                                confidence = 1.0,
                                source = docType == "standard" ? "sample_data" : "enterprise_doc"
                            });
                            break;
                        }

                    case "doctable":
                        {
                            var docType = config.GetValueOrDefault("docType")?.ToString() ?? "standard";
                            var ruleCode = config.GetValueOrDefault("ruleCode")?.ToString() ?? "";
                            var tableCode = config.GetValueOrDefault("tableCode")?.ToString() ?? "";

                            if (docType == "enterprise")
                                cacheKey = EntTableKey(taskCode, enterpriseCode ?? "", ruleCode, tableCode);
                            else
                                cacheKey = DocTableKey(taskCode, ruleCode, tableCode);

                            // TODO: 从 ent_table_extraction_result 加载真实数据
                            cacheValue = JsonSerializer.Serialize(new
                            {
                                rows = Array.Empty<object>(),
                                rowCount = 0,
                                confidence = 1.0,
                                source = docType == "standard" ? "sample_data" : "enterprise_doc"
                            });
                            break;
                        }

                    case "skill":
                        {
                            var skillCode = node.SkillCode ?? "";
                            if (!string.IsNullOrEmpty(skillCode))
                            {
                                cacheKey = SkillKey(taskCode, skillCode);
                                // TODO: 从 wf_skill_reflection 加载 Skill 配置
                                cacheValue = JsonSerializer.Serialize(new { skillCode, preloaded = true });
                            }
                            break;
                        }
                }

                if (!string.IsNullOrEmpty(cacheKey) && !string.IsNullOrEmpty(cacheValue))
                {
                    await SetAsync(cacheKey, cacheValue, ct);
                    cacheKeys.Add(cacheKey);
                    cachedCount++;
                }
            }

            sw.Stop();

            _wfLogger.CacheWarmupDone(taskCode, cachedCount, sw.ElapsedMilliseconds);

            return (cachedCount, cacheKeys);
        }

        // ── 清理 ──

        /// <summary>
        /// 清理任务级缓存
        /// <para>IMemoryCache 无键枚举能力 → 依赖任务执行期间记录的键列表精确清除（语义与旧 Redis KEYS+DEL 一致）</para>
        /// <para>⚠️ 本方法<b>不</b>打 <c>[CACHE_CLEANUP]</c> 日志 —— 由调用方
        /// <c>WfExecutionTaskService.FinishRunAsync</c> 统一打（避免重复两行）。</para>
        /// </summary>
        public async Task<int> CleanUpAsync(string taskCode, List<string>? knownKeys, CancellationToken ct = default)
        {
            int cleanedCount = 0;

            try
            {
                if (knownKeys != null)
                {
                    foreach (var key in knownKeys)
                    {
                        _cache.Remove(key);
                        _logger.LogDebug("[CACHE_DEL] key={Key}", key);
                        cleanedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE_CLEANUP] 失败, taskCode={TaskCode}", taskCode);
            }

            return await Task.FromResult(cleanedCount);
        }
    }
}
