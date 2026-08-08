using System.Collections.Generic;
using System.Threading.Tasks;

namespace YZH.CertPlatform.Services
{
    /// <summary>
    /// YZH V3.0 页面 UI 配置服务接口
    /// <para>提供数据库驱动的 UI 配置查询，供 Controller 层调用</para>
    /// </summary>
    public interface IYzhPageConfigService
    {
        /// <summary>
        /// 根据 pageKey 获取完整的页面配置（页面级 + 字段级）
        /// </summary>
        Task<PageConfigResult> GetPageConfigAsync(string pageKey);

        /// <summary>
        /// 获取所有可用的页面配置列表（用于配置管理页面）
        /// </summary>
        Task<List<PageConfigSummary>> GetAllPageConfigsAsync();

        /// <summary>
        /// 批量获取所有页面的完整配置（用于前端启动时全量加载）
        /// 返回格式：{ version, configs: { pageKey: { pageMeta, fieldConfigs } } }
        /// </summary>
        Task<AllConfigsResult> GetAllConfigsFullAsync();
    }

    /// <summary>
    /// 页面配置完整结果（包含页面元数据和字段配置）
    /// </summary>
    public class PageConfigResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object PageMeta { get; set; }
        public List<FieldConfigDto> FieldConfigs { get; set; } = new();
    }

    /// <summary>
    /// 页面配置摘要（用于列表展示）
    /// </summary>
    public class PageConfigSummary
    {
        public long Id { get; set; }
        public string PageKey { get; set; }
        public string PageTitle { get; set; }
        public string EntityName { get; set; }
        public string ControllerName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

        /// <summary>
        /// 字段配置 DTO（传输给前端的格式）
        /// </summary>
        public class FieldConfigDto
        {
            public string FieldName { get; set; }
            public string FieldAlias { get; set; }

            // 表格列
            public bool XsFlag { get; set; }
            public int ColumnSxh { get; set; }
            public string ColumnTitle { get; set; }
            public int ColumnWidth { get; set; }
            public string ColumnFixed { get; set; }
            public bool Sortable { get; set; }
            public string ColumnFormatter { get; set; }
            public bool ShowOverflow { get; set; }
            public string Align { get; set; }

            // 弹窗表单
            public bool BcFlag { get; set; }
            public string FormTitle { get; set; }
            public string ControlType { get; set; }
            public int GridRow { get; set; }
            public int GridCol { get; set; }
            public int GridRowSpan { get; set; }
            public int GridColSpan { get; set; }
            public bool Required { get; set; }
            public int MaxLength { get; set; }
            public string Placeholder { get; set; }
            public string DefaultValue { get; set; }
            public bool Readonly { get; set; }
            public bool Disabled { get; set; }
            public int? Precision { get; set; }
            public decimal? MinVal { get; set; }
            public decimal? MaxVal { get; set; }
            public int TextareaRows { get; set; }

            // 数据源
            public string DataKey { get; set; }
            public string RemoteUrl { get; set; }

            // 业务控制
            public int GroupIndex { get; set; }

            // 搜索区
            public bool SearchFlag { get; set; }
            public string SearchTitle { get; set; }
            public string SearchPlaceholder { get; set; }
            public string SearchControlType { get; set; }
            public int SearchWidth { get; set; }
        }

        /// <summary>
        /// 全量配置结果（启动时批量加载）
        /// </summary>
        public class AllConfigsResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            /// <summary>配置版本号（任一字段变更时递增，用于前端缓存失效判断）</summary>
            public string Version { get; set; }
            /// <summary>各页面完整配置字典 key=pageKey</summary>
            public Dictionary<string, PageConfigData> Configs { get; set; } = new();
        }

        /// <summary>
        /// 单个页面的配置数据（pageMeta + fieldConfigs）
        /// </summary>
        public class PageConfigData
        {
            public object PageMeta { get; set; }
            public List<FieldConfigDto> FieldConfigs { get; set; } = new();
        }
    }
