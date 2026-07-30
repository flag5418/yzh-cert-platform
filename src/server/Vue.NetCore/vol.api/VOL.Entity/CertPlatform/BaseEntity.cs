using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform
{
    /// <summary>
    /// 实体基类 - 所有业务实体必须继承此类
    /// 
    /// <para>
    /// 设计规范（V2.1）：
    /// - Id: 自增主键，用于数据库内部索引
    /// - Code: GUID 全局唯一编码，用于表间关联和数据迁移
    /// - 审计字段：记录创建/更新/删除的人和時間
    /// - 逻辑删除：通过 DeleteBy + DeleteTime 实现，不物理删除数据
    /// </para>
    /// 
    /// <para>
    /// 使用规则：
    /// 1. 所有业务实体必须继承此类
    /// 2. Code 在创建时自动生成 GUID，也支持手动传入（数据迁移场景）
    /// 3. CreateBy 默认填充当前登录用户 ID
    /// 4. CreateTime 默认当前时间
    /// 5. 更新操作时自动填充 UpdateBy 和 UpdateTime
    /// 6. 逻辑删除时填充 DeleteBy 和 DeleteTime
    /// </para>
    /// </summary>
    public class BaseEntity
    {
        /// <summary>
        /// 主键ID（自增）
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        /// <summary>
        /// 全局唯一编码（GUID），用于表间关联
        /// <para>创建时自动生成，也支持手动传入</para>
        /// </summary>
        [Required]
        [StringLength(36)]
        [Column("code")]
        public string Code { get; set; }

        /// <summary>
        /// 创建人ID（默认当前登录用户）
        /// <para>关联 Sys_User.Id</para>
        /// </summary>
        [Column("create_by")]
        public long? CreateBy { get; set; }

        /// <summary>
        /// 创建时间（默认当前时间）
        /// </summary>
        [Column("create_time")]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新人ID
        /// </summary>
        [Column("update_by")]
        public long? UpdateBy { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [Column("update_time")]
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 删除人ID（逻辑删除时使用）
        /// </summary>
        [Column("delete_by")]
        public long? DeleteBy { get; set; }

        /// <summary>
        /// 删除时间（逻辑删除时使用）
        /// </summary>
        [Column("delete_time")]
        public DateTime? DeleteTime { get; set; }

        #region 辅助方法

        /// <summary>
        /// 自动生成 Code（如果为空）
        /// </summary>
        public void GenerateCode()
        {
            if (string.IsNullOrWhiteSpace(Code))
            {
                Code = Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// 设置创建信息
        /// </summary>
        /// <param name="userId">当前用户ID</param>
        public void SetCreateInfo(long? userId)
        {
            CreateBy = userId;
            CreateTime = DateTime.Now;
            GenerateCode();
        }

        /// <summary>
        /// 设置更新信息
        /// </summary>
        /// <param name="userId">当前用户ID</param>
        public void SetUpdateInfo(long? userId)
        {
            UpdateBy = userId;
            UpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 设置删除信息（逻辑删除）
        /// </summary>
        /// <param name="userId">当前用户ID</param>
        public void SetDeleteInfo(long? userId)
        {
            DeleteBy = userId;
            DeleteTime = DateTime.Now;
        }

        /// <summary>
        /// 是否已逻辑删除
        /// </summary>
        [NotMapped]
        public bool IsDeleted => DeleteBy.HasValue && DeleteTime.HasValue;

        #endregion
    }
}
