using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Sys;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 全局系统参数服务
    /// 使用 IMemoryCache 缓存参数，减少数据库查询
    /// </summary>
    public class SysConfigService : ISysConfigService
    {
        private readonly VOLContext _db;
        private readonly IMemoryCache _cache;
        private static readonly object _lock = new object();

        public SysConfigService(VOLContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public string Get(string key)
        {
            return _cache.GetOrCreate($"sysconfig_{key}", entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                var config = _db.Set<SysConfig>()
                    .AsNoTracking()
                    .FirstOrDefault(c => c.ConfigKey == key);
                return config?.ConfigValue ?? "";
            });
        }

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
                return (T)(object)value;
            }
            catch
            {
                return default;
            }
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var value = Get(key);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            var value = Get(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;
            return value.ToLower() == "true" || value == "1";
        }

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

            _cache.Remove($"sysconfig_{key}");
        }

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
        }
    }
}
