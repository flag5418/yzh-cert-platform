-- 存量修复：上传确认时文件夹激活查询缺 includeDisabled，导致 IsValid=0 草稿文件夹永滞
-- 关联：StandardDirectoryService.UploadConfirmAsync（GetListAsync 缺 includeDisabled: true）
-- 执行：docker exec -i yzh-mysql sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" yzh_cert_platform' < fix_draft_folder_activate_V1.sql
-- 日期：2026-09-23

-- 1. 预检：卡在 IsValid=0 且上传任务已 completed 的文件夹
SELECT f.FolderCode, f.FolderName, f.TaskId, t.Status AS task_status
FROM cert_standard_directory_folder f
LEFT JOIN cert_upload_task t
  ON t.TaskId = f.TaskId COLLATE utf8mb4_0900_ai_ci
WHERE f.IsValid = 0 AND f.IsDeleted = 0
  AND (t.Status = 'completed' OR t.Status IS NULL);

-- 2. 修复：激活 + 清 TaskId（仅限任务已完成或任务已不存在的草稿）
-- JOIN 条件显式 COLLATE：两表 TaskId 列 collation 不同（unicode_ci vs 0900_ai_ci），隐式比较报 1267
UPDATE cert_standard_directory_folder f
LEFT JOIN cert_upload_task t
  ON t.TaskId = f.TaskId COLLATE utf8mb4_0900_ai_ci
SET f.IsValid = 1, f.TaskId = NULL
WHERE f.IsValid = 0 AND f.IsDeleted = 0
  AND (t.Status = 'completed' OR t.TaskId IS NULL);

-- 3. 复核：应返回 0
SELECT COUNT(*) AS remaining_stuck
FROM cert_standard_directory_folder
WHERE IsValid = 0 AND IsDeleted = 0 AND TaskId IS NULL;

-- 4. 验证目录可见文件夹数
SELECT DirectoryCode, COUNT(*) AS folder_cnt
FROM cert_standard_directory_folder
WHERE IsValid = 1 AND IsDeleted = 0 AND Enable = 1
GROUP BY DirectoryCode;
