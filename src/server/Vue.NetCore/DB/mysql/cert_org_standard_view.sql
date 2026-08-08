-- ============================================================
-- cert_org_standard 视图
-- 用于列表显示，包含标准信息和字典翻译
-- ============================================================

DROP VIEW IF EXISTS v_cert_org_standard;

CREATE VIEW v_cert_org_standard AS
SELECT 
    os.*,
    s.StandardCode,
    s.StandardName,
    s.VersionYear,
    cat.DicName AS CategoryName,
    sta.DicName AS StatusName
FROM cert_org_standard os
LEFT JOIN cert_iso_standard s ON os.StdId = s.Id
-- 分类字典
LEFT JOIN (
    SELECT dl.DicValue, dl.DicName, dl.Dic_ID
    FROM Sys_DictionaryList dl
    INNER JOIN Sys_Dictionary d ON dl.Dic_ID = d.Dic_ID
    WHERE d.DicNo = 'iso_category'
) cat ON s.Category COLLATE utf8mb4_unicode_ci = cat.DicValue COLLATE utf8mb4_unicode_ci
-- 状态字典
LEFT JOIN (
    SELECT dl.DicValue, dl.DicName, dl.Dic_ID
    FROM Sys_DictionaryList dl
    INNER JOIN Sys_Dictionary d ON dl.Dic_ID = d.Dic_ID
    WHERE d.DicNo = 'standard_status'
) sta ON s.Status COLLATE utf8mb4_unicode_ci = sta.DicValue COLLATE utf8mb4_unicode_ci;

-- 验证
SELECT Id, CbCode, StdId, StdCode, StandardName, CategoryName, StatusName 
FROM v_cert_org_standard 
LIMIT 10;

SELECT '✅ v_cert_org_standard 视图创建完成' AS Result;
