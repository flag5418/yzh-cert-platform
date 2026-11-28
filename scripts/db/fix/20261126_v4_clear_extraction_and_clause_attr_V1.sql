-- ============================================================
-- 2026-11-26 · V4 清空提取链路干扰数据（依据 docs/80-v4/24 号评估）
--
-- 目的：把「文档提取产物」与「条款属性产物」彻底干掉，回到干净起点，
--       再用修好的提取链路重建（不保留任何历史残留 / 旧字段口径）。
--
-- 清理对象：
--   1) yzh_queue / yzh_queue_task —— 停掉在跑的提取队列（同队列串行，逐份跑）
--   2) cert_document_extraction    —— 文档提取产物（彻底删除，硬删）
--   3) cert_clause_attr            —— 条款属性 EAV 产物（彻底删除，硬删）
--   4) cert_enterprise_doc         —— 复位每份文档的提取状态（ExtractStatus/ExtractMessage/ExtractTime）
--                                    ⛔ 不动 DocProfileJson（画像 = 提取的「输入」，不是产物）
--
-- 幂等：可重复执行。
-- ⚠️ 前置：调用方需确认数据库已备份（用户已确认备份）。
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1. 停掉所有在跑的队列与任务（已 processing 的任务由后端重启时中断）
UPDATE yzh_queue
   SET Status = 'cancelled', EndTime = NOW()
 WHERE Status IN ('running', 'pending');

UPDATE yzh_queue_task
   SET Status = 'cancelled', CompleteTime = NOW()
 WHERE Status IN ('pending', 'processing');

-- 2. 文档提取产物：硬删
DELETE FROM cert_document_extraction;

-- 3. 条款属性产物：硬删
DELETE FROM cert_clause_attr;

-- 4. 复位企业文档提取状态
UPDATE cert_enterprise_doc
   SET ExtractStatus = 'pending',
       ExtractMessage = NULL,
       ExtractTime = NULL
 WHERE IsDeleted = 0;

-- 5. 验证（期望 doc_ext=0 / clause_attr=0 / queue_running=0）
SELECT (SELECT COUNT(*) FROM cert_document_extraction)                                  AS doc_ext,
       (SELECT COUNT(*) FROM cert_clause_attr)                                          AS clause_attr,
       (SELECT COUNT(*) FROM yzh_queue      WHERE Status IN ('running','pending'))       AS queue_running,
       (SELECT COUNT(*) FROM yzh_queue_task WHERE Status IN ('pending','processing'))     AS task_open,
       (SELECT COUNT(*) FROM cert_enterprise_doc WHERE IsDeleted=0 AND ExtractStatus='done') AS doc_done;
