using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace YZH.Core.Stand.Models;

/// <summary>
///     所有业务实体的基类
///     对标老YZH架构的 BaseEntity，提供统一的标识字段和变更通知
///     注意：此类不引用EF Core，保持Stand层纯净
/// </summary>
public abstract class BaseEntity : INotifyPropertyChanged
{
    /// <summary>主键标识</summary>
    [Key]
    [StringLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建人</summary>
    [StringLength(64)]
    public string? CreateBy { get; set; }

    /// <summary>更新人</summary>
    [StringLength(64)]
    public string? UpdateBy { get; set; }

    /// <summary>前端选中标记（不持久化）</summary>
    public bool CheckFlag { get; set; }

    /// <summary>前端删除标记（不持久化）</summary>
    public bool DeleteFlag { get; set; }

    /// <summary>数据版本号（乐观锁，DB层映射）</summary>
    public byte[]? RowVersion { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
