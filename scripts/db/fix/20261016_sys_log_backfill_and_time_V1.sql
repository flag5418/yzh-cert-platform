-- ============================================================
-- 20261016_sys_log_backfill_and_time_V1.sql
-- ★ 操作日志 sys_log 存量治理（2026-10-16）
--
-- 两项内容：
--   ① 时间纠偏：CreateTime 由 UTC → 本地时间（+8h，Asia/Shanghai）
--   ② 旧行回填：把「真实接口信息」回填到 ApiPath / Module / TargetType / Action
--
-- 背景（用户报障「看不出是哪个接口执行了什么操作」）：
--   · 2026-10-16 之前的 sys_log 由旧 SysLogDbWriter 写入：
--       Module/Action/TargetType 只落 HTTP 动词与命名空间（如 Controllers / 新增 / System），
--       真实路径被塞进 Detail（`路径=/api/...`，且 Detail 列 XsFlag=false 不显示）⇒ 接口信息全丢。
--   · CreateTime 由 `DateTime.UtcNow` 写入（UTC）⇒ 列表「操作时间」比本地早 8 小时。
--
-- 代码已同步（本次一并修复，见 commit / REFERENCE）：
--   · BaseEntity.CreateTime / EntityService 审计填充 / TreeControllerBase /
--     SysLogDbWriter / SysRoleApi,SysUserPermission,Sys_RoleUser,Sys_RoleMenu /
--     ApiRepository / ApiScanner / SysLogController 默认排序 …… 全部 UtcNow → Now
--   · 规则：业务时间一律本地时间；UtcNow 仅限内部机制（JWT/缓存/超时/耗时/TTL）。
--
-- ⚠️⚠️ 时间纠偏段只对「UTC 时代」的行生效（CreateTime < 重启切换时刻 09:36:13），
--     切换后新行已是本地时间，不会被二次 +8h ⇒ 本脚本可安全重复执行（幂等）。
--     时区假设：服务器业务时区 = Asia/Shanghai（UTC+8 固定，无夏令时）。
-- ============================================================

-- ── ① 备份（幂等：已存在则不覆盖，保留首次备份） ──────────────
CREATE TABLE IF NOT EXISTS sys_log_bak_20261016 AS SELECT * FROM sys_log;

-- ── ② 时间纠偏：UTC → 本地（+8h，仅切换前旧行） ───────────────
UPDATE sys_log
SET CreateTime = CreateTime + INTERVAL 8 HOUR
WHERE CreateTime < '2026-10-16 09:36:13';

-- ── ③ 旧行回填：从 Detail 提取 `路径=/api/...` 还原接口信息 ────
--   仅处理：ApiPath 为空 且 Detail 含「路径=」的旧行（新行 ApiPath 已非空，天然跳过）
DROP TEMPORARY TABLE IF EXISTS _sl_fix;
CREATE TEMPORARY TABLE _sl_fix AS
SELECT
    Id,
    TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(Detail, '路径=', -1), ' | ', 1))            AS P,
    LOWER(SUBSTRING_INDEX(TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(Detail, '路径=', -1), ' | ', 1)), '/', -1)) AS seg
FROM sys_log
WHERE ApiPath IS NULL
  AND Detail LIKE '%路径=%';

UPDATE sys_log s
JOIN _sl_fix f ON f.Id = s.Id
SET
    s.ApiPath    = f.P,
    s.TargetType = SUBSTRING_INDEX(SUBSTRING_INDEX(f.P, '/', -2), '/', 1),
    s.Module = CASE
        WHEN f.P LIKE '%/Auditor/%'                                        THEN '专家端'
        WHEN f.P LIKE '%/Admin/Workflow/%' OR f.P LIKE '/api/Workflow/%'
             OR f.P LIKE '/api/PromptTemplate/%'                           THEN '业务管理'
        WHEN f.P LIKE '%/Admin/Foundation/%' OR f.P LIKE '/api/Foundation/%' THEN '基础资料'
        WHEN f.P LIKE '%/Admin/Cert/%' OR f.P LIKE '/api/Cert/%'           THEN '认证业务'
        WHEN f.P LIKE '%/System/%' OR f.P LIKE '/api/Organization/%'
             OR f.P LIKE '/api/Role/%'                                     THEN '系统管理'
        WHEN f.P LIKE '/api/User/%'                                       THEN '框架'
        ELSE '其他'
    END,
    s.Action = CONCAT(
        CASE
            WHEN f.seg REGEXP '^(get|query|list|filter|tree|config|page|search|load|detail|read|active|unread)' THEN '查询'
            WHEN f.seg REGEXP '^(export|download)'  THEN '导出'
            WHEN f.seg REGEXP '^(import|upload)'    THEN '导入'
            WHEN f.seg REGEXP '^(save|add|create|insert|new)' THEN '新增'
            WHEN f.seg REGEXP '^(update|edit|modify|set|change|enable|disable|valid)' THEN '编辑'
            WHEN f.seg REGEXP '^(delete|remove|clear|drop)' THEN '删除'
            WHEN f.seg REGEXP '^(login|signin)'     THEN '登录'
            WHEN f.seg REGEXP '^(logout|signout)'   THEN '登出'
            WHEN f.seg REGEXP '^sync'               THEN '同步'
            ELSE '操作'
        END,
        ' ',
        CONCAT(UPPER(LEFT(f.seg, 1)), SUBSTRING(f.seg, 2))
    );

DROP TEMPORARY TABLE IF EXISTS _sl_fix;

-- ── ④ 验证 ────────────────────────────────────────────────────
-- 4.1 时间：MAX(CreateTime) ≈ NOW()（差值 < 5 分钟）
SELECT 'sys_log' AS tbl, NOW() AS db_now, MAX(CreateTime) AS max_ct,
       TIMESTAMPDIFF(MINUTE, MAX(CreateTime), NOW()) AS diff_min FROM sys_log;

-- 4.2 回填覆盖：旧行剩余多少条 ApiPath 为空（期望仅剩「无路径」的 description 行，约 300+ 条）
SELECT COUNT(*) AS remain_null_apipath,
       SUM(Detail LIKE '%路径=%') AS remain_null_with_path
FROM sys_log WHERE ApiPath IS NULL OR ApiPath = '';

-- 4.3 抽样：旧行回填效果
SELECT Id, Module, Action, ApiPath, TargetType, CreateTime
FROM sys_log WHERE Id < 83000 ORDER BY Id DESC LIMIT 5;
