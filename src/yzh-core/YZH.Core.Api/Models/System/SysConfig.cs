using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.System;

/// <summary>
/// 全局系统参数配置实体（框架层底座）
/// <para>表名：cert_sys_config</para>
/// <para>列名规范：PascalCase（与 BaseEntity 标准属性名完全一致，无需映射）</para>
/// <para>唯一性由 ConfigController.OnBeforeAdd/OnBeforeUpdate 钩子校验（配置键 ConfigKey）</para>
/// </summary>
[SugarTable("cert_sys_config")]
public class SysConfig : BaseEntity, ISoftDelete, IIsValid
{
    /// <summary>有效标志（1=有效，0=无效）</summary>
    public int IsValid { get; set; } = 1;

    /// <summary>软删除标记（false=正常，true=已删除）</summary>
    public bool IsDeleted { get; set; }

    /// <summary>删除人 Code（仅软删除时赋值）</summary>
    public string? DeleteBy { get; set; }

    /// <summary>删除时间（仅软删除时赋值）</summary>
    public DateTime? DeleteTime { get; set; }

    /// <summary>排序号</summary>
    public int Sort { get; set; }

    /// <summary>备注</summary>
    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>配置键（唯一键）</summary>
    [Required]
    [StringLength(100)]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>
    ///     配置值
    ///     <para>⚠️ <b>2026-10-09 修</b>：原为 <c>[StringLength(4000)]</c>，而 DB 列早已被
    ///     <c>scripts/db/20261007_enterprise_normalize_enhance_V1.sql</c> 改成 <c>text</c>
    ///     （<c>59</c> §4：<b>视觉 JSON 天然长</b>）。但本框架<b>开启了自动模型校验</b>
    ///     （<c>Program.cs</c> 明确 <c>SuppressModelStateInvalidFilter</c> 仍为 false）⇒
    ///     超过 4000 字符的 <c>ConfigValue</c> 在进 DB 之前就被拒 ⇒ <b>列加宽了却到不了</b>。
    ///     取值 <c>16000</c>：<c>text</c> 上限 65535 <b>字节</b>，按最坏 4 字节/字符留余量。</para>
    /// </summary>
    [StringLength(16000)]
    public string? ConfigValue { get; set; }

    /// <summary>配置类型（string/int/bool/json）</summary>
    [StringLength(20)]
    public string ConfigType { get; set; } = "string";

    /// <summary>分类</summary>
    [StringLength(50)]
    public string? Category { get; set; }

    /// <summary>显示名称（前端展示用）</summary>
    [StringLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>描述</summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>是否只读（管理员不可修改）</summary>
    public int IsReadonly { get; set; }
}
