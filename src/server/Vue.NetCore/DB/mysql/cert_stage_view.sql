-- v_cert_stage 视图：cert_cert_stage + 字典翻译（CategoryName/StatusName）
-- 列别名统一为 PascalCase，与 C# CertStageView 属性名对齐，Dapper 直接映射
-- 字典来源：stage_category（流程/审核/证后）、stage_status（启用/停用）

DROP VIEW IF EXISTS v_cert_stage;

CREATE VIEW v_cert_stage AS
SELECT
    s.id            AS Id,
    s.code          AS Code,
    s.phase_code    AS StageCode,
    s.phase_name    AS StageName,
    s.description   AS Description,
    s.sort_order    AS SortOrder,
    s.category      AS Category,
    cat.DicName     AS CategoryName,
    s.status        AS Status,
    sta.DicName     AS StatusName,
    s.remark        AS Remark,
    s.enable        AS Enable,
    s.create_id     AS CreateID,
    s.creator       AS Creator,
    s.create_date   AS CreateDate,
    s.modify_id     AS ModifyID,
    s.modifier      AS Modifier,
    s.modify_date   AS ModifyDate,
    s.delete_id     AS DeleteID,
    s.deleter       AS Deleter,
    s.delete_time   AS DeleteTime
FROM cert_cert_stage s
-- 分类字典（stage_category）
LEFT JOIN (
    SELECT dl.DicValue, dl.DicName, dl.Dic_ID
    FROM Sys_DictionaryList dl
    INNER JOIN Sys_Dictionary d ON dl.Dic_ID = d.Dic_ID
    WHERE d.DicNo = 'stage_category'
) cat ON s.Category COLLATE utf8mb4_unicode_ci = cat.DicValue COLLATE utf8mb4_unicode_ci
-- 状态字典（stage_status）
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
