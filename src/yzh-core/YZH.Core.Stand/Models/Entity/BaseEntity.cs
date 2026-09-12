using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using SqlSugar;

namespace YZH.Core.Stand.Models.Entity;

/// <summary>
///     所有业务实体的基类
///     对标老YZH架构的 BaseEntity，提供统一的标识字段和变更通知
///     注意：此类不引用EF Core，保持Stand层纯净
///     
///     审计字段约定：
///     - CreateTime/CreateBy：创建时赋值，后续不变
///     - UpdateTime/UpdateBy：仅更新时赋值，新建时为 null
///     - DeleteTime/DeleteBy：仅软删除时赋值（硬删除直接移除记录）
///     
///     删除策略通过 [YZHDeleteStrategy] 特性控制
///     缓存策略通过 [YZHCacheEntity] 特性控制（默认不缓存）
/// </summary>
public abstract class BaseEntity : INotifyPropertyChanged
{
    /// <summary>主键标识（默认忽略，子类通过 [SugarColumn] 显式映射 DB 列名）</summary>
    [SugarColumn(IsIgnore = true)]
    [Key]
    [StringLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>业务编码 (GUID 业务键)</summary>
    [SugarColumn(IsIgnore = true)]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>创建时间（创建时赋值）</summary>
    [SugarColumn(IsIgnore = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建人 Code（存储用户业务编码，非自增 ID）</summary>
    [SugarColumn(IsIgnore = true)]
    [StringLength(64)]
    public string? CreateBy { get; set; }

    /// <summary>更新时间（仅更新时赋值，新建时为 null）</summary>
    [SugarColumn(IsIgnore = true)]
    public DateTime? UpdateTime { get; set; }

    /// <summary>更新人 Code（存储用户业务编码，非自增 ID）</summary>
    [SugarColumn(IsIgnore = true)]
    [StringLength(64)]
    public string? UpdateBy { get; set; }

    /// <summary>删除时间（仅软删除时赋值）</summary>
    [SugarColumn(IsIgnore = true)]
    public DateTime? DeleteTime { get; set; }

    /// <summary>删除人 Code（仅软删除时赋值，存储用户业务编码）</summary>
    [SugarColumn(IsIgnore = true)]
    [StringLength(64)]
    public string? DeleteBy { get; set; }

    /// <summary>是否删除标记（软删除标志，默认 false）</summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsDeleted { get; set; }

    /// <summary>有效标志（1=有效，0=无效，默认有效）</summary>
    [SugarColumn(IsIgnore = true)]
    public int IsValid { get; set; } = 1;

    /// <summary>前端选中标记（不持久化）</summary>
    [SugarColumn(IsIgnore = true)]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CheckFlag { get; set; }

    /// <summary>前端删除标记（不持久化）</summary>
    [SugarColumn(IsIgnore = true)]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool DeleteFlag { get; set; }

    /// <summary>数据版本号（乐观锁，DB层映射）</summary>
    [SugarColumn(IsIgnore = true)]
    public byte[]? RowVersion { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
