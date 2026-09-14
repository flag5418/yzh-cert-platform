using System;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform.Dir
{
    [SugarTable("cert_standard_directory_config")]
    public class StandardDirectoryConfig : BaseEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "Code", Length = 36)]
        public string Code { get; set; }

        [SugarColumn(ColumnName = "DirectoryCode", Length = 100)]
        public string DirectoryCode { get; set; }

        [SugarColumn(ColumnName = "StandardCode", Length = 50)]
        public string StandardCode { get; set; }

        [SugarColumn(ColumnName = "PhaseCode", Length = 50)]
        public string PhaseCode { get; set; }

        [SugarColumn(ColumnName = "RootFolderName", Length = 200, IsNullable = true)]
        public string RootFolderName { get; set; }

        [SugarColumn(ColumnName = "Status", Length = 20, IsNullable = true)]
        public string Status { get; set; } = "draft";

        [SugarColumn(ColumnName = "Enable")]
        public bool Enable { get; set; } = true;

        [SugarColumn(ColumnName = "CreateID", IsNullable = true)]
        public int? CreateID { get; set; }

        [SugarColumn(ColumnName = "CreateBy", Length = 50, IsNullable = true)]
        public string Creator { get; set; }

        [SugarColumn(ColumnName = "CreateDate")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "ModifyID", IsNullable = true)]
        public int? ModifyID { get; set; }

        [SugarColumn(ColumnName = "UpdateBy", Length = 50, IsNullable = true)]
        public string Modifier { get; set; }

        [SugarColumn(ColumnName = "ModifyDate", IsNullable = true)]
        public DateTime? ModifyDate { get; set; }

        [SugarColumn(ColumnName = "DeleteID", IsNullable = true)]
        public int? DeleteID { get; set; }

        [SugarColumn(ColumnName = "DeleteBy", Length = 50, IsNullable = true)]
        public string Deleter { get; set; }

        [SugarColumn(ColumnName = "DeleteTime", IsNullable = true)]
        public DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "Status_field", Length = 50, IsNullable = true)]
        public string Status_field { get; set; } = "active";

        [SugarColumn(ColumnName = "Enable_field")]
        public bool Enable_field { get; set; } = true;

        [SugarColumn(ColumnName = "Sort")]
        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnName = "Remark", ColumnDataType = "text", IsNullable = true)]
        public string Remark { get; set; }
    }
}
