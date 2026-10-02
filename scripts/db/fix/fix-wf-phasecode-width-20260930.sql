-- ============================================================
-- fix-wf-phasecode-width-20260930.sql
--
-- 背景（B1 阻断缺陷，25 号 §1.3 / §5.1）：
--   引擎层与提取层的阶段编码口径不一致，导致 NodeExecutor 的
--   docfield / doctable 节点【恒 not_found】⇒ 抛"缺失必要数据"，
--   任何规则都跑不出结果。
--
--   引擎层  wf_execution_task.PhaseCode  varchar(30)  存人读短码 '03'
--   提取层  cert_extraction_result.StageCode varchar(36) 存 GUID 29c1bcc…
--
--   NodeExecutor.cs:355 拿 contextParams["phaseCode"]（'03'）
--   去比 x.StageCode（GUID）⇒ 永不匹配 ⇒ 抛 WorkflowDataMissingException。
--
-- 为何漏掉：10 号 Q1-旧 裁决「阶段编码统一到 GUID」时只改了
--   配置层 2 张表（cert_validation_rule / cert_report_section），
--   ★ 引擎层 wf_execution_task.PhaseCode 的 varchar(30) 列宽约束被漏掉，
--   32 位 GUID 装不下 ⇒ 只能继续存短码。
--
-- 本脚本：扩列到 varchar(36) 放得下 GUID。
--   配套代码：ExpertItemExecutors.cs 的
--     PhaseCode = p.StageNo ?? p.StageCode   →   PhaseCode = p.StageCode
--   （StageNo 短码保留在 queue_item.Payload 里供前端展示，不再当过滤键）
--
-- 幂等：可重复执行。
-- ============================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────
-- 1. 扩列
-- ────────────────────────────────────────────────────────────
SET @exist := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'wf_execution_task'
    AND COLUMN_NAME  = 'PhaseCode'
    AND CHARACTER_MAXIMUM_LENGTH >= 36);

SET @ddl := IF(@exist = 1,
  'SELECT ''wf_execution_task.PhaseCode 已 >= varchar(36)，跳过'' AS msg',
  'ALTER TABLE wf_execution_task
     MODIFY COLUMN `PhaseCode` varchar(36) DEFAULT NULL
       COMMENT ''阶段编码（★GUID，关联 cert_cert_stage.Code；人读短码见 cert_cert_stage.StageCode）''');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ────────────────────────────────────────────────────────────
-- 2. 回填历史数据：短码 → GUID
--    映射依据：cert_cert_stage.StageCode（人读短码）→ cert_cert_stage.Code（GUID）
--
--    ⚠️ 需先确认映射 1:1（见下方 dry-run），不唯一则【不执行本段】。
--    ⚠️ 只改 PhaseCode 一个列的取值，不动其他任何字段（行数不变、Code 不变、
--       IsValid 不变）—— 执行历史的时间/结果/耗时全部保留。
-- ────────────────────────────────────────────────────────────

-- 2.1 dry-run：短码 → GUID 映射是否唯一
SELECT '★ 若 w > 0 说明短码映射不唯一，禁止回填' AS guard;
SELECT s.StageCode AS ShortCode, COUNT(*) AS w, MIN(s.Code) AS OneOfGuid
  FROM cert_cert_stage s
 WHERE s.StageCode IS NOT NULL AND s.StageCode <> ''
 GROUP BY s.StageCode
HAVING COUNT(*) > 1;

-- 2.2 待回填行数（只读确认）
SELECT COUNT(*) AS ToBackfill
  FROM wf_execution_task t
 WHERE t.PhaseCode IS NOT NULL
   AND t.PhaseCode <> ''
   AND CHAR_LENGTH(t.PhaseCode) <> 32
   AND EXISTS (SELECT 1 FROM cert_cert_stage s
                WHERE s.StageCode = t.PhaseCode AND CHAR_LENGTH(s.Code) = 32);

-- 2.3 执行回填（只更新能唯一解析出 GUID 的行）
UPDATE wf_execution_task t
  JOIN cert_cert_stage s
    ON s.StageCode = t.PhaseCode AND CHAR_LENGTH(s.Code) = 32
 SET t.PhaseCode = s.Code
WHERE t.PhaseCode IS NOT NULL
  AND t.PhaseCode <> ''
  AND CHAR_LENGTH(t.PhaseCode) <> 32;

-- 2.4 回填后复核：应剩 0 行非 GUID
SELECT COUNT(*) AS StillShortCode
  FROM wf_execution_task
 WHERE PhaseCode IS NOT NULL AND PhaseCode <> '' AND CHAR_LENGTH(PhaseCode) <> 32;

-- ────────────────────────────────────────────────────────────
-- 3. 验证（★ 必须用 CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin，
--    否则 information_schema.COLUMN_NAME 排序规则大小写不敏感会误判）
-- ────────────────────────────────────────────────────────────
SELECT CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin AS ColumnName,
       COLUMN_TYPE,
       IS_NULLABLE,
       COLUMN_COMMENT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME   = 'wf_execution_task'
   AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'PhaseCode';

-- 口径对照：引擎层与提取层现在应能用同一个值对上（★ 25 号步骤 0 的正反信号前提）
SELECT '引擎层' AS Layer, Code AS RefCode, PhaseCode
  FROM wf_execution_task
 WHERE PhaseCode IS NOT NULL AND PhaseCode <> ''
 ORDER BY CreateTime DESC LIMIT 5;

SELECT '提取层' AS Layer, StageCode AS RefCode
  FROM cert_extraction_result
 WHERE StageCode IS NOT NULL AND StageCode <> ''
 GROUP BY StageCode;
