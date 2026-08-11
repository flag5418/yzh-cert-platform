-- ============================================================
-- 修复 cert_standard_directory_file 表中的 StoragePath
-- 问题：170 条记录的 StoragePath 缺少 org code 前缀(CB001/)，
--       且用文件夹编码(带管道符|)代替了文件夹名称
-- 正确格式：/CB001/ISO134852016/STAGE01/{folderNames}/{fileName}
-- ============================================================

-- 使用递归 CTE 构建每个文件夹的完整路径名
-- 对于 Depth=1 的根文件夹，路径就是 FolderName
-- 对于子文件夹，路径是 父文件夹路径 + '/' + FolderName

-- 步骤 1：创建临时表存储文件夹路径映射
DROP TEMPORARY TABLE IF EXISTS temp_folder_paths;
CREATE TEMPORARY TABLE temp_folder_paths AS
WITH RECURSIVE folder_path_cte AS (
    -- 基础条件：根文件夹（Depth=1 或 ParentCode 为空）
    SELECT 
        FolderCode,
        FolderName as FolderPath,
        Depth
    FROM cert_standard_directory_folder
    WHERE Depth = 1 OR ParentCode IS NULL OR ParentCode = ''
    
    UNION ALL
    
    -- 递归条件：子文件夹
    SELECT 
        f.FolderCode,
        CONCAT(fp.FolderPath, '/', f.FolderName) as FolderPath,
        f.Depth
    FROM cert_standard_directory_folder f
    INNER JOIN folder_path_cte fp ON f.ParentCode = fp.FolderCode
    WHERE f.Depth > 1
)
SELECT * FROM folder_path_cte;

-- 步骤 2：验证文件夹路径映射
SELECT '=== 文件夹路径映射 ===' as info;
SELECT FolderCode, FolderPath, Depth FROM temp_folder_paths ORDER BY Depth;

-- 步骤 3：预览修复结果（不执行更新）
SELECT '=== 修复预览（前10条）===' as info;
SELECT 
    f.FileCode,
    f.FileName,
    f.StoragePath as OldPath,
    CONCAT('/CB001/ISO134852016/STAGE01/', tp.FolderPath, '/', f.FileName) as NewPath
FROM cert_standard_directory_file f
JOIN temp_folder_paths tp ON f.FolderCode = tp.FolderCode
WHERE f.StoragePath NOT LIKE '/CB001/%' AND f.StoragePath NOT LIKE '/1/%'
LIMIT 10;

-- 步骤 4：执行更新
UPDATE cert_standard_directory_file f
JOIN temp_folder_paths tp ON f.FolderCode = tp.FolderCode
SET f.StoragePath = CONCAT('/CB001/ISO134852016/STAGE01/', tp.FolderPath, '/', f.FileName)
WHERE f.StoragePath NOT LIKE '/CB001/%' AND f.StoragePath NOT LIKE '/1/%';

-- 步骤 5：验证更新结果
SELECT '=== 更新后统计 ===' as info;
SELECT 
    CASE 
        WHEN StoragePath LIKE '/CB001/%' THEN 'correct (with org code)'
        WHEN StoragePath LIKE '/1/%' THEN 'other standard'
        ELSE 'still broken'
    END as path_status,
    COUNT(*) as count
FROM cert_standard_directory_file
GROUP BY path_status;

-- 步骤 6：验证修复后的路径（前10条）
SELECT '=== 修复后路径（前10条）===' as info;
SELECT FileCode, FileName, StoragePath FROM cert_standard_directory_file WHERE StoragePath LIKE '/CB001/ISO134852016/%' ORDER BY FolderCode LIMIT 10;

DROP TEMPORARY TABLE IF EXISTS temp_folder_paths;
