-- =============================================================================
-- NC 结论判定体系 · 配置表（★ 18 号文档 §五 + §九）
-- -----------------------------------------------------------------------------
-- 目的：把「认证结论的判定规则」全部落配置表，标准确认后只改配置不改代码。
--
-- 【三张表】
--   ① cert_conclusion_rule       结论判定规则（阈值/结论词/模式开关）
--   ② cert_nc_judge_prompt      nc判断 的判定框架 Prompt（★全局复用一份）
--   ③ cert_nc_conclude_prompt   nc结论 的理由撰写 Prompt（★全局复用一份）
--
-- 【铁律】
--   七（列名三处一致 PascalCase）/ 八（显式 COLLATE utf8mb4_general_ci）
--   九（Enable 零容忍，唯一启用字段 IsValid）
--
-- 【★ 重要】
--   ① 表中所有阈值默认值是【占位值】，不是行业标准！
--      必须由认证机构 / 资深审核员确认后修改（18 号 §十一 Q1-Q7）。
--      未确认前仅可用于跑通链路，★不可用于真实认证结论。
--   ② 幂等：DROP + CREATE，可重复执行
-- =============================================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- =============================================================================
-- ① cert_conclusion_rule —— 认证结论判定规则
-- =============================================================================
DROP TABLE IF EXISTS `cert_conclusion_rule`;

CREATE TABLE `cert_conclusion_rule` (
  `Id`           bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`         varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`      varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证机构编码（租户隔离键）',

  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL
                 COMMENT '★标准编码（★全部标准通用规则可留空串 = 通用规则）',
  `PhaseCode`    varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL
                 COMMENT '阶段编码（★NULL=全阶段共用同一规则）',

  -- ─── 结论档位（★枚举词可改，改这里不动代码）───
  `LevelFull`    varchar(30) COLLATE utf8mb4_general_ci NOT NULL DEFAULT '完全符合'
                 COMMENT '★最高档结论词（默认占位，★待确认）',
  `LevelBasic`   varchar(30) COLLATE utf8mb4_general_ci NOT NULL DEFAULT '基本符合'
                 COMMENT '★中间档结论词（默认占位，★待确认）',
  `LevelReject`  varchar(30) COLLATE utf8mb4_general_ci NOT NULL DEFAULT '不符合'
                 COMMENT '★最低档结论词（默认占位，★待确认）',
  `ConclusionOrder` json      DEFAULT NULL
                 COMMENT '★结论档位顺序（数组，如 ["完全符合","基本符合","不符合"]，供枚举校验与降级判断）',

  -- ─── 阈值（★全部为占位值，必须确认后修改）───
  `MinorToBasicThreshold` int NOT NULL DEFAULT 3
                 COMMENT '★一般不符合 ≥ N → 基本符合（★占位 3，★待认证机构确认）',
  `MinorRejectThreshold`  int NOT NULL DEFAULT 5
                 COMMENT '★一般不符合 ≥ N → 不符合（★占位 5，★待认证机构确认）',

  -- ─── 跨过程失效升级（★单节点多维度范式独有能力）───
  `CrossProcessEnabled`   tinyint(1) NOT NULL DEFAULT 1
                 COMMENT '★是否启用跨过程失效升级（同过程多维度失效 → 严重不符合）',
  `CrossProcessThreshold` int NOT NULL DEFAULT 2
                 COMMENT '★同过程 ≥ N 个维度不符合 → 升级为严重不符合（★占位 2）',

  -- ─── 未检查项处理 ───
  `UnverifiedPolicy` varchar(30) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'cap_basic'
                 COMMENT '★存在 unverifiable 维度时：cap_basic=封顶到中间档 | reject=直接判不符合 | ignore=不处理',

  -- ─── ★ 结论生成模式（18 号 §九）───
  `ConclusionMode` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'AI_ASSISTED'
                 COMMENT '★结论生成模式：RULE_ONLY=仅规则不调AI | AI_ASSISTED=规则算结论+AI写理由 | EXPERT_MANUAL=专家全人工',
  `ManualRequired` tinyint(1) NOT NULL DEFAULT 0
                 COMMENT '★专家是否必须签署（★RULE_ONLY 且论证充分时可=0，此时报告须标注「未经人工签署」）',
  `OverrideAlertThreshold` int NOT NULL DEFAULT 30
                 COMMENT '★结论改动率告警阈值（%），专家改判率超此值提示规则需重新论证',

  -- ─── Prompt 绑定 ───
  `JudgePromptCode`    varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL
                 COMMENT 'nc判断 的判定框架 Prompt 编码（★全局复用，指向 cert_nc_judge_prompt）',
  `ConcludePromptCode` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL
                 COMMENT 'nc结论 的理由 Prompt 编码（★全局复用）',

  -- ─── 审计 ───
  `SourceRef`   varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL
                COMMENT '★规则来源（标准号+条款号+版本），★换版时改配置并更新此列，历史结论可回溯',
  `Status`      varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务状态',
  `Sort`        int          DEFAULT 0 COMMENT '排序号',
  `Remark`      varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`   tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`     int          NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）',
  `CreateBy`    varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`  datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`    varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`  datetime     DEFAULT NULL,
  `DeleteBy`    varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`  datetime     DEFAULT NULL,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`  (`Code`),
  UNIQUE KEY `uk_scope` (`OrgCode`, `StandardCode`, `PhaseCode`),
  KEY `idx_valid`       (`OrgCode`, `IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='★认证结论判定规则（行业标准，全部阈值可配置，★默认值待确认）';

-- =============================================================================
-- ② cert_nc_judge_prompt —— nc判断 的判定框架 Prompt（★全局复用一份）
-- -----------------------------------------------------------------------------
-- 设计要点（18 号 §9.3）：
--   ★ 专家逐条写的是【判定标准】(cert_validation_rule.dimensions.JudgeCriteria)
--   ★ 这里只写【判定框架】：四态语义 / 证据要求 / 维度关联提示 / 输出格式
--   ★ 规则数增加时本表【不变】—— 这就是省掉专家编写 Prompt 成本的关键
-- =============================================================================
DROP TABLE IF EXISTS `cert_nc_judge_prompt`;

CREATE TABLE `cert_nc_judge_prompt` (
  `Id`          bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`        varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务键',
  `OrgCode`     varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证机构编码',
  `PromptCode`  varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Prompt 编码（规则表 JudgePromptCode 引用此值）',
  `PromptName`  varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Prompt 名称',
  `SystemPrompt` text       COLLATE utf8mb4_general_ci COMMENT '系统提示词（角色设定）',
  `UserTemplate` longtext   COLLATE utf8mb4_general_ci NOT NULL COMMENT '★用户提示词模板（含 {{占位符}}）',
  `Model`        varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '模型（空=用节点配置/六键兜底）',
  `Temperature`  decimal(3,2) DEFAULT 0.10 COMMENT '★低温保证可复现（0=最确定）',
  `MaxTokens`    int         DEFAULT 2000 COMMENT '最大输出 token',
  `Version`      int         NOT NULL DEFAULT 1 COMMENT '★版本（Prompt 变更留痕，历史结论可回溯）',
  `IsDefault`    tinyint(1)  NOT NULL DEFAULT 0 COMMENT '是否默认（★同一 PromptCode 只能一条为 1）',

  `Status`     varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`       int          DEFAULT 0,
  `Remark`     varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`  tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`    int          NOT NULL DEFAULT 1,
  `CreateBy`   varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`   varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime     DEFAULT NULL,
  `DeleteBy`   varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime     DEFAULT NULL,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`       (`Code`),
  UNIQUE KEY `uk_promptcode` (`OrgCode`, `PromptCode`, `Version`),
  KEY `idx_default`          (`OrgCode`, `PromptCode`, `IsDefault`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='★nc判断 判定框架 Prompt（★全局复用，规则数增加时不变）';

-- =============================================================================
-- ③ cert_nc_conclude_prompt —— nc结论 的理由撰写 Prompt（★全局复用一份）
-- -----------------------------------------------------------------------------
-- 设计要点：★ 结论已由规则引擎算好并注入，AI 只负责写"为什么"，不得改结论
-- =============================================================================
DROP TABLE IF EXISTS `cert_nc_conclude_prompt`;

CREATE TABLE `cert_nc_conclude_prompt` (
  `Id`           bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`         varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务键',
  `OrgCode`      varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证机构编码',
  `PromptCode`   varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Prompt 编码',
  `PromptName`   varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Prompt 名称',
  `SystemPrompt` text        COLLATE utf8mb4_general_ci COMMENT '系统提示词',
  `UserTemplate` longtext    COLLATE utf8mb4_general_ci NOT NULL COMMENT '★用户提示词模板',
  `Model`        varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Temperature`  decimal(3,2) DEFAULT 0.20 COMMENT '★低温（理由表述不需创造性）',
  `MaxTokens`    int         DEFAULT 500 COMMENT '★理由限 1 句，token 收紧防啰嗦',
  `Version`      int         NOT NULL DEFAULT 1,
  `IsDefault`    tinyint(1)  NOT NULL DEFAULT 0,

  `Status`     varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`       int          DEFAULT 0,
  `Remark`     varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`  tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`    int          NOT NULL DEFAULT 1,
  `CreateBy`   varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`   varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime     DEFAULT NULL,
  `DeleteBy`   varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime     DEFAULT NULL,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`       (`Code`),
  UNIQUE KEY `uk_promptcode` (`OrgCode`, `PromptCode`, `Version`),
  KEY `idx_default`          (`OrgCode`, `PromptCode`, `IsDefault`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='★nc结论 理由撰写 Prompt（★结论由规则算好，AI 只写理由）';

-- =============================================================================
-- 种子数据（★ 用 CONCAT 显式拼接，避免 || 与数值运算混用被 MySQL 截断）
-- =============================================================================
SET @NL = CHAR(10);

-- ① nc判断 判定框架 Prompt（★全局唯一一份，规则数增加时不变）
INSERT INTO `cert_nc_judge_prompt` (
  Code, OrgCode, PromptCode, PromptName, SystemPrompt, UserTemplate,
  Temperature, MaxTokens, Version, IsDefault, Status, IsValid, CreateBy
) VALUES (
  'JC-PROMPT-0001',
  '906e8b2a962c4062b21144af4cc4abc0',
  'nc_judge',
  'NC判断 · 判定框架（全局复用）',
  CONCAT(
    '你是一名经验丰富的体系认证审核员。你的职责是依据审核标准与企业提供的客观材料，对给定的检查维度逐项作出客观判定。',
    @NL, '你只依据材料判断，材料不足时必须如实报告，严禁推测、严禁编造、严禁输出判定依据中没有的内容。'),
  CONCAT(
    '你是体系认证审核员。请根据以下材料，对照审核维度逐项判断。', @NL, @NL,
    '【审核标准】{{__RULE__.clause}}', @NL,
    '【条款原文】{{__RULE__.clauseContent}}', @NL, @NL,
    '【检查维度清单】', @NL, '{{__RULE__.dimensions}}', @NL, @NL,
    '【企业提供的客观材料】', @NL, '{{__DATA__}}', @NL, @NL,
    '【判定要求】', @NL,
    '1. ★ 只依据上述材料判断。材料不足以支撑某维度时，一律标 unverifiable，严禁推测。', @NL,
    '2. 每个维度必须给出 evidence（文件名+具体位置，如「审批台账.xlsx 第5行」）。unverifiable 时 evidence 留空字符串。', @NL,
    '3. ★ 四态严格区分，不得混用：', @NL,
    '   - conform      符合：材料证明已满足该维度要求', @NL,
    '   - nonconform   不符合：材料证明未满足。★必须写清「哪里不符合、差多少」', @NL,
    '   - na           不适用：该维度对企业无适用性（如企业无出口业务则出口文件控制不适用）', @NL,
    '   - unverifiable 无法判断：材料缺失或不足，无法得出结论', @NL,
    '4. ★ 维度关联提示（全局能力，不必逐条规则重复写）：', @NL,
    '   若两个及以上维度同时 nonconform 且属于同一过程（ProcessCode 相同），', @NL,
    '   在 desc 中注明「该过程整体失效」，供上层判定规则升级严重度。', @NL,
    '5. desc 只写客观事实，不写「建议」「应该」等主观表述。', @NL, @NL,
    '【输出格式】严格输出以下 JSON，不要任何解释文字：', @NL,
    '{"dimensions":[{"no":1,"name":"维度名","conformity":"conform|nonconform|na|unverifiable",',
    '"desc":"客观描述","evidence":"证据引用","confidence":0.0}]}'
  ),
  0.10, 2000, 1, 1, 'active', 1, 'system_init'
);

-- ② nc结论 理由 Prompt（★全局唯一一份）
INSERT INTO `cert_nc_conclude_prompt` (
  Code, OrgCode, PromptCode, PromptName, SystemPrompt, UserTemplate,
  Temperature, MaxTokens, Version, IsDefault, Status, IsValid, CreateBy
) VALUES (
  'CC-PROMPT-0001',
  '906e8b2a962c4062b21144af4cc4abc0',
  'nc_conclude',
  'NC结论 · 理由撰写（全局复用）',
  CONCAT(
    '你是一名认证审核报告撰写人。审核结论已由系统依据认证判定规则确定，',
    @NL, '你的唯一职责是用简洁准确的中文说明结论理由。你不得改变结论等级，不得添加依据清单中没有的内容。'),
  CONCAT(
    '请根据已判定的审核结果，写一句话说明结论理由。', @NL, @NL,
    '【已判定结论】（★由系统确定，不得更改）', @NL,
    '结论等级：{{__RULE__.conclusion}}', @NL,
    '依据维度：{{__RULE__.basis}}', @NL,
    '未检查项：{{__RULE__.unverified}}', @NL, @NL,
    '【各维度明细】', @NL, '{{__NODE__.dimensions}}', @NL, @NL,
    '【要求】', @NL,
    '1. ★ 不得改变结论等级', @NL,
    '2. ★ 不得添加「依据维度」中未列出的维度', @NL,
    '3. 一句话，不超过 60 字，须包含「哪里不符合 + 什么程度」', @NL,
    '4. 若存在未检查项，须在理由中体现', @NL, @NL,
    '【输出格式】严格输出：{"reason":"一句话理由"}'
  ),
  0.20, 500, 1, 1, 'active', 1, 'system_init'
);

-- ③ 结论规则（★阈值为占位值，必须确认后修改）
INSERT INTO `cert_conclusion_rule` (
  Code, OrgCode, StandardCode, PhaseCode,
  LevelFull, LevelBasic, LevelReject, ConclusionOrder,
  MinorToBasicThreshold, MinorRejectThreshold,
  CrossProcessEnabled, CrossProcessThreshold, UnverifiedPolicy,
  ConclusionMode, ManualRequired, OverrideAlertThreshold,
  JudgePromptCode, ConcludePromptCode, SourceRef,
  Status, IsValid, IsDeleted, CreateBy
) VALUES (
  'CR-GENERIC-0001',
  '906e8b2a962c4062b21144af4cc4abc0',
  '',                              -- ★ 空串 = 全部标准通用
  NULL,                           -- NULL = 全阶段
  '完全符合', '基本符合', '不符合',
  JSON_ARRAY('完全符合', '基本符合', '不符合'),
  3,                              -- ★ 占位值
  5,                              -- ★ 占位值
  1, 2,                           -- 跨过程升级：同过程 ≥2 维度失效 → 严重
  'cap_basic',                    -- 有未检查项 → 封顶到「基本符合」
  'AI_ASSISTED',                  -- ★ 默认：规则算结论 + AI 写理由
  0,                              -- 专家不强制签署
  30,                             -- 改动率 > 30% 告警
  'nc_judge', 'nc_conclude',
  '★占位规则：阈值未经认证机构确认，不可用于真实认证结论',
  'draft', 1, 0, 'system_init'
);

-- =============================================================================
-- 验证
-- =============================================================================
SELECT '① 三张表已建'         AS 检查项, COUNT(*) AS 实际值, 3 AS 期望值 FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_conclusion_rule','cert_nc_judge_prompt','cert_nc_conclude_prompt')
UNION ALL
SELECT '② 铁律九：IsValid 列数', COUNT(*), 3 FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_conclusion_rule','cert_nc_judge_prompt','cert_nc_conclude_prompt')
  AND COLUMN_NAME = 'IsValid'
UNION ALL
SELECT '③ 铁律九：无 Enable',    COUNT(*), 0 FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_conclusion_rule','cert_nc_judge_prompt','cert_nc_conclude_prompt')
  AND COLUMN_NAME IN ('Enable','enable')
UNION ALL
SELECT '④ 列名全 PascalCase',    COUNT(*), 0 FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_conclusion_rule','cert_nc_judge_prompt','cert_nc_conclude_prompt')
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]'
UNION ALL
SELECT '⑤ 排序规则数(应=1)',     COUNT(DISTINCT TABLE_COLLATION), 1 FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_conclusion_rule','cert_nc_judge_prompt','cert_nc_conclude_prompt')
UNION ALL
SELECT '⑥ judge prompt 种子',   COUNT(*), 1 FROM cert_nc_judge_prompt WHERE IsValid=1
UNION ALL
SELECT '⑦ conclude prompt 种子',COUNT(*), 1 FROM cert_nc_conclude_prompt WHERE IsValid=1
UNION ALL
SELECT '⑧ 规则种子',             COUNT(*), 1 FROM cert_conclusion_rule WHERE IsValid=1
UNION ALL
SELECT '⑨ 规则状态=draft(占位)',COUNT(*), 1 FROM cert_conclusion_rule WHERE Status='draft';
-- 期望：①3 ②3 ③0 ④0 ⑤1 ⑥1 ⑦1 ⑧1 ⑨1
