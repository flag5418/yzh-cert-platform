namespace YZH.Core.Stand.Enums;

/// <summary>
///     控件/列类型枚举
///     对标老YZH架构的 ControlType
///     定义前端根据 EntityConfig 动态渲染时使用的控件类型
/// </summary>
public enum ControlType
{
    /// <summary>文本输入框</summary>
    TextBox = 0,

    /// <summary>拼音输入</summary>
    PinYin = 1,

    /// <summary>日期选择器</summary>
    DatePicker = 2,

    /// <summary>只读文本</summary>
    Label = 3,

    /// <summary>密码输入框</summary>
    PasswordBox = 4,

    /// <summary>弹窗选择</summary>
    ButtonEdit = 5,

    /// <summary>图像显示</summary>
    Image = 6,

    /// <summary>视频显示</summary>
    Video = 7,

    /// <summary>音频播放</summary>
    Music = 8,

    /// <summary>附件上传</summary>
    Annex = 9,

    /// <summary>多行文本</summary>
    Memo = 10,

    /// <summary>数字输入</summary>
    Decimal = 11,

    /// <summary>单选框</summary>
    CheckBox = 12,

    /// <summary>下拉选择</summary>
    ComboBox = 13,

    /// <summary>级联选择</summary>
    Cascader = 14,

    /// <summary>其他类型</summary>
    Other = 15,

    /// <summary>按钮</summary>
    Button = 16,

    /// <summary>开关</summary>
    Switch = 17,

    /// <summary>时间日期选择</summary>
    DateTimePicker = 18,

    /// <summary>颜色选择</summary>
    ColorPicker = 19,

    /// <summary>滑块</summary>
    Slider = 20,

    /// <summary>树形选择</summary>
    TreeSelect = 21,

    /// <summary>
    ///     自定义插槽列（★ 2026-09-30 新增）。
    ///     <para><b>为什么必须存在</b>：前端 <c>entityAdapters.ts</c> 的 <c>toTableColumns</c> 用
    ///     <c>c.Type === 'CustomSlot'</c> 判定该列走 <c>&lt;slot name="column-{FieldName}"&gt;</c>
    ///     （进度条 / 状态标签等无法用纯文本渲染的列）。此前该值<b>只存在于前端约定、
    ///     后端枚举里没有</b> ⇒ <c>JsonStringEnumConverter</c> 反序列化抛异常 ⇒ 被
    ///     <c>EntityConfigHelper.LoadAndParse</c> 的 <c>catch</c> 静默吞掉 ⇒
    ///     <b>整个 EntityConfig 变成空配置（Columns=[]）</b>，页面「有数据行、一列都不显示、零报错」。
    ///     </para>
    ///     <para>⚠️ 新增任何 <c>Type</c> 值都必须<b>同时</b>加在这里，否则就是整份配置静默失效。</para>
    /// </summary>
    CustomSlot = 22
}
