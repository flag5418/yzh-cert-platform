namespace YZH.Core.Stand.Enums;

/// <summary>
///     控件/列类型枚举
///     对标老YZH架构的 ControlType
///     定义前端根据 GridConfig 动态渲染时使用的控件类型
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
    TreeSelect = 21
}
