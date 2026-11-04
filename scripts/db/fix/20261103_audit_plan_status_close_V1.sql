-- =============================================================================
-- 2026-11-03 · cert_audit_plan 增加 Status 列（计划可关闭）
--
-- 背景（用户 2026-11-03）：
--   认证计划可以关闭；一旦关闭，对应的认证准备 / 认证检查同时作废，只能查看。
--
-- 口径：
--   Status = 'active'  进行中（默认）
--   Status = 'closed'  已关闭（准备/检查只读）
--   ⛔ 不复用 IsValid：IsValid 仍表示启用/停用（铁律九）；状态列必须叫 Status。
--
-- 幂等：列已存在则跳过。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SET @has_status := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'cert_audit_plan'
      AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'Status'
);

SET @ddl := IF(
    @has_status = 0,
    'ALTER TABLE cert_audit_plan ADD COLUMN Status varchar(20) NOT NULL DEFAULT ''active'' COMMENT ''计划状态：active 进行中 / closed 已关闭'' AFTER Standards',
    'SELECT ''Status 列已存在，跳过'' AS Skip'
);

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 验证
SELECT CONCAT('Status 列数 = ', COUNT(*)) AS Result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_audit_plan'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'Status';

SELECT Status, COUNT(*) AS Cnt FROM cert_audit_plan GROUP BY Status;
