using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity;
using YZH.Entity.Admin.Platform;
using YZH.Entity.Admin.Platform.Base;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// 认证机构
    /// <para>表名：cert_certification_body</para>
    /// <para>域：A - 认证体系配置</para>
    /// </summary>
    [Page(
        PageKey = "cert_body",
        Title = "认证机构管理",
        ControllerName = "CertCertificationBody",
        SortField = "Id",
        SortOrder = "desc"
    )]
    [Entity(TableCnName = "认证机构管理", TableName = "cert_certification_body", ViewName = "v_certification_body", DBServer = "VOLContext")]
    [Table("cert_certification_body")]
    public class CertificationBody : EntityBase
    {
        #region snake_case 审计字段覆盖（create_id/create_date 等）

        [Column("create_by")]
        public new string CreateBy { get; set; }

        [Column("create_date")]
        public new DateTime? CreateDate { get; set; } = DateTime.Now;

        [Column("update_by")]
        public new string UpdateBy { get; set; }

        [Column("modify_date")]
        public new DateTime? ModifyDate { get; set; } = DateTime.Now;

        [Column("delete_by")]
        public new string DeleteBy { get; set; }

        [Column("delete_time")]
        public new DateTime? DeleteTime { get; set; }

        #endregion

        #region 业务字段

        [EntityColumn(Title = "机构名称", Width = 200, Order = 1, Sortable = true)]
        [Form(Title = "机构名称", Required = true, GridRow = 0, GridCol = 0, GridColSpan = 1)]
        [Search(Title = "机构名称")]
        [Required, StringLength(200)]
        [Editable(true)]
        [UniqueField("机构名称")]
        [Column("name")]
        public string Name { get; set; }

        [EntityColumn(Title = "简称", Width = 120, Order = 2)]
        [Form(Title = "简称", GridRow = 0, GridCol = 1)]
        [StringLength(100)]
        [Editable(true)]
        [Column("short_name")]
        public string ShortName { get; set; }

        [EntityColumn(Title = "机构编号", Width = 120, Order = 3, Sortable = true)]
        [Form(Title = "机构编号", Required = true, GridRow = 1, GridCol = 0)]
        [Search(Title = "机构编号")]
        [StringLength(50)]
        [Editable(true)]
        [UniqueField("机构编号")]
        [Column("cb_code")]
        public string CbCode { get; set; }

        [EntityColumn(Title = "法人", Width = 100, Order = 4)]
        [Form(Title = "法人", GridRow = 1, GridCol = 1)]
        [StringLength(100)]
        [Editable(true)]
        [Column("legal_person")]
        public string LegalPerson { get; set; }

        [EntityColumn(Title = "联系人", Width = 100, Order = 5)]
        [Form(Title = "联系人", GridRow = 2, GridCol = 0)]
        [StringLength(50)]
        [Editable(true)]
        [Column("contact_name")]
        public string ContactName { get; set; }

        [EntityColumn(Title = "联系电话", Width = 120, Order = 6)]
        [Form(Title = "联系电话", GridRow = 2, GridCol = 1)]
        [StringLength(20)]
        [Editable(true)]
        [Column("contact_phone")]
        public string ContactPhone { get; set; }

        [EntityColumn(Title = "邮箱", Width = 150, Order = 7)]
        [Form(Title = "邮箱", GridRow = 3, GridCol = 0, GridColSpan = 2)]
        [StringLength(200)]
        [Editable(true)]
        [Column("contact_email")]
        public string ContactEmail { get; set; }

        [EntityColumn(Visible = false)]
        [Form(Title = "地址", ControlType = "textarea", GridRow = 4, GridCol = 0, GridColSpan = 2, TextareaRows = 3)]
        [StringLength(500)]
        [Editable(true)]
        [Column("address")]
        public string Address { get; set; }

        [EntityColumn(Visible = false)]
        [Form(Title = "Logo", ControlType = "img", GridRow = 5, GridCol = 0)]
        [StringLength(500)]
        [Editable(true)]
        [Column("logo_url")]
        public string LogoUrl { get; set; }

        [EntityColumn(Visible = false)]
        [Form(Title = "业务范围", ControlType = "textarea", GridRow = 6, GridCol = 0, GridColSpan = 2)]
        [Column("scope_text")]
        public string ScopeText { get; set; }

        [EntityColumn(Visible = false)]
        [Form(Visible = false)]
        [Column("theme_config")]
        public string ThemeConfig { get; set; }

        [EntityColumn(Visible = false)]
        [Form(Visible = false)]
        [Column("login_config")]
        public string LoginConfig { get; set; }

        [EntityColumn(Visible = false)]
        [Form(Title = "最大用户数", GridRow = 7, GridCol = 0)]
        [Editable(true)]
        [Column("max_users")]
        public int MaxUsers { get; set; } = 100;

        [EntityColumn(Visible = false)]
        [Form(Title = "最大企业数", GridRow = 7, GridCol = 1)]
        [Editable(true)]
        [Column("max_enterprises")]
        public int MaxEnterprises { get; set; } = 1000;

        [EntityColumn(Title = "到期日期", Width = 120, Order = 8)]
        [Form(Title = "到期日期", ControlType = "date", GridRow = 8, GridCol = 0)]
        [Editable(true)]
        [Column("expire_date")]
        public DateTime? ExpireDate { get; set; }

        #endregion

        // Status, OrgCode, Code, Creator, Modifier, Deleter, Enable 继承自 EntityBase
    }
}
