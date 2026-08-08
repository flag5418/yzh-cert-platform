DROP VIEW IF EXISTS v_cert_stage;

CREATE VIEW v_cert_stage AS
SELECT 
    s.*,
    cat.DicName AS CategoryName,
    sta.DicName AS StatusName
FROM cert_cert_stage s
-- 分类字典
LEFT JOIN (
    SELECT dl.DicValue, dl.DicName, dl.Dic_ID
    FROM Sys_DictionaryList dl
    INNER JOIN Sys_Dictionary d ON dl.Dic_ID = d.Dic_ID
    WHERE d.DicNo = 'stage_category'
) cat ON s.Category COLLATE utf8mb4_unicode_ci = cat.DicValue COLLATE utf8mb4_unicode_ci
-- 状态字典
LEFT JOIN (
    SELECT dl.DicValue, dl.DicName, dl.Dic_ID
    FROM Sys_DictionaryList dl
    INNER JOIN Sys_Dictionary d ON dl.Dic_ID = d.Dic_ID
    WHERE d.DicNo = 'stage_status'
) sta ON s.Status COLLATE utf8mb4_unicode_ci = sta.DicValue COLLATE utf8mb4_unicode_ci;

-- 验证
SELECT Id, StageCode, StageName, Category, CategoryName, Status, StatusName 
FROM v_cert_stage 
ORDER BY SortOrder
LIMIT 10;
