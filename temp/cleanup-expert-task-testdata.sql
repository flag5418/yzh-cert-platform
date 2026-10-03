-- ══════════════════════════════════════════════════════════════════
-- 专家任务系统 —— S4 自测数据清理（临时脚本，位于 temp/，不入库）
--
-- 用途：把自测期间产生的「任务执行数据」清空，保留**长期实体与配置**：
--   ✅ 保留：cert_expert_nc_item / cert_expert_report_section_item（长期检查项实体）
--   ✅ 保留：cert_validation_rule、cert_iso_standard、cert_cert_stage、cert_enterprise、
--            cert_enterprise_stage（配置与主数据 —— 清掉就没法再建任务了）
--   ❌ 清空：任务头 / 标准子任务 / 队列 / 队列项 / 结论 / 日志 / 数据缺口
--
-- 执行：
--   docker exec -i yzh-mysql mysql -uroot -p$MYSQL_ROOT_PASSWORD --default-character-set=utf8mb4 \
--     yzh_cert_platform < temp/cleanup-expert-task-testdata.sql
-- ══════════════════════════════════════════════════════════════════

-- ⚠️ 顺序：先子表后主表（无外键，但便于中途失败时判断进度）

-- 1) 结论（按轮次的结果）
DELETE FROM cert_expert_nc_result;
DELETE FROM cert_expert_report_result;

-- 2) 队列项 → 队列
DELETE FROM cert_expert_task_queue_item;
DELETE FROM cert_expert_task_queue;

-- 3) 标准子任务
DELETE FROM cert_expert_task_standard;

-- 4) 运行日志 / 数据缺口
DELETE FROM cert_expert_task_log;
DELETE FROM cert_expert_task_data_gap;

-- 5) 任务头（最后删 —— ActiveLockKey 随之消失 ⇒ 业务锁自动释放）
DELETE FROM cert_expert_task;

-- 6) 长期检查项实体上残留的「当前结果指针」/ 轮次计数复位
--    （实体本身保留，只把指向已删结果的指针清掉，否则结果页会出现空行）
UPDATE cert_expert_nc_item
SET CurrentResultCode = NULL, RoundCount = 0, LastReviewTime = NULL;
UPDATE cert_expert_report_section_item
SET CurrentResultCode = NULL, RoundCount = 0, LastReviewTime = NULL;

-- ── 核对：应全部为 0 ──
SELECT 'task' t, COUNT(*) n FROM cert_expert_task
UNION ALL SELECT 'standard', COUNT(*) FROM cert_expert_task_standard
UNION ALL SELECT 'queue', COUNT(*) FROM cert_expert_task_queue
UNION ALL SELECT 'qitem', COUNT(*) FROM cert_expert_task_queue_item
UNION ALL SELECT 'nc_result', COUNT(*) FROM cert_expert_nc_result
UNION ALL SELECT 'rpt_result', COUNT(*) FROM cert_expert_report_result
UNION ALL SELECT 'log', COUNT(*) FROM cert_expert_task_log
UNION ALL SELECT 'gap', COUNT(*) FROM cert_expert_task_data_gap;
