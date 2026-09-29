-- ============================================================
-- 视图：v_cert_configured_rules
-- 用途：已配置提取规则列表（跨表 JOIN cert_doc_extraction_rule + cert_standard_directory_file）
-- 日期：2026-09-20
-- 更新：审计列改为 PascalCase（code→Code, skill→Skill）
-- 更新：2026-09-26 P1 重建 —— ⑩ PhaseCode → StageCode；⑱ JOIN 键改为 f.Code（原 f.FileCode 复合码已删）
-- ============================================================

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
