
using System.Collections;
using System.Collections.Generic;

namespace VOL.Entity.DomainModels
{
    public class SaveModel
    {
        public string TableName { get; set; }
        public Dictionary<string, object> MainData { get; set; }
        public List<Dictionary<string, object>> DetailData { get; set; }
        public List<object> DelKeys { get; set; }

        /// <summary>
        /// 从前台传入的其他参数(自定义扩展可以使用)
        /// </summary>
        public object Extra { get; set; }

        /// <summary>
        /// 一对多明细
        /// </summary>
        public List<DetailInfo> Details { get; set; }

        public List<SubDelInfo> SubDelInfo { get; set; }


        public bool IsFlow { get; set; }

        public string DataVersionField { get; set; }
        public string DataVersionValue { get; set; }
    }

    public class DetailInfo
    {
        public string Table { get; set; }

        public List<Dictionary<string, object>> Data { get; set; }
        public List<object> DelKeys { get; set; }
    }

    public class SubDelInfo
    {
        public bool IsProescc { get; set; }
        public string Table { get; set; }
        public List<object> DelKeys { get; set; }
    }
}
