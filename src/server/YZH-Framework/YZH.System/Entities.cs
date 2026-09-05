using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YZH.System.Entities
{
    /// <summary>
    /// 用户表（干净重写，映射真实 Sys_User 表结构）
    /// 彻底脱离 VOL 实体框架，仅保留与数据库一一对应的 POCO。
    /// </summary>
    [Table("Sys_User")]
    public class SysUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int User_Id { get; set; }
        public int Role_Id { get; set; }
        public string RoleName { get; set; }
        public string PhoneNo { get; set; }
        public string Remark { get; set; }
        public string Tel { get; set; }
        public string UserName { get; set; }
        public string UserPwd { get; set; }
        public string UserTrueName { get; set; }
        public string DeptName { get; set; }
        public int? Dept_Id { get; set; }
        public string Email { get; set; }
        public byte Enable { get; set; }
        public byte UserType { get; set; }
        public string OrgCode { get; set; }
        public long? OrgId { get; set; }
        public int? ParentUserId { get; set; }
        public int? Gender { get; set; }
        public string HeadImageUrl { get; set; }
        public int? IsRegregisterPhone { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime? LastModifyPwdDate { get; set; }
        public string Address { get; set; }
        public int? AppType { get; set; }
        public DateTime? AuditDate { get; set; }
        public int? AuditStatus { get; set; }
        public string Auditor { get; set; }
        public int? OrderNo { get; set; }
        public string Token { get; set; }
        public int? CreateID { get; set; }
        public DateTime? CreateDate { get; set; }
        public string Creator { get; set; }
        public string Mobile { get; set; }
        public string Modifier { get; set; }
        public DateTime? ModifyDate { get; set; }
        public int? ModifyID { get; set; }
        public string DeptIds { get; set; }
        public string wechat_openid { get; set; }
        public string wechat_unionid { get; set; }
    }

    [Table("Sys_Role")]
    public class SysRole
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Role_Id { get; set; }
        public DateTime? CreateDate { get; set; }
        public string Creator { get; set; }
        public string DeleteBy { get; set; }
        public string DeptName { get; set; }
        public int? Dept_Id { get; set; }
        public byte? Enable { get; set; }
        public string Modifier { get; set; }
        public DateTime? ModifyDate { get; set; }
        public int? OrderNo { get; set; }
        public int ParentId { get; set; }
        public string RoleName { get; set; }
    }

    [Table("Sys_Menu")]
    public class SysMenu
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Menu_Id { get; set; }
        public string MenuName { get; set; }
        public string Auth { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public byte? Enable { get; set; }
        public int? OrderNo { get; set; }
        public string TableName { get; set; }
        public int ParentId { get; set; }
        public string Url { get; set; }
        public DateTime? CreateDate { get; set; }
        public string Creator { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string Modifier { get; set; }
        public int? MenuType { get; set; }
    }

    [Table("Sys_Department")]
    public class SysDepartment
    {
        [Key]
        public Guid DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string DepartmentCode { get; set; }
        public Guid? ParentId { get; set; }
        public string DepartmentType { get; set; }
        public int? Enable { get; set; }
        public string Remark { get; set; }
        public int? CreateID { get; set; }
        public string Creator { get; set; }
        public DateTime? CreateDate { get; set; }
        public int? ModifyID { get; set; }
        public string Modifier { get; set; }
        public DateTime? ModifyDate { get; set; }
    }

    [Table("Sys_Dictionary")]
    public class SysDictionary
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Dic_ID { get; set; }
        public string Config { get; set; }
        public DateTime? CreateDate { get; set; }
        public int? CreateID { get; set; }
        public string Creator { get; set; }
        public string DBServer { get; set; }
        public string DbSql { get; set; }
        public string DicName { get; set; }
        public string DicNo { get; set; }
        public byte Enable { get; set; }
        public string Modifier { get; set; }
        public DateTime? ModifyDate { get; set; }
        public int? ModifyID { get; set; }
        public int? OrderNo { get; set; }
        public int ParentId { get; set; }
        public string Remark { get; set; }
    }

    [Table("Sys_DictionaryList")]
    public class SysDictionaryList
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DicList_ID { get; set; }
        public DateTime? CreateDate { get; set; }
        public int? CreateID { get; set; }
        public string Creator { get; set; }
        public string DicName { get; set; }
        public string DicValue { get; set; }
        public int? Dic_ID { get; set; }
        public byte? Enable { get; set; }
        public string Modifier { get; set; }
        public DateTime? ModifyDate { get; set; }
        public int? ModifyID { get; set; }
        public int? OrderNo { get; set; }
        public string Remark { get; set; }
        public string Color { get; set; }
    }

    [Table("Sys_RoleAuth")]
    public class SysRoleAuth
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Auth_Id { get; set; }
        public string AuthValue { get; set; }
        public DateTime? CreateDate { get; set; }
        public string Creator { get; set; }
        public int Menu_Id { get; set; }
        public string Modifier { get; set; }
        public DateTime? ModifyDate { get; set; }
        public int? Role_Id { get; set; }
        public int? User_Id { get; set; }
    }

    [Table("Sys_Log")]
    public class SysLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public string Code { get; set; }
        public string OrgCode { get; set; }
        public int? CreateID { get; set; }
        public string Creator { get; set; }
        public DateTime CreateDate { get; set; }
        public int? ModifyID { get; set; }
        public string Modifier { get; set; }
        public DateTime? ModifyDate { get; set; }
        public int? DeleteID { get; set; }
        public string Deleter { get; set; }
        public DateTime? DeleteTime { get; set; }
        public string Status { get; set; }
        public byte? Enable { get; set; }
        public int? Sort { get; set; }
        public string Remark { get; set; }
        public long? UserId { get; set; }
        public string Module { get; set; }
        public string Action { get; set; }
        public string TargetType { get; set; }
        public long? TargetId { get; set; }
        public string Detail { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }
}
