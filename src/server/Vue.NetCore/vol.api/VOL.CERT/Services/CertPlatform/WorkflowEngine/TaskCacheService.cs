using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSRedis;
using Microsoft.Extensions.Logging;
using VOL.CERT.Services.CertPlatform.WorkflowEngine.Models;

namespace VOL.CERT.Services.CertPlatform.WorkflowEngine
{
    /// <summary>
    /// 任务级缓存服务 — 管理工作流执行期间的 Redis 缓存
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §九（任务级缓存方案）</para>
    /// <para>生命周期：任务开始 → 预热缓存 → 执行期间读取 → 任务结束 → 清理缓存</para>
    /// <para>缓存键设计：</para>
    /// <para>  wf:task:{taskCode}:doc:field:{ruleCode}:{fieldCode}  → 标准文档字段值</para>
    /// <para>  wf:task:{taskCode}:doc:table:{ruleCode}:{tableCode}  → 标准文档表格数据</para>
    /// <para>  wf:task:{taskCode}:ent:field:{entCode}:{ruleCode}:{fieldCode}  → 企业文档字段值</para>
    /// <para>  wf:task:{taskCode}:ent:table:{entCode}:{ruleCode}:{tableCode}  → 企业文档表格数据</para>
    /// <para>  wf:task:{taskCode}:skill:{skillCode}  → Skill 配置</para>
    /// </summary>
    public class TaskCacheService
    {
        private readonly ILogger<TaskCacheService> _logger;

        /// <summary>缓存键前缀</summary>
        private const string KeyPrefix = "wf:task:";

        public TaskCacheService(ILogger<TaskCacheService> logger)
        {
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
        public async Task SetAsync<T>(string key, T value, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(value);

            try
            {
                await RedisHelper.SetAsync(key, json);
                _logger.LogDebug("[CACHE_SET] key={Key}, valueLength={Length}", key, json.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE_SET] 失败, key={Key}", key);
            }
        }

        // ── 读取 ──

        /// <summary>
        /// 读取缓存（泛型反序列化）
        /// </summary>
        public async Task<T> GetAsync<T>(string key, CancellationToken ct = default)
        {
            try
            {
                var json = await RedisHelper.GetAsync(key);
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogDebug("[CACHE_MISS] key={Key}", key);
                    return default;
                }

                _logger.LogDebug("[CACHE_HIT] key={Key}", key);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE_GET] 失败, key={Key}", key);
                return default;
            }
        }

        /// <summary>
        /// 读取缓存（原始 JSON 字符串）
        /// </summary>
        public async Task<string> GetRawAsync(string key, CancellationToken ct = default)
        {
            try
            {
                var json = await RedisHelper.GetAsync(key);
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogDebug("[CACHE_MISS] key={Key}", key);
                    return null;
                }

                _logger.LogDebug("[CACHE_HIT] key={Key}", key);
                return json;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE_GET] 失败, key={Key}", key);
                return null;
            }
        }

        // ── 批量预热 ──

        /// <summary>
        /// 预热缓存
        /// <para>文档：V3 §9.1 — 解析 nodes → 识别需要哪些文档数据 → 批量预热</para>
        /// <para>当前阶段：TODO 模拟预热，后续接入真实数据加载</para>
        /// </summary>
        public async Task<(int cachedCount, List<string> cacheKeys)> WarmUpAsync(
            string taskCode,
            ParsedWorkflow parsed,
            string enterpriseCode,
            CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[CACHE_WARMUP_START] taskCode={TaskCode}, nodeCount={NodeCount}",
                taskCode, parsed.NodeMap.Count);

            var cacheKeys = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int cachedCount = 0;

            foreach (var node in parsed.NodeMap.Values)
            {
                ct.ThrowIfCancellationRequested();

                var config = node.Config ?? new();
                var nodeType = node.NodeType?.ToLowerInvariant() ?? "";

                string cacheKey = null;
                string cacheValue = null;

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

                            // TODO: 从 cert_doc_extraction_rule 加载 sample_data
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

                            // TODO: 从 cert_doc_extraction_rule 加载表格数据
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

            _logger.LogInformation(
                "[CACHE_WARMUP_DONE] taskCode={TaskCode}, cachedKeys={Count}, durationMs={DurationMs}",
                taskCode, cachedCount, sw.ElapsedMilliseconds);

            return (cachedCount, cacheKeys);
        }

        // ── 清理 ──

        /// <summary>
        /// 清理任务级缓存
        /// <para>文档：V3 §9.1 — DEL wf:task:{taskCode}:*</para>
        /// </summary>
        public async Task<int> CleanUpAsync(string taskCode, CancellationToken ct = default)
        {
            var pattern = $"{KeyPrefix}{taskCode}:*";
            int cleanedCount = 0;

            try
            {
                // 使用 KEYS 查找所有匹配的键
                // 注意：生产环境 key 数量大时 KEYS 可能阻塞，后续可改为 SCAN
                var keys = (await RedisHelper.KeysAsync(pattern) ?? Array.Empty<string>()).ToList();

                // 批量删除
                if (keys.Count > 0)
                {
                    foreach (var key in keys)
                    {
                        await RedisHelper.DelAsync(key);
                        _logger.LogDebug("[CACHE_DEL] key={Key}", key);
                    }
                    cleanedCount = keys.Count;
                }

                _logger.LogInformation(
                    "[CACHE_CLEANUP] taskCode={TaskCode}, cleanedKeys={Count}",
                    taskCode, cleanedCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE_CLEANUP] 失败, taskCode={TaskCode}", taskCode);
            }

            return cleanedCount;
        }
    }
}
