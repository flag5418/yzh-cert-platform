using System.Collections.Generic;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// Skill 详情/保存 DTO（V1.4 精简版）
    /// <para>移除了引擎内部字段：SideEffect/OutputStrict/ReturnType/SkillPrompt/SkillType</para>
    /// <para>这些字段由 C# 代码 SkillBase 子类声明，不需要管理员在页面上维护</para>
    /// </summary>
    public class SkillDetailDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string SkillCode { get; set; } = string.Empty;
        public string SkillName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string Version { get; set; } = "1.0";
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string Remark { get; set; } = string.Empty;

        public List<WfSkillInput> Inputs { get; set; } = new();
        public List<WfSkillOutput> Outputs { get; set; } = new();
        public WfSkillReflection? Reflection { get; set; }
    }
}
