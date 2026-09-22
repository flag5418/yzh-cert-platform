-- ============================================================
-- 视图：v_standard_directory_root_files
-- 用途：查询目录根级别文件（FolderCode 为空或不存在的文件），含中间状态
-- 日期：2026-09-20
-- ============================================================

CREATE OR REPLACE VIEW v_standard_directory_root_files AS
SELECT 
    f.Id, f.code AS Code, f.FileCode, f.FileName, f.FileType, f.StoragePath,
    f.ConvertedStoragePath, f.ConvertStatus, f.ConvertMessage,
    f.UploadStatus, f.TaskId, f.DirectoryCode, f.FolderCode,
    f.IsValid, f.IsDeleted, f.Enable, f.FileSize
FROM cert_standard_directory_file f
WHERE (f.FolderCode IS NULL OR f.FolderCode = '')
   OR NOT EXISTS (
       SELECT 1 FROM cert_standard_directory_folder sf 
       WHERE sf.FolderCode = f.FolderCode AND sf.IsDeleted = 0
   );
