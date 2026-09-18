using System;
using SqlSugar;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Wf
{
    /// <summary>
    /// Prompt 模板实体（映射 wf_prompt_template）
    /// <para>迁移说明：原文件为旧架构 DataAnnotations 映射（[Table]/[Column]），</para>
    /// <para>新架构 ORM（IDbOrm/SqlSugar）只认 [SugarTable]/[SugarColumn]，故改写映射方式，</para>
    /// <para>命名空间与属性名保持不变（对齐 Entities/Dir 与 Entities/Wf 的既有惯例）。</para>
    /// <para>注意：表无 IsValid 列（生效标志是 is_active），因此本实体不声明 IsValid，</para>
    /// <para>避免框架自动追加 `IsValid = 1` 过滤导致 Unknown column。</para>
    /// </summary>
    [SugarTable("wf_prompt_template")]
    public class PromptTemplate : BaseEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "code", Length = 100)]
        public new string? Code { get; set; }

        [SugarColumn(ColumnName = "org_code", Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        [SugarColumn(ColumnName = "creator", Length = 50, IsNullable = true)]
        public new string? Creator { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = true)]
        public new DateTime? CreateDate { get; set; }

        [SugarColumn(ColumnName = "modifier", Length = 50, IsNullable = true)]
        public new string? Modifier { get; set; }

        [SugarColumn(ColumnName = "modify_date", IsNullable = true)]
        public new DateTime? ModifyDate { get; set; }

        [SugarColumn(ColumnName = "deleter", Length = 50, IsNullable = true)]
        public new string? Deleter { get; set; }

        [SugarColumn(ColumnName = "delete_time", IsNullable = true)]
        public new DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "status", Length = 50, IsNullable = true)]
        public new string? Status { get; set; }

        [SugarColumn(ColumnName = "enable")]
        public new bool Enable { get; set; } = true;

        [SugarColumn(ColumnName = "sort")]
        public new int Sort { get; set; }

        [SugarColumn(ColumnName = "remark", Length = 500, IsNullable = true)]
        public new string? Remark { get; set; }

        [SugarColumn(ColumnName = "prompt_code", Length = 100)]
        public string PromptCode { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "prompt_name", Length = 200)]
        public string PromptName { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "prompt_type", Length = 50)]
        public string PromptType { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "skill_target", Length = 50, IsNullable = true)]
        public string? SkillTarget { get; set; }

        [SugarColumn(ColumnName = "template", ColumnDataType = "mediumtext", IsNullable = true)]
        public string? Template { get; set; }

        [SugarColumn(ColumnName = "description", ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        [SugarColumn(ColumnName = "version")]
        public int Version { get; set; } = 1;

        [SugarColumn(ColumnName = "is_active")]
        public bool IsActive { get; set; } = true;

        [SugarColumn(ColumnName = "last_test_result", ColumnDataType = "text", IsNullable = true)]
        public string? LastTestResult { get; set; }
    }
}
