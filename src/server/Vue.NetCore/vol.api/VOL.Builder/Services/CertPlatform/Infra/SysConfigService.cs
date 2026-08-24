using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Sys;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 全局系统参数服务（全局缓存模式）。
    ///
    /// 缓存策略：
    /// - 首次访问时从数据库加载全部参数到内存字典（ConcurrentDictionary 线程安全）
    /// - 后续读取直接走内存，零数据库查询
    /// - 保存时自动刷新缓存（单 key 刷新 + 全量刷新）
    /// - 应用重启时缓存自动重建（首次访问触发）
    ///
    /// 使用方式（任意 Service/Controller 注入 ISysConfigService 即可）：
    /// <code>
    /// var apiKey = _config.Get("ai_api_key");
    /// var maxTokens = _config.GetInt("ai_max_tokens", 4096);
    /// var enableCache = _config.GetBool("enable_cache", true);
    /// </code>
    /// </summary>
    public class SysConfigService : ISysConfigService
    {
        private readonly VOLContext _db;
        private readonly IMemoryCache _cache;

        /// <summary>全量参数缓存（线程安全字典，读取无锁）</summary>
        private static readonly ConcurrentDictionary<string, string> _configCache = new();
        private static readonly object _initLock = new();
        private static bool _initialized = false;

        public SysConfigService(VOLContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        /// <summary>
        /// 确保全量缓存已加载。首次调用时从数据库批量读取，后续直接返回。
        /// </summary>
        private void EnsureCacheLoaded()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;
                LoadAllToCache();
                _initialized = true;
            }
        }

        /// <summary>
        /// 从数据库加载全部系统参数到内存缓存。
        /// </summary>
        private void LoadAllToCache()
        {
            var allConfigs = _db.Set<SysConfig>()
                .AsNoTracking()
                .ToList();

            _configCache.Clear();
            foreach (var config in allConfigs)
            {
                _configCache[config.ConfigKey] = config.ConfigValue ?? "";
            }
            Console.WriteLine($"[SysConfig] 全量缓存已加载，共 {allConfigs.Count} 条参数");
        }

        /// <summary>
        /// 刷新全量缓存（保存参数后自动调用，也可手动调用）。
        /// </summary>
        public void RefreshCache()
        {
            lock (_initLock)
            {
                LoadAllToCache();
                _initialized = true;
            }
        }

        /// <summary>
        /// 获取单个参数值（从内存缓存读取，零数据库查询）。
        /// </summary>
        public string Get(string key)
        {
            EnsureCacheLoaded();
            return _configCache.TryGetValue(key, out var value) ? value : "";
        }

        /// <summary>
        /// 获取强类型参数值（自动转换 int/long/bool/decimal/string）。
        /// </summary>
        public T Get<T>(string key)
        {
            var value = Get(key);
            if (string.IsNullOrEmpty(value))
                return default;

            try
            {
                var t = typeof(T);
                if (t == typeof(int))
                    return (T)(object)int.Parse(value);
                if (t == typeof(long))
                    return (T)(object)long.Parse(value);
                if (t == typeof(bool))
                    return (T)(object)(value.ToLower() == "true" || value == "1");
                if (t == typeof(decimal))
                    return (T)(object)decimal.Parse(value);
                if (t == typeof(double))
                    return (T)(object)double.Parse(value);
                if (t == typeof(float))
                    return (T)(object)float.Parse(value);
                return (T)(object)value;
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// 获取 int 类型参数（带默认值）。
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            var value = Get(key);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取 bool 类型参数（带默认值）。
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            var value = Get(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;
            return value.ToLower() == "true" || value == "1";
        }

        /// <summary>
        /// 保存单个参数并自动刷新缓存。
        /// </summary>
        public async Task SetAsync(string key, string value)
        {
            var config = await _db.Set<SysConfig>()
                .FirstOrDefaultAsync(c => c.ConfigKey == key);

            if (config == null)
                return;

            if (config.IsReadonly == 1)
                throw new InvalidOperationException($"参数 {key} 是只读的，无法修改");

            config.ConfigValue = value;
            config.ModifyDate = DateTime.Now;
            await _db.SaveChangesAsync();

            // 刷新单个 key 的缓存 + 保留兼容性（清除旧 IMemoryCache 条目）
            _configCache[key] = value;
            _cache.Remove($"sysconfig_{key}");
        }

        /// <summary>
        /// 按分类查询参数列表（直接查数据库，不走缓存，因为管理页面需要最新数据）。
        /// </summary>
        public async Task<List<SysConfigDto>> GetByCategoryAsync(string category)
        {
            var configs = await _db.Set<SysConfig>()
                .AsNoTracking()
                .Where(c => c.Category == category)
                .OrderBy(c => c.SortOrder)
                .Select(c => new SysConfigDto
                {
                    Id = c.Id,
                    ConfigKey = c.ConfigKey,
                    ConfigValue = c.ConfigValue,
                    ConfigType = c.ConfigType,
                    Category = c.Category,
                    DisplayName = c.DisplayName,
                    Description = c.Description,
                    SortOrder = c.SortOrder,
                    IsReadonly = c.IsReadonly
                })
                .ToListAsync();

            return configs;
        }

        /// <summary>
        /// 批量更新参数并自动刷新全量缓存。
        /// </summary>
        public async Task UpdateBatchAsync(List<CertConfigUpdateDto> configs)
        {
            foreach (var dto in configs)
            {
                var config = await _db.Set<SysConfig>()
                    .FirstOrDefaultAsync(c => c.ConfigKey == dto.ConfigKey);

                if (config == null || config.IsReadonly == 1)
                    continue;

                config.ConfigValue = dto.ConfigValue;
                config.ModifyDate = DateTime.Now;
                _cache.Remove($"sysconfig_{dto.ConfigKey}");
            }

            await _db.SaveChangesAsync();

            // 批量更新后全量刷新缓存，确保一致性
            RefreshCache();
        }
    }
}
