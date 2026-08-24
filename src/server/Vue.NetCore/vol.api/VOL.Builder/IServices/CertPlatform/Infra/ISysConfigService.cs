using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 全局系统参数服务接口。
    /// 缓存策略：首次访问全量加载 → 后续走内存 → 保存时自动刷新。
    /// </summary>
    public interface ISysConfigService : IDependency
    {
        /// <summary>获取字符串参数（从内存缓存读取，零数据库查询）</summary>
        string Get(string key);

        /// <summary>获取强类型参数（自动转换 int/bool/decimal 等）</summary>
        T Get<T>(string key);

        /// <summary>获取 int 参数（带默认值）</summary>
        int GetInt(string key, int defaultValue = 0);

        /// <summary>获取 bool 参数（带默认值）</summary>
        bool GetBool(string key, bool defaultValue = false);

        /// <summary>保存单个参数并自动刷新缓存</summary>
        Task SetAsync(string key, string value);

        /// <summary>按分类查询参数列表（管理页面用，直接查数据库）</summary>
        Task<List<SysConfigDto>> GetByCategoryAsync(string category);

        /// <summary>批量更新参数并自动刷新全量缓存</summary>
        Task UpdateBatchAsync(List<CertConfigUpdateDto> configs);

        /// <summary>手动刷新全量缓存（保存参数后自动调用，也可手动调用）</summary>
        void RefreshCache();
    }

    public class SysConfigDto
    {
        public long Id { get; set; }
        public string ConfigKey { get; set; }
        public string ConfigValue { get; set; }
        public string ConfigType { get; set; }
        public string Category { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public int IsReadonly { get; set; }
    }

    public class CertConfigUpdateDto
    {
        public string ConfigKey { get; set; }
        public string ConfigValue { get; set; }
    }
}
