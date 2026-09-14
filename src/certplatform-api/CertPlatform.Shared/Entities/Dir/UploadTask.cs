using System;
using SqlSugar;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform.Dir
{
    [SugarTable("cert_upload_task")]
    public class UploadTask : BaseEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "code", Length = 64)]
        public new string Code { get; set; }

        [SugarColumn(ColumnName = "TaskId", Length = 64)]
        public string TaskId { get; set; }

        [SugarColumn(ColumnName = "DirectoryCode", Length = 128)]
        public string DirectoryCode { get; set; }

        [SugarColumn(ColumnName = "TotalFiles")]
        public int TotalFiles { get; set; } = 0;

        [SugarColumn(ColumnName = "TotalSize")]
        public long TotalSize { get; set; } = 0;

        [SugarColumn(ColumnName = "SuccessCount")]
        public int SuccessCount { get; set; } = 0;

        [SugarColumn(ColumnName = "status", Length = 20)]
        public string Status { get; set; } = "initialized";

        [SugarColumn(ColumnName = "CreateBy", Length = 64)]
        public string Creator { get; set; }

        [SugarColumn(ColumnName = "CreateTime")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "UpdateTime", IsNullable = true)]
        public DateTime? ModifyDate { get; set; }

        [SugarColumn(ColumnName = "ExpireTime", IsNullable = true)]
        public DateTime? ExpireTime { get; set; }
    }
}
