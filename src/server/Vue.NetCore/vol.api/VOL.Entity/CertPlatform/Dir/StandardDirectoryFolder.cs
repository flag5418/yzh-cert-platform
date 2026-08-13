using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
// using System.ComponentModel.DataAnnotations.The;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Dir
{
    [Entity(TableCnName = "标准目录文件夹", TableName = "cert_standard_directory_folder", DBServer = "VOLContext")]
    [Table("cert_standard_directory_folder")]
    public class StandardDirectoryFolder : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [NotMapped]
        public List<StandardDirectoryFolder> Children { get; set; }

        [MaxLength(36)]
        [Column("Code")]
        public string Code { get; set; }

        [MaxLength(150)]
        [Column("FolderCode")]
        public string FolderCode { get; set; }

        [MaxLength(100)]
        [Column("DirectoryCode")]
        public string DirectoryCode { get; set; }

        [MaxLength(150)]
        [Column("ParentCode")]
        public string ParentCode { get; set; }

        [MaxLength(200)]
        [Column("FolderName")]
        public string FolderName { get; set; }

        [Column("Depth")]
        public int Depth { get; set; } = 1;

        [Column("SortOrder")]
        public int SortOrder { get; set; } = 0;

        [MaxLength(20)]
        [Column("Status")]
        public string Status { get; set; } = "draft";

        [Column("Enable")]
        public bool Enable { get; set; } = true;

        [Column("CreateID")]
        public int? CreateID { get; set; }

        [MaxLength(50)]
        [Column("Creator")]
        public string Creator { get; set; }

        [Column("CreateDate")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [Column("ModifyID")]
        public int? ModifyID { get; set; }

        [MaxLength(50)]
        [Column("Modifier")]
        public string Modifier { get; set; }

        [Column("ModifyDate")]
        public DateTime? ModifyDate { get; set; }

        [Column("DeleteID")]
        public int? DeleteID { get; set; }

        [MaxLength(50)]
        [Column("Deleter")]
        public string Deleter { get; set; }

        [Column("DeleteTime")]
        public DateTime? DeleteTime { get; set; }

        [MaxLength(50)]
        [Column("Status_field")]
        public string Status_field { get; set; } = "active";

        [Column("Enable_field")]
        public bool Enable_field { get; set; } = true;

        [Column("Sort")]
        public int Sort { get; set; } = 0;

        [Column("Remark")]
        public string Remark { get; set; }

        /// <summary>
        /// 创建此文件夹的上传任务ID
        /// </summary>
        [MaxLength(64)]
        [Column("TaskId")]
        public string TaskId { get; set; }

        /// <summary>
        /// 有效标志: 0=无效(预创建), 1=有效(已确认)
        /// </summary>
        [Column("IsValid")]
        public bool IsValid { get; set; }

        [NotMapped]
        /// <summary>是否强制重命名（有子项时）。默认 false：有子项时后端要求确认 force=true</summary>
        public bool Force { get; set; } = false;

        /// <summary>
        /// 完整路径（从根到当前文件夹），用于路径判定
        /// 示例：4记录文件/内审记录
        /// </summary>
        [MaxLength(1024)]
        [Column("FullPath")]
        public string FullPath { get; set; }
    }
}
