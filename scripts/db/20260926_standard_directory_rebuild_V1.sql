-- ============================================================
-- P1 迁移：标准目录链重建（《标准目录与企业资料-存储与编码规范-V4》§3 + 决策 ⑦–⑳）
-- 日期：2026-09-26
--
-- 前置：① 已备份 backup/yzh_cert_platform_before_wipe_20260926.sql（mysqldump 全库）
--       ② 已备份 backup/minio-cert-platform-20260926/（MinIO 对象）
--       ③ 本脚本第 0 步会清空数据 —— 这是刻意的，因为列宽收窄（varchar(150)→(36)）
--          在 STRICT_TRANS_TABLES 下遇到旧数据会直接报错；先清再改是唯一安全顺序。
--
-- 决策依据：
--   ⑦  删除全部复合编码（FileCode / FolderCode / DirectoryCode）
--   ⑨  根节点 ParentCode 用 ''（非 NULL）—— MySQL 唯一索引允许多个 NULL，用 NULL 拦不住同名根
--   ⑩  PhaseCode → StageCode 术语统一
--   ⑫  cert_enterprise / cert_enterprise_stage 一并清空
--   ⑬  唯一键不含 IsDeleted（软删行仍占键位 → 建前"含已删"查重 + 复活）
--   ⑱  提取规则键 StandardFileCode 的取值改为标准目录文件行的 Code（GUID）
--   ⑲  StandardCode 统一存 cert_iso_standard.Code（GUID），删除前端洗码逻辑
--   ⑳  ★ 标准目录 = 平台全局库 ⇒ 三张表【不加】OrgCode 列；路径去掉 {OrgCode} 段
-- ============================================================

-- ------------------------------------------------------------
-- 0. 数据清理（P2）
-- ------------------------------------------------------------
DELETE FROM cert_standard_directory_file;
DELETE FROM cert_standard_directory_folder;
DELETE FROM cert_standard_directory_config;
DELETE FROM cert_upload_task;
DELETE FROM yzh_queue_task;
DELETE FROM yzh_queue_resource_lock;
DELETE FROM yzh_queue;
DELETE FROM cert_enterprise_stage;
DELETE FROM cert_enterprise;

-- ------------------------------------------------------------
-- 1. 先删依赖视图（它们引用了即将被删/改名的列）
-- ------------------------------------------------------------
DROP VIEW IF EXISTS v_standard_directory_root_files;
DROP VIEW IF EXISTS v_cert_configured_rules;
DROP VIEW IF EXISTS v_upload_task_detail;

-- ------------------------------------------------------------
-- 2. cert_standard_directory_config
--    ⛔ DirectoryCode 复合编码删除（本行 Code 即业务键）
--    ⑩ PhaseCode → StageCode，值域改为 cert_cert_stage.Code（GUID）
--    ⑲ StandardCode 值域改为 cert_iso_standard.Code（GUID）
--    ⑳ 不新增 OrgCode —— 平台全局库
-- ------------------------------------------------------------
ALTER TABLE cert_standard_directory_config
  DROP INDEX uk_directory_code,
  DROP INDEX uk_standard_phase,
  DROP COLUMN DirectoryCode,
  CHANGE COLUMN PhaseCode StageCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         NOT NULL DEFAULT '' COMMENT '阶段 Code → cert_cert_stage.Code（GUID）',
  MODIFY COLUMN StandardCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         NOT NULL DEFAULT '' COMMENT '标准 Code → cert_iso_standard.Code（GUID）';

ALTER TABLE cert_standard_directory_config
  ADD UNIQUE KEY uk_std_stage (StandardCode, StageCode);

-- ------------------------------------------------------------
-- 3. cert_standard_directory_folder
--    ⛔ FolderCode 复合编码删除（本行 Code 即业务键）
--    DirectoryCode → ConfigCode（值 = cert_standard_directory_config.Code）
--    ⑨ ParentCode 根节点用 '' —— 列 NOT NULL DEFAULT ''（C# 侧受 ITreeEntity 约束仍可空，写库前 ?? ""）
--    FullPath 收窄 1024 → 500（唯一索引长度上限）
-- ------------------------------------------------------------
ALTER TABLE cert_standard_directory_folder
  DROP INDEX uk_folder_code,
  DROP COLUMN FolderCode,
  CHANGE COLUMN DirectoryCode ConfigCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         NOT NULL DEFAULT '' COMMENT '配置 Code → cert_standard_directory_config.Code',
  MODIFY COLUMN ParentCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         NOT NULL DEFAULT '' COMMENT '父文件夹 Code → 本表 Code；根节点恒为 空串（⛔ 不用 NULL）',
  MODIFY COLUMN FullPath varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         DEFAULT NULL COMMENT '相对配置根的文件夹路径';

ALTER TABLE cert_standard_directory_folder
  ADD UNIQUE KEY uk_cfg_parent_name (ConfigCode, ParentCode, FolderName);

-- ------------------------------------------------------------
-- 4. cert_standard_directory_file
--    ⛔ FileCode 复合编码删除（本行 Code 即业务键）
--    DirectoryCode → ConfigCode；FolderCode 值域改为 folder.Code，根级文件 ''
--    FullPath 收窄 1024 → 500（唯一索引长度上限）
-- ------------------------------------------------------------
ALTER TABLE cert_standard_directory_file
  DROP INDEX uk_file_code,
  DROP COLUMN FileCode,
  CHANGE COLUMN DirectoryCode ConfigCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         NOT NULL DEFAULT '' COMMENT '配置 Code → cert_standard_directory_config.Code',
  MODIFY COLUMN FolderCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         NOT NULL DEFAULT '' COMMENT '文件夹 Code → cert_standard_directory_folder.Code；根级文件恒为 空串',
  MODIFY COLUMN FullPath varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         DEFAULT NULL COMMENT '逻辑相对路径（判重键）= 文件夹路径 + 文件名';

ALTER TABLE cert_standard_directory_file
  ADD UNIQUE KEY uk_cfg_fullpath (ConfigCode, FullPath);

-- ------------------------------------------------------------
-- 5. cert_upload_task：DirectoryCode → ConfigCode
-- ------------------------------------------------------------
ALTER TABLE cert_upload_task
  CHANGE COLUMN DirectoryCode ConfigCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         NOT NULL DEFAULT '' COMMENT '配置 Code → cert_standard_directory_config.Code';

-- ------------------------------------------------------------
-- 6. 提取链：⑩ PhaseCode → StageCode（三张表统一）
--    ⑱ StandardFileCode 值域改为标准目录文件行 Code（GUID）⇒ 收窄到 36
-- ------------------------------------------------------------
ALTER TABLE cert_doc_extraction_rule
  CHANGE COLUMN PhaseCode StageCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         DEFAULT NULL COMMENT '阶段 Code → cert_cert_stage.Code（GUID）',
  MODIFY COLUMN StandardFileCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         DEFAULT NULL COMMENT '标准目录文件行 Code（GUID）；⛔ 不再是 FL-xxx 复合码';

ALTER TABLE cert_extraction_result
  CHANGE COLUMN PhaseCode StageCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         DEFAULT NULL COMMENT '阶段 Code → cert_cert_stage.Code（GUID）',
  MODIFY COLUMN StandardFileCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         DEFAULT NULL COMMENT '标准目录文件行 Code（GUID）';

ALTER TABLE cert_table_extraction_result
  CHANGE COLUMN PhaseCode StageCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         DEFAULT NULL COMMENT '阶段 Code → cert_cert_stage.Code（GUID）',
  MODIFY COLUMN StandardFileCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
         DEFAULT NULL COMMENT '标准目录文件行 Code（GUID）';

-- ------------------------------------------------------------
-- 7. 重建视图（与 scripts/db/views/*.sql 保持一致）
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW v_standard_directory_root_files AS
SELECT
    f.Id, f.Code, f.FileName, f.FileType, f.StoragePath,
    f.ConvertedStoragePath, f.ConvertStatus, f.ConvertMessage,
    f.PreviewPdfPath, f.MarkdownPath, f.MarkdownStatus, f.MarkdownMessage,
    f.UploadStatus, f.TaskId, f.ConfigCode, f.FolderCode,
    f.IsValid, f.IsDeleted, f.FileSize
FROM cert_standard_directory_file f
WHERE f.FolderCode = '';

CREATE OR REPLACE VIEW v_cert_configured_rules AS
SELECT
    r.Code AS RuleCode,
    r.StandardFileCode AS StandardFileCode,
    COALESCE(f.FileName, r.StandardFileCode) AS FileName,
    COALESCE(r.StandardCode, '') AS StandardCode,
    COALESCE(r.StageCode, '') AS StageCode,
    r.Skill AS Skill,
    r.DocIsValid AS DocIsValid,
    r.Status AS Status,
    r.CreateTime AS CreateTime,
    r.UpdateTime AS UpdateTime
FROM cert_doc_extraction_rule r
LEFT JOIN cert_standard_directory_file f
    ON r.StandardFileCode = f.Code AND f.IsDeleted = 0
WHERE r.IsDeleted = 0 AND r.IsValid = 1;

CREATE OR REPLACE VIEW v_upload_task_detail AS
SELECT
    t.TaskId,
    t.ConfigCode,
    t.TotalFiles,
    t.SuccessCount,
    t.Status,
    t.ExpireTime,
    f.Code AS FileCode,
    f.FileName,
    f.UploadStatus,
    f.StoragePath,
    f.IsValid AS FileIsValid,
    f.IsDeleted AS FileIsDeleted
FROM cert_upload_task t
LEFT JOIN cert_standard_directory_file f
    ON t.TaskId = f.TaskId COLLATE utf8mb4_unicode_ci AND f.IsDeleted = 0;

-- ------------------------------------------------------------
-- 8. 校验（预期：8 行，全部 ok）
-- ------------------------------------------------------------
SELECT 'config 无 DirectoryCode' AS chk, IF(COUNT(*)=0,'ok','FAIL') AS r
  FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='cert_standard_directory_config' AND COLUMN_NAME IN ('DirectoryCode','PhaseCode')
UNION ALL
SELECT 'config 有 StageCode', IF(COUNT(*)=1,'ok','FAIL')
  FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='cert_standard_directory_config' AND COLUMN_NAME='StageCode'
UNION ALL
SELECT 'folder 无 FolderCode', IF(COUNT(*)=0,'ok','FAIL')
  FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='cert_standard_directory_folder' AND COLUMN_NAME='FolderCode'
UNION ALL
SELECT 'file 无 FileCode', IF(COUNT(*)=0,'ok','FAIL')
  FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='cert_standard_directory_file' AND COLUMN_NAME='FileCode'
UNION ALL
SELECT '三表无 OrgCode', IF(COUNT(*)=0,'ok','FAIL')
  FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME IN ('cert_standard_directory_config','cert_standard_directory_folder','cert_standard_directory_file')
   AND COLUMN_NAME='OrgCode'
UNION ALL
-- ⚠️ 必须 COUNT(DISTINCT INDEX_NAME)：STATISTICS 每个索引列占一行，直接 COUNT(*) 会把
--    「3 个索引 7 列」算成 7 ⇒ 假失败。
SELECT '新唯一键齐备', IF(COUNT(DISTINCT INDEX_NAME)=3,'ok','FAIL')
  FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()
   AND INDEX_NAME IN ('uk_std_stage','uk_cfg_parent_name','uk_cfg_fullpath')
UNION ALL
SELECT '提取链已改 StageCode', IF(COUNT(*)=3,'ok','FAIL')
  FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME IN ('cert_doc_extraction_rule','cert_extraction_result','cert_table_extraction_result')
   AND COLUMN_NAME='StageCode'
UNION ALL
SELECT '三个视图已重建', IF(COUNT(*)=3,'ok','FAIL')
  FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME IN ('v_standard_directory_root_files','v_cert_configured_rules','v_upload_task_detail');
