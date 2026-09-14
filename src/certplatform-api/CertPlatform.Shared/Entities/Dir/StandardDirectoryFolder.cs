using System;
using System.Collections.Generic;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform.Dir
{
    [SugarTable("cert_standard_directory_folder")]
    public class StandardDirectoryFolder : BaseEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(IsIgnore = true)]
        public List<StandardDirectoryFolder>? Children { get; set; }

        [SugarColumn(ColumnName = "Code", Length = 36, IsNullable = true)]
        public string? Code { get; set; }

        [SugarColumn(ColumnName = "FolderCode", Length = 150, IsNullable = true)]
        public string? FolderCode { get; set; }

        [SugarColumn(ColumnName = "DirectoryCode", Length = 100, IsNullable = true)]
        public string? DirectoryCode { get; set; }

        [SugarColumn(ColumnName = "ParentCode", Length = 150, IsNullable = true)]
        public string? ParentCode { get; set; }

        [SugarColumn(ColumnName = "FolderName", Length = 200, IsNullable = true)]
        public string? FolderName { get; set; }

        [SugarColumn(ColumnName = "Depth")]
        public int Depth { get; set; } = 1;

        [SugarColumn(ColumnName = "SortOrder")]
        public int SortOrder { get; set; } = 0;

        [SugarColumn(ColumnName = "status", Length = 20, IsNullable = true)]
        public string? Status { get; set; } = "draft";

        [SugarColumn(ColumnName = "Enable")]
        public bool Enable { get; set; } = true;

        [SugarColumn(ColumnName = "CreateID", IsNullable = true)]
        public int? CreateID { get; set; }

        [SugarColumn(ColumnName = "CreateBy", Length = 50, IsNullable = true)]
        public string? Creator { get; set; }

        [SugarColumn(ColumnName = "CreateDate")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "ModifyID", IsNullable = true)]
        public int? ModifyID { get; set; }

        [SugarColumn(ColumnName = "UpdateBy", Length = 50, IsNullable = true)]
        public string? Modifier { get; set; }

        [SugarColumn(ColumnName = "ModifyDate", IsNullable = true)]
        public DateTime? ModifyDate { get; set; }

        [SugarColumn(ColumnName = "DeleteID", IsNullable = true)]
        public int? DeleteID { get; set; }

        [SugarColumn(ColumnName = "DeleteBy", Length = 50, IsNullable = true)]
        public string? Deleter { get; set; }

        [SugarColumn(ColumnName = "DeleteTime", IsNullable = true)]
        public DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "Status_field", Length = 50, IsNullable = true)]
        public string? Status_field { get; set; } = "active";

        [SugarColumn(ColumnName = "Enable_field")]
        public bool Enable_field { get; set; } = true;

        [SugarColumn(ColumnName = "Sort")]
        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnName = "Remark", ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(ColumnName = "TaskId", Length = 64, IsNullable = true)]
        public string? TaskId { get; set; }

        [SugarColumn(ColumnName = "IsValid")]
        public new int IsValid { get; set; } = 1;

        [SugarColumn(IsIgnore = true)]
        public bool Force { get; set; } = false;

        [SugarColumn(ColumnName = "FullPath", Length = 1024, IsNullable = true)]
        public string? FullPath { get; set; }
    }
}
