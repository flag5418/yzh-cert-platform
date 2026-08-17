using System.Collections.Generic;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// Skill 详情/保存 DTO：主表 + 输入模板 + 输出契约 + 反射 + API
    /// 前端编辑页整体读写，Service 保存时主子表事务处理
    /// </summary>
    public class SkillDetailDto
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string SkillCode { get; set; }
        public string SkillName { get; set; }
        public string SkillType { get; set; }
        public string Category { get; set; }
        public bool SideEffect { get; set; }
        public string Description { get; set; }
        public string SkillPrompt { get; set; }
        public bool IsActive { get; set; } = true;
        public bool OutputStrict { get; set; } = true;
        public string ReturnType { get; set; } = "json";
        public string Version { get; set; } = "1.0";
        public string Icon { get; set; }
        public string Color { get; set; }
        public int SortOrder { get; set; }
        public string Remark { get; set; }

        public List<WfSkillInput> Inputs { get; set; } = new();
        public List<WfSkillOutput> Outputs { get; set; } = new();
        public WfSkillReflection Reflection { get; set; }
        public WfSkillApi Api { get; set; }
    }
}
