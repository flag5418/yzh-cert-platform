using System;
using System.Collections.Generic;

namespace YZH.System
{
    /// <summary>
    /// 统一 API 返回体（兼容 YZHBaseApiClient 解析：status / rows / total / data / msg）
    /// </summary>
    public class YzhApiResult
    {
        [Newtonsoft.Json.JsonProperty("status")]
        public bool Status { get; set; } = true;
        [Newtonsoft.Json.JsonProperty("msg")]
        public string Msg { get; set; } = "";
        [Newtonsoft.Json.JsonProperty("data")]
        public object Data { get; set; }
        [Newtonsoft.Json.JsonProperty("rows")]
        public List<object> Rows { get; set; }
        [Newtonsoft.Json.JsonProperty("total")]
        public int Total { get; set; }
    }

    /// <summary>
    /// GetPageData 查询参数（与前端 YZHBaseApiClient 约定一致）
    /// </summary>
    public class PageQuery
    {
        public int page { get; set; } = 1;
        public int rows { get; set; } = 20;
        public string sort { get; set; } = "";
        public string order { get; set; } = "desc";
        public List<FilterItem> filter { get; set; }
    }

    /// <summary>
    /// 过滤项（Name=字段, Value=值, DisplayType: == / contains / in）
    /// </summary>
    public class FilterItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string DisplayType { get; set; } = "contains";
    }

    /// <summary>
    /// 保存模型（Add/Update 请求体，兼容 VOL SaveModel 信封）
    /// </summary>
    public class SaveModel<T>
    {
        public string TableName { get; set; }
        public T MainData { get; set; }
        public object DetailData { get; set; }
        public List<object> DelKeys { get; set; }
    }
}
