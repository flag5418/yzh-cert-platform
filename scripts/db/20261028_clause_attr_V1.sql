-- ============================================================================
-- 条款属性池 + 条款属性配置（V2 后台两层）
-- 设计依据：docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md §九
-- 日期：2026-10-28
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 yzh_cert_platform < 本文件
--
-- ⚠️ 幂等：全部 IF NOT EXISTS / INSERT ... ON DUPLICATE KEY UPDATE
-- ⚠️ 本脚本**只建 2 张表 + 5 个字典 + 8 条属性种子**，不碰任何既有表
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- 一、L0 属性池（30–50 行，不分层、不设必填集、不设 ValueSource）
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_clause_attr_def` (
  `Id`        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`      VARCHAR(50)  NOT NULL COMMENT '★属性码（业务键），如 Artifacts',
  `AttrName`  VARCHAR(100) NOT NULL COMMENT '属性名（中文），如 企业应提供的资料',
  `ValueType` VARCHAR(20)  NOT NULL COMMENT '★值类型：text | dict | bool | json | prompt',
  `DictCode`  VARCHAR(50)  NULL     COMMENT 'dict / json(受控多选) 必填：字典 DicNo',
  `Remark`    VARCHAR(500) NULL     COMMENT '作用（给配置者看的一句话）',
  `IsValid`   INT          NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=停用）',
  `Sort`      INT          NOT NULL DEFAULT 0 COMMENT '排序号',
  `CreateBy`  VARCHAR(50)  NULL,
  `CreateTime` DATETIME    DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`  VARCHAR(50)  NULL,
  `UpdateTime` DATETIME    NULL,
  `DeleteBy`  VARCHAR(50)  NULL,
  `DeleteTime` DATETIME    NULL,
  `Status`    VARCHAR(50)  NULL COMMENT '业务状态（保留）',
  `IsDeleted` TINYINT(1)   NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_sort` (`IsValid`,`Sort`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='L0 条款属性池（属性全集，跨标准复用；⛔ 不含 ValueSource / 分层 / 必填集）';

-- ────────────────────────────────────────────────────────────────────────────
-- 二、L1 条款属性配置（每条款 5–12 行）
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_clause_attr` (
  `Id`           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`         VARCHAR(36)  NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `StandardCode` VARCHAR(36)  NOT NULL COMMENT '★标准短码：iso9001 / iso14001 / iso45001 / iso13485',
  `ClauseCode`   VARCHAR(36)  NOT NULL COMMENT '★条款 Code（cert_iso_clause.Code）',
  `AttrCode`     VARCHAR(50)  NOT NULL COMMENT '★属性码（cert_clause_attr_def.Code）',
  `ValueSource`  VARCHAR(20)  NOT NULL COMMENT '★fixed=条款已定(存值本身) | reference=条款只给期望(存期望)；⛔ 只有这两档',
  `AttrValue`    TEXT         NULL     COMMENT '值；json/prompt 存原文（prompt 即提示词文本，可含 {{skill:xxx}} 占位）',
  `FilterCode`   VARCHAR(100) GENERATED ALWAYS AS (
                    CASE WHEN `AttrCode` IN ('Obligation','Severity','Applicability')
                         THEN LEFT(`AttrValue`, 100) END
                 ) STORED COMMENT '生成列：仅给 3 个枚举型属性做过滤索引，其余为 NULL',
  `IsValid`      INT          NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=停用）',
  `Sort`         INT          NOT NULL DEFAULT 0 COMMENT '同条款内展示顺序',
  `CreateBy`     VARCHAR(50)  NULL,
  `CreateTime`   DATETIME     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`     VARCHAR(50)  NULL,
  `UpdateTime`   DATETIME     NULL,
  `DeleteBy`     VARCHAR(50)  NULL,
  `DeleteTime`   DATETIME     NULL,
  `Status`       VARCHAR(50)  NULL COMMENT '业务状态（保留）',
  `Remark`       VARCHAR(500) NULL,
  `IsDeleted`    TINYINT(1)   NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_attr` (`StandardCode`,`ClauseCode`,`AttrCode`),
  KEY `idx_clause` (`StandardCode`,`ClauseCode`,`IsValid`,`Sort`),
  KEY `idx_source` (`StandardCode`,`ValueSource`),
  KEY `idx_attr` (`AttrCode`,`IsValid`),
  KEY `idx_filter` (`AttrCode`,`FilterCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='L1 条款属性配置（某条款用哪些属性 + 值 + 固定还是参考；⛔ 不含企业实际值）';

-- ────────────────────────────────────────────────────────────────────────────
-- 三、字典（5 个）
--     ⚠️ Sys_DictionaryList.DicCode = Sys_Dictionary.Code（大写），不是 DicNo
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `Sys_Dictionary` (`Code`,`DicNo`,`DicName`,`Remark`,`IsValid`,`IsDeleted`,`OrderNo`) VALUES
 ('CLAUSE_OBLIGATION',   'clause_obligation',   '条款约束强度', '应/必须=shall、宜=should、可=may、注=note；来自 24 号 §十二 语法实测', 1,0, 910),
 ('CLAUSE_APPLICABILITY','clause_applicability','条款适用性',   'general=通用（无条件）、conditional=有条件（须求值 ApplicabilityCondition）', 1,0, 920),
 ('CLAUSE_SEVERITY',     'clause_severity',     '条款严重度',   'critical/major/minor；材料级严重度由判定规则按 ArtifactType 给，不在属性层', 1,0, 930),
 ('ARTIFACT_TYPE',       'artifact_type',       '企业应备材料类型','★Artifacts 提示词输出 ArtifactHit.Kind 必须落这里的 DicValue（⛔ 不回自由文本）', 1,0, 940),
 ('PROCESS_DOMAIN',      'process_domain',      '过程域受控词表', '条款管哪些过程（多选）；企业部门声明负责哪些过程 → 取交集得部门×条款矩阵', 1,0, 950),
 ('CLAUSE_ATTR_VALUE_TYPE','clause_attr_value_type','条款属性值类型','★只有 5 种：text/dict/bool/json/prompt；⛔ 无 int/date/*_multi', 1,0, 960),
 ('CLAUSE_VALUE_SOURCE', 'clause_value_source', '条款值来源',   'fixed=条款已定(存值) | reference=条款只给期望(存期望)；⛔ 只有这两档，无 runtime', 1,0, 970)
ON DUPLICATE KEY UPDATE `DicName`=VALUES(`DicName`), `Remark`=VALUES(`Remark`), `OrderNo`=VALUES(`OrderNo`);

INSERT INTO `Sys_DictionaryList` (`Code`,`DicCode`,`DicValue`,`DicName`,`Remark`,`IsValid`,`IsDeleted`,`OrderNo`) VALUES
 -- 约束强度
 ('CD_CO_shall','CLAUSE_OBLIGATION','shall','应/必须','强约束，可开 NC',1,0,10),
 ('CD_CO_should','CLAUSE_OBLIGATION','should','宜','弱约束，一般只出观察项',1,0,20),
 ('CD_CO_may','CLAUSE_OBLIGATION','may','可','允许项，不构成要求',1,0,30),
 ('CD_CO_note','CLAUSE_OBLIGATION','note','注','说明，不产生要求',1,0,40),
 -- 适用性
 ('CD_CA_general','CLAUSE_APPLICABILITY','general','通用','无条件适用',1,0,10),
 ('CD_CA_conditional','CLAUSE_APPLICABILITY','conditional','有条件','须按 ApplicabilityCondition 求值',1,0,20),
 -- 严重度
 ('CD_CS_critical','CLAUSE_SEVERITY','critical','严重','系统性失效/体系性缺失',1,0,10),
 ('CD_CS_major','CLAUSE_SEVERITY','major','一般不符合','单点缺失',1,0,20),
 ('CD_CS_minor','CLAUSE_SEVERITY','minor','轻微','观察项/建议项',1,0,30),
 -- 企业应备材料类型（ArtifactHit.Kind 落这里）
 ('CD_AT_ledger','ARTIFACT_TYPE','ledger','台账','设备台账/供方名录/培训台账等',1,0,10),
 ('CD_AT_certificate','ARTIFACT_TYPE','certificate','证书','校准证书/检定证书/资质证书',1,0,20),
 ('CD_AT_procedure','ARTIFACT_TYPE','procedure','程序文件','程序/办法/规程（体系文件）',1,0,30),
 ('CD_AT_record','ARTIFACT_TYPE','record','记录','检验记录/内审记录/培训记录',1,0,40),
 ('CD_AT_list','ARTIFACT_TYPE','list','清单','文件清单/记录清单/法律法规清单',1,0,50),
 ('CD_AT_report','ARTIFACT_TYPE','report','报告','内审报告/管理评审报告/检验报告',1,0,60),
 ('CD_AT_plan','ARTIFACT_TYPE','plan','计划','校准计划/培训计划/审核计划',1,0,70),
 ('CD_AT_license','ARTIFACT_TYPE','license','资质证照','营业执照/生产许可/3C',1,0,80),
 ('CD_AT_mark','ARTIFACT_TYPE','mark','标识','校准标识/状态标识/区域标识',1,0,90),
 ('CD_AT_other','ARTIFACT_TYPE','other','其他','无法归入上述类型',1,0,99),
 -- 过程域
 ('CD_PD_production','PROCESS_DOMAIN','production','生产','生产/制造/施工',1,0,10),
 ('CD_PD_sales','PROCESS_DOMAIN','sales','销售','销售/合同/顾客沟通',1,0,20),
 ('CD_PD_design','PROCESS_DOMAIN','design','设计','设计开发/工艺设计',1,0,30),
 ('CD_PD_purchase','PROCESS_DOMAIN','purchase','采购','采购/外协/供方管理',1,0,40),
 ('CD_PD_inspection','PROCESS_DOMAIN','inspection','检验','进货/过程/出厂检验、试验',1,0,50),
 ('CD_PD_metrology','PROCESS_DOMAIN','metrology','计量','测量设备/校准/检定',1,0,60),
 ('CD_PD_warehouse','PROCESS_DOMAIN','warehouse','仓储','仓储/搬运/防护/标识',1,0,70),
 ('CD_PD_after_sales','PROCESS_DOMAIN','after_sales','售后','售后服务/投诉处理/召回',1,0,80),
 ('CD_PD_hr','PROCESS_DOMAIN','hr','人力','人力资源/培训/能力/职业健康',1,0,90),
 ('CD_PD_management','PROCESS_DOMAIN','management','管理','管理层/策划/评审/内审/改进',1,0,100),
 -- 条款属性值类型（只 5 种）
 ('CD_VT_text','CLAUSE_ATTR_VALUE_TYPE','text','文本','单值文本',1,0,10),
 ('CD_VT_dict','CLAUSE_ATTR_VALUE_TYPE','dict','字典','单值，取 DictCode 指定字典',1,0,20),
 ('CD_VT_bool','CLAUSE_ATTR_VALUE_TYPE','bool','布尔','是/否',1,0,30),
 ('CD_VT_json','CLAUSE_ATTR_VALUE_TYPE','json','JSON','结构化多值（如受控词表多选）',1,0,40),
 ('CD_VT_prompt','CLAUSE_ATTR_VALUE_TYPE','prompt','提示词','值即提示词，内部须输出结构化 JSON',1,0,50),
 -- 条款值来源（只 2 档）
 ('CD_VS_fixed','CLAUSE_VALUE_SOURCE','fixed','固定','条款已定，存值本身',1,0,10),
 ('CD_VS_reference','CLAUSE_VALUE_SOURCE','reference','参考','条款只给期望，存期望',1,0,20)
ON DUPLICATE KEY UPDATE `DicName`=VALUES(`DicName`), `DicValue`=VALUES(`DicValue`), `OrderNo`=VALUES(`OrderNo`);

-- ────────────────────────────────────────────────────────────────────────────
-- 四、属性池种子（8 条 —— 24 号 §3.4）
--     ⛔ 不加 PoolLayer / AppliesTo / 必填集 / ValueSource
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_clause_attr_def` (`Code`,`AttrName`,`ValueType`,`DictCode`,`Remark`,`IsValid`,`Sort`) VALUES
 ('ClauseIntent','条款作用','text',NULL,
  '一句话说明这条款到底在管什么；给 LLM 做语义分析用（不参与判定）',1,10),
 ('Obligation','约束强度','dict','clause_obligation',
  '应/必须=shall、宜=should、可=may、注=note；判定闸门：≠shall 只出观察项',1,20),
 ('Applicability','适用性','dict','clause_applicability',
  '通用 / 有条件；na（不适用）是企业侧判定结果，不在本表',1,30),
 ('ApplicabilityCondition','适用条件','text',NULL,
  '有条件时的触发条件原文（如「当要求测量溯源时」）；程序按企业事实求值',1,40),
 ('Process','过程','json','process_domain',
  '这条款管哪些过程（受控词表多选）→ 与企业部门职责取交集，得部门×条款矩阵',1,50),
 ('Artifacts','企业应提供的资料','prompt',NULL,
  '① 企业侧要求：值即提示词，从企业资料里认出规范性文档/内审资料/记录；输出 ArtifactHit[]，Kind 落 artifact_type 字典码；⛔ 只产"读到了什么"，不产结论',1,60),
 ('EvidenceChain','证据链要求','prompt',NULL,
  '② 审核员侧要求：值即提示词，生成取证剧本（看什么/问谁/抽几份）；输出 EvidenceStep[]；⛔ 取证剧本不提前给企业',1,70),
 ('Severity','严重度','dict','clause_severity',
  '条款级严重度（critical/major/minor）；材料级严重度由判定规则按 ArtifactType 给',1,80)
ON DUPLICATE KEY UPDATE
  `AttrName`=VALUES(`AttrName`), `ValueType`=VALUES(`ValueType`),
  `DictCode`=VALUES(`DictCode`), `Remark`=VALUES(`Remark`), `Sort`=VALUES(`Sort`);

-- ────────────────────────────────────────────────────────────────────────────
-- 五、自检
-- ────────────────────────────────────────────────────────────────────────────
SELECT '--- 属性池 ---' AS `check`;
SELECT `Code`,`AttrName`,`ValueType`,IFNULL(`DictCode`,'-') AS DictCode,`Sort`
FROM `cert_clause_attr_def` WHERE `IsDeleted`=0 ORDER BY `Sort`;

SELECT '--- 字典项数 ---' AS `check`;
SELECT `DicCode`, COUNT(*) AS cnt FROM `Sys_DictionaryList`
WHERE `IsDeleted`=0 AND `DicCode` IN ('CLAUSE_OBLIGATION','CLAUSE_APPLICABILITY','CLAUSE_SEVERITY','ARTIFACT_TYPE','PROCESS_DOMAIN','CLAUSE_ATTR_VALUE_TYPE','CLAUSE_VALUE_SOURCE')
GROUP BY `DicCode`;

SELECT '--- 条款配置 ---' AS `check`;
SELECT COUNT(*) AS rows_now FROM `cert_clause_attr` WHERE `IsDeleted`=0;
