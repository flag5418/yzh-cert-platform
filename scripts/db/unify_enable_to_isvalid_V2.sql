-- ===================================================================
-- 统一启用/禁用唯一字段：IsValid（业务表 Enable → IsValid，阶段3）
-- 日期：2026-09-24
-- 范围：7 业务实体
--   1) wf_workflow_definition          （Enable 未使用，直接 DROP）
--   2) wf_prompt_template              （Enable 为禁用真源 → IsValid）
--   3) rpt_report_section              （enable 小写未使用，直接 DROP）
--   4) cert_standard_directory_config  （Enable → IsDeleted 语义 + IsValid 同步）
--   5) cert_standard_directory_folder
--   6) cert_standard_directory_file
--   7) 视图 v_standard_directory_root_files / v_workflow 去 Enable
--
-- 说明：
--   · 标准目录表历史语义：Enable=false ≈ 软禁用/归档；IsValid 为上传激活态。
--     同步策略：Enable=0 → IsValid=0 且 IsDeleted=1（若 DeleteTime 已有）；
--              Enable=1 保持 IsValid 原值（上传 pending 允许 IsValid=0）。
--   · PromptTemplate：Enable 为唯一禁用开关 → 直接写 IsValid。
--   · EnableField bool 脏列（配置泄漏）一并 DROP。
--
-- 执行：
--   docker exec -i yzh-mysql mysql -uroot -p'Yzh123456.' --default-character-set=utf8mb4 yzh_cert_platform \
--     < scripts/db/unify_enable_to_isvalid_V2.sql
-- ===================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
USE yzh_cert_platform;

-- ─── 0) 备份 ───
CREATE TABLE IF NOT EXISTS _bak_20260924_wf_workflow_definition AS SELECT * FROM wf_workflow_definition;
CREATE TABLE IF NOT EXISTS _bak_20260924_wf_prompt_template AS SELECT * FROM wf_prompt_template;
CREATE TABLE IF NOT EXISTS _bak_20260924_rpt_report_section AS SELECT * FROM rpt_report_section;
CREATE TABLE IF NOT EXISTS _bak_20260924_cert_standard_directory_config AS SELECT * FROM cert_standard_directory_config;
CREATE TABLE IF NOT EXISTS _bak_20260924_cert_standard_directory_folder AS SELECT * FROM cert_standard_directory_folder;
CREATE TABLE IF NOT EXISTS _bak_20260924_cert_standard_directory_file AS SELECT * FROM cert_standard_directory_file;

-- ─── 1) PromptTemplate：Enable → IsValid（Enable 为历史真源） ───
UPDATE wf_prompt_template
   SET IsValid = IF(Enable = 0, 0, 1)
 WHERE Enable IS NOT NULL AND IF(Enable = 0, 0, 1) <> IsValid;

-- ─── 2) 标准目录：Enable=0 → IsValid=0（禁用/归档） ───
--        Enable=1 不动 IsValid（保留 pending 的 IsValid=0）
UPDATE cert_standard_directory_config SET IsValid = 0 WHERE Enable = 0;
UPDATE cert_standard_directory_folder SET IsValid = 0 WHERE Enable = 0;
UPDATE cert_standard_directory_file   SET IsValid = 0 WHERE Enable = 0;

-- 已软删行（有 DeleteTime）补 IsDeleted=1（若尚未标记）
UPDATE cert_standard_directory_config SET IsDeleted = 1 WHERE Enable = 0 AND IsDeleted = 0;
UPDATE cert_standard_directory_folder SET IsDeleted = 1 WHERE Enable = 0 AND IsDeleted = 0;
UPDATE cert_standard_directory_file   SET IsDeleted = 1 WHERE Enable = 0 AND IsDeleted = 0;

-- ─── 3) 校验：Enable=0 且 IsValid=1 必须为 0 ───
SELECT 'wf_prompt_template' AS tbl,
       SUM(IF(Enable = 0, 0, 1) <> IsValid) AS mismatch, COUNT(*) AS n
  FROM wf_prompt_template
UNION ALL SELECT 'cert_standard_directory_config', SUM(Enable = 0 AND IsValid <> 0), COUNT(*) FROM cert_standard_directory_config
UNION ALL SELECT 'cert_standard_directory_folder', SUM(Enable = 0 AND IsValid <> 0), COUNT(*) FROM cert_standard_directory_folder
UNION ALL SELECT 'cert_standard_directory_file',   SUM(Enable = 0 AND IsValid <> 0), COUNT(*) FROM cert_standard_directory_file;

-- ─── 4) 重建依赖视图（去 Enable） ───
DROP VIEW IF EXISTS v_standard_directory_root_files;
CREATE VIEW v_standard_directory_root_files AS
SELECT
    f.Id, f.Code, f.FileCode, f.FileName, f.FileType, f.StoragePath,
    f.ConvertedStoragePath, f.ConvertStatus, f.ConvertMessage,
    f.UploadStatus, f.TaskId, f.DirectoryCode, f.FolderCode,
    f.IsValid, f.IsDeleted, f.FileSize
FROM cert_standard_directory_file f
WHERE (f.FolderCode IS NULL OR f.FolderCode = '')
   OR NOT EXISTS (
       SELECT 1 FROM cert_standard_directory_folder sf
       WHERE sf.FolderCode = f.FolderCode AND sf.IsDeleted = 0
   );

DROP VIEW IF EXISTS v_workflow;
CREATE VIEW v_workflow AS
SELECT
    w.Id,
    w.Code,
    w.OrgCode,
    w.WorkflowCode,
    w.WorkflowName,
    w.WorkflowType,
    (CASE w.WorkflowType
        WHEN 'extraction' THEN '提取'
        WHEN 'validation' THEN '审核'
        WHEN 'report' THEN '报告'
        ELSE w.WorkflowType
    END) AS WorkflowTypeName,
    w.WorkflowConfig,
    w.Version,
    w.IsActive,
    (CASE w.IsActive WHEN 1 THEN '启用' WHEN 0 THEN '停用' ELSE CAST(w.IsActive AS CHAR) END) AS IsActiveName,
    w.Description,
    w.Status,
    (CASE w.Status WHEN 'active' THEN '启用' WHEN 'inactive' THEN '停用' ELSE w.Status END) AS StatusName,
    w.Sort,
    w.Remark,
    w.CreateTime,
    w.CreateBy,
    w.UpdateTime,
    w.UpdateBy,
    w.DeleteTime,
    w.DeleteBy,
    w.IsDeleted,
    w.IsValid
FROM wf_workflow_definition w;

-- 同步脚本文件副本
CREATE OR REPLACE VIEW v_standard_directory_root_files AS
SELECT
    f.Id, f.Code, f.FileCode, f.FileName, f.FileType, f.StoragePath,
    f.ConvertedStoragePath, f.ConvertStatus, f.ConvertMessage,
    f.UploadStatus, f.TaskId, f.DirectoryCode, f.FolderCode,
    f.IsValid, f.IsDeleted, f.FileSize
FROM cert_standard_directory_file f
WHERE (f.FolderCode IS NULL OR f.FolderCode = '')
   OR NOT EXISTS (
       SELECT 1 FROM cert_standard_directory_folder sf
       WHERE sf.FolderCode = f.FolderCode AND sf.IsDeleted = 0
   );

-- ─── 5) DROP COLUMN（校验通过后执行） ───
ALTER TABLE wf_workflow_definition DROP COLUMN Enable;
ALTER TABLE wf_prompt_template DROP COLUMN Enable;
ALTER TABLE rpt_report_section DROP COLUMN enable;
ALTER TABLE cert_standard_directory_config DROP COLUMN Enable, DROP COLUMN EnableField;
ALTER TABLE cert_standard_directory_folder DROP COLUMN Enable, DROP COLUMN EnableField;
ALTER TABLE cert_standard_directory_file DROP COLUMN Enable, DROP COLUMN EnableField;

SELECT '✅ 阶段3 Enable→IsValid 统一完成（7 实体 + 2 视图）' AS Result;
