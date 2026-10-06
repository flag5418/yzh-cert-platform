-- ══════════════════════════════════════════════════════════════════════════
-- 20261005 · cert_doc_template_anchor 增加「锚点锁定」列（IsLocked）
-- ══════════════════════════════════════════════════════════════════════════
-- 背景（用户原话，2026-10-05）：
--   「锚点的设置增加锁定，因为可能设置好了几个字段的属性，但我们 word 的模板不合适，
--     我们需要更改，我们也可以记录哪些字段的设置我们已经确定了」
--   「锚点是实施人员操作的，他认可了这个设置规则 ok 了，就加上锚点了」
--
-- 语义：
--   IsLocked = 1 ⇒ 实施人员**已认可**该锚点的设置规则。
--     · 换模板**重新扫描**时，该锚点的配置（数据源 / 参数 / 提示词 / 操作）**必须保留**
--       （由 DocTemplateAnchor/scan 做 merge，见 52 号 TODO 的 B6）
--   IsLocked = 0 ⇒ 未认可，重扫时按扫描结果覆盖。
--
-- ⛔ 不另设 LockedBy / LockedTime：
--   BaseEntity 的 UpdateBy / UpdateTime 已经记录「谁在何时认可的」。
--
-- 幂等：可重复执行（先探测列是否存在）
-- ══════════════════════════════════════════════════════════════════════════

SET @exist := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_doc_template_anchor'
    AND COLUMN_NAME  = 'IsLocked'
);

SET @sql := IF(@exist = 0,
  'ALTER TABLE cert_doc_template_anchor
     ADD COLUMN IsLocked TINYINT(1) NOT NULL DEFAULT 0
     COMMENT ''锁定=实施人员已认可该锚点的设置规则（换模板重扫时保留配置）''
     AFTER Required',
  'SELECT ''IsLocked 已存在，跳过'' AS msg');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ── 校验 ──
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME   = 'cert_doc_template_anchor'
  AND COLUMN_NAME  = 'IsLocked';

SELECT COUNT(*) AS total, SUM(IsLocked) AS locked
FROM cert_doc_template_anchor;
