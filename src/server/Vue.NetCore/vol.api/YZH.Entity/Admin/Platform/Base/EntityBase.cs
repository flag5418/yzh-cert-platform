using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform
{
    /// <summary>
    /// YZH 实体基类，继承 Vol 的 BaseEntity（空基类），扩展统一审计和业务字段。
    /// 
    /// 设计原则（严格遵循 YZH-建设原则-V1.md §4.1）：
    /// 1. CreateBy / UpdateBy / DeleteBy → string 类型（存储用户 Code，如 USER_000001）
    /// 2. Creator / Modifier / Deleter → string 类型（操作人姓名）
    /// 3. Code 作为业务编码，与数据库主键 Id 分离（不依赖自增 ID 做业务标识）
    /// 4. Enable 字段统一处理逻辑删除（true = 启用，false = 禁用/已删除）
    /// 5. OrgCode 不在基类定义，由需要多租户隔离的子类自行声明
    /// 
    /// 自动填充规则（由 ServiceBase 接管，业务代码禁止手动设置）：
    /// - 新建: CreateBy + Creator + CreateDate
    /// - 编辑: UpdateBy + Modifier + UpdateDate (ModifyDate)
    /// - 删除: DeleteBy + Deleter + DeleteTime + Enable = false
    /// 
    /// 状态：[DONE] 审计字段改为 Code 关联（2026-09-07）
    /// </summary>
    public abstract class EntityBase : BaseEntity
    {
        #region 主键

        /// <summary>
        /// 主键（自增，由数据库生成）
        /// EF Core 要求每个实体必须有主键
        /// Vol 框架的 ValidationValueForDbType 依赖 [Column(TypeName)] 获取数据库类型
        /// </summary>
        [Key]
        [Column(TypeName = "bigint")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        #endregion

        #region 业务编码

        /// <summary>
        /// 业务编码（自动生成，非主键，用于业务标识）
        /// 新建时自动赋值 Guid 短格式，无需前端传参、无需钩子干预
        /// 示例：a1b2c3d4e5f6...
        /// 注意：无 [Column] 映射，PascalCase 表自动映射 Code；snake_case 表需用 new + [Column("code")] 覆盖
        /// </summary>
        [MaxLength(100)]
        public string Code { get; set; } = Guid.NewGuid().ToString("N");

        #endregion

        #region 审计字段 - 创建信息

        /// <summary>
        /// 创建人 Code（对应 Sys_User.Code，varchar 类型，如 USER_000001）
        /// 由框架在新增时自动填充
        /// 禁止业务代码手动设置！
        /// 注意：无 [Column] 映射；PascalCase 表自动映射 CreateBy；snake_case 表需用 new + [Column("create_by")] 覆盖
        /// </summary>
        [MaxLength(50)]
        public string CreateBy { get; set; }

        /// <summary>
        /// 创建人姓名（对应 Sys_User.UserName）
        /// 由框架在新增时自动填充 UserContext.Current.UserName
        /// </summary>
        [MaxLength(50)]
        [Column("creator")]
        public string Creator { get; set; }

        /// <summary>
        /// 创建时间
        /// 由框架在新增时自动填充 DateTime.Now
        /// 注意：无 [Column] 映射；snake_case 表需用 new + [Column("create_date")] 覆盖
        /// </summary>
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        #endregion

        #region 审计字段 - 修改信息

        /// <summary>
        /// 修改人 Code（对应 Sys_User.Code，varchar 类型，如 USER_000001）
        /// 由框架在更新时自动填充
        /// 禁止业务代码手动设置！
        /// 注意：无 [Column] 映射；PascalCase 表需映射为 UpdateBy；snake_case 表需用 new + [Column("update_by")] 覆盖
        /// </summary>
        [MaxLength(50)]
        public string UpdateBy { get; set; }

        /// <summary>
        /// 修改人姓名（对应 Sys_User.UserName）
        /// 由框架在更新时自动填充 UserContext.Current.UserName
        /// </summary>
        [MaxLength(50)]
        [Column("modifier")]
        public string Modifier { get; set; }

        /// <summary>
        /// 修改时间（原 ModifyDate，语义更名）
        /// 由框架在更新时自动填充 DateTime.Now
        /// 注意：无 [Column] 映射；snake_case 表需用 new + [Column("modify_date")] 覆盖
        /// </summary>
        public DateTime? ModifyDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 修改时间（ModifyDate 的别名，与 UpdateBy 对齐命名）
        /// </summary>
        [NotMapped]
        public DateTime? UpdateDate
        {
            get => ModifyDate;
            set => ModifyDate = value;
        }

        #endregion

        #region 审计字段 - 删除信息

        /// <summary>
        /// 删除人 Code（对应 Sys_User.Code，varchar 类型，如 USER_000001）
        /// 由框架在逻辑删除时自动填充
        /// 仅当 Enable = false 时有值
        /// 注意：无 [Column] 映射；snake_case 表需用 new + [Column("delete_by")] 覆盖
        /// </summary>
        [MaxLength(50)]
        public string DeleteBy { get; set; }

        /// <summary>
        /// 删除人姓名（对应 Sys_User.UserName）
        /// 由框架在逻辑删除时自动填充
        /// 仅当 Enable = false 时有值
        /// </summary>
        [MaxLength(50)]
        [Column("deleter")]
        public string Deleter { get; set; }

        /// <summary>
        /// 删除时间
        /// 由框架在逻辑删除时自动填充 DateTime.Now
        /// 仅当 Enable = false 时有值
        /// 注意：无 [Column] 映射；snake_case 表需用 new + [Column("delete_time")] 覆盖
        /// </summary>
        public DateTime? DeleteTime { get; set; } = DateTime.Now;

        #endregion

        #region 状态与辅助字段

        /// <summary>
        /// 业务状态（如：active, inactive, pending, approved, rejected 等）
        /// 与 Enable 字段区分：Enable 是系统级启用/禁用，Status 是业务级状态
        /// 默认值：active
        /// </summary>
        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "active";

        /// <summary>
        /// 启用状态（true = 启用, false = 禁用/逻辑删除）
        /// 默认值：true
        /// </summary>
        [Column("enable")]
        public bool Enable { get; set; } = true;

        #endregion

        #region 辅助字段

        /// <summary>
        /// 备注
        /// 注意：无 [Column] 映射；snake_case 表需用 new + [Column("remark")] 覆盖
        /// </summary>
        [MaxLength(500)]
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
        public void MarkAsDeleted(string userCode, string userName)
        {
            Enable = false;
            DeleteBy = userCode;
            Deleter = userName;
            DeleteTime = DateTime.Now;
        }

        /// <summary>
        /// 标记为禁用（不记录删除信息，由框架调用）
        /// </summary>
        public void MarkAsDisabled()
        {
            Enable = false;
            // 不设置 DeleteBy/DeleteTime，表示只是禁用而非删除
        }

        /// <summary>
        /// 填充创建信息（由 ServiceBase 在新增时调用）
        /// userCode: 用户 Code（如 USER_000001）
        /// userName: 用户姓名
        /// </summary>
        public void FillCreateInfo(string userCode, string userName)
        {
            CreateBy = userCode;
            Creator = userName;
            CreateDate = DateTime.Now;
        }

        /// <summary>
        /// 填充修改信息（由 ServiceBase 在更新时调用）
        /// userCode: 用户 Code（如 USER_000001）
        /// userName: 用户姓名
        /// </summary>
        public void FillModifyInfo(string userCode, string userName)
        {
            UpdateBy = userCode;
            Modifier = userName;
            ModifyDate = DateTime.Now;
        }

        #endregion
    }
}
