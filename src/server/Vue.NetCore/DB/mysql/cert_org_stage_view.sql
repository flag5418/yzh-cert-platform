-- ============================================================
-- cert_org_stage 视图
-- 用于列表显示，包含阶段信息和字典翻译
-- ============================================================

DROP VIEW IF EXISTS v_cert_org_stage;

CREATE VIEW v_cert_org_stage AS
SELECT 
    os.Id,
    os.CbCode,
    os.StageId,
    os.StageCode,
    s.StageName,
    s.SortOrder,
    cat.DicName AS CategoryName,
    sta.DicName AS StatusName,
    os.EnabledAt,
    os.Remark
FROM cert_org_stage os
LEFT JOIN cert_cert_stage s ON os.StageId = s.Id
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
SELECT Id, CbCode, StageId, StageCode, StageName, CategoryName, StatusName 
FROM v_cert_org_stage 
LIMIT 10;

SELECT '✅ v_cert_org_stage 视图创建完成' AS Result;
