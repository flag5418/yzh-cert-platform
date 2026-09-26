-- =====================================================================
-- create-enterprise-stage-2026-09-25.sql
--
-- 目的：新建「企业-阶段-标准」关联表 cert_enterprise_stage
--
-- 业务（用户 2026-09-25 裁定）：
--   * 专家端「阶段关联」——左侧企业，右侧「阶段 + 标准」树表，勾选即关联
--   * 一个企业在**不同阶段**可能申请**一个到多个标准**的认证
--     → 关联三元组 =（EnterpriseCode, StageCode, StandardCode）
--
-- 设计取舍：
--   ① 阶段表**统一用 cert_cert_stage**（用户裁定）—— 菜单 MENU_00203「认证阶段定义」
--      已指向它，机构-阶段关联（cert_org_stage）也读它；cert_phase_definition
--      为重复实现的孤儿（前端零引用 / 0 行），本次不动。
--   ② 关联键走**业务键**（准则 A）：EnterpriseCode / StageCode / StandardCode，
--      不用 Id。StageCode 关联 cert_cert_stage.StageCode，与 cert_org_stage 一致。
--   ③ **不加外键**：cert_iso_standard.Code 是 varchar(36)，本表 StandardCode 是
--      varchar(50)，类型不等长 → 加 FK 会报 errno 150。既有 cert_org_stage 亦无 FK。
--   ④ 唯一键 uk_ent_stage_std **不含 IsDeleted** —— 因为本表在代码层用
--      [YZHDeleteStrategy(Mode = DeleteMode.Hard)] **物理删除**（取消勾选 = 删行，
--      关联行无历史价值）。若含 IsDeleted，二次软删会撞唯一键（即 N58 的坑）。
--
-- 遵守铁律：
--   铁律七  列名 PascalCase（DB = C# = TS 逐字一致）
--   铁律八  全库统一 utf8mb4_general_ci，DDL 显式带 COLLATE
--   铁律九  禁 Enable 列；启禁用统一走 IsValid
--
-- 幂等：CREATE TABLE IF NOT EXISTS
-- =====================================================================

SET @db := DATABASE();

CREATE TABLE IF NOT EXISTS `cert_enterprise_stage` (
  `Id`             bigint       NOT NULL AUTO_INCREMENT,
  `Code`           varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `EnterpriseCode` varchar(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业编码（关联 cert_enterprise.Code）',
  `StageCode`      varchar(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段编码（关联 cert_cert_stage.StageCode）',
  `StandardCode`   varchar(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码（关联 cert_iso_standard.Code）',
  `Status`         varchar(20)  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none',
  `Remark`         varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy`       varchar(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`       varchar(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`     datetime     DEFAULT NULL,
  `DeleteBy`       varchar(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`     datetime     DEFAULT NULL,
  `IsDeleted`      tinyint(1)   NOT NULL DEFAULT '0',
  `IsValid`        tinyint(1)   DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_ent_stage_std` (`EnterpriseCode`,`StageCode`,`StandardCode`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_stage_code` (`StageCode`),
  KEY `idx_standard_code` (`StandardCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业-阶段-标准关联（专家端）';

-- =====================================================================
-- 验证 1：表结构（应 14 列，字符集全部 utf8mb4_general_ci）
-- =====================================================================
SELECT
  ORDINAL_POSITION AS pos,
  COLUMN_NAME,
  COLUMN_TYPE,
  IS_NULLABLE,
  COLUMN_DEFAULT,
  COLLATION_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_stage'
ORDER BY ORDINAL_POSITION;

-- 验证 2：索引（应含 PRIMARY / uk_code / uk_ent_stage_std / 3 个 idx_*）
SELECT INDEX_NAME, NON_UNIQUE, SEQ_IN_INDEX, COLUMN_NAME
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_stage'
ORDER BY INDEX_NAME, SEQ_IN_INDEX;

-- 验证 3：命名体检（应 0 行）
SELECT CONCAT('命名违规列: ', COLUMN_NAME) AS violation
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_stage'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z][A-Za-z0-9]*$';

-- 验证 4：字符集体检（应 0 行）
SELECT CONCAT('字符集违规列: ', COLUMN_NAME, ' = ', COLLATION_NAME) AS violation
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_stage'
  AND COLLATION_NAME IS NOT NULL AND COLLATION_NAME <> 'utf8mb4_general_ci';

-- 验证 5：Enable 零容忍（应 0 行，铁律九）
SELECT CONCAT('Enable 违规列: ', COLUMN_NAME) AS violation
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_stage'
  AND BINARY COLUMN_NAME IN ('Enable', 'enable');
