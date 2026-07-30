using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOL.Entity.DomainModels
{
    public class PageDataOptions
    {
        public int Page { get; set; }
        public int Rows { get; set; }
        public int Total { get; set; }
        public string TableName { get; set; }

        /// <summary>
        /// 三级明细表(2023.09.18)
        /// </summary>
        public string DetailTable { get; set; }
        public string Sort { get; set; }
        /// <summary>
        /// 排序方式
        /// </summary>
        public string Order { get; set; }
        public string Wheres { get; set; }
        public bool Export { get; set; }
        public object Value { get; set; }


        /// <summary>
        /// 查询条件
        /// </summary>
        public List<SearchParameters> Filter { get; set; }
        /// <summary>
        ///  2024.02.03增加导出列表与界面显示字段一致
        /// </summary>
        public string[] Columns { get; set; }

        public Dictionary<string, string> Summary { get; set; }
    }
    public class SearchParameters
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string DisplayType { get; set; }

        public List<string> Fields { get; set; }

        public string Group { get; set; }

        public bool Or { get; set; }
    }

}