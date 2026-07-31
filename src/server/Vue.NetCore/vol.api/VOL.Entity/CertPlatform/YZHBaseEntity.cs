using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform
{
    /// <summary>
    /// YZH 实体基类，继承 Vol 的 BaseEntity（空基类），扩展统一审计和业务字段。
    /// 
    /// 设计原则（严格遵循 YZH-建设原则-V1.md §4.1）：
    /// 1. CreateID / ModifyID / DeleteID → int? 类型（对应 UserContext.Current.UserId）
    /// 2. Creator / Modifier / Deleter → string 类型（操作人姓名）
    /// 3. Code 作为业务编码，与数据库主键 Id 分离（不依赖自增 ID 做业务标识）
    /// 4. Enable 字段统一处理逻辑删除（true = 启用，false = 禁用/已删除）
    /// 5. OrgCode 字段支持多租户隔离（由 [YZHMultiTenant] 特性自动填充）
    /// 
    /// 自动填充规则（由 YZHServiceBase 接管，业务代码禁止手动设置）：
    /// - 新建: CreateID + Creator + CreateDate + OrgCode
    /// - 编辑: ModifyID + Modifier + ModifyDate
    /// - 删除: DeleteID + Deleter + DeleteTime + Enable = false
    /// 
    /// 状态：[DONE] Phase 1 基础字段定义完成
    /// </summary>
    public class YZHBaseEntity : BaseEntity
    {
        #region 主键

        /// <summary>
        /// 主键（自增，由数据库生成）
        /// EF Core 要求每个实体必须有主键
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        #endregion

        #region 业务编码

        /// <summary>
        /// 业务编码（由 YZHCodeRule 生成，非主键，用于业务标识）
        /// 示例：CB001-2026001（机构编号-年份流水号）
        /// </summary>
        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string Code { get; set; }

        /// <summary>
        /// 多租户组织编码（用于数据隔离）
        /// 由 [YZHMultiTenant] 特性自动填充，值为 UserContext.Current.OrgCode
        /// 
        /// TODO:P2 - Phase 2 实现多租户过滤时启用此字段
        /// 当前仅作为预留字段，不影响现有功能
        /// </summary>
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string OrgCode { get; set; }

        #endregion

        #region 审计字段 - 创建信息

        /// <summary>
        /// 创建人 ID（对应 Sys_User.Id，int 类型）
        /// 由框架在新增时自动填充 UserContext.Current.UserId
        /// 禁止业务代码手动设置！
        /// </summary>
        public int? CreateID { get; set; }

        /// <summary>
        /// 创建人姓名（对应 Sys_User.UserName）
        /// 由框架在新增时自动填充 UserContext.Current.UserName
        /// </summary>
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string Creator { get; set; }

        /// <summary>
        /// 创建时间
        /// 由框架在新增时自动填充 DateTime.Now
        /// </summary>
        [Column(TypeName = "datetime2")]
        public DateTime? CreateDate { get; set; }

        #endregion

        #region 审计字段 - 修改信息

        /// <summary>
        /// 修改人 ID（对应 Sys_User.Id，int 类型）
        /// 由框架在更新时自动填充 UserContext.Current.UserId
        /// 禁止业务代码手动设置！
        /// </summary>
        public int? ModifyID { get; set; }

        /// <summary>
        /// 修改人姓名（对应 Sys_User.UserName）
        /// 由框架在更新时自动填充 UserContext.Current.UserName
        /// </summary>
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string Modifier { get; set; }

        /// <summary>
        /// 修改时间
        /// 由框架在更新时自动填充 DateTime.Now
        /// </summary>
        [Column(TypeName = "datetime2")]
        public DateTime? ModifyDate { get; set; }

        #endregion

        #region 审计字段 - 删除信息

        /// <summary>
        /// 删除人 ID（对应 Sys_User.Id，int 类型）
        /// 由框架在逻辑删除时自动填充 UserContext.Current.UserId
        /// 仅当 Enable = false 时有值
        /// 
        /// TODO:P3 - Phase 3 实现删除策略时正式使用
        /// </summary>
        public int? DeleteID { get; set; }

        /// <summary>
        /// 删除人姓名（对应 Sys_User.UserName）
        /// 由框架在逻辑删除时自动填充
        /// 仅当 Enable = false 时有值
        /// 
        /// TODO:P3 - Phase 3 实现删除策略时正式使用
        /// </summary>
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string Deleter { get; set; }

        /// <summary>
        /// 删除时间
        /// 由框架在逻辑删除时自动填充 DateTime.Now
        /// 仅当 Enable = false 时有值
        /// 
        /// TODO:P3 - Phase 3 实现删除策略时正式使用
        /// </summary>
        [Column(TypeName = "datetime2")]
        public DateTime? DeleteTime { get; set; }

        #endregion

        #region 状态与辅助字段

        /// <summary>
        /// 业务状态（如：active, inactive, pending, approved, rejected 等）
        /// 与 Enable 字段区分：Enable 是系统级启用/禁用，Status 是业务级状态
        /// 默认值：active
        /// </summary>
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Status { get; set; } = "active";

        /// <summary>
        /// 启用状态（true = 启用, false = 禁用/逻辑删除）
        /// 默认值：true
        /// 
        /// 设计说明：
        /// - 此字段同时承担"是否启用"和"逻辑删除标记"双重职责
        /// - 配合 DeleteTime 可区分"禁用"（DeleteTime 为 null）和"已删除"（DeleteTime 有值）
        /// - 如果需要更细粒度的控制，可在 Phase 3 引入独立的 IsDeleted 字段
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// 排序号（默认 0，数字越小越靠前）
        /// 用于前端列表排序
        /// </summary>
        public int Sort { get; set; } = 0;

        /// <summary>
        /// 备注
        /// </summary>
        [MaxLength(500)]
        [Column(TypeName = "nvarchar(500)")]
        public string Remark { get; set; }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 判断实体是否已被逻辑删除
        /// 条件：Enable == false && DeleteTime != null
        /// </summary>
        [NotMapped]
        public bool IsDeleted => !Enable && DeleteTime.HasValue;

        /// <summary>
        /// 判断实体是否被禁用但未删除
        /// 条件：Enable == false && DeleteTime == null
        /// </summary>
        [NotMapped]
        public bool IsDisabled => !Enable && !DeleteTime.HasValue;

        /// <summary>
        /// 标记为逻辑删除（由框架调用，禁止业务代码直接调用）
        /// </summary>
        public void MarkAsDeleted(int userId, string userName)
        {
            Enable = false;
            DeleteID = userId;
            Deleter = userName;
            DeleteTime = DateTime.Now;
        }

        /// <summary>
        /// 标记为禁用（不记录删除信息，由框架调用）
        /// </summary>
        public void MarkAsDisabled()
        {
            Enable = false;
            // 不设置 DeleteID/DeleteTime，表示只是禁用而非删除
        }

        /// <summary>
        /// 填充创建信息（由 YZHServiceBase 在新增时调用）
        /// </summary>
        public void FillCreateInfo(int userId, string userName, string orgCode = null)
        {
            CreateID = userId;
            Creator = userName;
            CreateDate = DateTime.Now;
            
            if (!string.IsNullOrEmpty(orgCode))
            {
                OrgCode = orgCode;
            }
        }

        /// <summary>
        /// 填充修改信息（由 YZHServiceBase 在更新时调用）
        /// </summary>
        public void FillModifyInfo(int userId, string userName)
        {
            ModifyID = userId;
            Modifier = userName;
            ModifyDate = DateTime.Now;
        }

        #endregion
    }
}
