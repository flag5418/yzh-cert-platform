using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface ISysConfigService : IDependency
    {
        string Get(string key);
        T Get<T>(string key);
        int GetInt(string key, int defaultValue = 0);
        bool GetBool(string key, bool defaultValue = false);
        Task SetAsync(string key, string value);
        Task<List<CertPlatform.SysConfigDto>> GetByCategoryAsync(string category);
        Task UpdateBatchAsync(List<CertConfigUpdateDto> configs);
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
