DROP VIEW IF EXISTS v_iso_standard;

CREATE VIEW v_iso_standard AS
SELECT 
    s.*,
    cat.DicName AS CategoryName,    -- 分类中文名
    sta.DicName AS StatusName         -- 状态中文名
FROM cert_iso_standard s
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
SELECT Id, StandardCode, StandardName, Category, CategoryName, Status, StatusName 
FROM v_iso_standard 
LIMIT 5;
