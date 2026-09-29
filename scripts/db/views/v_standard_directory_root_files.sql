-- ============================================================
-- 视图：v_standard_directory_root_files
-- 用途：查询目录根级别文件（FolderCode 为空串的文件），含中间状态
-- 日期：2026-09-20
-- 更新：2026-09-26 补双产物链 4 列（PreviewPdfPath/MarkdownPath/MarkdownStatus/MarkdownMessage）
--       ★ 原因：视图列是「白名单」——不加列，根级文件在列表里就拿不到产物路径与提取链状态，
--         前端只会看到空的预览/提取状态（静默失败，无任何报错）。
-- 更新：2026-09-26 P1 重建 —— 复合编码 FileCode/DirectoryCode 已删（改 Code / ConfigCode）；
--       根节点判据由「NULL 或 不存在于 folder」简化为 `FolderCode = ''`（决策 ⑨ 根用空串）。
-- ============================================================

CREATE OR REPLACE VIEW v_standard_directory_root_files AS
SELECT 
    f.Id, f.Code, f.FileName, f.FileType, f.StoragePath,
    f.ConvertedStoragePath, f.ConvertStatus, f.ConvertMessage,
    f.PreviewPdfPath, f.MarkdownPath, f.MarkdownStatus, f.MarkdownMessage,
    f.UploadStatus, f.TaskId, f.ConfigCode, f.FolderCode,
    f.IsValid, f.IsDeleted, f.FileSize
FROM cert_standard_directory_file f
WHERE f.FolderCode = '';
