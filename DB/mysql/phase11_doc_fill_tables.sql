-- ============================================================================
-- phase11 · 标准文档填写（Skill 体系）—— S-1 建表 + 归一列
--
-- 依据：docs/20-体系认证/03-详细设计/05-企业资料规范化/
--        37-标准文档填写规则设计-V1.md  §4.2（表 1/2/3）
--        39-文档填写Skill详细设计-V1.md §3.7（cert_doc_fill_log）/ §17.3（cert_doc_ai_suggestion）
--        38-文档填写Skill开发计划-V1.md §13.6 S-1（.doc/.xls 归一链 → EditableStoragePath）
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 \
--         yzh_cert_platform < DB/mysql/phase11_doc_fill_tables.sql
--
-- ★ 对齐铁律（记忆 §二十 / §铁律）：每表必带 IsDeleted + IsValid 双列，
--   且**唯一键不含 IsDeleted**（否则「软删后重建」撞唯一键 1062）。
-- ============================================================================

SET NAMES utf8mb4;

-- ----------------------------------------------------------------------------
-- 表 1 · cert_doc_template（模板主表）
-- 基线 = 37 号 §4.2 表1（= 21 号 §4.3 表1 按 §4.4 两处冲突裁决调整后）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_template` (
  `Id`               bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`             varchar(36)  NOT NULL                COMMENT '业务键GUID',
  `CreateTime`       datetime     NOT NULL                COMMENT '上传时间',
  `CreateBy`         varchar(64)  DEFAULT NULL,
  `UpdateTime`       datetime     DEFAULT NULL,
  `UpdateBy`         varchar(64)  DEFAULT NULL,

  -- ① 宿主：一标准文档一模板（⛔ 不引入版本号，换版覆盖 + 归档）
  `StandardFileCode` varchar(36)  NOT NULL                COMMENT '宿主标准文档 → cert_standard_directory_file.Code',
  `OrgCode`          varchar(36)  NOT NULL DEFAULT ''     COMMENT '冗余：机构',
  `StandardCode`     varchar(36)  NOT NULL DEFAULT ''     COMMENT '冗余：标准',
  `StageCode`        varchar(36)  NOT NULL DEFAULT ''     COMMENT '冗余：阶段',

  -- ② 文件
  `FileKind`         varchar(10)  NOT NULL                COMMENT 'docx / xlsx（上传侧已统一，只有这两种）',
  `FileName`         varchar(500) NOT NULL                COMMENT '上传时原始文件名',
  `StoragePath`      varchar(512) NOT NULL                COMMENT '模板路径 → PathBuilder.TemplateFile()（_template/ 段下）',
  `SourceSha256`     varchar(64)  DEFAULT NULL            COMMENT '模板指纹：相同则跳过重扫',

  -- ③ 层 2 全文填写规则（★ 引用独立表，⛔ 不内嵌提示词）
  `FillPromptCode`   varchar(100) DEFAULT NULL            COMMENT '全文填写规则 → cert_doc_fill_prompt.PromptCode（空=不走全文规则）',

  -- ④ 扫描结果
  `ScanStatus`       varchar(20)  NOT NULL DEFAULT 'pending' COMMENT 'pending/processing/completed/failed',
  `ScanMessage`      varchar(1024) DEFAULT NULL           COMMENT '失败原因',
  `ScanTime`         datetime     DEFAULT NULL            COMMENT '最近扫描时间',
  `PartCount`        int          NOT NULL DEFAULT 0      COMMENT '识别到的锚点总数',
  `BookmarkCount`    int          NOT NULL DEFAULT 0      COMMENT '识别到的书签数',
  `MarkCount`        int          NOT NULL DEFAULT 0      COMMENT '识别到的 YZH_Mark 标记数',
  `ViolationJson`    json         DEFAULT NULL            COMMENT '【W1-W9/E1-E8 校验结果】[{"code":"E6","level":"error","message":"…","anchor":"…"}]',
  `SummaryJson`      json         DEFAULT NULL            COMMENT '概览 {"sections":3,"headers":["default","first"],"bookmarks":8}',

  -- ⑤ 发布
  `PublishStatus`    varchar(20)  NOT NULL DEFAULT 'draft' COMMENT 'draft=已上传未扫描 / scanned=已扫描待处理 / ready=校验通过待发布 / published=已发布',
  `Remark`           varchar(500) DEFAULT NULL,
  `IsDeleted`        tinyint(1)   NOT NULL DEFAULT 0,
  `DeleteBy`         varchar(64)  DEFAULT NULL,
  `DeleteTime`       datetime     DEFAULT NULL,
  `IsValid`          int          NOT NULL DEFAULT 1      COMMENT '启用状态（⛔ 禁用 Enable）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`          (`Code`),
  UNIQUE KEY `uk_standard_file` (`StandardFileCode`),
  KEY `idx_sha`     (`SourceSha256`),
  KEY `idx_publish` (`PublishStatus`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='标准文档空白模板（一文档一模板，无版本号）';

-- ----------------------------------------------------------------------------
-- 表 2 · cert_doc_template_anchor（锚点清单）
-- 基线 = 37 号 §4.2 表2（= 21 号 §4.3 表2 + 22 号 §八 的 6 个增补列）
--
-- ★★ 相对 37 号 DDL 的 2 处修正（实施时发现，见 39 号 V1.2 修正块）：
--   ① `Required` / `IsOrphan` 由 `bit(1)` → **`tinyint(1)`**：
--      全库其余 60+ 张表统一用 tinyint(1) 表示 bool（如 cert_fill_param_def.IsDeleted），
--      `bit(1)` 在 SqlSugar 下映射行为不一致，会造成「同一实体两种读法」。
--   ② `SheetName` / `SectionIndex` / `HeaderKind` 由 **可空 → NOT NULL DEFAULT**：
--      `uk_tpl_anchor` 含这三列，而 **MySQL 唯一索引中 NULL 互不冲突** ⇒
--      原 DDL 下「同一锚点标两次」**检不出来**，且重扫的 `ON DUPLICATE KEY` **永远不触发**
--      ⇒ 与 21 号 §4.3 声明的「天然幂等 + 可检出重复标注」**直接矛盾**。
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_template_anchor` (
  `Id`            bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`          varchar(36)  NOT NULL                COMMENT '业务键GUID',
  `CreateTime`    datetime     NOT NULL,
  `CreateBy`      varchar(64)  DEFAULT NULL,
  `UpdateTime`    datetime     DEFAULT NULL,
  `UpdateBy`      varchar(64)  DEFAULT NULL,

  `TemplateCode`  varchar(36)  NOT NULL                COMMENT '所属模板 → cert_doc_template.Code',

  -- ① 定位（D3 承载形态）
  `AnchorType`    varchar(20)  NOT NULL                COMMENT 'scalar / block / table / table_total / domain（★ block 为 22 号补入）',
  `AnchorKind`    varchar(20)  NOT NULL DEFAULT 'token' COMMENT 'token=Token文本 / bookmark=书签 / range=Excel区域',
  `AnchorRef`     varchar(200) NOT NULL                COMMENT '{{ENT_NAME}} / ROW_培训记录 / Sheet1!A11:F11',
  `SheetName`     varchar(100) NOT NULL DEFAULT ''     COMMENT '★ Excel 工作表名（空串=不适用；⛔ 不可空，见建表说明②）',
  `SectionIndex`  int          NOT NULL DEFAULT 0      COMMENT '★ Word 分节序号（0=不适用；⛔ 不可空，见建表说明②）',
  `HeaderKind`    varchar(20)  NOT NULL DEFAULT ''     COMMENT '★ Word 页眉页脚：default/first/even（空串=不适用；⛔ 不可空）',
  `DomainKind`    varchar(20)  DEFAULT NULL            COMMENT '域子类：text=写值 / auto=交给Word算（PAGE/NUMPAGES）',

  -- ② 字段绑定
  `FieldCode`     varchar(100) DEFAULT NULL            COMMENT '★绑定的语义字段（须与 cert_doc_field_def.FieldCode 对齐）',
  `ColumnsJson`   json         DEFAULT NULL            COMMENT '表格列顺序 ["TRAIN_DATE","TRAIN_TOPIC","HOURS"]',
  `TokenModifiersJson` json    DEFAULT NULL            COMMENT '修饰符 {"fmt":"0.00","def":"—","src":"global"}',

  -- ③ ★ D1 取值来源（有序列表 + 组合方式）—— 22 号 §八
  `SourceSpec`    json         DEFAULT NULL            COMMENT '取值来源规格：{combine,separator,expr,sources[]}；sources 有序',
  `SourceSummary` varchar(500) DEFAULT NULL            COMMENT '来源摘要（列表展示用，由 SourceSpec 生成）',

  -- ④ ★ D2 写入方式 —— 22 号 §八
  `WriteMode`     varchar(20)  NOT NULL DEFAULT 'overwrite' COMMENT 'replace/overwrite/append/remove',
  `OriginalText`  text         DEFAULT NULL            COMMENT '原值快照（仅 replace/remove 需要，支撑差异与回滚）',
  `ConditionJson` json         DEFAULT NULL            COMMENT '写入条件（仅 remove 必填）',

  -- ⑤ 值类型（原 ValueType 语义收窄，⛔ 不再是"来源"）
  `ValueType`     varchar(20)  NOT NULL DEFAULT 'text' COMMENT '值类型：text/number/date/bool/enum',
  `DefaultText`   varchar(200) DEFAULT NULL            COMMENT '空值兜底文案',
  `NumberFormat`  varchar(64)  DEFAULT NULL            COMMENT '★格式串（.NET 方言，见 39 号 §十六）',

  -- ⑥ 模板侧只读快照
  `MergeJson`     json         DEFAULT NULL            COMMENT '{"rowSpan":1,"colSpan":3,"anchor":"A3"}',
  `StyleJson`     json         DEFAULT NULL            COMMENT '写入需保留的样式快照（只读，不改）',
  `MarkStyleName` varchar(50)  DEFAULT NULL            COMMENT '人工标记样式名（YZH_Mark），自验收依据',

  `Required`      tinyint(1)   NOT NULL DEFAULT 0      COMMENT '是否必填',
  `IsOrphan`      tinyint(1)   NOT NULL DEFAULT 0      COMMENT '★重传后消失的锚点（不删，标记保留，因其可能已有填过的值）',
  `Sort`          int          NOT NULL DEFAULT 0,
  `Remark`        varchar(500) DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0,
  `DeleteBy`      varchar(64)  DEFAULT NULL,
  `DeleteTime`    datetime     DEFAULT NULL,
  `IsValid`       int          NOT NULL DEFAULT 1,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_tpl_anchor` (`TemplateCode`,`AnchorType`,`AnchorKind`,`SheetName`,`SectionIndex`,`HeaderKind`,`AnchorRef`),
  KEY `idx_field`  (`FieldCode`),
  KEY `idx_orphan` (`TemplateCode`,`IsOrphan`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='模板锚点清单（Token/书签/区域）+ 字段填写规则';

-- ----------------------------------------------------------------------------
-- 表 3 · cert_doc_fill_prompt（全文填写规则）
-- 基线 = 37 号 §4.2 表3（= 23 号 §5.2，未改动）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_fill_prompt` (
  `Id`            bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`          varchar(36)  NOT NULL                COMMENT '全局唯一编码（GUID）',
  `OrgCode`       varchar(50)  NOT NULL DEFAULT ''     COMMENT '认证机构编码（配置层归属）',

  `PromptCode`    varchar(100) NOT NULL                COMMENT 'Prompt 编码，如 doc_fill（cert_doc_template.FillPromptCode 引用此值）',
  `PromptName`    varchar(200) NOT NULL                COMMENT '名称，如 标准文档通用填写',
  `SystemPrompt`  text         DEFAULT NULL            COMMENT '系统提示词（角色设定）',
  `UserTemplate`  longtext     DEFAULT NULL            COMMENT '★用户提示词模板（含 {{__FILL__.xxx}} 占位符）',
  `OutputSchema`  json         DEFAULT NULL            COMMENT '★期望输出结构（字段清单 + 表格清单），用于校验 AI 返回',

  `Model`         varchar(100) DEFAULT NULL            COMMENT '模型（空=六键兜底）',
  `Temperature`   decimal(3,2) NOT NULL DEFAULT 0.00   COMMENT '★低温保证可复现',
  `MaxTokens`     int          NOT NULL DEFAULT 4000   COMMENT '最大输出 token',
  `Version`       int          NOT NULL DEFAULT 1      COMMENT '★版本（Prompt 变更留痕）',
  `IsDefault`     tinyint(1)   NOT NULL DEFAULT 0      COMMENT '是否默认（★同一 PromptCode 只能一条为 1）',

  `CreateBy`      varchar(50)  DEFAULT NULL,
  `CreateTime`    datetime     DEFAULT NULL,
  `UpdateBy`      varchar(50)  DEFAULT NULL,
  `UpdateTime`    datetime     DEFAULT NULL,
  `DeleteBy`      varchar(50)  DEFAULT NULL,
  `DeleteTime`    datetime     DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`       int          NOT NULL DEFAULT 1      COMMENT '启用状态（⛔ 禁用 Enable）',
  `Status`        varchar(50)  DEFAULT NULL,
  `Sort`          int          NOT NULL DEFAULT 0,
  `Remark`        varchar(500) DEFAULT NULL,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`            (`Code`),
  UNIQUE KEY `uk_org_prompt_ver`  (`OrgCode`,`PromptCode`,`Version`),
  KEY `idx_default`               (`PromptCode`,`IsDefault`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='标准文档全文填写规则（服务端装配的提示词模板）';

-- ----------------------------------------------------------------------------
-- 表 4 · cert_doc_fill_log（文档填写执行留痕）
-- 基线 = 39 号 §3.7 DDL，逐字
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_fill_log` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `OrgCode` varchar(36) NOT NULL,
  `EnterpriseCode` varchar(36) NOT NULL,
  `StageCode` varchar(36) NOT NULL DEFAULT '',
  `StandardCode` varchar(36) NOT NULL DEFAULT '',
  `TemplateFileCode` varchar(36) NOT NULL COMMENT '模板文件编码',
  `FileKind` varchar(10) NOT NULL DEFAULT '' COMMENT 'word/excel',
  `OutputStoragePath` varchar(512) NOT NULL DEFAULT '' COMMENT '产物路径（⛔ 不覆盖模板）',
  `TotalAnchors` int NOT NULL DEFAULT 0,
  `ResolvedCount` int NOT NULL DEFAULT 0,
  `PendingCount` int NOT NULL DEFAULT 0,
  `Completion` decimal(5,4) NOT NULL DEFAULT 0.0000,
  `RegionCount` int NOT NULL DEFAULT 0,
  `ClonedRows` int NOT NULL DEFAULT 0,
  `Verified` tinyint(1) NOT NULL DEFAULT 0 COMMENT '自验收：无残留锚点且无残留标记',
  `LeftoverTokens` json DEFAULT NULL COMMENT '残留锚点原文',
  `PendingsJson` json DEFAULT NULL COMMENT '待办明细',
  `RetrievedDocCodes` json DEFAULT NULL COMMENT '★ 两级过滤入选的企业文档 Code 清单（可审计）',
  `SkillTrace` json DEFAULT NULL COMMENT '各 Skill 调用轨迹',
  `PromptTokens` int NOT NULL DEFAULT 0,
  `CompletionTokens` int NOT NULL DEFAULT 0,
  `DurationMs` int NOT NULL DEFAULT 0,
  `Status` varchar(20) NOT NULL DEFAULT 'success' COMMENT 'success/partial/failed',
  `Message` varchar(1024) DEFAULT NULL,
  `CreateTime` datetime NOT NULL,
  `CreateBy` varchar(64) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(64) DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeleteBy` varchar(64) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsValid` int NOT NULL DEFAULT 1,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_doc_fill_log_code` (`Code`),
  KEY `idx_doc_fill_log_ent` (`EnterpriseCode`,`TemplateFileCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='文档填写执行留痕';

-- ----------------------------------------------------------------------------
-- 表 5 · cert_doc_ai_suggestion（AI 填写建议：多候选 + 人工裁决）
-- 基线 = 39 号 §17.3 DDL，逐字
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_ai_suggestion` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `OrgCode` varchar(36) NOT NULL,
  `EnterpriseCode` varchar(36) NOT NULL,
  `StageCode` varchar(36) NOT NULL DEFAULT '',
  `StandardCode` varchar(36) NOT NULL DEFAULT '',

  `TemplateFileCode` varchar(36) NOT NULL COMMENT '目标模板（cert_standard_directory_file.Code）',
  `AnchorCode` varchar(128) NOT NULL COMMENT '目标单元格锚点',
  `FormKind` varchar(20) NOT NULL DEFAULT 'scalar' COMMENT 'scalar / table',

  `BatchCode` varchar(36) NOT NULL COMMENT '★ 一次 AI 分析的批次（整体重跑/回滚用）',
  `SuggestionIndex` int NOT NULL DEFAULT 0 COMMENT '★ 同锚点第 N 个候选（0 起）',

  `SuggestedValue` text COMMENT 'AI 建议值（表格为 JSON）',
  `ValueKind` varchar(20) NOT NULL DEFAULT 'text' COMMENT 'text/number/date/bool',
  `NumberFormat` varchar(64) DEFAULT NULL COMMENT '★ 格式串（.NET 方言，见 39 号 §十六）',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT '置信度 0~1',

  `SourceDocCode` varchar(36) DEFAULT NULL COMMENT '★ 数据源：企业文档 Code',
  `SourceLocation` varchar(256) DEFAULT NULL COMMENT '★ 数据源：文档内位置（页/段/表）',
  `SourceSnippet` text COMMENT '★ 证据原文片段（「一键看证据摘要」）',
  `Reason` varchar(1000) DEFAULT NULL COMMENT 'AI 给的理由',

  `IsSelected` tinyint(1) NOT NULL DEFAULT 0 COMMENT '★ 人工是否选定（同锚点至多一条为 1，由应用层保证）',
  `ManualValue` text COMMENT '人工改写后的值（≠建议值）',
  `SelectedBy` varchar(64) DEFAULT NULL,
  `SelectedTime` datetime DEFAULT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'pending' COMMENT 'pending/accepted/rejected/edited',

  `SkillCode` varchar(50) DEFAULT NULL COMMENT '产出它的 Skill（src_ai_field / src_ai_table）',
  `ModelName` varchar(100) DEFAULT NULL,
  `PromptTokens` int NOT NULL DEFAULT 0,
  `CompletionTokens` int NOT NULL DEFAULT 0,
  `DurationMs` int NOT NULL DEFAULT 0,

  `CreateTime` datetime NOT NULL,
  `CreateBy` varchar(64) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(64) DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeleteBy` varchar(64) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsValid` int NOT NULL DEFAULT 1,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_ai_suggestion_code` (`Code`),
  KEY `idx_ai_suggestion_target` (`EnterpriseCode`,`TemplateFileCode`,`AnchorCode`),
  KEY `idx_ai_suggestion_batch` (`BatchCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='AI 填写建议（多候选 + 人工裁决）';

-- ----------------------------------------------------------------------------
-- 归一链 · cert_standard_directory_file 加「可编辑版本」四列
--
-- 背景：库里 668 份标准文档中 **567 份 `.doc` + 44 份 `.xls`（合计 91.5%）**
--       在 NPOI 2.7.2 下**连读都不行**（无 NPOI.HWPF / 旧 HSSF 限制）
--       ⇒ 必须先归一成 `.docx`/`.xlsx` 才能进入「填写」链。
--
-- 方案（38 号 Q-10 = A）：新增**独立列**，⛔ 不复用遗留的 `ConvertedStoragePath`
--       （`StandardDirectoryFile.cs:134` 已标注「停止写入新值」，复用会让「遗留排空」与「新链」混在一起）。
--
-- ★ 四列镜像既有 `MarkdownPath` / `MarkdownStatus` / `MarkdownMessage` / `MarkdownDate` 的形态，
--   便于批量回填端点记录「成功/失败/原因」并支持断点续跑。
-- ⚠️ 全部可空 ⇒ 对既有 668 行**零影响**（不需要回填默认值）。
-- ----------------------------------------------------------------------------
ALTER TABLE `cert_standard_directory_file`
  ADD COLUMN `EditableStoragePath` varchar(512)  DEFAULT NULL COMMENT '★归一后的可编辑版本路径（.docx/.xlsx）；空=尚未归一' AFTER `ConvertDate`,
  ADD COLUMN `EditableStatus`      varchar(20)   DEFAULT NULL COMMENT '★归一状态：pending/completed/failed；空=不需要归一（本来就是 docx/xlsx）' AFTER `EditableStoragePath`,
  ADD COLUMN `EditableMessage`     varchar(1024) DEFAULT NULL COMMENT '★归一失败原因' AFTER `EditableStatus`,
  ADD COLUMN `EditableDate`        datetime      DEFAULT NULL COMMENT '★最近归一时间' AFTER `EditableMessage`;

-- ============================================================================
-- 验证（执行后应输出 5 张表 + 4 列）
-- ============================================================================
SELECT TABLE_NAME, TABLE_COMMENT
  FROM information_schema.TABLES
 WHERE TABLE_SCHEMA = 'yzh_cert_platform'
   AND TABLE_NAME IN ('cert_doc_template','cert_doc_template_anchor','cert_doc_fill_prompt',
                      'cert_doc_fill_log','cert_doc_ai_suggestion')
 ORDER BY TABLE_NAME;

SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_COMMENT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = 'yzh_cert_platform'
   AND TABLE_NAME = 'cert_standard_directory_file'
   AND COLUMN_NAME LIKE 'Editable%'
 ORDER BY ORDINAL_POSITION;
