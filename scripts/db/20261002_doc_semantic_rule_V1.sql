-- ============================================================================
-- 20261002_doc_semantic_rule_V1.sql
-- 文档语义规则 —— 地基迁移（34 号 §九 行动顺序 0 / 2 / 3）
--
-- 设计依据（docs/20-体系认证/03-详细设计/05-企业资料规范化/）：
--   34-核心模块定位与端到端流程-V1.md      §七 B1 / §五 字典边界 / §九 0-3
--   33-文档语义规则设计-V1.md                §五 DDL（本脚本的语义列来源）
--   02-数据模型与表结构-V1.md               §3.1 契约表五要素 1/2/5（基础列来源）
--   21-空白模板数据模型与表结构清单-V1.md    §4.3 表 3 cert_tag_dict（标签字典 DDL 来源）
--   14-需求深化讨论收敛与增补设计-V1.md      D14 双侧同规则 / D15 分组
--   26-核心菜单功能设计（待审批）-V2.md     §4.3 先定字典再写提示词 / 补 6 AnalyzePolicy
--
-- 本脚本一次性完成三件事（⛔ 不拆两次迁移同一张表）：
--   ① cert_standard_directory_file 补 6 列（02 §4.1 + 14 D19「P0 即加」）
--   ② 受控字典 3 个：DOC_CATEGORY / DOC_PURPOSE / ANALYZE_POLICY
--   ③ 新建 2 表：cert_tag_dict（21 §4.3 + 6 扩展）
--                  cert_standard_doc_contract（02 §3.1 + 17 语义/分析列）
--
-- ⛔ 铁律：
--   1. DB列名 = C#属性名 = TS字段名，PascalCase 逐字一致
--   2. 唯一键不含 IsDeleted
--   3. 启用/禁用唯一字段 = IsValid（int，0/1）。⛔ 禁 Enable
--   4. 身份段（OrgCode/EnterpriseCode/StandardCode/StageCode）⛔ 禁 NULL，一律 NOT NULL DEFAULT ''
--   5. 字符集/排序规则全库统一 utf8mb4 + utf8mb4_general_ci（建表必须显式写 COLLATE）
--   6. 定位/删除/更新只用业务键 Code（准则 A）；uk_standard_file_code 是契约表的天然幂等键
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- 一、cert_standard_directory_file 补 6 列
--     【B1 阻断项】不加则 04 §5.2 加权公式无分支可选、裁决与状态机无处落
-- ────────────────────────────────────────────────────────────────────────────

-- 1.1 DocCategory：fixed / hybrid / editable —— 决定匹配公式分支与流程是否短路
--     ⛔ 不得留空（05 §3 默认 editable），故 NOT NULL DEFAULT 'editable'
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'DocCategory') = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `DocCategory` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT ''editable'' COMMENT ''★文档分类：fixed=固定文档(匹配即终点) / hybrid=混合 / editable=可编写(进⑧⑨填充)。决定 04 §5.2 加权公式分支，⛔ 不得留空'' AFTER `ComplianceRequired`',
  'SELECT ''DocCategory 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 1.2 MatchState：匹配状态机（03 §2.3）
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'MatchState') = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `MatchState` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT ''none'' COMMENT ''匹配状态：none=未跑 / recalled=已召回 / scored=已精排 / conflicted=冲突 / matched=已裁决 / unmatched=拒识'' AFTER `DocCategory`',
  'SELECT ''MatchState 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 1.3 InstanceState：标准文档实例状态（03 §2.4）
--     ⚠️ fixed 文档裁决后直接 matched，⛔ 不设 filling（34 §2.3 短路分支）
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'InstanceState') = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `InstanceState` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT ''none'' COMMENT ''实例状态：none=无 / pending=待填充 / filling=填充中 / filled=已填充 / confirmed=已确认 / archived=已归档。fixed 文档裁决后直接到 matched，不进 filling'' AFTER `MatchState`',
  'SELECT ''InstanceState 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 1.4 ContractCode：该行对应的标准文档契约（业务关联，冗余便于查询）
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'ContractCode') = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `ContractCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '''' COMMENT ''关联契约 → cert_standard_doc_contract.Code（空串=该标准文档未配契约，不参与匹配）'' AFTER `InstanceState`',
  'SELECT ''ContractCode 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 1.5 ParentFileCode：目录层级（14 D19 要求 P0 即加）
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'ParentFileCode') = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `ParentFileCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '''' COMMENT ''父文件 Code → cert_standard_directory_file.Code（根级恒空串）'' AFTER `FolderCode`',
  'SELECT ''ParentFileCode 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 1.6 SectionAnchor：章节锚点（14 D19）
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'SectionAnchor') = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `SectionAnchor` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''章节锚点（大文档切片定位用，可空）'' AFTER `ParentFileCode`',
  'SELECT ''SectionAnchor 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 1.7 索引：按分类 + 匹配状态过滤（裁决页与缺口清单的常用谓词）
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND INDEX_NAME = 'idx_doccat_match') = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD INDEX `idx_doccat_match` (`EnterpriseCode`,`DocCategory`,`MatchState`)',
  'SELECT ''idx_doccat_match 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ────────────────────────────────────────────────────────────────────────────
-- 二、受控字典（26 §4.3：⛔ 必须先定字典、再写提示词）
--     否则 LLM 返回不同分类名 ⇒ 是「不可用」而非「不准确」
-- ────────────────────────────────────────────────────────────────────────────

INSERT INTO `Sys_Dictionary` (`Code`, `DicName`, `DicNo`, `ParentCode`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`, `Remark`)
VALUES
  ('DOC_CATEGORY',   '企业资料分类受控值', 'doc_category',   '26b0f1d2ae6c11f1953796fd503fd974', 420, 1, 0, NOW(), 'seed_doc_semantic', '语义分析输出的分类枚举（粗分类，区别于 cert_tag_dict 标签与 DocCategory 三分类）'),
  ('DOC_PURPOSE',    '企业资料作用受控值', 'doc_purpose',    '26b0f1d2ae6c11f1953796fd503fd974', 430, 1, 0, NOW(), 'seed_doc_semantic', '语义分析输出的作用意图枚举（证明什么）'),
  ('ANALYZE_POLICY', '资料分析策略',       'analyze_policy', '26b0f1d2ae6c11f1953796fd503fd974', 440, 1, 0, NOW(), 'seed_doc_semantic', '26 §3.5.3 补6 AnalyzePolicy：analyze/skip/ignore（用途级，R-8 永不忽略 L0 存档）')
ON DUPLICATE KEY UPDATE
  `DicName` = VALUES(`DicName`), `ParentCode` = VALUES(`ParentCode`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

INSERT INTO `Sys_DictionaryList` (`Code`, `DicCode`, `DicValue`, `DicName`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  -- ① 分类受控值（粗分类；与 cert_tag_dict 标签、DocCategory 三分类是三套正交概念，见 34 §五）
  (CONCAT('DC_', 'quality_system'),  'DOC_CATEGORY', 'quality_system',  '体系文件',   10, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DC_', 'record'),          'DOC_CATEGORY', 'record',          '记录',       20, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DC_', 'license'),         'DOC_CATEGORY', 'license',         '资质证照',   30, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DC_', 'report'),          'DOC_CATEGORY', 'report',          '报告',       40, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DC_', 'policy'),          'DOC_CATEGORY', 'policy',          '制度/方针',  50, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DC_', 'plan'),            'DOC_CATEGORY', 'plan',            '计划/方案',  60, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DC_', 'list'),            'DOC_CATEGORY', 'list',            '清单/台账',  70, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DC_', 'other'),           'DOC_CATEGORY', 'other',           '其它',       99, 1, 0, NOW(), 'seed_doc_semantic'),
  -- ② 作用受控值（LLM 输出的 purpose 必须落到这里，越界记 warning 不落库）
  (CONCAT('DP_', 'org_structure'),   'DOC_PURPOSE', 'org_structure',   '证明组织架构与职责',   10, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'internal_audit'),  'DOC_PURPOSE', 'internal_audit',  '证明内审已实施',       20, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'mgmt_review'),     'DOC_PURPOSE', 'mgmt_review',     '证明管理评审已实施',   30, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'doc_control'),     'DOC_PURPOSE', 'doc_control',     '证明文件受控',         40, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'training'),        'DOC_PURPOSE', 'training',        '证明人员能力与培训',   50, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'supplier'),        'DOC_PURPOSE', 'supplier',        '证明供方与采购管控',   60, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'process_control'), 'DOC_PURPOSE', 'process_control', '证明过程与生产管控',   70, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'inspection'),      'DOC_PURPOSE', 'inspection',      '证明检验与试验实施',   80, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'nc_correct'),      'DOC_PURPOSE', 'nc_correct',      '证明不符合项已整改',   90, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'equipment'),       'DOC_PURPOSE', 'equipment',       '证明设备与设施维护',  100, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'subject'),         'DOC_PURPOSE', 'subject',         '主体资格证明（执照/许可）', 110, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'evidence'),        'DOC_PURPOSE', 'evidence',        '运行客观证据',        120, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('DP_', 'other'),           'DOC_PURPOSE', 'other',           '其它',                999, 1, 0, NOW(), 'seed_doc_semantic'),
  -- ③ 分析策略（26 补 6；R-8：用途级，L0 存档永不忽略）
  (CONCAT('AP_', 'analyze'), 'ANALYZE_POLICY', 'analyze', '全流程分析（语义+提取+匹配候选）', 10, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('AP_', 'skip'),    'ANALYZE_POLICY', 'skip',    '只存档作证据：固定文档(指纹接管) 或 值已在全局参数里定义 —— 不分析不提取不进匹配候选', 20, 1, 0, NOW(), 'seed_doc_semantic'),
  (CONCAT('AP_', 'ignore'),  'ANALYZE_POLICY', 'ignore',  '完全无关：不进任何清单（仅 L0 存档）', 30, 1, 0, NOW(), 'seed_doc_semantic')
ON DUPLICATE KEY UPDATE
  `DicValue` = VALUES(`DicValue`), `DicName` = VALUES(`DicName`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

-- ────────────────────────────────────────────────────────────────────────────
-- 三、cert_tag_dict —— 标签字典（受控词表 = 04 号 R2/R3 召回键词表）
--     DDL 基础列来自 21 §4.3 表 3；6 个扩展列来自 33 号 §五（Q1/Q2 建议）
--     ★ 定位（34 §6.4）：五路召回中 R2/R3 的原料供给侧
-- ────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_tag_dict` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务键GUID',
  `CreateTime` datetime NOT NULL,
  `CreateBy` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,

  `TagCode` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '★标签编码（业务键，PascalCase，如 RecordInternalAudit）',
  `TagName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '标签显示名（如 内审记录）',
  `TagGroup` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '分组：文档类型 / 业务域 / 标准条款',
  `ApplicableSide` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'both' COMMENT '作用侧：standard / enterprise / both（14 D14 双侧同规则，缺一不可比）',

  -- ── 33 号 §五 扩展列（Q1/Q2 建议，本脚本一并落地）──
  `StandardCodes` json DEFAULT NULL COMMENT '适用标准清单 ["iso9001","iso13485"]；NULL=全部标准。按标准裁剪标签集合（34 §6.2 标签是 标准×文档 的函数）',
  `MatchFeature` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★编号前缀/文件名特征（如 XASL-QR-）—— 04 号 S1 指纹的 L0 规则落点，零 LLM 命中',
  `SampleDocNames` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '样例文档名（逗号分隔），供提示词 few-shot 与人工核对',
  `TagPurposeHint` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '该标签下的文档通常起什么作用（给 doc_content 提示词的先验）',
  `GenSource` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'manual' COMMENT '来源：ai=元提示词生成 / manual=人工新建 / mixed=AI 生成后人工修正',
  `IsManualCorrected` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否被人工修正过（1=人工改过，AI 批量重跑⛔ 不得覆盖）',

  `Sort` int NOT NULL DEFAULT '0' COMMENT '排序号',
  `Status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'active' COMMENT '状态：active=启用 / archived=归档',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `DeleteBy` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）。⛔ 禁 Enable',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_tag_code` (`TagCode`),
  KEY `idx_group` (`TagGroup`,`IsValid`,`Sort`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标签字典（受控词表，04 号 R2/R3 召回键词表；14 D14 双侧同规则）';

-- ────────────────────────────────────────────────────────────────────────────
-- 四、cert_standard_doc_contract —— 标准文档契约（五要素 1/2/5 + 语义分析列）
--     基础列 = 02 §3.1（⛔ 逐字沿用，不重造）；语义/分析列 = 33 号 §五
--     ★ 定位（34 §6.3）：文档语义规则的「标准侧落点」，一文件一契约
--     ★ 为什么落契约表而不是 cert_doc_template（33 Q3）：模板行只有 167 份中
--       上传了模板的才有行，契约表 uk_standard_file_code 覆盖全部
-- ────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_standard_doc_contract` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务键GUID',
  `CreateTime` datetime NOT NULL COMMENT '创建时间',
  `CreateBy` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '更新人Code',

  -- ── 身份段（⛔ 禁 NULL）──
  `StandardFileCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '★宿主标准文件 Code → cert_standard_directory_file.Code（一文件一契约，业务定位键）',
  `ConfigCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '目录配置 Code → cert_standard_directory_config.Code（冗余，过滤用）',
  `StandardCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '★标准 Code → cert_iso_standard.Code（冗余）',
  `StageCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '阶段 Code → cert_cert_stage.Code（冗余）',

  -- ── 五要素 1 / 2 / 5 ──
  `DocName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '【要素1】标准文档名称（= 标准文件名，冗余供匹配读取，改名时同步）',
  `DocPurpose` text COMMENT '【要素2】文档作用（四段式：①是什么/核心内容 ②审核关注点 ③来源口径 ④包含信息）。人读存此列；机器读存 InfoItemsJson',
  `DocCategory` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'editable' COMMENT '【分类】fixed=固定文档(匹配即终点) / editable=可编写 / hybrid=混合。决定 04 §5.2 公式分支',
  `DocRole` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'required' COMMENT 'required=必需 / optional=可选 / reference=参考 / attachment=附件',
  `MatchPrompt` text COMMENT '【要素5a】匹配提示词：告诉 LLM 如何判断企业文档是否对应本文档（输出 JSON 契约见 04 §4.3）',
  `SynthesisPrompt` text COMMENT '【要素5b】填充提示词：不可直接结构化的段落如何组织撰写',
  `FingerprintJson` json DEFAULT NULL COMMENT '【固定文档】指纹规则集：{"fileNames":[],"regex":[],"keyFields":[{"name":"","pattern":""}],"minScore":0.8}',
  `FileNamePattern` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准文件名正则/通配（匹配侧兜底，主用 cert_standard_directory_file.FilePattern）',
  `Keywords` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '召回关键词，逗号分隔（人工可维护，提升 R3 召回率）',
  `AutoConfirmThreshold` decimal(3,2) DEFAULT NULL COMMENT '自动确认阈值（NULL=用全局默认 ent_norm_auto_threshold=0.85）',
  `ReviewThreshold` decimal(3,2) DEFAULT NULL COMMENT '人工复核下限，低于此值直接判不匹配（NULL=全局默认 ent_norm_review_threshold=0.50）',
  `Status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'draft' COMMENT 'draft=草稿 / active=生效 / archived=归档',
  `RuleCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '关联提取规则 Code → cert_doc_extraction_rule.Code（有则契约 3/4 号要素存在）',

  -- ── 33 号 §五：语义分析列（标签 + 作用的结构化伴生产出）──
  `TagsJson` json DEFAULT NULL COMMENT '标签数组 ["RecordInternalAudit"] —— 每个值 ⛔ 必须 ∈ cert_tag_dict.TagCode，越界置 OTHER 并记 Message',
  `TagsSource` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标签来源：ai / manual / carried',
  `TagsReason` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '打标签的理由（LLM 返回，人工复核用）',
  `TagsConfidence` decimal(3,2) DEFAULT NULL COMMENT '标签置信度 0.00~1.00',
  `DocPurposeSource` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '作用来源：ai / manual / carried',
  `DocPurposeConfidence` decimal(3,2) DEFAULT NULL COMMENT '作用置信度 0.00~1.00',
  `InfoItemsJson` json DEFAULT NULL COMMENT '★【要素2 第四段·机器读】包含信息结构化清单 [{"Name":"统一社会信用代码","Required":true,"Hint":"18位"}] —— 供 R3 倒排与覆盖度计算',

  -- ── 33 号 §五：分析元数据（可观测 + 可追溯 + 可重跑）──
  `AnalyzeStatus` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'pending' COMMENT '语义分析状态：pending / running / completed / failed / manual（人工直接写入）',
  `AnalyzeMessage` varchar(1024) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '分析失败原因或越界告警',
  `ModelName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '本次分析实际使用的模型名（快照，不跟随 cert_sys_config 变动）',
  `PromptCode` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '使用的提示词 PromptCode（wf_prompt_template.PromptCode）',
  `PromptVersion` int DEFAULT NULL COMMENT '提示词版本（wf_prompt_template.Version）',
  `PromptTokens` int DEFAULT NULL COMMENT '输入 Token 数',
  `CompletionTokens` int DEFAULT NULL COMMENT '输出 Token 数',
  `DurationMs` int DEFAULT NULL COMMENT '耗时（毫秒）',
  `IsManualCorrected` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否被人工修正过（1=人工改过，批量重跑⛔ 不得覆盖）',
  `AnalyzeTime` datetime DEFAULT NULL COMMENT '最近一次分析完成时间',

  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除',
  `DeleteBy` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人Code',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）。⛔ 禁 Enable',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_standard_file_code` (`StandardFileCode`),
  KEY `idx_config_code` (`ConfigCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_stage_code` (`StageCode`),
  KEY `idx_status` (`Status`,`IsValid`),
  KEY `idx_analyze` (`AnalyzeStatus`,`IsValid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准文档契约（五要素1/2/5 + 语义标签/作用；34 §6.3 文档语义规则的标准侧落点）';

-- ────────────────────────────────────────────────────────────────────────────
-- 五、验证
-- ────────────────────────────────────────────────────────────────────────────

-- 5.1 六列是否齐（应返回 6 行）
SELECT COLUMN_NAME, COLUMN_DEFAULT, COLUMN_TYPE, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_standard_directory_file'
  AND COLUMN_NAME IN ('DocCategory','MatchState','InstanceState','ContractCode','ParentFileCode','SectionAnchor')
ORDER BY ORDINAL_POSITION;

-- 5.2 两表是否建成
SELECT TABLE_NAME, TABLE_COLLATION, TABLE_COMMENT
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_tag_dict','cert_standard_doc_contract');

-- 5.3 字典是否可查（应返回 8 + 13 + 3 = 24 行）
SELECT d.`DicNo`, d.`DicName`, l.`DicValue`, l.`DicName` AS ItemName, l.`OrderNo`
FROM `Sys_Dictionary` d
JOIN `Sys_DictionaryList` l ON l.`DicCode` = d.`Code`
WHERE d.`DicNo` IN ('doc_category','doc_purpose','analyze_policy') AND d.`IsDeleted` = 0
ORDER BY d.`DicNo`, l.`OrderNo`;

-- 5.4 ⛔ 列命名铁律验证 —— 两新表不允许出现非 PascalCase 列（应返回 0 行）
--     ⚠️ 必须用 CONVERT(... USING utf8mb4) COLLATE utf8mb4_bin，
--        否则 information_schema 排序规则大小写不敏感 ⇒ 永远 0 行（假阴性）
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_tag_dict','cert_standard_doc_contract','cert_standard_directory_file')
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]'
  AND COLUMN_NAME NOT IN ('Id')
ORDER BY TABLE_NAME, COLUMN_NAME;

-- 5.5 ⛔ Enable 零容忍（铁律九，应返回 0 行）
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_tag_dict','cert_standard_doc_contract')
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('Enable','enable')
ORDER BY TABLE_NAME, COLUMN_NAME;
