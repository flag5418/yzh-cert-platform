using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;
using YZH.Core.Audit;
using YZH.Core.CodeRule;
using YZH.Core.DeleteStrategy;

namespace YZH.Core.Entities
{
    /// <summary>
    /// YZH 实体基类 - 在 Vol.BaseEntity 基础之上追加审计和业务字段
    /// 
    /// 设计原则：
    /// - 统一字段命名：Create/Modify/Delete 前缀，避免 CreatedAt/UpdatedAt 混用
    /// - Code 作为业务编码，与数据库主键 Id 分离
    /// - Enable 字段统一处理逻辑删除（true = 启用, false = 禁用/已删除）
    /// - Sort 字段默认 0，用于前端排序
    /// 
    /// 自动填充规则（由框架接管，业务代码无需关心）：
    /// - CreateID / Creator / CreateDate：新增时自动填充
    /// - ModifyID / Modifier / ModifyDate：更新时自动填充
    /// - DeleteID / Deleter / DeleteTime：逻辑删除时自动填充
    /// - OrgCode：不在基类定义，由需要多租户隔离的子类自行声明
    /// 
    /// 状态：[DONE] Phase 1 完整实现（12 字段 + 辅助方法）
    /// </summary>
    public class YZHBaseEntity : BaseEntity
    {
        #region 业务编码

        /// <summary>
        /// 业务编码（由 YZHCodeRule 生成，非主键，用于业务标识）
        /// 示例：CB001-2026001（机构编号-年份流水号）
        /// </summary>
        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string Code { get; set; }

        #endregion

        #region 审计字段 - 创建信息

        /// <summary>创建人 ID（int 类型，对应 Sys_User.Id）</summary>
        public int? CreateID { get; set; }

        /// <summary>创建人姓名</summary>
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string Creator { get; set; }

        /// <summary>创建时间</summary>
        [Column(TypeName = "datetime2")]
        public DateTime? CreateDate { get; set; }

        #endregion

        #region 审计字段 - 修改信息

        /// <summary>修改人 ID</summary>
        public int? ModifyID { get; set; }

        /// <summary>修改人姓名</summary>
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string Modifier { get; set; }

        /// <summary>修改时间</summary>
        [Column(TypeName = "datetime2")]
        public DateTime? ModifyDate { get; set; }

        #endregion

        #region 审计字段 - 删除信息

        /// <summary>删除人 ID（仅当 Enable=false 时有值）</summary>
        public int? DeleteID { get; set; }

        /// <summary>删除人姓名</summary>
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string Deleter { get; set; }

        /// <summary>删除时间</summary>
        [Column(TypeName = "datetime2")]
        public DateTime? DeleteTime { get; set; }

        #endregion

        #region 状态与辅助字段

        /// <summary>启用状态（true=启用, false=禁用/已删除）</summary>
        public bool Enable { get; set; } = true;

        /// <summary>排序号（默认0）</summary>
        public int Sort { get; set; } = 0;

        /// <summary>备注</summary>
        [MaxLength(500)]
        [Column(TypeName = "nvarchar(500)")]
        public string Remark { get; set; }

        #endregion

        #region 辅助方法

        /// <summary>判断是否已逻辑删除</summary>
        [NotMapped]
        public bool IsDeleted => !Enable && DeleteTime.HasValue;

        /// <summary>判断是否被禁用但未删除</summary>
        [NotMapped]
        public bool IsDisabled => !Enable && !DeleteTime.HasValue;

        /// <summary>标记为逻辑删除（由框架调用）</summary>
        public void MarkAsDeleted(int userId, string userName)
        {
            Enable = false;
            DeleteID = userId;
            Deleter = userName;
            DeleteTime = DateTime.Now;
        }

        /// <summary>标记为禁用（不记录删除信息）</summary>
        public void MarkAsDisabled()
        {
            Enable = false;
        }

        /// <summary>填充创建信息（由 YZHServiceBase 调用）</summary>
        public void FillCreateInfo(int userId, string userName)
        {
            CreateID = userId;
            Creator = userName;
            CreateDate = DateTime.Now;
        }

        /// <summary>填充修改信息（由 YZHServiceBase 调用）</summary>
        public void FillModifyInfo(int userId, string userName)
        {
            ModifyID = userId;
            Modifier = userName;
            ModifyDate = DateTime.Now;
        }

        #endregion
    }
}
