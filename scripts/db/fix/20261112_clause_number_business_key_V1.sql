-- 2026-11-12 条款关联从 Code 改为业务键 (StandardNo, ClauseNumber) —— 第一阶段
-- 背景：标准条款重建后旧 Code 全部软删，属性 / 计划 / 检查 / 准备按旧 Code 关联会静默失效。
-- 目标：新增业务键字段并回填；ClauseCode 暂时保留为运行时缓存，不删列、不改旧接口。
-- 幂等：加列 / 加索引 / 回填都先判存在。

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

DROP PROCEDURE IF EXISTS _tmp_add_column_if_missing;
DELIMITER $$
CREATE PROCEDURE _tmp_add_column_if_missing(
    IN p_table VARCHAR(64),
    IN p_column VARCHAR(64),
    IN p_definition VARCHAR(500)
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = p_table
          AND column_name = p_column
    ) THEN
        SET @ddl = CONCAT('ALTER TABLE `', p_table, '` ADD COLUMN `', p_column, '` ', p_definition);
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END$$
DELIMITER ;

CALL _tmp_add_column_if_missing('cert_clause_attr', 'ClauseNumber', 'VARCHAR(50) NULL COMMENT ''条款编号（业务键，替代易变的 ClauseCode）'' AFTER `ClauseCode`');
CALL _tmp_add_column_if_missing('cert_audit_plan_detail', 'ClauseRefsJson', 'LONGTEXT NULL COMMENT ''条款业务键 JSON：[{StandardNo,ClauseNumber}]'' AFTER `ClauseNumbers`');
CALL _tmp_add_column_if_missing('cert_evidence', 'StandardNo', 'VARCHAR(50) NULL COMMENT ''标准短码（iso9001）'' AFTER `ClauseCode`');
CALL _tmp_add_column_if_missing('cert_evidence', 'ClauseNumber', 'VARCHAR(50) NULL COMMENT ''条款编号（业务键）'' AFTER `StandardNo`');
CALL _tmp_add_column_if_missing('cert_onsite_plan_item', 'ClauseRefsJson', 'LONGTEXT NULL COMMENT ''条款业务键 JSON：[{StandardNo,ClauseNumber}]'' AFTER `ClauseCodes`');

DROP PROCEDURE IF EXISTS _tmp_add_column_if_missing;

DROP PROCEDURE IF EXISTS _tmp_add_index_if_missing;
DELIMITER $$
CREATE PROCEDURE _tmp_add_index_if_missing(
    IN p_table VARCHAR(64),
    IN p_index VARCHAR(64),
    IN p_columns VARCHAR(500)
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE()
          AND table_name = p_table
          AND index_name = p_index
    ) THEN
        SET @ddl = CONCAT('CREATE INDEX `', p_index, '` ON `', p_table, '` (', p_columns, ')');
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END$$
DELIMITER ;

CALL _tmp_add_index_if_missing('cert_clause_attr', 'idx_ca_std_number', '`StandardCode`, `ClauseNumber`, `IsDeleted`, `IsValid`');
CALL _tmp_add_index_if_missing('cert_evidence', 'idx_evi_std_number', '`StandardNo`, `ClauseNumber`, `IsDeleted`, `IsValid`');

DROP PROCEDURE IF EXISTS _tmp_add_index_if_missing;

-- ① 属性表回填条款号：ClauseCode 仍指向旧行也能取到编号
UPDATE cert_clause_attr a
JOIN cert_iso_clause c ON c.Code = a.ClauseCode
SET a.ClauseNumber = c.ClauseNumber
WHERE (a.ClauseNumber IS NULL OR a.ClauseNumber = '')
  AND c.ClauseNumber IS NOT NULL
  AND c.ClauseNumber <> '';

-- ② 计划明细回填业务键 JSON：按 Clauses 里的 Code 找条款号 + 标准短码，保持原顺序
DROP TEMPORARY TABLE IF EXISTS tmp_plan_clause_refs;
CREATE TEMPORARY TABLE tmp_plan_clause_refs AS
SELECT
    d.Code AS DetailCode,
    CAST(CONCAT(
        '[',
        GROUP_CONCAT(
            JSON_OBJECT('StandardNo', std.StandardCode, 'ClauseNumber', c.ClauseNumber)
            ORDER BY jt.ord SEPARATOR ','
        ),
        ']'
    ) AS CHAR) AS RefsJson,
    COUNT(*) AS CodeCount,
    SUM(CASE WHEN c.Code IS NULL OR std.Code IS NULL THEN 1 ELSE 0 END) AS MissingCount
FROM cert_audit_plan_detail d
JOIN JSON_TABLE(
    CONCAT('["', REPLACE(COALESCE(d.Clauses, ''), ',', '","'), '"]'),
    '$[*]' COLUMNS (
        ord FOR ORDINALITY,
        clause_code VARCHAR(100) PATH '$'
    )
) jt
LEFT JOIN cert_iso_clause c ON c.Code = jt.clause_code
LEFT JOIN cert_iso_standard std ON std.Code = c.StandardCode AND std.IsDeleted = 0
WHERE d.IsDeleted = 0
  AND COALESCE(d.Clauses, '') <> ''
  AND (d.ClauseRefsJson IS NULL OR d.ClauseRefsJson = '')
GROUP BY d.Code
HAVING MissingCount = 0;

UPDATE cert_audit_plan_detail d
JOIN tmp_plan_clause_refs m ON m.DetailCode = d.Code
SET d.ClauseRefsJson = m.RefsJson,
    d.UpdateTime = NOW()
WHERE d.IsDeleted = 0
  AND (d.ClauseRefsJson IS NULL OR d.ClauseRefsJson = '');

DROP TEMPORARY TABLE IF EXISTS tmp_plan_clause_refs;

-- ③ 验证：应无空条款号属性、无空 refs 的已配置计划明细
SELECT
    (SELECT COUNT(*) FROM cert_clause_attr WHERE IsDeleted = 0 AND IsValid = 1 AND (ClauseNumber IS NULL OR ClauseNumber = '')) AS attr_missing_number,
    (SELECT COUNT(*) FROM cert_audit_plan_detail WHERE IsDeleted = 0 AND COALESCE(Clauses, '') <> '' AND (ClauseRefsJson IS NULL OR ClauseRefsJson = '')) AS plan_missing_refs;
