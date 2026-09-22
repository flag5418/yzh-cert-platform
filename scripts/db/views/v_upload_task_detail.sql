-- ============================================================
-- 视图：v_upload_task_detail
-- 用途：上传任务详情（包含文件状态信息，用于上传流程中间状态查询）
-- 日期：2026-09-20
-- ============================================================

CREATE OR REPLACE VIEW v_upload_task_detail AS
SELECT 
    t.TaskId,
    t.DirectoryCode,
    t.TotalFiles,
    t.SuccessCount,
    t.Status,
    t.ExpireTime,
    f.FileCode,
    f.FileName,
    f.UploadStatus,
    f.StoragePath,
    f.IsValid AS FileIsValid,
    f.IsDeleted AS FileIsDeleted
FROM cert_upload_task t
LEFT JOIN cert_standard_directory_file f 
    ON t.TaskId = f.TaskId COLLATE utf8mb4_unicode_ci AND f.IsDeleted = 0;
