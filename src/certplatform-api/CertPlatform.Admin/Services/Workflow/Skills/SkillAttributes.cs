using System;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// Skill 类级特性（必填）。声明 Skill 的编码、名称、返回类型、说明。
    /// <para>移植自：旧 YZH.Core.Workflow.SkillAttributes.cs（结构零改动，仅 namespace 调整）</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SkillAttribute : Attribute
    {
        /// <summary>Skill 编码，如 "compare"</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>中文名，如 "值比较"</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>result 的类型：string / number / date / boolean / json</summary>
        public string ReturnType { get; set; } = "json";

        /// <summary>作用说明</summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Skill 参数级特性（可选）。用于补充参数的中文描述、绑定模式、字典来源。
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class SkillParamAttribute : Attribute
    {
        /// <summary>参数中文描述</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 绑定模式（默认 LinkOrConstant）
        /// </summary>
        public SkillParamBindMode BindMode { get; set; } = SkillParamBindMode.LinkOrConstant;

        /// <summary>
        /// 字典编码（BindMode=Enum 时必填）。
        /// 前端据此加载选项列表（旧 Sys_Dictionary.DicNo 对应物）。
        /// </summary>
        public string? EnumSource { get; set; }
    }

    /// <summary>
    /// Skill 参数绑定模式枚举。
    /// </summary>
    public enum SkillParamBindMode
    {
        /// <summary>仅连线：参数值必须来自上游节点输出</summary>
        Link = 0,

        /// <summary>连线或常量：画布上可切换为手动输入（默认）</summary>
        LinkOrConstant = 1,

        /// <summary>字典选择：从字典服务按 EnumSource 加载选项</summary>
        Enum = 2
    }

    /// <summary>
    /// 标记依赖注入参数。反射时跳过此参数，运行时从 DI 容器获取。
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class FromServiceAttribute : Attribute { }
}
