using System.Collections.Generic;

namespace YZH.Entity.Admin.Platform.Base
{
    /// <summary>
    /// 页面完整UI配置
    /// 包含页面元数据和所有字段配置
    /// </summary>
    public class PageUIConfig
    {
        /// <summary>页面元数据</summary>
        public PageMeta PageMeta { get; set; } = new PageMeta();

        /// <summary>字段配置列表</summary>
        public List<FieldConfig> FieldConfigs { get; set; } = new List<FieldConfig>();
    }

    /// <summary>
    /// 页面元数据
    /// 定义页面的全局配置
    /// </summary>
    public class PageMeta
    {
        /// <summary>页面唯一标识</summary>
        public string PageKey { get; set; }

        /// <summary>页面标题</summary>
        public string PageTitle { get; set; }

        /// <summary>实体名称</summary>
        public string EntityName { get; set; }

        /// <summary>数据库表名</summary>
        public string TableName { get; set; }

        /// <summary>后端Controller名</summary>
        public string ControllerName { get; set; }

        /// <summary>主键字段名</summary>
        public string KeyField { get; set; }

        /// <summary>主键类型（number/guid/string）</summary>
        public string KeyFieldType { get; set; }

        /// <summary>默认排序字段</summary>
        public string SortField { get; set; }

        /// <summary>默认排序方向（asc/desc）</summary>
        public string SortOrder { get; set; }

        /// <summary>编辑弹窗宽度（px）</summary>
        public int DialogWidth { get; set; }

        /// <summary>编辑弹窗最大高度</summary>
        public string DialogMaxHeight { get; set; }

        /// <summary>编辑弹窗标签宽度（px）</summary>
        public int DialogLabelWidth { get; set; }

        /// <summary>搜索模式（fixed/togglable/hidden）</summary>
        public string SearchMode { get; set; }

        /// <summary>可见按钮列表</summary>
        public string[] VisibleButtons { get; set; }

        /// <summary>是否显示行号</summary>
        public bool ShowRowNumber { get; set; }

        /// <summary>是否显示复选框</summary>
        public bool CheckboxSelection { get; set; }

        /// <summary>是否显示操作列</summary>
        public bool ShowActionColumn { get; set; }
    }

    /// <summary>
    /// 字段配置
    /// 定义单个字段在表格、表单、搜索中的配置
    /// </summary>
    public class FieldConfig
    {
        // ===== 标识 =====
        /// <summary>字段名（与实体属性名一致）</summary>
        public string FieldName { get; set; }

        /// <summary>字段别名（默认同FieldName）</summary>
        public string FieldAlias { get; set; }

        /// <summary>字段类型（string/number/boolean/date）</summary>
        public string FieldType { get; set; }

        /// <summary>是否是主键字段</summary>
        public bool IsKey { get; set; }

        // ===== 表格列配置 =====
        /// <summary>表格是否显示</summary>
        public bool XsFlag { get; set; }

        /// <summary>列显示序号（越小越靠左）</summary>
        public int ColumnSxh { get; set; }

        /// <summary>列头标题</summary>
        public string ColumnTitle { get; set; }

        /// <summary>列宽（px）</summary>
        public int ColumnWidth { get; set; }

        /// <summary>列固定位置（left/right）</summary>
        public string ColumnFixed { get; set; }

        /// <summary>可排序</summary>
        public bool Sortable { get; set; }

        /// <summary>对齐方式（left/center/right）</summary>
        public string Align { get; set; }

        /// <summary>文本溢出省略号</summary>
        public bool ShowOverflow { get; set; }

        /// <summary>自定义格式化器名称</summary>
        public string ColumnFormatter { get; set; }

        // ===== 表单字段配置 =====
        /// <summary>是否保存到数据库（true=保存，false=视图字段不保存）</summary>
        public bool BcFlag { get; set; }

        /// <summary>表单标签</summary>
        public string FormTitle { get; set; }

        /// <summary>控件类型（input/textarea/select/number/decimal/date/switch/cascader/treeSelect/file/img/slot/hidden）</summary>
        public string ControlType { get; set; }

        /// <summary>Grid行号（从0开始）</summary>
        public int GridRow { get; set; }

        /// <summary>Grid列号（从0开始）</summary>
        public int GridCol { get; set; }

        /// <summary>跨行数</summary>
        public int GridRowSpan { get; set; }

        /// <summary>跨列数</summary>
        public int GridColSpan { get; set; }

        /// <summary>必填</summary>
        public bool Required { get; set; }

        /// <summary>最大长度（0=不限）</summary>
        public int MaxLength { get; set; }

        /// <summary>占位文本</summary>
        public string Placeholder { get; set; }

        /// <summary>默认值</summary>
        public string DefaultValue { get; set; }

        /// <summary>只读</summary>
        public bool Readonly { get; set; }

        /// <summary>禁用</summary>
        public bool Disabled { get; set; }

        /// <summary>字典编号（select/treeSelect/cascader）</summary>
        public string DataKey { get; set; }

        /// <summary>远程数据源URL</summary>
        public string RemoteUrl { get; set; }

        /// <summary>小数精度（number/decimal）</summary>
        public int Precision { get; set; }

        /// <summary>最小值</summary>
        public double? MinVal { get; set; }

        /// <summary>最大值</summary>
        public double? MaxVal { get; set; }

        /// <summary>文本域行数（textarea）</summary>
        public int TextareaRows { get; set; }

        // ===== 搜索条件配置 =====
        /// <summary>作为搜索条件</summary>
        public bool SearchFlag { get; set; }

        /// <summary>搜索标签</summary>
        public string SearchTitle { get; set; }

        /// <summary>搜索占位文本</summary>
        public string SearchPlaceholder { get; set; }

        /// <summary>搜索控件类型</summary>
        public string SearchControlType { get; set; }

        /// <summary>搜索控件宽度（px）</summary>
        public int SearchWidth { get; set; }
    }
}
