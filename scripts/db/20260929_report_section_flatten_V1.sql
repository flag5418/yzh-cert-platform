-- =============================================================================
-- 报告定义去主表化（★ D34 / D35）
-- -----------------------------------------------------------------------------
-- 目标：把「报告章节定义」改造成与「NC 检查规则定义」同样的扁平结构
--
-- 【改造前】两层：cert_report_template（主表）→ rpt_report_section（章节）
--   · 章节表无 StandardCode / PhaseCode，每次查询都要 JOIN 主表
--   · ReportCode 语义错位：DDL 声明 FK→rpt_audit_report，实际存 Template.Code
--   · 启用标志不一致：主表 IsValid / 章节表 IsActive（违反铁律九）
--
-- 【改造后】一层：cert_report_section（★同时改名，N1 铁律：cert_ 开头）
--   · 章节自带 OrgCode（认证机构）+ StandardCode + PhaseCode
--   · 与 cert_validation_rule 完全对称（同为配置层、同为三元组定位）
--   · 唯一键 (OrgCode, StandardCode, PhaseCode, SortOrder)
--
-- 【实测依据】（2026-09-29）
--   cert_report_template       0 行 → 废弃零成本
--   rpt_report_section         0 行 → 改名零成本
--   rpt_report_section 已有 IsValid int 列（★不是 ADD，是 MODIFY）
--
-- 铁律：七（列名三处一致）/ 八（显式 COLLATE）/ 九（IsValid 唯一）/ 十（SQL 外置）
-- 幂等：DROP + CREATE 重建 rpt_report_section；ALTER 段落可重复跑
-- =============================================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- =============================================================================
-- 1. 关闭外键检查（改名需要）
-- =============================================================================
SET FOREIGN_KEY_CHECKS = 0;

-- =============================================================================
-- 2. 旧表改名：rpt_report_section → cert_report_section
--    cert_report_template → z_deprecated_cert_report_template
--    （★ 0 行，改名零数据风险；列结构在第 4 步统一重建）
--    ⛔ RENAME TABLE 不支持 IF EXISTS，先改名报错即说明已改名过，可忽略
-- =============================================================================
-- ★ 前置：本段【非幂等】，只可执行一次。已执行过请从第 3 步继续。
--   （rpt_report_section_source 的 fk_src_section 需先手工 DROP，已单独执行）
SET FOREIGN_KEY_CHECKS = 0;

RENAME TABLE `rpt_report_section_source` TO `z_deprecated_rpt_report_section_source`;
RENAME TABLE `rpt_report_section`        TO `cert_report_section`;
RENAME TABLE `cert_report_template`      TO `z_deprecated_cert_report_template`;

SET FOREIGN_KEY_CHECKS = 1;

-- 3.1 废弃标记
ALTER TABLE `z_deprecated_rpt_report_section_source`
  COMMENT = '【已废弃 2026-09-29】字段级追溯归后台工作流设计实现（D08）。0 行，保留历史可追溯性。';

-- =============================================================================
-- 3. 主表废弃标记（★ 不 DROP，保留可追溯性；表已改名为 z_deprecated_ 前缀便于识别）
-- =============================================================================
ALTER TABLE `z_deprecated_cert_report_template`
  COMMENT = '【已废弃 2026-09-29】报告主表已废弃（D34）：不做报表系统，章节定义改为扁平结构。保留历史数据，不再写入。';

-- =============================================================================
-- 4. 章节表：重建为扁平结构
-- =============================================================================
DROP TABLE IF EXISTS `cert_report_section`;

CREATE TABLE `cert_report_section` (
  -- ─── 主键 / 通用列 ───
  `Id`           bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`         varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键，★定位只用 Code）',
  `OrgCode`      varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★认证机构编码（★配置层归属，不是专家工作区）',
  `CreateBy`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`   datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`   datetime     DEFAULT NULL,
  `DeleteBy`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`   datetime     DEFAULT NULL,
  `Status`       varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务状态',
  `Sort`         int          DEFAULT 0 COMMENT '排序号',
  `Remark`       varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`    tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`      int          NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）★铁律九唯一启用字段',

  -- ─── ★ 归属三元组（★改造核心：自带，不再 JOIN 主表）───
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★标准编码（cert_iso_standard.Code）',
  `PhaseCode`    varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★阶段编码（★cert_cert_stage.Code，GUID 不是业务码）',

  -- ─── 章节内容 ───
  `SectionName`    varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '章节名称',
  `SectionNameEn`  varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '章节名称（英文）',
  `SectionContent` longtext    COLLATE utf8mb4_general_ci COMMENT '★章节内容（模板示例正文，★纯文本）',
  `SortOrder`      int          DEFAULT 0 COMMENT '★章节排序（★唯一键组成）',

  -- ─── 关联与工作流（与 cert_validation_rule 对称）───
  `ClauseCode`      varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★对应条款编码（★可空：概述/结论类章节不映射条款）',
  `WorkflowCode`    varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '工作流编码（wf_workflow_definition.Code）',
  `WorkflowConfig`  longtext    COLLATE utf8mb4_general_ci COMMENT '★工作流 DAG（★注意：与规则的 RuleJson 字段名不同）',
  `LayoutJson`      text        COLLATE utf8mb4_general_ci COMMENT '画布布局',
  `SectionJson`     text        COLLATE utf8mb4_general_ci COMMENT '章节扩展配置',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`       (`Code`),
  UNIQUE KEY `uk_scope_sort` (`OrgCode`, `StandardCode`, `PhaseCode`, `SortOrder`),
  KEY `idx_scope`            (`OrgCode`, `StandardCode`, `PhaseCode`, `IsValid`, `SortOrder`),
  KEY `idx_clause`           (`ClauseCode`),
  KEY `idx_workflow`         (`WorkflowCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='★体系认证报告章节定义（配置层，扁平结构，与 cert_validation_rule 对称）';

-- =============================================================================
-- 5. EntityConfig 需要的字段注释同步（本期不建 EntityConfig，前端用 TreeTableLogic 显式声明）
-- =============================================================================

-- =============================================================================
-- 6. 验证
-- =============================================================================
-- 6.1 表已改名/废弃
SELECT TABLE_NAME, TABLE_COMMENT FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_report_section','rpt_report_section',
                     'cert_report_template','z_deprecated_cert_report_template');
-- 期望：cert_report_section（无 COMMENT 前缀★）+ z_deprecated_（含【已废弃】）
--       rpt_report_section 与 cert_report_template 均为 0 行

-- 6.2 列名三处一致的骨架（铁律七）
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_report_section'
ORDER BY ORDINAL_POSITION;
-- 期望：★无 ReportCode（★语义错位列已随主表消失）

-- 6.3 铁律九：IsValid 存在、无 Enable/enable
SELECT
  SUM(COLUMN_NAME = 'IsValid')                    AS HasIsValid,
  SUM(COLUMN_NAME = 'IsActive')                   AS HasIsActive,
  SUM(COLUMN_NAME IN ('Enable','enable'))         AS HasEnable
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_report_section';
-- 期望：HasIsValid=1, HasIsActive=0, HasEnable=0

-- 6.4 排序规则（铁律八）
SELECT TABLE_COLLATION FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_report_section';
-- 期望：utf8mb4_general_ci

-- 6.5 ★ 唯一键可执行性验证（MySQL 索引列不允许函数，这里是纯列，合法）
SHOW INDEX FROM cert_report_section WHERE Key_name = 'uk_scope_sort';
-- 期望：4 列（OrgCode, StandardCode, PhaseCode, SortOrder）

-- =============================================================================
-- 7. 同步对象
--    · 实体：CertPlatform.Shared/Entities/Rpt/ReportSection.cs（★由 C# 侧实施脚本处理）
--    · 控制器：CertPlatform.Admin/Controllers/Workflow/ReportDefinitionController.cs
--    · 前端：cert-admin/src/pages/workflow/report-rule/{index.vue,logic.ts}
--            cert-admin/src/pages/workflow/report-rule-config/index.vue
--    · API：cert-share/src/api/workflow/report-rule.ts
--    · 类型：cert-share/src/types/cert.ts（ReportSection / ReportTemplate）
-- =============================================================================
