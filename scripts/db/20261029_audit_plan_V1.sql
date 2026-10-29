-- =============================================================================
-- 认证计划（按条款为核心的体系认证入场计划）
-- 日期：2026-10-29
-- 目的：为「认证计划」功能建表（专家端），并给标准表补「简称」列
--
-- ① cert_iso_standard 增加 Abbrev（标准简称，如 iso9001→e），用于条款序号前缀（e 4.3）
-- ② cert_audit_plan        计划主表：企业 / 阶段 / 标准（逗号 code）/ 是否启用
-- ③ cert_audit_plan_detail 计划明细：条款（逗号 code）/ 部门（逗号 code）/ 部门名称 / 条款序号
-- ④ 菜单 MENU_AUD_17 /audit-plan（专家系统下）
--
-- ★ 字符集/排序规则全库统一 utf8mb4 + utf8mb4_general_ci（铁律八）
-- ★ 列名 = PascalCase（DB / C# / TS 三处一致）
-- ★ 启用列 = IsValid（铁律九，不存在 Enable）
-- ★ 幂等：可重复执行
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- ① cert_iso_standard 增加 Abbrev 列（标准简称）
--    幂等：先判断列是否存在，避免重复 ADD COLUMN 报错
-- -----------------------------------------------------------------------------
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_iso_standard'
    AND COLUMN_NAME = 'Abbrev'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_iso_standard` ADD COLUMN `Abbrev` VARCHAR(20) NULL DEFAULT NULL COMMENT ''标准简称（如 iso9001→e），用于条款序号前缀显示'' AFTER `VersionYear`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 种子：常见标准简称（仅当为空时写入，不覆盖人工维护值）
UPDATE `cert_iso_standard` SET `Abbrev` = 'e' WHERE `StandardCode` = 'iso9001'  AND (`Abbrev` IS NULL OR `Abbrev` = '');
UPDATE `cert_iso_standard` SET `Abbrev` = 'n' WHERE `StandardCode` = 'iso14001' AND (`Abbrev` IS NULL OR `Abbrev` = '');
UPDATE `cert_iso_standard` SET `Abbrev` = 's' WHERE `StandardCode` = 'iso45001' AND (`Abbrev` IS NULL OR `Abbrev` = '');
UPDATE `cert_iso_standard` SET `Abbrev` = 'm' WHERE `StandardCode` = 'iso13485' AND (`Abbrev` IS NULL OR `Abbrev` = '');
UPDATE `cert_iso_standard` SET `Abbrev` = 'f' WHERE `StandardCode` = 'iso22000' AND (`Abbrev` IS NULL OR `Abbrev` = '');
UPDATE `cert_iso_standard` SET `Abbrev` = 'i' WHERE `StandardCode` = 'iso27001' AND (`Abbrev` IS NULL OR `Abbrev` = '');

-- -----------------------------------------------------------------------------
-- ② cert_audit_plan：计划主表
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_audit_plan` (
  `Id`             BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`           VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  `Name`           VARCHAR(200)          DEFAULT NULL COMMENT '计划名称（默认=企业·阶段·认证计划）',
  `EnterpriseCode` VARCHAR(50)  NOT NULL COMMENT '企业编码（关联 cert_enterprise.Code）',
  `StageCode`      VARCHAR(50)  NOT NULL COMMENT '阶段编码（关联 cert_cert_stage.Code，GUID）',
  `Standards`      VARCHAR(500)          DEFAULT NULL COMMENT '认证标准（多个逗号隔开，存 ISOStandard.Code，GUID）',

  `Sort`           INT          NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`         TEXT                  DEFAULT NULL COMMENT '备注',

  `IsValid`        INT          NOT NULL DEFAULT 1  COMMENT '有效标志（1=启用 0=停用）——铁律九',
  `IsDeleted`      TINYINT(1)   NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`       VARCHAR(64)           DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`     DATETIME     NULL DEFAULT NULL COMMENT '更新时间（新建为空，更新时填）',
  `UpdateBy`       VARCHAR(64)           DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`     DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`       VARCHAR(64)           DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_stage_code` (`StageCode`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='认证计划主表（专家端 —— 按条款为核心的体系认证入场计划）';

-- -----------------------------------------------------------------------------
-- ③ cert_audit_plan_detail：计划明细（每条组合 = 条款×部门）
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_audit_plan_detail` (
  `Id`             BIGINT        NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`           VARCHAR(50)   NOT NULL COMMENT '业务键（GUID）',
  `PlanCode`       VARCHAR(50)   NOT NULL COMMENT '所属计划（关联 cert_audit_plan.Code）',

  `Clauses`        VARCHAR(1000)          DEFAULT NULL COMMENT '条款（多个逗号隔开，存 ISOClause.Code，GUID）',
  `Departments`    VARCHAR(500)           DEFAULT NULL COMMENT '部门（多个逗号隔开，存 CertEnterpriseDept.Code，GUID）',
  `DeptNames`      VARCHAR(500)           DEFAULT NULL COMMENT '部门名称（多个逗号隔开，冗余便于展示）',
  `ClauseNumbers`  VARCHAR(1000)          DEFAULT NULL COMMENT '条款序号（多个逗号隔开，冗余便于展示）',

  `Sort`           INT           NOT NULL DEFAULT 0  COMMENT '排序号（计划内顺序）',
  `Remark`         TEXT                   DEFAULT NULL COMMENT '备注',

  `IsValid`        INT           NOT NULL DEFAULT 1  COMMENT '有效标志（1=启用 0=停用）',
  `IsDeleted`      TINYINT(1)    NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`       VARCHAR(64)            DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`     DATETIME      NULL DEFAULT NULL COMMENT '更新时间（新建为空，更新时填）',
  `UpdateBy`       VARCHAR(64)            DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`     DATETIME               DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`       VARCHAR(64)            DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_plan_code` (`PlanCode`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='认证计划明细（每条组合 = 条款×部门；合并/拆分由此表行数体现）';

-- -----------------------------------------------------------------------------
-- ④ 菜单：认证计划（专家系统 MENU_AUD_00 下）
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_17', 'MENU_AUD_00', '认证计划', 'Calendar', 290, '/audit-plan', 'auditor',
   '按条款为核心的体系认证入场计划：企业/阶段/多标准 → 部门+条款组合 → 计划主表+明细（报告按标准挂载的骨架）',
   1, 0, NOW(), 'seed_auditor')
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `Icon`        = VALUES(`Icon`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `Description` = VALUES(`Description`),
  `IsDeleted`   = 0;

-- 授权给专家角色
INSERT INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
SELECT REPLACE(UUID(), '-', ''), 'ROLE_AUDIT_CLIENT_ADMIN', m.`Code`, m.`OrderNo`, NOW(), 'seed_auditor'
FROM `Sys_Menu` m
WHERE m.`Code` = 'MENU_AUD_17'
  AND m.`IsDeleted` = 0
  AND NOT EXISTS (
    SELECT 1 FROM `Sys_RoleMenu` rm
    WHERE rm.`RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
      AND rm.`MenuCode` = m.`Code`
  );

-- -----------------------------------------------------------------------------
-- ⑤ 自检输出
-- -----------------------------------------------------------------------------
SELECT '--- 新建表确认（应含 cert_audit_plan / cert_audit_plan_detail）---' AS `check`;
SELECT TABLE_NAME, TABLE_COMMENT
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_audit_plan', 'cert_audit_plan_detail');

SELECT '--- cert_iso_standard.Abbrev 列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_iso_standard' AND COLUMN_NAME = 'Abbrev';

SELECT '--- 菜单确认（应含 MENU_AUD_17）---' AS `check`;
SELECT `Code`, `ParentCode`, `MenuName`, `Url`, `IsValid`, `Tag`
FROM `Sys_Menu` WHERE `Code` = 'MENU_AUD_17' AND `IsDeleted` = 0;
