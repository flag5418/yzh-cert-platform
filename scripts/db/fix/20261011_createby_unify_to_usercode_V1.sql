-- ══════════════════════════════════════════════════════════════════════════
-- 20261011 · CreateBy / UpdateBy 口径统一：**存用户 Code**
--            （不是登录名 UserName / 不是显示名 UserTrueName / 不是脚本标记）
-- ══════════════════════════════════════════════════════════════════════════
-- 依据（用户 2026-10-11 裁决 · 原文）：
--   「createby 应该存储的是用户的 code，而不是姓名，如果有问题，帮我一并分析
--     是不是这个也有问题」→ 选定「全链路统一 Code」＋「CreateBy 与 UpdateBy 一起收口」
--
-- 契约（本来就有，只是有实现违反它）：
--   · `BaseEntity` 注释：`CreateBy 创建人 Code` / `UpdateBy 更新人 Code`（均 [StringLength(64)]）
--   · `EntityService.AddCore`   自动填充 `dyn.CreateBy  = _userContext.UserCode`
--   · `EntityService.UpdateCore` 自动填充 `dyn.UpdateBy = _userContext.UserCode`
--   · 同键方：JWT `code` claim / `IUserContext.UserCode` / SignalR 推送分组键
--             / `cert_message.UserCode` / `YzhPushMessage.UserCode`
--
-- 本次实测违规面（= 全库含 CreateBy/UpdateBy 的**所有基表**逐行比对
--               sys_user.Code / UserName / UserTrueName，见 verify_createby.sql）：
--   首轮（登录名 UserName → Code）已修 98 行：
--     · yzh_queue                79 行 `wzm`（100% 违反 ⇒ 队列监控页「创建人」=
--                                  登录名、企业资料规范化「操作人」姓名永远解析不出来）
--     · wf_skill_input 6 / wf_skill_output 2 / wf_skill_reflection 2 行 `admin`
--     · cert_enterprise 1 / cert_enterprise_stage 1 行 `admin`
--     · Sys_Dictionary / Sys_DictionaryList 少量 `admin`
--   本脚本继续修（显示名 UserTrueName → Code，109 行 CreateBy + 53 行 UpdateBy）：
--     · Sys_Dictionary 24 / Sys_DictionaryList 83 / Sys_Role 5 /
--       sys_user 1 / cert_standard_family 1 行 `超级管理员`
--     · Sys_Role 4 / Sys_Dictionary 18 / Sys_DictionaryList 31 / sys_user 1 行 UpdateBy `超级管理员`
--
--   ⛔ 不迁移的两类（**有意保留**，不是漏网）：
--     A. 脚本 / 种子标记：`seed_*``system*``SYSTEM*``SYSTEM_FIX``migration``restore``
--        `auditor_reg``menu-restructure``fix_20261010``standard-family-20261008``
--        `sys_migration``manual_fix_*``menu_doc_semantic_rule``repair-*``fix_stage_align_*`
--        ⇒ 它们记录「这行由哪个脚本创建」，改成用户 = **伪造审计留痕**。
--     B. 历史遗留、**无对应用户**：`测试管理员` / `测试超级管理员` / `xxx`
--        ⇒ 映射不唯一或找不到用户的一律**不猜**（见 verify 的白名单）。
--
-- 用法：
--   docker exec -i yzh-mysql mysql -uroot -p*** --default-character-set=utf8mb4 \
--     yzh_cert_platform < scripts/db/fix/20261011_createby_unify_to_usercode_V1.sql
--   然后跑只读校验：scripts/db/verify/verify_createby.sql（期望「C_违规」= 0）
--
-- 幂等：所有 UPDATE 都带「当前值不是合法 Code」前置 ⇒ 重复执行无副作用。
--       （`NOT IN (SELECT Code FROM tmp_codes)` 即「还不是 Code 才动」）
-- 可回滚：迁移前的原值全部备份在 `_bak_usercode_20261011`
--         （SrcTable + SrcColumn + RowId + OldValue）；首轮旧表
--         `_bak_createby_20261011` 的原值已自动承接进来。
-- 执行台账：`_mig_usercode_20261011_result`（逐表逐列命中行数，便于出报告）。
--
-- ⚠️ 为什么用「临时表 + 存储过程」而不是逐表写 JOIN：
--    MySQL 8 不允许 `UPDATE t JOIN (SELECT ... FROM t) m`（ERROR 1093
--    You can't specify target table for update in FROM clause）。`sys_user`
--    自更新时必然踩到 ⇒ 改成先把映射物化进 TEMPORARY TABLE 再 JOIN。
--    ★ 本机 `lower_case_table_names=2`：`sys_user` 与 `Sys_User` 是**同一张表**。
-- ⚠️ 视图（v_*）不更新：它们跟随基表，且不可 UPDATE。
-- ══════════════════════════════════════════════════════════════════════════

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
SET SESSION group_concat_max_len = 4194304;

-- ──────────────────────────────────────────────────────────────────────────
-- ⓪ 列宽兜底：Code 是「业务编码」（BaseEntity 声明 varchar(36) / 本库实测最长 36，
--    含 GUID；CreateBy 标注 [StringLength(64)]），而以下 4 列只有 varchar(30)
--    ⇒ 严格模式下写入 GUID Code 直接 ERROR 1406，非严格模式**静默截断**
--    （截断后的 CreateBy 既不等于任何 Code，也不等于任何姓名 = 最坏的一类坏数据）。
--    统一放宽到 varchar(64)，与 BaseEntity 的 [StringLength(64)] 对齐。
-- ──────────────────────────────────────────────────────────────────────────
ALTER TABLE `Sys_Dictionary`
  MODIFY COLUMN `CreateBy` VARCHAR(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  MODIFY COLUMN `UpdateBy` VARCHAR(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL;

ALTER TABLE `Sys_DictionaryList`
  MODIFY COLUMN `CreateBy` VARCHAR(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  MODIFY COLUMN `UpdateBy` VARCHAR(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL;

-- ──────────────────────────────────────────────────────────────────────────
-- ① 备份表（统一 Schema：表 + 列 + 行 Id + 原值）
-- ──────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `_bak_usercode_20261011` (
  `SrcTable`   VARCHAR(64)  NOT NULL COMMENT '来源表',
  `SrcColumn`  VARCHAR(64)  NOT NULL COMMENT '来源列（CreateBy / UpdateBy）',
  `RowId`      BIGINT       NULL     COMMENT '该表主键值',
  `OldValue`   VARCHAR(190) NULL     COMMENT '迁移前的值',
  `BackupTime` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY `uk_src_col_row` (`SrcTable`, `SrcColumn`, `RowId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='CreateBy/UpdateBy 口径统一（2026-10-11）迁移前备份';

-- 承接首轮（CreateBy 专用旧备份表）已记录的原值，保证「一份台账可回滚」
SET @has_legacy_bak = (SELECT COUNT(*) FROM information_schema.TABLES
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '_bak_createby_20261011');
SET @sql = IF(@has_legacy_bak > 0,
  'INSERT IGNORE INTO `_bak_usercode_20261011` (`SrcTable`,`SrcColumn`,`RowId`,`OldValue`,`BackupTime`)
   SELECT `SrcTable`, ''CreateBy'', `RowId`, `OldCreateBy`, `BackupTime` FROM `_bak_createby_20261011`',
  'SELECT ''（无首轮旧备份表，跳过承接）'' AS Note');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ──────────────────────────────────────────────────────────────────────────
-- ② 映射物化（临时表；只收「唯一命中」的键，不唯一就不猜）
--    本库实测：UserName 与 UserTrueName 在 sys_user 内均无重名（已校验）。
-- ──────────────────────────────────────────────────────────────────────────
DROP TEMPORARY TABLE IF EXISTS `tmp_map_name`;
CREATE TEMPORARY TABLE `tmp_map_name` (
  `K` VARCHAR(100) NOT NULL COMMENT '登录名 UserName',
  `Code` VARCHAR(50) NOT NULL COMMENT '用户 Code',
  PRIMARY KEY (`K`)
) ENGINE=InnoDB;
INSERT INTO `tmp_map_name` (`K`, `Code`)
SELECT `UserName`, MIN(`Code`) FROM `sys_user`
 WHERE `UserName` IS NOT NULL AND `UserName` <> ''
 GROUP BY `UserName` HAVING COUNT(*) = 1;

DROP TEMPORARY TABLE IF EXISTS `tmp_map_true`;
CREATE TEMPORARY TABLE `tmp_map_true` (
  `K` VARCHAR(100) NOT NULL COMMENT '显示名 UserTrueName（真名不保证唯一 ⇒ 只收唯一命中）',
  `Code` VARCHAR(50) NOT NULL COMMENT '用户 Code',
  PRIMARY KEY (`K`)
) ENGINE=InnoDB;
INSERT INTO `tmp_map_true` (`K`, `Code`)
SELECT `UserTrueName`, MIN(`Code`) FROM `sys_user`
 WHERE `UserTrueName` IS NOT NULL AND `UserTrueName` <> ''
 GROUP BY `UserTrueName` HAVING COUNT(*) = 1;

DROP TEMPORARY TABLE IF EXISTS `tmp_codes`;
CREATE TEMPORARY TABLE `tmp_codes` (
  `Code` VARCHAR(50) NOT NULL COMMENT '全部合法用户 Code',
  PRIMARY KEY (`Code`)
) ENGINE=InnoDB;
INSERT INTO `tmp_codes` (`Code`)
SELECT DISTINCT `Code` FROM `sys_user` WHERE `Code` IS NOT NULL AND `Code` <> '';

-- 执行台账（逐表逐列）
CREATE TABLE IF NOT EXISTS `_mig_usercode_20261011_result` (
  `SrcTable`        VARCHAR(64) NOT NULL,
  `SrcColumn`       VARCHAR(64) NOT NULL,
  `EmptyToNull`     INT NOT NULL DEFAULT 0 COMMENT '空串归一为 NULL 的行数',
  `MatchedByUserName` INT NOT NULL DEFAULT 0 COMMENT '登录名 → Code 的行数',
  `MatchedByTrueName` INT NOT NULL DEFAULT 0 COMMENT '显示名 → Code 的行数',
  `RunTime`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`SrcTable`, `SrcColumn`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='CreateBy/UpdateBy 口径统一执行台账';

-- ──────────────────────────────────────────────────────────────────────────
-- ③ 全表刷新：自动枚举**所有**含 CreateBy / UpdateBy 的基表列（含将来新增的）
-- ──────────────────────────────────────────────────────────────────────────
DROP PROCEDURE IF EXISTS `sp_usercode_unify_20261011`;

DELIMITER $$
CREATE PROCEDURE `sp_usercode_unify_20261011`()
BEGIN
  DECLARE v_done   INT DEFAULT 0;
  DECLARE v_tbl    VARCHAR(64);
  DECLARE v_col    VARCHAR(64);
  DECLARE v_pk     VARCHAR(64);
  DECLARE v_n1     INT DEFAULT 0;
  DECLARE v_n2     INT DEFAULT 0;
  DECLARE v_n3     INT DEFAULT 0;

  DECLARE cur CURSOR FOR
    SELECT c.TABLE_NAME, c.COLUMN_NAME
      FROM information_schema.COLUMNS c
      JOIN information_schema.TABLES t
        ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
     WHERE c.TABLE_SCHEMA = DATABASE()
       AND c.COLUMN_NAME IN ('CreateBy', 'UpdateBy')
       AND t.TABLE_TYPE = 'BASE TABLE'
       AND c.TABLE_NAME NOT LIKE '\_bak\_%'
       AND c.TABLE_NAME NOT LIKE '\_mig\_%'
       AND c.TABLE_NAME NOT LIKE 'tmp\_%'
     ORDER BY c.TABLE_NAME, c.COLUMN_NAME;

  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;

  OPEN cur;
  read_loop: LOOP
    FETCH cur INTO v_tbl, v_col;
    IF v_done THEN LEAVE read_loop; END IF;

    -- 主键列名（无主键的表只刷新、不备份）
    SELECT k.COLUMN_NAME INTO v_pk
      FROM information_schema.KEY_COLUMN_USAGE k
     WHERE k.TABLE_SCHEMA = DATABASE() AND k.TABLE_NAME = v_tbl
       AND k.CONSTRAINT_NAME = 'PRIMARY'
     ORDER BY k.ORDINAL_POSITION LIMIT 1;

    -- ③-1 备份「当前值不是合法 Code」的行（INSERT IGNORE ⇒ 重复执行不覆盖原值）
    IF v_pk IS NOT NULL THEN
      SET @s = CONCAT(
        'INSERT IGNORE INTO `_bak_usercode_20261011` (`SrcTable`,`SrcColumn`,`RowId`,`OldValue`) ',
        'SELECT ''', v_tbl, ''', ''', v_col, ''', t.`', v_pk, '`, t.`', v_col, '` FROM `', v_tbl, '` t ',
        'WHERE t.`', v_col, '` IS NOT NULL AND t.`', v_col, '` <> '''' ',
        '  AND NOT EXISTS (SELECT 1 FROM `tmp_codes` c WHERE c.`Code` = t.`', v_col, '`)');
      PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
    END IF;

    -- ③-2 空串归一为 NULL（此前 '' 与 NULL 两种「无操作人」并存 ⇒ `<> ''` 判据会漏一半）
    SET @s = CONCAT('UPDATE `', v_tbl, '` SET `', v_col, '` = NULL WHERE `', v_col, '` = ''''');
    PREPARE st FROM @s; EXECUTE st; SET v_n1 = ROW_COUNT(); DEALLOCATE PREPARE st;

    -- ③-3 登录名 UserName → 用户 Code
    SET @s = CONCAT(
      'UPDATE `', v_tbl, '` t JOIN `tmp_map_name` m ON m.`K` = t.`', v_col, '` ',
      'SET t.`', v_col, '` = m.`Code` ',
      'WHERE t.`', v_col, '` IS NOT NULL AND t.`', v_col, '` <> '''' ',
      '  AND t.`', v_col, '` NOT IN (SELECT `Code` FROM `tmp_codes`)');
    PREPARE st FROM @s; EXECUTE st; SET v_n2 = ROW_COUNT(); DEALLOCATE PREPARE st;

    -- ③-4 显示名 UserTrueName → 用户 Code（只对唯一命中生效）
    SET @s = CONCAT(
      'UPDATE `', v_tbl, '` t JOIN `tmp_map_true` m ON m.`K` = t.`', v_col, '` ',
      'SET t.`', v_col, '` = m.`Code` ',
      'WHERE t.`', v_col, '` IS NOT NULL AND t.`', v_col, '` <> '''' ',
      '  AND t.`', v_col, '` NOT IN (SELECT `Code` FROM `tmp_codes`)');
    PREPARE st FROM @s; EXECUTE st; SET v_n3 = ROW_COUNT(); DEALLOCATE PREPARE st;

    IF v_n1 > 0 OR v_n2 > 0 OR v_n3 > 0 THEN
      INSERT INTO `_mig_usercode_20261011_result`
             (`SrcTable`, `SrcColumn`, `EmptyToNull`, `MatchedByUserName`, `MatchedByTrueName`)
      VALUES (v_tbl, v_col, v_n1, v_n2, v_n3)
      ON DUPLICATE KEY UPDATE
        `EmptyToNull`        = `EmptyToNull` + v_n1,
        `MatchedByUserName`  = `MatchedByUserName` + v_n2,
        `MatchedByTrueName`  = `MatchedByTrueName` + v_n3,
        `RunTime`            = CURRENT_TIMESTAMP;
    END IF;
  END LOOP;
  CLOSE cur;
END$$
DELIMITER ;

CALL `sp_usercode_unify_20261011`();
DROP PROCEDURE `sp_usercode_unify_20261011`;

-- ──────────────────────────────────────────────────────────────────────────
-- ④ 执行台账（累计，逐表逐列命中行数；重跑幂等 ⇒ 第二次全为 0 增量）
-- ──────────────────────────────────────────────────────────────────────────
SELECT `SrcTable`, `SrcColumn`, `EmptyToNull`, `MatchedByUserName`, `MatchedByTrueName`, `RunTime`
  FROM `_mig_usercode_20261011_result`
 ORDER BY `SrcTable`, `SrcColumn`;

SELECT '备份总行数' AS CheckItem, COUNT(*) AS Rows_ FROM `_bak_usercode_20261011`
UNION ALL
SELECT '执行台账行数', COUNT(*) FROM `_mig_usercode_20261011_result`
UNION ALL
SELECT '仍在非 Code（含白名单例外）', COUNT(*) FROM (
  SELECT 1 FROM `yzh_queue` WHERE `CreateBy` IS NOT NULL AND `CreateBy` <> ''
    AND NOT EXISTS (SELECT 1 FROM `sys_user` u WHERE u.`Code` = `yzh_queue`.`CreateBy`)
) z;
