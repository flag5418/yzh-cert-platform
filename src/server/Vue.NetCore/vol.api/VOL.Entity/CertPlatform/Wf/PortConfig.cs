using System.Text.Json.Serialization;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// 端口声明模型 — 节点的输入/输出端口定义
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §2.5</para>
    /// </summary>
    public class PortConfig
    {
        /// <summary>端口名称（inputs 的 key）</summary>
        public string Name { get; set; }

        /// <summary>显示名称</summary>
        public string Label { get; set; }

        /// <summary>数据类型：string / number / boolean / json / signal</summary>
        public string Type { get; set; } = "string";

        /// <summary>绑定模式：Link（仅连线）/ LinkOrConstant（可连线可输入）/ Enum（字典选择）</summary>
        public string BindMode { get; set; } = "Link";

        /// <summary>是否必填</summary>
        public bool Required { get; set; }

        /// <summary>最大入边数（默认 1，end 节点的 result 端口 = 999）</summary>
        [JsonPropertyName("maxIn")]
        public int MaxIn { get; set; } = 1;

        /// <summary>面板显示：visible / hidden</summary>
        public string Display { get; set; } = "visible";

        /// <summary>端口角色：data（数据端口）/ anchor（控制流锚点，如 branch 的 success/failure）</summary>
        public string Role { get; set; } = "data";

        /// <summary>字典来源（仅 Enum 模式）</summary>
        [JsonPropertyName("enumSource")]
        public string EnumSource { get; set; }

        /// <summary>默认值</summary>
        [JsonPropertyName("defaultValue")]
        public object DefaultValue { get; set; }
    }
}
