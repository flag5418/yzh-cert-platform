using System;

namespace YZH.Entity.DomainModels
{
    /// <summary>
    /// 左树右表实体接口约定
    /// 适用于组织机构、标准目录等树形结构场景
    /// </summary>
    public interface ITreeTableEntity
    {
        /// <summary>主键</summary>
        Guid Id { get; set; }

        /// <summary>显示名称</summary>
        string Name { get; set; }

        /// <summary>编码</summary>
        string Code { get; set; }

        /// <summary>父节点 ID</summary>
        Guid? ParentId { get; set; }

        /// <summary>是否启用</summary>
        int? Enable { get; set; }

        /// <summary>备注</summary>
        string Remark { get; set; }

        /// <summary>创建人</summary>
        string Creator { get; set; }

        /// <summary>创建时间</summary>
        DateTime? CreateDate { get; set; }

        /// <summary>修改人</summary>
        string Modifier { get; set; }

        /// <summary>修改时间</summary>
        DateTime? ModifyDate { get; set; }
    }
}
