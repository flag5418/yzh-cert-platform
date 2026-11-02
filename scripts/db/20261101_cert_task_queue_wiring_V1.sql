-- =============================================================================
-- 认证任务 · 队列接线（V2 专项 — 46 号 §五 / M3）
-- 日期：2026-11-01
--
-- 背景：`StartTaskAsync` 原来只改 `Status='running'`，既不投队列也不遍历 task_item
--       （46 号 F1）⇒ 点「开始」等于什么都没发生。
--
-- 本脚本只做一件事：
--   ① cert_task 加 QueueCode —— 记住「这次执行投到哪条 yzh_queue」
--      用途：暂停要 CancelQueueAsync(queueCode) / 页面锁要查队列进度 / 排查要能反查
--   ② cert_task 加 QueueAttempt —— 重跑次数（同一任务可多次入队，QueueCode 会被覆盖，
--      旧队列靠 QueueAttempt 留痕）
--   ③ 验收
--
-- ★ 幂等：可重复执行
-- ★ 为什么不建新表：队列本身就是 yzh_queue / yzh_queue_task（框架统一队列），
--   业务侧只需要一个「反查指针」，加一列足够。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SELECT '========== 开始：认证任务 队列接线 V1 ==========' AS `step`;


-- ─────────────────────────────────────────────────────────────────────────────
-- ① cert_task.QueueCode
-- ─────────────────────────────────────────────────────────────────────────────

SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_task' AND COLUMN_NAME = 'QueueCode'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_task` ADD COLUMN `QueueCode` VARCHAR(50) NULL DEFAULT NULL COMMENT ''本次执行投递的队列（关联 yzh_queue.QueueCode；暂停/取消/进度反查用）'' AFTER `Message`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_task' AND COLUMN_NAME = 'QueueAttempt'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_task` ADD COLUMN `QueueAttempt` INT NOT NULL DEFAULT 0 COMMENT ''第几次入队（重跑 +1；QueueCode 只留最后一次）'' AFTER `QueueCode`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_task' AND INDEX_NAME = 'idx_queue_code'
);
SET @ddl := IF(@idx_exists = 0,
  'ALTER TABLE `cert_task` ADD KEY `idx_queue_code` (`QueueCode`)',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ─────────────────────────────────────────────────────────────────────────────
-- ② 验收
-- ─────────────────────────────────────────────────────────────────────────────

SELECT '---------- 验收 ----------' AS `step`;

SELECT '2.1 cert_task 新列' AS `check`,
       GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION) AS `columns`,
       IF(COUNT(*) = 2, 'OK', '❌ 缺列') AS `result`
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_task'
  AND COLUMN_NAME IN ('QueueCode', 'QueueAttempt');

SELECT '2.2 索引' AS `check`,
       IF(COUNT(*) > 0, 'OK', '❌ 缺索引') AS `result`
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_task' AND INDEX_NAME = 'idx_queue_code';

SELECT '========== 完成：队列接线就绪 ==========' AS `step`;
