using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using SqlSugar;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Stand.Models.Entity;

/// <summary>
///     所有业务实体的基类（YZH 架构铁律）
///
///     架构设计原则：
///     ┌─────────────────────────────────────────────────────────────────────┐
///     │  1. 强制字段（基类直接定义，不带 IsIgnore，所有表必须有）：            │
///     │     - Id            自增主键（bigint，物理自增）                     │
///     │     - Code          业务编码（GUID，唯一标识）                       │
///     │     - CreateTime    创建时间                                         │
///     │     - CreateBy      创建人 Code                                      │
///     │     - UpdateTime    更新时间                                         │
///     │     - UpdateBy      更新人 Code                                      │
///     │                                                                     │
///     │  2. 可选接口（按需子类实现，表有对应列才继承）：                       │
///     │     - ISoftDelete    软删除（IsDeleted/DeleteBy/DeleteTime）          │
///     │     - IIsValid       有效性（IsValid）                                │
///     │     - ITreeEntity    树形结构（ParentCode/Name）                      │
///     │                                                                     │
///     │  3. 架构层可依赖强制字段进行自动管理：                                │
///     │     - AddCore() 中自动填充 Code/CreateTime/CreateBy                   │
///     │     - UpdateCore() 中自动填充 UpdateTime/UpdateBy                     │
///     │     - 缺 Code 列时基类会报错（强制约束）                              │
///     └─────────────────────────────────────────────────────────────────────┘
///
///     注意：此类不引用 ORM，保持 Stand 纯净；字段映射由子类或 ORM 配置决定
/// </summary>
public abstract class BaseEntity : INotifyPropertyChanged
{
    // ── 强制字段（所有表必须存在，不带 IsIgnore） ──

    /// <summary>自增主键（物理自增 bigint，所有表必须存在）</summary>
    [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>业务编码（GUID 业务键，新增时框架自动生成，不可修改）</summary>
    [SugarColumn(ColumnName = "Code")]
    [StringLength(64)]
    public string? Code { get; set; }

    /// <summary>创建时间（创建时赋值，后续不变）</summary>
    [SugarColumn(ColumnName = "CreateTime")]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建人 Code（存储用户业务编码，非自增 ID）</summary>
    [SugarColumn(ColumnName = "CreateBy")]
    [StringLength(64)]
    public string? CreateBy { get; set; }

    /// <summary>更新时间（仅更新时赋值，新建时为 null）</summary>
    [SugarColumn(ColumnName = "UpdateTime")]
    public DateTime? UpdateTime { get; set; }

    /// <summary>更新人 Code（仅更新时赋值，存储用户业务编码）</summary>
    [SugarColumn(ColumnName = "UpdateBy")]
    [StringLength(64)]
    public string? UpdateBy { get; set; }

    // ── 前端标记（不持久化） ──

    /// <summary>前端选中标记（不持久化到数据库）</summary>
    [SugarColumn(IsIgnore = true)]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CheckFlag { get; set; }

    /// <summary>前端删除标记（不持久化到数据库）</summary>
    [SugarColumn(IsIgnore = true)]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool DeleteFlag { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
