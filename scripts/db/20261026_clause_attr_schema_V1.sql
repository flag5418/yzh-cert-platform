-- ============================================================================
-- 条款属性存储（标准级，无机构归属）
-- 版本：V1    日期：2026-10-26
-- 前置：63 号《标准条款属性字典与两逻辑主线》
-- 状态：★ 草案，未执行。待 D1–D5 确认后跑。
--
-- 三张表：
--   cert_iso_clause_attr       条款属性主表        1 条款 : 1 行
--   cert_iso_clause_artifact   条款 → 应有产物     1 条款 : N 行
--   cert_doc_form_schema       产物 → 必备字段     可复用、可版本化
--
-- 三条铁律：
--   1. OrgCode 恒空（平台标准级）。机构加严另建覆盖表，不写本表。
--   2. 业务列一律可空、不给默认值。NULL = 未设置（待办）；
--      空数组/空串 = 明确不要求（已完成）；Applicability=na_by_org = 不适用。
--      给了默认值就分不清"未设置"和"明确设为 X"，稀疏模型立刻失效。
--   3. 表名 cert_ 前缀、列名 PascalCase，与 C# 属性一致。
--
-- 为什么重建条款树不会丢属性：
--   cert_iso_clause.Code = MD5(slug|条款号)，是确定性的。
--   重跑解析器时同一条款号 ⇒ 同一 Code ⇒ 属性行不受影响；
--   只有被删掉的条款才级联删除属性（ON DELETE CASCADE），这正是期望行为。
-- ============================================================================

SET NAMES utf8mb4;

-- ---------------------------------------------------------------------------
-- ① 条款属性主表
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_iso_clause_attr` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) DEFAULT NULL COMMENT '机构 Code；本表恒空（平台标准级）',
  `CreateBy` varchar(50) DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) DEFAULT 'none',
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  `Sort` int DEFAULT '0',
  `Remark` varchar(500) DEFAULT NULL,

  `ClauseCode` varchar(36) NOT NULL COMMENT '条款 Code（= cert_iso_clause.Code）',
  `StandardCode` varchar(36) NOT NULL COMMENT '冗余：所属标准 Code（按标准批量统计用）',

  `Intent` varchar(200) DEFAULT NULL COMMENT 'A4 作用：这条款管什么、要达成什么（文档匹配主键）',
  `Obligation` varchar(10) DEFAULT NULL COMMENT 'B1 强制程度：shall/should/may/note',
  `Applicability` varchar(20) DEFAULT NULL COMMENT 'B2 适用性：universal/conditional/scope/na_by_org',
  `ApplicabilityConditionJson` varchar(500) DEFAULT NULL COMMENT 'B3 适用条件，如 {"has_design_activity":true}',

  `RequirementItemsJson` text COMMENT 'C1 分项要求：[{"key":"a","text":"...","mandatory":true}]',
  `EvidencePointsJson` text COMMENT 'C4 取证要点：["...","..."]',

  `EvidenceKinds` varchar(100) DEFAULT NULL COMMENT 'D1 证据形态（逗号串）：doc,record,field,site,person,system',
  `CheckModes` varchar(100) DEFAULT NULL COMMENT 'D2 检查方式（逗号串）：existence,completeness,content,timeliness,consistency,traceability',
  `CriteriaJson` text COMMENT 'D3 判据参数：[{"type":"interval","expr":"max_months=12","desc":"..."}]',
  `Depth` varchar(10) DEFAULT NULL COMMENT 'D4 取样深度：single/sample/all/census',
  `Severity` varchar(20) DEFAULT NULL COMMENT 'D5 缺失严重度：major/minor/observation/none',
  `Question` varchar(500) DEFAULT NULL COMMENT 'D6 检查单提问句',

  `RelatedClausesJson` varchar(500) DEFAULT NULL COMMENT 'A5 关联条款：["7.1.3","8.4"]',

  `Source` varchar(20) DEFAULT 'ai' COMMENT 'E1 来源：ai/expert/case/import',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'E2 AI 置信度 0.00~1.00',
  `ReviewStatus` varchar(20) DEFAULT 'draft' COMMENT 'E3 复核状态：draft/validated/reviewed/locked',
  `ReviewBy` varchar(50) DEFAULT NULL COMMENT 'E4 复核人',
  `ReviewTime` datetime DEFAULT NULL COMMENT 'E4 复核时间',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_clause_code` (`ClauseCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_obligation` (`Obligation`),
  KEY `idx_applicability` (`Applicability`),
  KEY `idx_severity` (`Severity`),
  KEY `idx_review_status` (`ReviewStatus`),
  CONSTRAINT `fk_clauseattr_clause` FOREIGN KEY (`ClauseCode`)
    REFERENCES `cert_iso_clause` (`Code`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='标准条款属性（标准级，无机构归属）';


-- ---------------------------------------------------------------------------
-- ② 条款 → 应有产物
--    为什么是子表而不是 JSON：产物要被"证据单元"按 Code 引用、要挂 FormSchema，
--    且一份产物可被多个条款共用（质量手册覆盖 20 条款），必须能独立成行。
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_iso_clause_artifact` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) DEFAULT NULL COMMENT '机构 Code；本表恒空（平台标准级）',
  `CreateBy` varchar(50) DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) DEFAULT 'none',
  `IsValid` tinyint NOT NULL DEFAULT '1',
  `Sort` int DEFAULT '0',
  `Remark` varchar(500) DEFAULT NULL,

  `ClauseCode` varchar(36) NOT NULL COMMENT '所属条款 Code',
  `ArtifactName` varchar(200) NOT NULL COMMENT '产物名称，如《监视和测量设备控制程序》',
  `ArtifactType` varchar(20) NOT NULL COMMENT 'procedure=程序 / record=记录 / report=报告 / plan=计划 / list=清单 / mark=标识 / site=现场状态',
  `IsMandatory` tinyint(1) NOT NULL DEFAULT '1' COMMENT '1=必须有；0=有则查',
  `EvidenceKind` varchar(20) DEFAULT NULL COMMENT '该产物对应的证据形态（一般 doc/record/site）',
  `FormSchemaCode` varchar(36) DEFAULT NULL COMMENT '→ cert_doc_form_schema.Code（该产物必须有哪些字段）',
  `DocPatternJson` varchar(500) DEFAULT NULL COMMENT '兜底文件名匹配：["*计量*","*校准*"]（Intent 匹配失败时才用）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_artifact_type` (`ArtifactType`),
  KEY `idx_form_schema` (`FormSchemaCode`),
  CONSTRAINT `fk_artifact_clause` FOREIGN KEY (`ClauseCode`)
    REFERENCES `cert_iso_clause` (`Code`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='条款应有产物（标准级）';


-- ---------------------------------------------------------------------------
-- ③ 产物 → 必备字段（表单 schema，可复用、可版本化）
--    C3 Fields 落在这里：字段是"产物"的属性，不是"条款"的属性。
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_form_schema` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) DEFAULT NULL COMMENT '机构 Code；本表恒空（平台标准级）',
  `CreateBy` varchar(50) DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) DEFAULT 'none',
  `IsValid` tinyint NOT NULL DEFAULT '1',
  `Sort` int DEFAULT '0',
  `Remark` varchar(500) DEFAULT NULL,

  `SchemaName` varchar(200) NOT NULL COMMENT '表单名，如「特殊岗位资格清单」',
  `FieldsJson` text NOT NULL COMMENT '[{"key":"cert_no","label":"证书编号","required":true,"type":"string","rule":""}]',
  `Version` varchar(20) NOT NULL DEFAULT 'V1' COMMENT '版本（换版时新增行，不改旧行）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_name_version` (`SchemaName`,`Version`),
  KEY `idx_schema_name` (`SchemaName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='产物表单字段 schema（标准级）';


-- ============================================================================
-- 样例数据（可选，仅用于验证结构；正式数据由生成流水线产出）
-- 用真实条款 Code，可直接跑
-- ============================================================================

-- 7.1.5 监视和测量资源 —— 「全维度」样例
INSERT INTO `cert_iso_clause_attr`
(`Code`,`ClauseCode`,`StandardCode`,`Intent`,`Obligation`,`Applicability`,
 `RequirementItemsJson`,`EvidencePointsJson`,`EvidenceKinds`,`CheckModes`,`CriteriaJson`,
 `Depth`,`Severity`,`Question`,`RelatedClausesJson`,`Source`,`Confidence`,`ReviewStatus`)
VALUES
('a1000000-0000-4000-8000-000000000715',
 'd698c4ece19d60dbd165fb6dc53d3ec2','c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62',
 '保证用于判定产品符合性的测量结果可信，即设备受控、按期校准、可溯源',
 'shall','universal',
 '[{"key":"7.1.5.1","text":"确定并提供所需的监视和测量资源，确保其适用性","mandatory":true},
   {"key":"7.1.5.2","text":"测量设备对照能溯源到国际或国家标准的测量标准进行校准或检定","mandatory":true},
   {"key":"7.1.5.2","text":"识别测量设备的校准状态","mandatory":true},
   {"key":"7.1.5.2","text":"对已校准设备进行防护，防止失效或损坏","mandatory":true}]',
 '["设备台账是否覆盖全部在用测量设备","校准记录的有效期是否覆盖本次审核期","校准机构是否具备资质","设备是否有校准状态标识"]',
 'record,site',
 'completeness,timeliness',
 '[{"type":"interval","expr":"calibration_valid_at_audit_date","desc":"校准有效期须覆盖审核日"}]',
 'sample','major',
 '请提供本年度监视和测量设备台账及校准/检定证书，并说明校准状态的标识方式',
 '["7.1.3"]',
 'expert',0.95,'reviewed')
ON DUPLICATE KEY UPDATE `Intent`=VALUES(`Intent`),`Obligation`=VALUES(`Obligation`);

-- 7.3 意识 —— 「稀疏」样例：只约束 1~2 个维度，无强制产物
INSERT INTO `cert_iso_clause_attr`
(`Code`,`ClauseCode`,`StandardCode`,`Intent`,`Obligation`,`Applicability`,
 `EvidencePointsJson`,`EvidenceKinds`,`CheckModes`,`Depth`,`Severity`,`Question`,
 `Source`,`Confidence`,`ReviewStatus`)
VALUES
('a1000000-0000-4000-8000-000000000730',
 'ad4d520c32206137ea1eb98de756f6e3','c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62',
 '确保在组织控制下工作的人员知晓质量方针、相关目标及自身对体系有效性的贡献',
 'shall','universal',
 '["抽问现场人员是否知道本岗位的质量目标","是否知道不合格对本人的影响"]',
 'person','content',
 'sample','minor',
 '请随机抽问 2~3 名现场人员：本公司质量方针是什么？你岗位的质量目标是什么？',
 'expert',0.90,'reviewed')
ON DUPLICATE KEY UPDATE `Intent`=VALUES(`Intent`),`Obligation`=VALUES(`Obligation`);
-- 注意：本行 Artifacts 为空 —— 7.3 不要求任何成文信息，这是正常的稀疏，不是漏填。


-- ============================================================================
-- 验证
-- ============================================================================
-- 表是否建好
SELECT TABLE_NAME, TABLE_COMMENT FROM information_schema.TABLES
WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME LIKE 'cert_iso_clause_attr%'
   OR TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_doc_form_schema';

-- 覆盖率（稀疏口径）：只统计"该有的维度"
SELECT
  COUNT(*)                                                                   AS 条款数,
  SUM(Intent IS NOT NULL)                                                    AS 有作用,
  SUM(Obligation IS NOT NULL)                                                AS 有强制程度,
  SUM(Applicability IS NOT NULL)                                             AS 有适用性,
  SUM(EvidenceKinds IS NOT NULL)                                             AS 有证据形态,
  SUM(CheckModes IS NOT NULL)                                                AS 有检查方式,
  SUM(ReviewStatus='locked')                                                 AS 已锁定
FROM cert_iso_clause_attr WHERE IsDeleted=0;

-- 按属性筛选（这就是"独立列"的价值）
SELECT c.ClauseNumber, c.Title, a.Intent
FROM cert_iso_clause_attr a JOIN cert_iso_clause c ON c.Code=a.ClauseCode
WHERE a.IsDeleted=0 AND a.Obligation='shall' AND FIND_IN_SET('site', a.EvidenceKinds)
ORDER BY c.Sort;
