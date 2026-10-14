-- ============================================================
-- 20261014_queue_msg_createtime_local_V1.sql
-- ★ 存量时间纠偏：yzh_queue / yzh_queue_resource_lock / cert_message
--   的 UTC CreateTime/LockTime → 本地时间（+8 小时，Asia/Shanghai）
--
-- 背景（2026-10-14 用户报障：队列监控同一行 CreateTime 与 StartTime 差 8 小时）：
--   · QueueManager.CreateQueueAsync 曾写 CreateTime = DateTime.UtcNow（UTC），
--     而 StartTime / yzh_queue_task 全部 DateTime.Now（本地）⇒ 同行两列差 8h，
--     按 CreateTime 排序把新队列排错位置、按日期筛选跨 UTC 日界漏数。
--   · yzh_queue_resource_lock.LockTime 曾 UTC，而 ReleaseTime 由 SQL NOW()（本地）写入。
--   · CertMessageService 曾写 CreateTime = UtcNow ⇒ MessageBell「N分钟前」错 8h。
--
-- 代码已同步改为 DateTime.Now（QueueManager / YzhQueueResourceLock / CertMessageService /
-- EnterpriseNormalizeController 去 ToUtc 补偿），本脚本只纠存量。
--
-- ⚠️⚠️ 本脚本只能执行一次（+8h 无幂等保护）！执行前必须先跑备份段。
--   时区假设：服务器业务时区 = Asia/Shanghai（UTC+8 固定，无夏令时）。
-- ============================================================

-- ── ① 备份（幂等：已存在则不覆盖，保留首次备份） ──────────────
CREATE TABLE IF NOT EXISTS yzh_queue_bak_createtime_20261014 AS
  SELECT * FROM yzh_queue;
CREATE TABLE IF NOT EXISTS yzh_queue_resource_lock_bak_locktime_20261014 AS
  SELECT * FROM yzh_queue_resource_lock;
CREATE TABLE IF NOT EXISTS cert_message_bak_createtime_20261014 AS
  SELECT * FROM cert_message;

-- ── ② 纠偏（+8 小时） ─────────────────────────────────────────
-- yzh_queue：全部行的 CreateTime 均由 UtcNow 写入（自 2026-09-14 建队列系统起）
UPDATE yzh_queue
SET CreateTime = CreateTime + INTERVAL 8 HOUR;

-- 资源锁：仅 LockTime 是 UTC；ReleaseTime 本来就是 NOW()（本地），⛔ 不动
UPDATE yzh_queue_resource_lock
SET LockTime = LockTime + INTERVAL 8 HOUR
WHERE LockTime IS NOT NULL;

-- 站内信：自 2026-10-10 建 CertMessageService 起全部 UtcNow
UPDATE cert_message
SET CreateTime = CreateTime + INTERVAL 8 HOUR;

-- ── ③ 验证（期望：MAX(CreateTime) ≈ NOW()，差值 < 5 分钟） ─────
SELECT 'yzh_queue'        AS tbl, NOW() AS db_now, MAX(CreateTime) AS max_ct,
       TIMESTAMPDIFF(MINUTE, MAX(CreateTime), NOW()) AS diff_min FROM yzh_queue
UNION ALL
SELECT 'cert_message',    NOW(), MAX(CreateTime),
       TIMESTAMPDIFF(MINUTE, MAX(CreateTime), NOW()) FROM cert_message
UNION ALL
SELECT 'yzh_queue_resource_lock', NOW(), MAX(LockTime),
       TIMESTAMPDIFF(MINUTE, MAX(LockTime), NOW()) FROM yzh_queue_resource_lock;
