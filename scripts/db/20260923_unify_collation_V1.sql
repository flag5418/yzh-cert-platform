-- =============================================================================
-- 全库字符集 / 排序规则统一 V1 —— 统一到 utf8mb4_general_ci
-- 日期：2026-09-23
-- 对应规则：`项目全局规则.md` §十六「数据库-实体命名铁律」→ 铁律八
--
-- ★ 规则来源（用户 2026-09-23 决策，原文）：
--   「我们绝大部分的表是 utf8mb4_general_ci，先按这个统一，再数据库规则中强调，
--     全部依据 utf8mb4_general_ci 建设，针对非 utf8mb4_general_ci，重建表，
--     字符集不统一，后续问题会很多。」
--
-- =============================================================================
-- ★ 为什么必须统一（已实际发生的故障，非理论风险）
-- =============================================================================
-- 「列 vs 列」比较/关联时，两侧 collation 不同 → 直接报错：
--   ERROR 1267 (HY000): Illegal mix of collations
--     (utf8mb4_0900_ai_ci,IMPLICIT) and (utf8mb4_general_ci,IMPLICIT) for operation '='
--
-- 首次踩到：`Sys_RoleMenu.MenuCode`(0900_ai_ci) vs `Sys_Menu.Code`(general_ci)，
--   导致专家系统菜单授权 SQL 的 `NOT EXISTS ... rm.MenuCode = m.Code` 直接失败。
--
-- ⚠️ 故障为何极易漏测：「列 vs 字面量」**不会**报错 —— 字面量 coercibility=4，
--    列(=2) 胜出，故单表 `UPDATE ... WHERE Tag = ''` 一切正常；
--    只有**跨表关联**才暴露。这意味着单表增删改查全绿、上线后才炸。
--
-- =============================================================================
-- ★ 转换前的实际乱象（三套排序规则并存 + 一类已废弃字符集）
-- =============================================================================
-- | 来源                                              | 排序规则              | 表数 |
-- |---------------------------------------------------|-----------------------|------|
-- | 历史 Vol 导入（all_tables_ddl.sql，已显式写 COLLATE）| utf8mb4_general_ci    | 66   |
-- | 建表写 `DEFAULT CHARSET=utf8mb4` **但未写 COLLATE**  | utf8mb4_0900_ai_ci    | 17   |
-- | 建表**完全不写 charset**（继承 @@collation_server）  | utf8mb4_unicode_ci    | 17   |
--
-- 另有 **37 个 `Remark` 列是 `utf8mb3_general_ci`**（分布于 34 张表）。
--   ⚠️ utf8mb3（即旧 utf8）**无法存储 4 字节字符**（emoji、部分生僻汉字）→ 写入即报
--      「Incorrect string value」，且这是 MySQL 官方已标记废弃的字符集。
--
-- =============================================================================
-- ★ 两个根因（本脚本只能治标；治本见 §5「防复发」）
-- =============================================================================
-- ① `CREATE TABLE ... DEFAULT CHARSET=utf8mb4`（不带 COLLATE）
--      → 取的是 **utf8mb4 这个字符集自身的默认排序规则 = utf8mb4_0900_ai_ci**，
--        **不是**数据库默认值。这是最反直觉、也最容易踩的一点。
-- ② `@@collation_server = utf8mb4_unicode_ci` → 建表完全不写 charset 时继承它。
--    → 二者都会**绕过「库默认」**。
--
-- ⇒ 唯一可靠写法：建表**显式**写 `COLLATE=utf8mb4_general_ci`（见 §5）。
--
-- =============================================================================
-- ★ 执行方式
-- =============================================================================
--   docker exec -i yzh-mysql mysql -uroot -p'Yzh123456.' --default-character-set=utf8mb4 \
--     yzh_cert_platform < scripts/db/20260923_unify_collation_V1.sql
--
-- ★ 转换前备份（务必先做；本脚本已按此流程执行过）
--   docker exec -e MYSQL_PWD='Yzh123456.' yzh-mysql mysqldump -uroot --single-transaction \
--     --routines --triggers --events --default-character-set=utf8mb4 yzh_cert_platform \
--     > /tmp/yzh-backup-20260923/yzh_cert_platform_pre_collation.sql
--
-- ★ 幂等：已转换的表重复执行结果相同，无副作用。
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. 转换前基线（留档，便于事后比对）
-- -----------------------------------------------------------------------------
SELECT '--- [1/7] 转换前：表级排序规则分布 ---' AS `phase`;
SELECT TABLE_COLLATION, COUNT(*) AS `table_cnt`
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
GROUP BY TABLE_COLLATION ORDER BY `table_cnt` DESC;

SELECT '--- [1/7] 转换前：外键数量（转换后必须相等，基线 = 52）---' AS `phase`;
SELECT COUNT(*) AS `fk_cnt` FROM information_schema.TABLE_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY';

SELECT '--- [1/7] 转换前：非 general_ci 字符列数量 ---' AS `phase`;
SELECT COLLATION_NAME, COUNT(*) AS `col_cnt`
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND COLLATION_NAME IS NOT NULL
GROUP BY COLLATION_NAME ORDER BY `col_cnt` DESC;

-- -----------------------------------------------------------------------------
-- 2. 关闭外键检查
--    ★ 必需，不是保险：本库有 52 个外键。MySQL 规定 `ALTER TABLE ... CONVERT TO
--      CHARACTER SET` 若涉及被外键引用的列会直接拒绝（ERROR 1832）。
--      由于**两侧最终都会变成 general_ci**，临时关闭检查是安全的。
-- -----------------------------------------------------------------------------
SET FOREIGN_KEY_CHECKS = 0;

-- -----------------------------------------------------------------------------
-- 3. 库默认值 → general_ci（治标：让「完全不写 charset」的新表落到 general_ci）
-- -----------------------------------------------------------------------------
ALTER DATABASE `yzh_cert_platform`
  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- 4. 逐表重建（CONVERT TO 会重建表，并转换该表**全部**字符列）
--
--    清单口径（去重后 68 张）：
--      表级 collation ≠ general_ci  ∪  含任一非 general_ci 列
--
--    ★ 第二类不可省：有 34 张表**表级已是 general_ci，但 `Remark` 列是 utf8mb3**。
--      只按表级 collation 筛选会整批漏掉这 34 张表。
--      清单由 information_schema 生成，口径见脚本头部，勿手工增删。
-- -----------------------------------------------------------------------------
ALTER TABLE `audit_checklist_item`          CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `audit_evidence`                CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `audit_finding`                 CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `audit_nonconformity`           CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `audit_project`                 CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `audit_rectification`           CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `audit_task`                    CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_ai_config`                CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_ai_usage_log`             CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_application`              CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_auditor_profile`          CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_cert_stage`               CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_certification_body`       CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_clause_extraction_rule`   CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_directory_template`       CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_doc_extraction_rule`      CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_doc_field_def`            CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_doc_table_def`            CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_doc_table_field_def`      CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_enterprise`               CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_file_requirement`         CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_iso_clause`               CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_iso_standard`             CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_message`                  CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_org_standard`             CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_report_template`          CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_standard_directory_config` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_standard_directory_file`  CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_standard_directory_folder` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_standard_phase_config`    CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_sys_config`               CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_upload_task`              CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_validation_rule`          CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `cert_validation_rule_source`   CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_enterprise`                CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_enterprise_document`       CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_enterprise_file`           CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_enterprise_phase`          CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_extraction_result`         CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_file_compliance_check`     CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_file_pre_check_result`     CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_file_version`              CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `ent_table_extraction_result`   CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `rpt_audit_report`              CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `rpt_report_section`            CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `rpt_report_section_source`     CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `rpt_report_task`               CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `sys_api`                       CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `sys_config`                    CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `sys_log`                       CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `Sys_Organization`              CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `sys_role_api`                  CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `sys_user_permission`           CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_field_label_mapping`        CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_prompt_template`            CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_skill`                      CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_skill_api`                  CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_skill_category`             CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_skill_input`                CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_skill_output`               CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_skill_reflection`           CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_workflow_definition`        CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `wf_workflow_execution_log`     CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `yzh_field_config`              CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `yzh_page_config`               CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `yzh_queue`                     CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `yzh_queue_resource_lock`       CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE `yzh_queue_task`                CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- 5. 重建视图（2 个）
--    视图不能 ALTER，只能 DROP + CREATE。
--
--    ★ 视图的「字面量派生列」会把**建视图时的 collation_connection 固化**下来，
--      这是最隐蔽的一类残留：表全改完了，视图里仍留着旧 collation。
--      本库的视图源脚本 `rebuild_views_pascalcase.sql` 既无 `SET NAMES` 也无 `COLLATE`，
--      故 `CASE ... THEN '启用'` 这类列会固化成连接默认值。
--      → 修法：表达式后**显式**加 `COLLATE utf8mb4_general_ci`（比依赖 SET NAMES 更抗重跑）。
--
--    ⚠️ DROP VIEW 会一并删除该视图上的授权（mysql.tables_priv），重建后需按需重授。
-- -----------------------------------------------------------------------------

-- 5.1 v_workflow
--     病灶：`cast(w.IsActive as char charset utf8mb4)` —— 只给字符集、不给排序规则，
--     于是取 utf8mb4 的默认排序规则 `utf8mb4_0900_ai_ci`，与视图其它列不一致。
DROP VIEW IF EXISTS `v_workflow`;
CREATE ALGORITHM=UNDEFINED SQL SECURITY DEFINER VIEW `v_workflow` AS
SELECT
  `w`.`Id`             AS `Id`,
  `w`.`Code`           AS `Code`,
  `w`.`OrgCode`        AS `OrgCode`,
  `w`.`WorkflowCode`   AS `WorkflowCode`,
  `w`.`WorkflowName`   AS `WorkflowName`,
  `w`.`WorkflowType`   AS `WorkflowType`,
  (CASE `w`.`WorkflowType`
     WHEN 'extraction' THEN '提取'
     WHEN 'validation' THEN '审核'
     WHEN 'report'     THEN '报告'
     ELSE `w`.`WorkflowType` END) AS `WorkflowTypeName`,
  `w`.`WorkflowConfig` AS `WorkflowConfig`,
  `w`.`Version`        AS `Version`,
  `w`.`IsActive`       AS `IsActive`,
  (CASE `w`.`IsActive`
     WHEN 1 THEN '启用'
     WHEN 0 THEN '停用'
     ELSE CAST(`w`.`IsActive` AS CHAR CHARACTER SET utf8mb4) COLLATE utf8mb4_general_ci END) AS `IsActiveName`,
  `w`.`Description`    AS `Description`,
  `w`.`Status`         AS `Status`,
  (CASE `w`.`Status`
     WHEN 'active'   THEN '启用'
     WHEN 'inactive' THEN '停用'
     ELSE `w`.`Status` END) AS `StatusName`,
  `w`.`Sort`           AS `Sort`,
  `w`.`Remark`         AS `Remark`,
  `w`.`Enable`         AS `Enable`,
  `w`.`CreateTime`     AS `CreateTime`,
  `w`.`CreateBy`       AS `CreateBy`,
  `w`.`UpdateTime`     AS `UpdateTime`,
  `w`.`UpdateBy`       AS `UpdateBy`,
  `w`.`DeleteTime`     AS `DeleteTime`,
  `w`.`DeleteBy`       AS `DeleteBy`,
  `w`.`IsDeleted`      AS `IsDeleted`,
  `w`.`IsValid`        AS `IsValid`
FROM `wf_workflow_definition` `w`;

-- 5.2 v_cert_phase_definition
--     病灶：`CASE p.IsValid WHEN 1 THEN '启用' ELSE '停用' END` 的结果排序规则取自
--     建视图时的连接（当时是 utf8mb4_0900_ai_ci），固化在 StatusName 列上。
DROP VIEW IF EXISTS `v_cert_phase_definition`;
CREATE ALGORITHM=UNDEFINED SQL SECURITY DEFINER VIEW `v_cert_phase_definition` AS
SELECT
  `p`.`Id`            AS `Id`,
  `p`.`Code`          AS `Code`,
  `p`.`PhaseCode`     AS `PhaseCode`,
  `p`.`PhaseName`     AS `PhaseName`,
  `p`.`SequenceOrder` AS `SequenceOrder`,
  `p`.`Description`   AS `Description`,
  `p`.`IsValid`       AS `IsValid`,
  (CASE `p`.`IsValid` WHEN 1 THEN '启用' ELSE '停用' END) COLLATE utf8mb4_general_ci AS `StatusName`,
  `p`.`CreateTime`    AS `CreateTime`,
  `p`.`CreateBy`      AS `CreateBy`,
  `p`.`UpdateTime`    AS `UpdateTime`,
  `p`.`UpdateBy`      AS `UpdateBy`,
  `p`.`DeleteTime`    AS `DeleteTime`,
  `p`.`DeleteBy`      AS `DeleteBy`,
  `p`.`IsDeleted`     AS `IsDeleted`
FROM `cert_phase_definition` `p`
WHERE `p`.`IsDeleted` = 0;

-- -----------------------------------------------------------------------------
-- 6. 重建 5 个 DDL 辅助存储过程
--    ★ 它们原先的 `Database Collation` 是 `utf8mb4_unicode_ci`（取自创建时的
--      collation_connection）。过程内部的局部变量 / 字面量会继承该 collation，
--      与 general_ci 的 information_schema 列比较时存在 1267 风险。
--    ★ 附带修正一个隐患：这 5 个过程**此前在仓库中没有任何源脚本**（是直接建在库里的），
--      换机器 / 重建库就会丢失。本次借机固化，使其首次可复现。
--    ★ 过程 collation 取自创建时的连接，故必须先 SET NAMES。
-- -----------------------------------------------------------------------------
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

DROP PROCEDURE IF EXISTS `safe_add_column`;
DELIMITER $$
CREATE PROCEDURE `safe_add_column`(
  IN p_table_name  VARCHAR(100),
  IN p_column_name VARCHAR(100),
  IN p_column_def  TEXT,
  IN p_comment     VARCHAR(500)
)
BEGIN
  DECLARE col_count INT;
  SELECT COUNT(*) INTO col_count
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = p_table_name
    AND COLUMN_NAME  = p_column_name;

  IF col_count = 0 THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table_name, '` ADD COLUMN `', p_column_name, '` ', p_column_def, " COMMENT '", p_comment, "'");
    PREPARE stmt FROM @sql;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
  END IF;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS `safe_add_index`;
DELIMITER $$
CREATE PROCEDURE `safe_add_index`(
  IN p_table_name    VARCHAR(100),
  IN p_index_name    VARCHAR(100),
  IN p_index_columns TEXT
)
BEGIN
  DECLARE idx_count INT;
  SELECT COUNT(*) INTO idx_count
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = p_table_name
    AND INDEX_NAME   = p_index_name;

  IF idx_count = 0 THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table_name, '` ADD INDEX `', p_index_name, '` (', p_index_columns, ')');
    PREPARE stmt FROM @sql;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
  END IF;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS `p_safe_change_column`;
DELIMITER $$
CREATE PROCEDURE `p_safe_change_column`(
  IN p_table   VARCHAR(64),
  IN p_old_col VARCHAR(64),
  IN p_new_col VARCHAR(64),
  IN p_def     VARCHAR(500)
)
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_old_col) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` CHANGE COLUMN `', p_old_col, '` `', p_new_col, '` ', p_def);
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Renamed ', p_table, '.', p_old_col, ' → ', p_new_col) AS result;
  ELSEIF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_new_col) THEN
    SELECT CONCAT('Skipped: ', p_table, ' already has ', p_new_col) AS result;
  ELSE
    SELECT CONCAT('Error: ', p_table, ' has neither ', p_old_col, ' nor ', p_new_col) AS result;
  END IF;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS `p_safe_drop_column`;
DELIMITER $$
CREATE PROCEDURE `p_safe_drop_column`(
  IN p_table VARCHAR(64),
  IN p_col   VARCHAR(64)
)
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_col) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` DROP COLUMN `', p_col, '`');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Dropped ', p_table, '.', p_col) AS result;
  ELSE
    SELECT CONCAT('Skipped: ', p_table, '.', p_col, ' does not exist') AS result;
  END IF;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS `p_safe_drop_index`;
DELIMITER $$
CREATE PROCEDURE `p_safe_drop_index`(
  IN p_table VARCHAR(64),
  IN p_index VARCHAR(64)
)
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE table_schema = DATABASE() AND table_name = p_table AND index_name = p_index) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` DROP INDEX `', p_index, '`');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Dropped index ', p_index, ' from ', p_table) AS result;
  ELSE
    SELECT CONCAT('Skipped: index ', p_index, ' not found on ', p_table) AS result;
  END IF;
END$$
DELIMITER ;

-- -----------------------------------------------------------------------------
-- 7. 恢复外键检查
-- -----------------------------------------------------------------------------
SET FOREIGN_KEY_CHECKS = 1;

-- -----------------------------------------------------------------------------
-- 8. 校验（任一不达标即视为失败，需人工介入）
-- -----------------------------------------------------------------------------
SELECT '--- [8/8] 校验1：基表排序规则非 general_ci 的数量（期望 0）---' AS `phase`;
SELECT COUNT(*) AS `bad_tables`
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
  AND TABLE_COLLATION <> 'utf8mb4_general_ci';

SELECT '--- [8/8] 校验2：非 general_ci 字符列数量（期望 0；含视图列）---' AS `phase`;
SELECT COLLATION_NAME, COUNT(*) AS `col_cnt`
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND COLLATION_NAME IS NOT NULL
  AND COLLATION_NAME <> 'utf8mb4_general_ci'
GROUP BY COLLATION_NAME;

SELECT '--- [8/8] 校验3：外键数量（期望 = 52，与转换前基线一致）---' AS `phase`;
SELECT COUNT(*) AS `fk_cnt` FROM information_schema.TABLE_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY';

SELECT '--- [8/8] 校验4：存储过程 Database Collation（期望全部 general_ci）---' AS `phase`;
SELECT ROUTINE_NAME, DATABASE_COLLATION FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE();

SELECT '--- [8/8] 校验5：跨表「列-列」关联探针（必须不报 1267）---' AS `phase`;
SELECT
  (SELECT COUNT(*) FROM `Sys_RoleMenu` rm JOIN `Sys_Menu` m ON rm.`MenuCode` = m.`Code`) AS `rolemenu_join_rows`,
  (SELECT COUNT(*) FROM `wf_skill_api` a JOIN `wf_skill` s ON a.`SkillCode` = s.`SkillCode`) AS `skill_join_rows`,
  (SELECT COUNT(*) FROM `v_workflow`) AS `v_workflow_rows`;

SELECT '--- 结论：bad_tables / bad_columns / fk_cnt 三项必须为 0 / 0 / 52 ---' AS `summary`;
SELECT
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
       AND TABLE_COLLATION <> 'utf8mb4_general_ci') AS `bad_tables`,
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND COLLATION_NAME IS NOT NULL
       AND COLLATION_NAME <> 'utf8mb4_general_ci') AS `bad_columns`,
  (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
     WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY') AS `fk_cnt`,
  (SELECT COUNT(*) FROM information_schema.ROUTINES
     WHERE ROUTINE_SCHEMA = DATABASE() AND DATABASE_COLLATION <> 'utf8mb4_general_ci') AS `bad_routines`;

-- =============================================================================
-- ★ 防复发（治本；只跑本脚本会被后续新表重新污染）
-- =============================================================================
-- 1) 建表**必须显式**写 `COLLATE=utf8mb4_general_ci`：
--      ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='...';
--    ⛔ 禁止只写 `DEFAULT CHARSET=utf8mb4`（会落到 utf8mb4_0900_ai_ci）。
--    ⛔ 禁止完全不写 charset（会落到 @@collation_server = utf8mb4_unicode_ci）。
-- 2) 已修复的缺 COLLATE 建表脚本（4 个）：
--      scripts/db/create_sys_organization.sql
--      scripts/db/cert_phase_definition_setup.sql
--      scripts/db/001-create-api-permission-tables.sql
--      scripts/db/create_queue_tables.sql
-- 3) 新列同样禁止 utf8mb3（`CHARACTER SET utf8` / `utf8mb3`）。
-- 4) 定期体检：
--      SELECT TABLE_NAME, TABLE_COLLATION FROM information_schema.TABLES
--       WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE'
--         AND TABLE_COLLATION <> 'utf8mb4_general_ci';
-- 5) 注意：`@@collation_server` 仍为 `utf8mb4_unicode_ci`（服务器级，改它需动
--    docker MySQL 配置并重启）。本脚本已把**库级**默认改成 general_ci，只要建表
--    显式写 COLLATE 即不受服务器级影响。若要彻底消除，可在 my.cnf 设
--    `collation-server = utf8mb4_general_ci`。
-- =============================================================================
