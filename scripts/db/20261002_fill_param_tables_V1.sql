-- ============================================================================
-- 20261002_fill_param_tables_V1.sql
-- 体系认证全局参数 —— 后台定义表 + 企业值表
--
-- 设计依据：docs/20-体系认证/03-详细设计/05-企业资料规范化/
--           22-填写单元属性分类与取值来源模型-V1.md §七（全局参数 2 表）
--           23-全局填写规则与参数维护设计-V1.md §四（标准 × 阶段裁剪）
--           25-纸面实验实测报告-V1.md（167 份真实文档实测支撑本表必要性）
--
-- 业务定位（2026-10-02 用户口径）：
--   后台管理「体系认证全局参数定义」= 按 机构 × 标准 × 阶段 定义参数（本表 = 定义）
--   企业端「企业全局参数定义」    = 企业基本信息 + 后台定义参数 的合并列表（企业填值）
--
-- ⛔ 铁律：
--   1. DB列名 = C#属性名 = TS字段名，PascalCase 逐字一致
--   2. 唯一键不含 IsDeleted（软删后须能重建 → 建前「含已删」查重并复活）
--   3. 启用/禁用唯一字段 = IsValid（int，0/1）。⛔ 禁 Enable
--   4. 身份段（OrgCode/EnterpriseCode/StandardCode/StageCode）⛔ 禁 NULL
-- ============================================================================

SET NAMES utf8mb4;

-- ────────────────────────────────────────────────────────────────────────────
-- 表 1：cert_fill_param_def —— 体系认证全局参数定义（后台管理维护）
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_fill_param_def` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',

  `OrgCode` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '★机构编码（cert_certification_body.Code，租户隔离键，⛔禁NULL）',
  `StandardCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '★标准Code（cert_iso_standard.Code，GUID）',
  `StageCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '★阶段Code（cert_cert_stage.Code，GUID，⛔不是业务短码 jd01/03）',

  `ParamCode` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '★参数编码（同一 机构+标准+阶段 下唯一，如 company_name / doc_prefix / legal_person）',
  `ParamName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '参数名称（展示用，如「企业全称」）',
  `GroupName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT '基础信息' COMMENT '分组名（界面按此分组展示）',

  `ValueType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'text' COMMENT '值类型：text | number | date | enum | bool',
  `EnumOptions` json DEFAULT NULL COMMENT '枚举选项（ValueType=enum 时必填）：[{"Value":"A","Label":"甲"}...]',

  `SourceKind` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'global' COMMENT '★取值来源类别：global=全局参数 | replace=替换 | headerFooter=页眉页脚 | ai=AI生成 | manual=人工填写 | compute=计算派生',
  `SourceExpr` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '自动取值表达式（SourceKind=global 时用）：enterprise.Name / enterprise.Address / enterprise.CreditCode / system.Date 等',
  `MaintainMode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'auto' COMMENT '★维护方式：auto=自动映射（企业端只读+跳转） | manual=企业手工填 | both=自动带出但允许企业覆盖',
  `DefaultValue` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '默认值（企业端首次带出时使用）',
  `Placeholder` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '输入提示',

  `IsRequired` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否必填（企业端未填则文档填充时产出待办）',
  `IsBuiltin` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否内置参数（内置不可删除，如企业全称/信用代码）',
  `SortOrder` int NOT NULL DEFAULT '0' COMMENT '排序号',
  `Description` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '参数说明（给企业看的填写指引）',

  `CreateBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务状态（保留）',
  `Sort` int DEFAULT '0' COMMENT '排序号（框架字段）',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_org_std_stage_param` (`OrgCode`,`StandardCode`,`StageCode`,`ParamCode`),
  KEY `idx_scope` (`OrgCode`,`StandardCode`,`StageCode`,`IsValid`,`SortOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='体系认证全局参数定义（后台管理：按 机构×标准×阶段 定义）';


-- ────────────────────────────────────────────────────────────────────────────
-- 表 2：cert_fill_param_value —— 企业全局参数值（企业端完善）
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_fill_param_value` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',

  `OrgCode` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '★机构编码（租户隔离键，⛔禁NULL）',
  `EnterpriseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '★企业Code（cert_enterprise.Code，⛔禁NULL）',
  `EnterpriseName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '企业名称（冗余快照，列表页免JOIN）',
  `StandardCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '★标准Code（cert_iso_standard.Code，GUID）',
  `StageCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '★阶段Code（cert_cert_stage.Code，GUID）',

  `ParamCode` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '★参数编码（对应 cert_fill_param_def.ParamCode）',
  `ParamName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '参数名称（冗余快照，定义被改后仍可追溯）',
  `GroupName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '分组名（冗余快照）',
  `ValueType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT 'text' COMMENT '值类型（冗余快照）',
  `EnumOptions` json DEFAULT NULL COMMENT '枚举选项（冗余快照，企业端下拉用）',

  `ParamValue` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '★参数值（企业填写的最终值）',
  `ValueSource` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'auto' COMMENT '★值来源：auto=自动映射带出 | manual=企业手工填写 | ai=AI生成 | import=批量导入',
  `SourceRef` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '自动取值来源说明（如「企业基础信息 · 企业全称」，用于界面提示「此项来自企业管理」）',
  `IsManualEdited` tinyint(1) NOT NULL DEFAULT '0' COMMENT '企业是否人工改过（=1 时不再被自动映射覆盖）',

  `MaintainMode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'auto' COMMENT '维护方式（冗余快照，决定企业端可编辑性）',
  `IsRequired` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否必填（冗余快照）',
  `IsFilled` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否已完善（列表角标 / 完成度统计用）',
  `SortOrder` int NOT NULL DEFAULT '0' COMMENT '排序号（冗余快照）',

  `CreateBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务状态（保留）',
  `Sort` int DEFAULT '0' COMMENT '排序号（框架字段）',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_ent_std_stage_param` (`EnterpriseCode`,`StandardCode`,`StageCode`,`ParamCode`),
  KEY `idx_ent_scope` (`OrgCode`,`EnterpriseCode`,`StandardCode`,`StageCode`,`IsValid`),
  KEY `idx_ent_list` (`EnterpriseCode`,`SortOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业全局参数值（企业端完善；自动映射 + 人工覆盖）';


-- ============================================================================
-- 验证 SQL（跑完必须全绿）
-- ============================================================================
-- ① 两表存在且列名为 PascalCase（★ 必须用 utf8mb4_bin，否则永远返回 0 行 = 假阴性）
-- SELECT TABLE_NAME, COUNT(*) AS cols,
--        SUM(CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin REGEXP '^[a-z]') AS bad_lowercase
--   FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA='yzh_cert_platform'
--    AND TABLE_NAME IN ('cert_fill_param_def','cert_fill_param_value')
--  GROUP BY TABLE_NAME;
-- 期望：bad_lowercase = 0
--
-- ② ⛔ 禁 Enable 列
-- SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA='yzh_cert_platform'
--    AND TABLE_NAME IN ('cert_fill_param_def','cert_fill_param_value')
--    AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'Enable';
-- 期望：0 行
