using System;

namespace YZH.Entity.Admin.Platform.Base
{
    /// <summary>
    /// 删除模式枚举
    /// </summary>
    public enum DeleteMode
    {
        /// <summary>逻辑删除（默认）</summary>
        Logical = 0,
        /// <summary>物理删除</summary>
        Physical = 1,
        /// <summary>级联删除</summary>
        Cascade = 2
    }

    /// <summary>
    /// 页面级配置特性
    /// 标记在实体类上，定义前端页面的全局配置
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class PageAttribute : Attribute
    {
        /// <summary>页面唯一标识</summary>
        public string PageKey { get; set; }

        /// <summary>页面标题</summary>
        public string Title { get; set; }

        /// <summary>后端Controller名（不含Controller后缀）</summary>
        public string ControllerName { get; set; }

        /// <summary>主键字段名（默认Id）</summary>
        public string KeyField { get; set; } = "Id";

        /// <summary>默认排序字段</summary>
        public string SortField { get; set; } = "Id";

        /// <summary>默认排序方向（asc/desc）</summary>
        public string SortOrder { get; set; } = "desc";

        /// <summary>编辑弹窗宽度（px）</summary>
        public int DialogWidth { get; set; } = 800;

        /// <summary>编辑弹窗最大高度</summary>
        public string DialogMaxHeight { get; set; } = "60vh";

        /// <summary>编辑弹窗标签宽度（px）</summary>
        public int DialogLabelWidth { get; set; } = 120;

        /// <summary>搜索模式（fixed/togglable/hidden）</summary>
        public string SearchMode { get; set; } = "fixed";

        /// <summary>可见按钮列表</summary>
        public string[] VisibleButtons { get; set; } = new[] { "add", "refresh", "batchDelete" };

        /// <summary>是否显示行号</summary>
        public bool ShowRowNumber { get; set; } = false;

        /// <summary>是否显示复选框</summary>
        public bool CheckboxSelection { get; set; } = true;

        /// <summary>是否显示操作列</summary>
        public bool ShowActionColumn { get; set; } = true;
    }

    /// <summary>
    /// 表格列配置特性
    /// 标记在属性上，定义该字段在表格中的显示方式
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class EntityColumnAttribute : Attribute
    {
        /// <summary>是否在表格中显示</summary>
        public bool Visible { get; set; } = true;

        /// <summary>显示顺序（越小越靠左）</summary>
        public int Order { get; set; } = 999;

        /// <summary>列宽（px）</summary>
        public int Width { get; set; } = 120;

        /// <summary>列头标题</summary>
        public string Title { get; set; }

        /// <summary>可排序</summary>
        public bool Sortable { get; set; } = false;

        /// <summary>固定位置（left/right）</summary>
        public string Fixed { get; set; }

        /// <summary>对齐方式（left/center/right）</summary>
        public string Align { get; set; } = "left";

        /// <summary>文本溢出省略号</summary>
        public bool ShowOverflow { get; set; } = true;

        /// <summary>自定义格式化器名称</summary>
        public string Formatter { get; set; }
    }

    /// <summary>
    /// 表单字段配置特性
    /// 标记在属性上，定义该字段在编辑弹窗中的表单配置
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FormAttribute : Attribute
    {
        /// <summary>是否在表单中显示</summary>
        public bool Visible { get; set; } = true;

        /// <summary>表单标签</summary>
        public string Title { get; set; }

        /// <summary>控件类型（input/textarea/select/number/decimal/date/switch/cascader/treeSelect/file/img/slot/hidden）</summary>
        public string ControlType { get; set; } = "input";

        /// <summary>必填</summary>
        public bool Required { get; set; } = false;

        /// <summary>Grid行号（从0开始）</summary>
        public int GridRow { get; set; } = 0;

        /// <summary>Grid列号（从0开始）</summary>
        public int GridCol { get; set; } = 0;

        /// <summary>跨行数</summary>
        public int GridRowSpan { get; set; } = 1;

        /// <summary>跨列数</summary>
        public int GridColSpan { get; set; } = 1;

        /// <summary>占位文本</summary>
        public string Placeholder { get; set; }

        /// <summary>默认值</summary>
        public string DefaultValue { get; set; }

        /// <summary>只读</summary>
        public bool Readonly { get; set; } = false;

        /// <summary>禁用</summary>
        public bool Disabled { get; set; } = false;

        /// <summary>最大长度（0=不限）</summary>
        public int MaxLength { get; set; } = 0;

        /// <summary>字典编号（select/treeSelect/cascader）</summary>
        public string DataKey { get; set; }

        /// <summary>远程数据源URL</summary>
        public string RemoteUrl { get; set; }

        /// <summary>小数精度（number/decimal）</summary>
        public int Precision { get; set; } = 0;

        /// <summary>最小值</summary>
        public double? MinVal { get; set; }

        /// <summary>最大值</summary>
        public double? MaxVal { get; set; }

        /// <summary>文本域行数（textarea）</summary>
        public int TextareaRows { get; set; } = 3;
    }

    /// <summary>
    /// 搜索条件配置特性
    /// 标记在属性上，定义该字段是否作为搜索条件
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SearchAttribute : Attribute
    {
        /// <summary>作为搜索条件</summary>
        public bool IsSearch { get; set; } = true;

        /// <summary>搜索标签</summary>
        public string Title { get; set; }

        /// <summary>搜索控件类型（默认取controlType）</summary>
        public string ControlType { get; set; }

        /// <summary>搜索占位文本</summary>
        public string Placeholder { get; set; }

        /// <summary>搜索控件宽度（px）</summary>
        public int Width { get; set; } = 200;

        /// <summary>字典编号</summary>
        public string DataKey { get; set; }
    }

    /// <summary>
    /// 删除策略特性
    /// 标记在实体类上，声明式配置删除模式
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DeleteStrategyAttribute : Attribute
    {
        /// <summary>删除模式（默认Logical）</summary>
        public DeleteMode Mode { get; set; } = DeleteMode.Logical;

        /// <summary>级联删除的实体类型列表（仅Mode=Cascade时有效）</summary>
        public Type[] CascadeEntities { get; set; }

        /// <summary>是否允许强制删除有关联数据</summary>
        public bool ForceDelete { get; set; } = false;
    }

    /// <summary>
    /// 视图查询特性
    /// 标记在实体类上，声明该实体对应的查询视图类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class QueryViewAttribute : Attribute
    {
        /// <summary>视图实体类型</summary>
        public Type ViewType { get; set; }

        public QueryViewAttribute(Type viewType)
        {
            ViewType = viewType;
        }
    }

    /// <summary>
    /// 左树右表特性
    /// 标记在右表实体上，声明树数据来源和联动过滤字段
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TreeSourceAttribute : Attribute
    {
        /// <summary>树数据来源的Controller名</summary>
        public string TreeController { get; set; }

        /// <summary>右表过滤字段名</summary>
        public string FilterField { get; set; }

        /// <summary>树节点Key字段（默认Code）</summary>
        public string TreeKeyField { get; set; } = "Code";

        /// <summary>树节点显示字段（默认Name）</summary>
        public string TreeLabelField { get; set; } = "Name";
    }
}
