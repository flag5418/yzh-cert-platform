-- =============================================================================
-- 专家端基础资料：审核人员管理 + 企业部门管理
-- 日期：2026-10-29
-- 目的：为认证计划入场做准备
--
-- ① cert_audit_member：审核人员管理
--    - 维护认证审核人员基本信息（姓名/职称/专业/资质/联系方式）
--    - 工作区隔离（OrgCode），不同工作区独立维护本区的审核人员库
--    - 不关联登录账号，入场计划时由计划编制人从池子里挑
--
-- ② cert_enterprise_dept：企业部门管理
--    - 按企业建立部门树（不限层级），每部门带负责人信息
--    - 关联 cert_enterprise，入场计划选部门、定访谈对象时引用
--    - 暂不负责过程(Process)，那是体系认证计划章节解决的问题
--
-- ★ 字符集/排序规则全库统一 utf8mb4 + utf8mb4_general_ci（铁律八）
-- ★ 列名 = PascalCase（DB / C# / TS 三处一致）
-- ★ 幂等：可重复执行
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- ① cert_audit_member：审核人员管理
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_audit_member` (
  `Id`           BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`         VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  -- 工作区隔离
  `OrgCode`      VARCHAR(50)  NOT NULL COMMENT '所属工作区/虚拟机构编码（专家平台的租户隔离键）',

  -- 基本信息
  `Name`         VARCHAR(100) NOT NULL COMMENT '姓名',
  `Gender`       INT          NOT NULL DEFAULT 0 COMMENT '性别：0=未知 1=男 2=女',
  `Title`        VARCHAR(100)          DEFAULT NULL COMMENT '职称（高级工程师/审核员/高级审核员等）',
  `Profession`   VARCHAR(100)          DEFAULT NULL COMMENT '专业领域',
  `WorkYears`    INT                   DEFAULT NULL COMMENT '从业年限',
  `Phone`        VARCHAR(50)           DEFAULT NULL COMMENT '联系电话',
  `Email`        VARCHAR(100)          DEFAULT NULL COMMENT '邮箱',
  `QualificationNo` VARCHAR(100)       DEFAULT NULL COMMENT '资质证书编号',

  -- 通用字段
  `Sort`         INT          NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`       TEXT                  DEFAULT NULL COMMENT '备注',

  -- BaseEntity 审计字段（铁律六：创建/更新人必须是用户 Code，此处=工作区种子/操作人）
  `IsValid`      INT          NOT NULL DEFAULT 1  COMMENT '有效标志（1=有效 0=禁用）——铁律九统一',
  `IsDeleted`    TINYINT(1)   NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间（铁律十：本地时间）',
  `CreateBy`     VARCHAR(64)  DEFAULT NULL COMMENT '创建人',
  `UpdateTime`   DATETIME     NULL DEFAULT NULL COMMENT '更新时间（新建为空，更新时填）',
  `UpdateBy`     VARCHAR(64)  DEFAULT NULL COMMENT '更新人',
  `DeleteTime`   DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`     VARCHAR(64)           DEFAULT NULL COMMENT '删除人',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_org_qualno` (`OrgCode`, `QualificationNo`),
  KEY `idx_org_code` (`OrgCode`),
  KEY `idx_name` (`Name`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='审核人员管理（专家端 —— 入场计划指派审核员时从池子挑选，不关联登录账号）';

-- -----------------------------------------------------------------------------
-- ② cert_enterprise_dept：企业部门管理
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_enterprise_dept` (
  `Id`           BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`         VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  -- 归属企业
  `EnterpriseCode` VARCHAR(50) NOT NULL COMMENT '所属企业编码（关联 cert_enterprise.Code）',

  -- 部门信息
  `DeptName`     VARCHAR(200) NOT NULL COMMENT '部门名称',
  `ParentCode`   VARCHAR(50)          DEFAULT NULL COMMENT '上级部门编码（NULL = 顶层部门，不限层级）',

  -- 负责人信息
  `LeaderName`   VARCHAR(50)           DEFAULT NULL COMMENT '负责人姓名',
  `LeaderPhone`  VARCHAR(50)           DEFAULT NULL COMMENT '负责人联系电话',
  `LeaderTitle`  VARCHAR(100)          DEFAULT NULL COMMENT '负责人职务',

  -- 通用字段
  `Sort`         INT          NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`       TEXT                  DEFAULT NULL COMMENT '备注',

  -- BaseEntity 审计字段
  `IsValid`      INT          NOT NULL DEFAULT 1  COMMENT '有效标志（1=有效 0=禁用）',
  `IsDeleted`    TINYINT(1)   NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`     VARCHAR(64)           DEFAULT NULL COMMENT '创建人',
  `UpdateTime`   DATETIME     NULL DEFAULT NULL COMMENT '更新时间（新建为空，更新时填）',
  `UpdateBy`     VARCHAR(64)           DEFAULT NULL COMMENT '更新人',
  `DeleteTime`   DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`     VARCHAR(64)           DEFAULT NULL COMMENT '删除人',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_parent_code` (`ParentCode`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='企业部门管理（专家端 —— 入场计划选择部门、定访谈对象时引用，不限层级，暂不负责过程）';

-- -----------------------------------------------------------------------------
-- ③ 自检输出
-- -----------------------------------------------------------------------------
SELECT '--- 新建表确认 ---' AS `check`;
SELECT TABLE_NAME, TABLE_COMMENT
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_audit_member', 'cert_enterprise_dept');

SELECT '--- cert_audit_member 列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_audit_member'
ORDER BY ORDINAL_POSITION;

SELECT '--- cert_enterprise_dept 列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_dept'
ORDER BY ORDINAL_POSITION;
