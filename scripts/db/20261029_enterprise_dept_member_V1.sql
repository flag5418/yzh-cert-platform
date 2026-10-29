-- =============================================================================
-- 专家端：企业部门人员（cert_enterprise_dept_member）
-- 日期：2026-10-29
-- 目的：企业机构管理页改造为「左树右表」
--   - 左树：cert_enterprise（根节点=企业，多企业并列） + cert_enterprise_dept（子节点=部门，不限层级）
--   - 右表：cert_enterprise_dept_member（本表）—— 选中部门后维护该部门人员
--
-- 用途：入场计划「准备阶段派沟通任务、计划阶段排日程、现场阶段定访谈对象」
--
-- ★ 字符集/排序规则全库统一 utf8mb4 + utf8mb4_general_ci（铁律八）
-- ★ 列名 = PascalCase（DB / C# / TS 三处一致，铁律七）
-- ★ 幂等：可重复执行
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `cert_enterprise_dept_member` (
  `Id`             BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`           VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  -- 归属（工作区隔离走 EnterpriseCode → 本工作区企业集合）
  `EnterpriseCode` VARCHAR(50)  NOT NULL COMMENT '所属企业编码（冗余，关联 cert_enterprise.Code）',
  `DeptCode`       VARCHAR(50)  NOT NULL COMMENT '所属部门编码（关联 cert_enterprise_dept.Code）',

  -- 人员信息
  `Name`           VARCHAR(100) NOT NULL COMMENT '姓名',
  `Position`       VARCHAR(100)          DEFAULT NULL COMMENT '职务',
  `RoleType`       VARCHAR(100)          DEFAULT NULL COMMENT '角色（沟通对象／访谈对象／陪同人员等）',
  `Phone`          VARCHAR(50)           DEFAULT NULL COMMENT '联系电话',
  `Email`          VARCHAR(100)          DEFAULT NULL COMMENT '邮箱',

  -- 通用字段
  `Sort`           INT          NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`         TEXT                  DEFAULT NULL COMMENT '备注',

  -- BaseEntity 审计字段（铁律六：三列 ≥ varchar(50)，一律存用户 Code）
  `IsValid`        INT          NOT NULL DEFAULT 1  COMMENT '有效标志（1=有效 0=禁用）——铁律九统一',
  `IsDeleted`      TINYINT(1)   NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间（铁律十：本地时间）',
  `CreateBy`       VARCHAR(64)           DEFAULT NULL COMMENT '创建人（Sys_User.Code）',
  `UpdateTime`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `UpdateBy`       VARCHAR(64)           DEFAULT NULL COMMENT '更新人（Sys_User.Code）',
  `DeleteTime`     DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`       VARCHAR(64)           DEFAULT NULL COMMENT '删除人（Sys_User.Code）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_dept_code` (`DeptCode`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='企业部门人员（专家端 —— 选中部门后维护该部门人员：沟通对象／访谈对象等）';

-- -----------------------------------------------------------------------------
-- 自检输出
-- -----------------------------------------------------------------------------
SELECT '--- 建表确认 ---' AS `check`;
SELECT TABLE_NAME, TABLE_COMMENT
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_enterprise_dept_member';

SELECT '--- 列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_dept_member'
ORDER BY ORDINAL_POSITION;

SELECT '--- 索引确认 ---' AS `check`;
SELECT INDEX_NAME, COLUMN_NAME, NON_UNIQUE
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_dept_member'
ORDER BY INDEX_NAME, SEQ_IN_INDEX;
