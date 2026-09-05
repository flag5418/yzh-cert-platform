-- ============================================================================
-- V2 视图脚本：cert_*, wf_* 视图
-- 日期：2026-09-05
-- 说明：视图返回列名必须与 EF Core 实体 [Column] 映射一致（snake_case）
--       仅 JOIN 扩展字段（翻译/中文名）使用 PascalCase（[NotMapped]）
-- ============================================================================

SET NAMES utf8mb4;

-- ============================================================
-- v_cert_stage：认证阶段视图
-- 翻译：CategoryName（分类中文名）
-- ============================================================
DROP VIEW IF EXISTS v_cert_stage;

CREATE VIEW v_cert_stage AS
SELECT
    s.id,
    s.code,
    s.phase_code,           -- EF 映射: StageCode via [Column("phase_code")]
    s.phase_name,           -- EF 映射: StageName via [Column("phase_name")]
    s.description,
    s.sort_order,           -- EF 映射: SortOrder via [Column("sort_order")]
    s.category,
    cat.DicName     AS CategoryName,  -- [NotMapped] 扩展字段
    s.status,
    CASE s.status
        WHEN 'active'   THEN '启用'
        WHEN 'inactive' THEN '停用'
        ELSE s.status
    END             AS StatusName,    -- [NotMapped] 扩展字段
    s.remark,
    s.enable,
    s.create_id,
    s.creator,
    s.create_date,
    s.modify_id,
    s.modifier,
    s.modify_date,
    s.delete_id,
    s.deleter,
    s.delete_time
FROM cert_cert_stage s
LEFT JOIN Sys_DictionaryList cat
    ON cat.DicValue = s.category COLLATE utf8mb4_unicode_ci
    AND cat.Dic_ID = (SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'stage_category' LIMIT 1)
;

-- ============================================================
-- v_workflow：工作流定义视图
-- 翻译：IsActiveName, StatusName, WorkflowTypeName
-- 注意：DB 列名混合 - 业务字段 PascalCase，审计字段 snake_case
-- ============================================================
DROP VIEW IF EXISTS v_workflow;

CREATE VIEW v_workflow AS
SELECT
    w.Id,
    w.Code,
    w.OrgCode,
    w.WorkflowCode,
    w.WorkflowName,
    w.WorkflowType,
    CASE w.WorkflowType
        WHEN 'extraction'  THEN '提取'
        WHEN 'validation'  THEN '审核'
        WHEN 'report'      THEN '报告'
        ELSE w.WorkflowType
    END             AS WorkflowTypeName,  -- [NotMapped] 扩展字段
    w.WorkflowConfig,
    w.Version,
    w.IsActive,
    CASE w.IsActive
        WHEN 1 THEN '启用'
        WHEN 0 THEN '停用'
        ELSE CAST(w.IsActive AS CHAR)
    END             AS IsActiveName,      -- [NotMapped] 扩展字段
    w.Description,
    w.status,
    CASE w.status
        WHEN 'active'   THEN '启用'
        WHEN 'inactive' THEN '停用'
        ELSE w.status
    END             AS StatusName,        -- [NotMapped] 扩展字段
    w.Sort,
    w.Remark,
    w.enable,
    w.create_id,
    w.creator,
    w.create_date,
    w.modify_id,
    w.modifier,
    w.modify_date,
    w.delete_id,
    w.deleter,
    w.delete_time
FROM wf_workflow_definition w
;

-- ============================================================
-- v_iso_standard：ISO 标准视图
-- 翻译：CategoryName（分类中文名）、CbName（认证机构名称）
-- DB 列名 snake_case，视图仅对 JOIN 扩展字段做 PascalCase 别名
-- ============================================================
DROP VIEW IF EXISTS v_iso_standard;

CREATE VIEW v_iso_standard AS
SELECT
    s.Id,
    s.Code,
    s.OrgCode,
    s.cb_code,
    cb.short_name   AS CbName,          -- [NotMapped] 扩展字段
    s.standard_code,
    s.standard_name,
    s.version_year,
    s.category,
    cat.DicName     AS CategoryName,    -- [NotMapped] 扩展字段
    s.description,
    s.status,
    CASE s.status
        WHEN 'active'   THEN '启用'
        WHEN 'inactive' THEN '停用'
        ELSE s.status
    END             AS StatusName,      -- [NotMapped] 扩展字段
    s.Sort,
    s.Remark,
    s.enable,
    s.create_id,
    s.creator,
    s.create_date,
    s.modify_id,
    s.modifier,
    s.modify_date,
    s.delete_id,
    s.deleter,
    s.delete_time
FROM cert_iso_standard s
LEFT JOIN cert_certification_body cb ON s.cb_code = cb.Code COLLATE utf8mb4_unicode_ci
LEFT JOIN Sys_DictionaryList cat
    ON cat.DicValue = s.category COLLATE utf8mb4_unicode_ci
    AND cat.Dic_ID = (SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'iso_category' LIMIT 1)
;

-- ============================================================
-- 视图创建验证
-- ============================================================
SELECT '✅ v_cert_stage 创建完成' AS Result;
SELECT '✅ v_workflow 创建完成' AS Result;
SELECT '✅ v_iso_standard 创建完成' AS Result;
