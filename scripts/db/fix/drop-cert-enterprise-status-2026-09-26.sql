-- =============================================================================
-- 删除：cert_enterprise.Status 列（2026-09-26）
-- -----------------------------------------------------------------------------
-- 【作用】DROP `cert_enterprise`.`Status`。
--         用户 2026-09-26 裁定原话：「isvalid和status的问题，是不同的两个含义，
--         status我觉得没什么意义，直接删除」→ 不保留、不降级为注释、不留死列。
--
-- 【用法】★ 用同目录包装脚本（含自锁 + 自动备份）：
--           ./drop-cert-enterprise-status-2026-09-26.sh          # 只诊断，不写库
--           ./drop-cert-enterprise-status-2026-09-26.sh --apply  # 备份后执行
--         手工执行（不推荐）：
--           docker exec -i yzh-mysql mysql -uroot -p*** \
--             --default-character-set=utf8mb4 yzh_cert_platform < 本文件
--         ⚠️ 本文件**不含 `USE`** —— 目标库由调用方给出（否则库名白名单自锁形同虚设）
--
-- 【依赖】docker 容器 yzh-mysql；表 `cert_enterprise`
-- 【维护人】脚本规范 V1（2026-09-26）
--
-- ⛔ 安全约束（scripts/README.md 铁律 B6）
--   本脚本执行 **DDL（DROP COLUMN）**，**不可回滚**，只能靠备份恢复。
--   · 2026-09-26 实测：**无视图 / 无存储过程 / 无函数**引用 `cert_enterprise`（0 命中）
--   · 代码侧已同步删除（4 处）：`Enterprise.Status` 属性 / `EnterpriseController.OnBeforeAdd`
--     里的 Status 赋值 / `Enterprise.json`（列 + SearchFields）/ `cert-share` 的
--     `Enterprise.Status` / `enterprises/logic.ts` 默认值
--   · ⛔ **执行顺序强制**：**必须先编译并重启后端** —— 运行中的旧程序仍会 INSERT 该列，
--     DROP 后会报 `ERROR 1054 Unknown column 'Status'`
--   · 包装脚本写库前自动 `mysqldump` 到 `scripts/db/backup/`
--
-- 【幂等】用 `information_schema` 判存在才 DROP → 可重复执行
-- 【B7】步骤 1 是守卫：有下游依赖则**主动报错中止**（不是警告后继续）
-- 【铁律十】SQL 外置（不写进 .sh）；B4 写入放 `db/fix/`
-- =============================================================================

-- =============================================================================
-- 步骤 1：前置守卫 —— 若存在引用 cert_enterprise 的视图 → 主动中止（B7）
-- -----------------------------------------------------------------------------
-- ⚠️ MySQL 不允许在**存储程序之外**用 `SIGNAL`，故用「故意引用不存在的表」强制
--    `ERROR 1146` 中止脚本（batch 模式下 mysql 会非 0 退出）。**表名本身就是错误信息**。
-- =============================================================================
SET @dep_views := (
  SELECT COUNT(*) FROM information_schema.VIEWS
  WHERE TABLE_SCHEMA = DATABASE() AND VIEW_DEFINITION LIKE '%cert_enterprise%'
);

SET @dep_routines := (
  SELECT COUNT(*) FROM information_schema.ROUTINES
  WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_DEFINITION LIKE '%cert_enterprise%'
);

SET @guard_sql := IF(
  @dep_views > 0 OR @dep_routines > 0,
  'SELECT 1 FROM `ABORT_cert_enterprise_has_dependents_please_fix_views_first`',
  'SELECT ''OK: 无视图/存储过程依赖 cert_enterprise，继续'' AS Info'
);

PREPARE guard FROM @guard_sql;
EXECUTE guard;
DEALLOCATE PREPARE guard;

-- =============================================================================
-- 步骤 2：幂等 DROP（列不存在则跳过，不报错）
-- =============================================================================
SET @has_col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_enterprise'
    AND COLUMN_NAME  = 'Status'
);

SET @ddl := IF(
  @has_col > 0,
  'ALTER TABLE `cert_enterprise` DROP COLUMN `Status`',
  'SELECT ''SKIP: cert_enterprise.Status 不存在（已删除或从未创建）'' AS Info'
);

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- =============================================================================
-- 验证（逐段对照，全部应满足「期望」）
-- =============================================================================

-- 验证 1：Status 列已不存在（期望 0 行）
SELECT '--- 验证 1. Status 列已删除（期望 0 行）---' AS Info;

SELECT COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME   = 'cert_enterprise'
  AND COLUMN_NAME  = 'Status';

-- 验证 2：剩余列清单（人工核对，应含 IsValid 且不含 Status）
SELECT '--- 验证 2. cert_enterprise 剩余列 ---' AS Info;

SELECT ORDINAL_POSITION AS Ord, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, IFNULL(COLUMN_DEFAULT, '@@NULL@@') AS ColDefault
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise'
ORDER BY ORDINAL_POSITION;

-- 验证 3：数据未受影响（行数与启禁用状态）
SELECT '--- 验证 3. 数据未受影响 ---' AS Info;

SELECT COUNT(*) AS TotalRows,
       SUM(IsDeleted = 0) AS AliveRows,
       SUM(IsDeleted = 1) AS DeletedRows,
       SUM(IsValid = 1)   AS ValidRows
FROM `cert_enterprise`;
