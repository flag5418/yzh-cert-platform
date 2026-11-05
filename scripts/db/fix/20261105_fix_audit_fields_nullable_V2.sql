-- ====================================================================
-- 20261105_fix_audit_fields_nullable_V2.sql
-- 补充修复：Entity 中 string?/int?/DateTime? 属性对应列仍为 NOT NULL 的问题
--
-- 根因：CodeFirst 建表时普遍未识别 C# 可空类型；前两批批量修复覆盖了
--       通用审计字段 + 常见可空字段，但 entity 特有的 string? 属性
--       （如 LeaderTitle 等）仍有遗漏。
--
-- 策略：逐实体扫描 entity 文件 → 提取所有 string?/int?/DateTime? 属性
--       → 与 DB 的 NOT NULL 列交叉比对 → 生成 ALTER。
-- 以下列出 scanner 确认的可空属性及其对应表。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ──────────────────────────────────────────────────────────────────
-- cert_audit_plan（CertAuditPlan.cs）
--  可空属性：Standards, PlanKind, CoverageMode, AuditYear, PreviousPlanCode
-- ──────────────────────────────────────────────────────────────────
ALTER TABLE cert_audit_plan
  MODIFY Standards       varchar(255) NULL,
  MODIFY PlanKind        varchar(255) NULL,
  MODIFY CoverageMode    varchar(255) NULL,
  MODIFY AuditYear       int          NULL,
  MODIFY PreviousPlanCode varchar(255) NULL;

-- ──────────────────────────────────────────────────────────────────
-- cert_audit_plan_detail（CertAuditPlanDetail.cs）
--  可空属性：Clauses, Departments, DeptNames, ClauseNumbers, OwnerCode, OwnerName, Remark
-- ──────────────────────────────────────────────────────────────────
ALTER TABLE cert_audit_plan_detail
  MODIFY Clauses         varchar(1000) NULL,
  MODIFY Departments     varchar(500)  NULL,
  MODIFY DeptNames       varchar(500)  NULL,
  MODIFY ClauseNumbers   varchar(1000) NULL,
  MODIFY OwnerCode       varchar(64)   NULL,
  MODIFY OwnerName       varchar(64)   NULL,
  MODIFY Remark          text          NULL;

-- ──────────────────────────────────────────────────────────────────
-- cert_enterprise_dept（CertEnterpriseDept.cs）
--  可空属性：LeaderTitle（手工已补）
-- ──────────────────────────────────────────────────────────────────
-- LeaderTitle 已修复 (LeaderTitle varchar(255) NULL)

-- ──────────────────────────────────────────────────────────────────
-- cert_cert_stage（CertStageView / CertStage.cs）
--  可空属性需确认（Category, StatusName）
-- ──────────────────────────────────────────────────────────────────
ALTER TABLE cert_cert_stage
  MODIFY Category        varchar(255) NULL,
  MODIFY Status          varchar(255) NULL,
  MODIFY StatusName      varchar(255) NULL;

-- ──────────────────────────────────────────────────────────────────
-- cert_certification_body（CB entity，LogoUrl, ScopeText, ThemeConfig, LoginConfig,
--   MaxUsers, MaxEnterprises, ExpireDate 可空）
-- ──────────────────────────────────────────────────────────────────
ALTER TABLE cert_certification_body
  MODIFY LogoUrl         varchar(255) NULL,
  MODIFY ScopeText       varchar(255) NULL,
  MODIFY ThemeConfig     varchar(255) NULL,
  MODIFY LoginConfig     varchar(255) NULL,
  MODIFY MaxUsers        int          NULL,
  MODIFY MaxEnterprises  int          NULL,
  MODIFY ExpireDate      datetime     NULL;

-- ──────────────────────────────────────────────────────────────────
-- cert_ai_config（Provider, ApiKey, Model, MaxTokens）
-- ──────────────────────────────────────────────────────────────────
ALTER TABLE cert_ai_config
  MODIFY Provider        varchar(50)  NULL,
  MODIFY ApiKey          varchar(500) NULL,
  MODIFY Model           varchar(100) NULL,
  MODIFY MaxTokens       int          NULL;

-- ──────────────────────────────────────────────────────────────────
-- cert_expert_task_queue_item（TaskKind, EvidenceFingerprint, ModelName, InputSnapshotJson,
--   ResultSummaryJson, DurationMs, ErrorMessage 等可空）
-- ──────────────────────────────────────────────────────────────────
ALTER TABLE cert_expert_task_queue_item
  MODIFY TaskKind        varchar(255) NULL;

-- ──────────────────────────────────────────────────────────────────
-- cert_clause_attr / cert_clause_attr_def（AttrValue, Remark 等）
-- ──────────────────────────────────────────────────────────────────
ALTER TABLE cert_clause_attr
  MODIFY AttrValue       varchar(255) NULL,
  MODIFY Remark          varchar(255) NULL;

ALTER TABLE cert_clause_attr_def
  MODIFY Remark          varchar(255) NULL;

-- ──────────────────────────────────────────────────────────────────
-- 自检：列出仍有 NOT NULL 的 entity-typed 列
-- ──────────────────────────────────────────────────────────────────
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME LIKE BINARY 'cert\_%'
  AND IS_NULLABLE = 'NO'
  AND COLUMN_NAME NOT IN ('Id','IsValid','IsDeleted','CreateTime','Name','OrgName',
    'OrgCode','EnterpriseCode','DeptName','MenuName','Code','Sort','StandardCode',
    'StageCode','TaskCode','PlanCode','ItemCode','RoleCode','UserCode','PhaseCode',
    'ParentCode','Gender','OrgLevel','NodeType','IsLeaf','DictCode','IsValid','IsDeleted')
  AND DATA_TYPE IN ('varchar','char','int','bigint','datetime','text','longtext','mediumtext','decimal','smallint','tinyint')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
