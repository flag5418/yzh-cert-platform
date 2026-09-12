-- ============================================================
--  ISO 标准管理 — 数据库视图
--  功能：v_iso_standard 视图，含 CategoryName / StatusName 字典翻译
--  来源：老项目 cert_iso_standard_view.sql
-- ============================================================

DROP VIEW IF EXISTS v_iso_standard;

CREATE VIEW v_iso_standard AS
SELECT 
    s.id,
    s.code,
    s.standard_code,
    s.standard_name,
    s.version_year,
    s.category,
    s.description,
    s.remark,
    s.enable,
    s.create_date,
    s.modify_date,
    s.delete_time,
    s.create_by,
    s.update_by,
    s.delete_by,
    -- 分类中文名（质量管理/环境管理/医疗器械等）
    cat.DicName AS category_name,
    -- 状态中文名（草稿/已发布/已停用）
    sta.DicName AS status_name
FROM cert_iso_standard s
-- 分类字典
LEFT JOIN (
    SELECT dl.DicValue, dl.DicName, dl.Dic_ID
    FROM Sys_DictionaryList dl
    INNER JOIN Sys_Dictionary d ON dl.Dic_ID = d.Dic_ID
    WHERE d.DicNo = 'iso_category'
) cat ON s.category COLLATE utf8mb4_unicode_ci = cat.DicValue COLLATE utf8mb4_unicode_ci
-- 状态字典
LEFT JOIN (
    SELECT dl.DicValue, dl.DicName, dl.Dic_ID
    FROM Sys_DictionaryList dl
    INNER JOIN Sys_Dictionary d ON dl.Dic_ID = d.Dic_ID
    WHERE d.DicNo = 'standard_status'
) sta ON s.status COLLATE utf8mb4_unicode_ci = sta.DicValue COLLATE utf8mb4_unicode_ci;
