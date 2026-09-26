-- ============================================================
-- 专家平台「企业管理」前置：数据库清理与对齐
-- 日期：2026-09-25
-- 备份：backup/before-ent-cleanup-20260925.sql
--
-- 用户决策（2026-09-25）：
--   ① 未到开发阶段的 ent_* 表【直接删除】——不修、不改名、不设计，
--      等真要做这些功能时再讨论表结构
--   ② 企业表统一到现有 cert_enterprise
--   ③ DefaultGroups 保留「企业用户」占位分组（代码不改）
--
-- 说明：
--   · 9 张 ent_* 表全部 0 行，删除零数据损失
--   · 先断开 4 个【入向】外键（audit_* / rpt_* 指向 ent_* 的），
--     否则 DROP TABLE 会被外键拦住
--   · ent_* 之间的内部外键随表一起删除，按依赖顺序（子表先）执行
--   · 铁律十：SQL 外置，.sh 只做编排
-- ============================================================

USE `yzh_cert_platform`;

-- ============================================================
-- 1. 完善工作区挂靠机构编码
--    工作区 OrgCode = 机构 CbCode（AuditorRegisterService.cs:187）
--    工作区 66bbf572….OrgCode='CB001' 是孤儿 → 把机构补成 CB001
--    ★ 只改 Name/ShortName/CbCode，【不改 Code】
--      （cert_org_standard / cert_org_stage 用 Code 关联，改了会断链）
-- ============================================================
UPDATE `cert_certification_body`
SET `Name`       = '河北雄安尚龙认证有限公司',
    `ShortName`  = '尚龙认证',
    `CbCode`     = 'CB001',
    `UpdateBy`   = 'manual_fix_20260925',
    `UpdateTime` = NOW()
WHERE `Code` = '906e8b2a962c4062b21144af4cc4abc0';

-- ============================================================
-- 2. cert_enterprise 列对齐（使其能承载 Enterprise 实体）
--    实体字段：OrgCode / EnterpriseNo / Name / ShortName / CreditCode /
--              LegalPerson / Province / City / Address / IndustryType /
--              EmployeeCount / CertScope / Contact* / ArchiveDate
-- ============================================================
ALTER TABLE `cert_enterprise`
  -- Code char(36) → varchar(36)：与项目其余表一致
  MODIFY COLUMN `Code`       varchar(36) NOT NULL         COMMENT '全局唯一编码（GUID）',
  -- 放开 NOT NULL：不是所有企业都填了统一社会信用代码
  MODIFY COLUMN `CreditCode` varchar(50) DEFAULT NULL     COMMENT '统一社会信用代码',
  -- tinyint → varchar(50)：业务状态（与 cert_certification_body.Status 一致）
  MODIFY COLUMN `Status`     varchar(50) DEFAULT 'active' COMMENT '业务状态',
  -- 补齐实体需要但表里没有的 4 列
  ADD COLUMN `EnterpriseNo` varchar(20) DEFAULT NULL COMMENT '企业编号（工作区内唯一）' AFTER `OrgCode`,
  ADD COLUMN `CertScope`    text                    COMMENT '认证范围描述'             AFTER `EmployeeCount`,
  ADD COLUMN `ArchiveDate`  date        DEFAULT NULL COMMENT '归档日期'                AFTER `CertScope`,
  ADD COLUMN `Sort`         int         DEFAULT 0    COMMENT '排序号';

-- 唯一约束：全局唯一 → 工作区作用域（同一企业可存在于不同虚拟机构）
ALTER TABLE `cert_enterprise`
  DROP INDEX `uk_credit_code`,
  ADD UNIQUE KEY `uk_org_ent_no`     (`OrgCode`, `EnterpriseNo`),  -- 工作区内编号不重复
  ADD UNIQUE KEY `uk_org_ent_name`   (`OrgCode`, `Name`),          -- 工作区内名称不重复
  ADD UNIQUE KEY `uk_org_ent_credit` (`OrgCode`, `CreditCode`),    -- 工作区内同一企业只有一份档案
  ADD KEY        `idx_credit_code`   (`CreditCode`);               -- 跨工作区按信用代码查

-- ============================================================
-- 3. 断开指向 ent_* 的【入向】外键（4 个）
--    不断开的话，第 4 步 DROP TABLE 会被拦住
--    断开后 audit_* / rpt_* 的对应列保留为普通列（这些功能也还没开发）
-- ============================================================
ALTER TABLE `audit_finding`       DROP FOREIGN KEY `fk_finding_file`;
ALTER TABLE `audit_task`          DROP FOREIGN KEY `fk_task_phase`;
ALTER TABLE `audit_nonconformity` DROP FOREIGN KEY `fk_nc_sourcecheck`;
ALTER TABLE `rpt_report_task`     DROP FOREIGN KEY `fk_rpttask_phase`;

-- ============================================================
-- 4. 删除 9 张 ent_* 表（未到开发阶段，全部 0 行）
--    按依赖顺序：先叶子表，最后根表 ent_enterprise
-- ============================================================
-- 4.1 叶子表（无 ent_* 子表）
DROP TABLE IF EXISTS `ent_extraction_result`;
DROP TABLE IF EXISTS `ent_table_extraction_result`;
DROP TABLE IF EXISTS `ent_file_compliance_check`;
DROP TABLE IF EXISTS `ent_file_pre_check_result`;
DROP TABLE IF EXISTS `ent_file_version`;

-- 4.2 中间层：file → document
DROP TABLE IF EXISTS `ent_enterprise_file`;
DROP TABLE IF EXISTS `ent_enterprise_document`;

-- 4.3 phase → enterprise
DROP TABLE IF EXISTS `ent_enterprise_phase`;
DROP TABLE IF EXISTS `ent_enterprise`;

-- ============================================================
-- 5. 验证
-- ============================================================

-- 5.1 残留的 ent_* 表（期望 0）
SELECT COUNT(*) AS RemainingEntTables
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME LIKE 'ent\_%';

-- 5.2 悬空外键（期望 0 行）—— 引用了不存在的表
SELECT rc.TABLE_NAME, rc.CONSTRAINT_NAME, rc.REFERENCED_TABLE_NAME
FROM information_schema.REFERENTIAL_CONSTRAINTS rc
LEFT JOIN information_schema.TABLES t
  ON t.TABLE_SCHEMA = rc.CONSTRAINT_SCHEMA AND t.TABLE_NAME = rc.REFERENCED_TABLE_NAME
WHERE rc.CONSTRAINT_SCHEMA = 'yzh_cert_platform' AND t.TABLE_NAME IS NULL;

-- 5.3 cert_enterprise 最终结构
SHOW CREATE TABLE `cert_enterprise`;

-- 5.4 挂靠链已通（期望 WsOrgCode='CB001'，CertBodyName='河北雄安尚龙认证有限公司'）
SELECT o.Code AS WsCode, o.OrgName AS WsName, o.OrgCode AS WsOrgCode,
       cb.CbCode, cb.Name AS CertBodyName
FROM `sys_organization` o
LEFT JOIN `cert_certification_body` cb ON cb.CbCode = o.OrgCode AND cb.IsDeleted = 0
WHERE o.Code = '66bbf57219d74e21bf164b7e42380c19';

-- 5.5 标准/阶段关联未被破坏（期望各 1 行）
SELECT 'cert_org_standard' AS t, COUNT(*) AS n FROM `cert_org_standard`
WHERE `OrgCode` = '906e8b2a962c4062b21144af4cc4abc0'
UNION ALL
SELECT 'cert_org_stage',          COUNT(*)     FROM `cert_org_stage`
WHERE `OrgCode` = '906e8b2a962c4062b21144af4cc4abc0';

-- 5.6 表前缀分布（ent 应消失）
SELECT SUBSTRING_INDEX(TABLE_NAME, '_', 1) AS prefix, COUNT(*) AS cnt
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
GROUP BY prefix ORDER BY cnt DESC;
