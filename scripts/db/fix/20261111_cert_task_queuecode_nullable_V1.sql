-- ============================================================================
-- cert_task.QueueCode 改为可空（2026-11-11）
--
-- 现象：认证准备/认证检查页点「自动分析」→ 后端 InsertAsync(CertTask) 报
--       Column 'QueueCode' cannot be null；前端只显示「成功 0 项，失败 1 项」。
-- 根因：实体 CertTask.QueueCode 是 string?（建任务时尚未入队），
--       但表列建成 NOT NULL 无默认值 ⇒ 严格模式 1364。
-- 语义：QueueCode 只在真正投递 yzh_queue 后才有值，创建阶段本就为空。
-- 幂等：MODIFY 可重复执行。
-- ============================================================================
ALTER TABLE `cert_task`
    MODIFY COLUMN `QueueCode` varchar(255) NULL DEFAULT NULL COMMENT '本次执行投递的队列（yzh_queue.QueueCode；未入队=NULL）';

SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_task'
  AND COLUMN_NAME = 'QueueCode';
