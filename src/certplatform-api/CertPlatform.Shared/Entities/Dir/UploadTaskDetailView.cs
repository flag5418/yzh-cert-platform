using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>
    /// 上传任务详情视图实体
    /// <para>映射视图：v_upload_task_detail</para>
    /// <para>用途：查询上传任务及其关联文件状态（用于上传流程各步骤验证）</para>
    /// </summary>
    [SugarTable("v_upload_task_detail")]
    public class UploadTaskDetailView : BaseEntity
    {
        // 视图无自增主键和基础审计字段，标记为忽略
        [SugarColumn(IsIgnore = true)]
        public new long Id { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new string? Code { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new DateTime CreateTime { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new string? CreateBy { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new DateTime? UpdateTime { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new string? UpdateBy { get; set; }

        /// <summary>任务ID</summary>
        [SugarColumn(ColumnName = "TaskId")]
        public string TaskId { get; set; } = "";

        /// <summary>目录编码</summary>
        [SugarColumn(ColumnName = "DirectoryCode")]
        public string DirectoryCode { get; set; } = "";

        /// <summary>总文件数</summary>
        [SugarColumn(ColumnName = "TotalFiles")]
        public int TotalFiles { get; set; }

        /// <summary>成功文件数</summary>
        [SugarColumn(ColumnName = "SuccessCount")]
        public int SuccessCount { get; set; }

        /// <summary>任务状态</summary>
        [SugarColumn(ColumnName = "Status")]
        public string Status { get; set; } = "";

        /// <summary>过期时间</summary>
        [SugarColumn(ColumnName = "ExpireTime")]
        public DateTime? ExpireTime { get; set; }

        /// <summary>文件编码</summary>
        [SugarColumn(ColumnName = "FileCode")]
        public string? FileCode { get; set; }

        /// <summary>文件名称</summary>
        [SugarColumn(ColumnName = "FileName")]
        public string? FileName { get; set; }

        /// <summary>上传状态</summary>
        [SugarColumn(ColumnName = "UploadStatus")]
        public string? UploadStatus { get; set; }

        /// <summary>存储路径</summary>
        [SugarColumn(ColumnName = "StoragePath")]
        public string? StoragePath { get; set; }

        /// <summary>文件有效性</summary>
        [SugarColumn(ColumnName = "FileIsValid")]
        public int? FileIsValid { get; set; }

        /// <summary>文件删除标记</summary>
        [SugarColumn(ColumnName = "FileIsDeleted")]
        public bool? FileIsDeleted { get; set; }

        // 视图只读，禁用增删改相关字段
        [SugarColumn(IsIgnore = true)]
        public bool IsDeleted { get; set; }

        [SugarColumn(IsIgnore = true)]
        public int IsValid { get; set; } = 1;
    }
}
