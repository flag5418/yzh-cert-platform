-- ============================================================================
-- 20261003_enterprise_original_V1.sql
-- 企业原始资料管理（36 号 §3，T0.1 地基）
--
-- 一次完成五件事（⛔ 不拆多次迁移同一批对象）：
--   ① 新建 cert_enterprise_original_file          —— L1 文件生命周期 + 当前活跃版 + 分析策略
--   ② 新建 cert_enterprise_original_file_version  —— 历史版本（只追加，照抄企业资料库）
--   ③ 新建 cert_enterprise_original_upload_task    —— 上传批次（五段式 upload/cancel 定位草稿行）
--   ④ 新建 cert_enterprise_doc_profile            —— L3 画像（33 号 §5.4 原样搬运，此前只在 md 未落库）
--   ⑤ 受控字典 POLICY_REASON / POLICY_SOURCE + 专家端菜单 MENU_AUD_11 + 角色授权
--
-- ⛔ 跑完必须执行：
--    ./scripts/db/verify/sync_menu_urls.sh          刷新菜单快照（否则守卫 R12 基于过期数据）
--    文末 §六 的 DB 验证 SELECT（必须 0 行）
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- ① cert_enterprise_original_file
--
-- ⚠️ D7（2026-10-03 用户拍板）：唯一键【只有 Code】。曾尝试
--    uk_ent_stage_path(EnterpriseCode,StageCode,RelFolderPath,FileName)
--    = (36+36+512+300)×4 = 3536 B > InnoDB DYNAMIC 行格式上限 3072 B
--    MySQL 8.0.46 实跑：ERROR 1071 Specified key was too long
--    ⇒ 定位改走普通索引 + 应用层 Sha256 逐字判重。
--
-- ⚠️ D8（2026-10-03 用户拍板）：必须有版本管理，与企业资料库同构 ——
--    旧字节走 PathBuilder.Archive → 同文件夹 _archive/{文件名}.v{n}，
--    元数据追加进 ② 表；主行 StoragePath 恒指当前活跃版 ⇒ 外部引用永不失效。
--
-- ⚠️ D9（2026-10-03 用户拍板）：Id 无任何业务语义，⛔ 永不入 WHERE / 关联 /
--    add-update 分流 / 存在性判定；定位·删除·更新·传参一律只用 Code。
--
-- ⚠️ 状态列枚举必须对齐 cert-share/src/utils/convertStatus.ts:11-18
--    = none/pending/converting/completed/failed/unsupported
--    ⛔ 写 converted 会让前端 CLASS_MAP 未命中 ⇒ 静默显示「未知状态」（无任何报错）。
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_enterprise_original_file` (
  `Id`                bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID（⛔零语义，定位一律用 Code）',
  `Code`              varchar(36)  NOT NULL                COMMENT '★业务键 GUID（唯一键；铁律四唯一合法定位/删除/更新依据）',
  `CreateTime`        datetime     NOT NULL                COMMENT '创建时间',
  `CreateBy`          varchar(64)  DEFAULT NULL            COMMENT '创建人Code',
  `UpdateTime`        datetime     DEFAULT NULL            COMMENT '更新时间',
  `UpdateBy`          varchar(64)  DEFAULT NULL            COMMENT '更新人Code',

  -- 身份段（P13：NOT NULL DEFAULT ''，⛔ 禁 NULL —— 否则定位索引与判重同时失效）
  `EnterpriseCode`    varchar(36)  NOT NULL DEFAULT ''     COMMENT '★企业Code → cert_enterprise.Code',
  `StageCode`         varchar(36)  NOT NULL DEFAULT ''     COMMENT '★阶段Code → cert_cert_stage.Code',
  `OrgCode`           varchar(36)  NOT NULL DEFAULT ''     COMMENT '机构Code（冗余，P13）',

  -- 批次（upload/cancel 定位草稿行，见 ③）
  `UploadTaskCode`    varchar(36)  NOT NULL DEFAULT ''     COMMENT '当前激活上传批次 → cert_enterprise_original_upload_task.Code；空=不属任何批次',

  -- 文件
  `RelFolderPath`     varchar(512) NOT NULL DEFAULT ''     COMMENT '相对 {Ent}/{Stage}/ 的文件夹路径，/ 分隔，空串=根。⛔ 不入唯一索引（3072B 上限）',
  `FileName`          varchar(300) NOT NULL DEFAULT ''     COMMENT '文件原名（含扩展名）',
  `FileType`          varchar(20)  NOT NULL DEFAULT ''     COMMENT '扩展名（小写，含点，如 .docx）',
  `FileSize`          bigint       NOT NULL DEFAULT 0      COMMENT '字节数（当前版）',
  `Sha256`            varchar(64)  NOT NULL DEFAULT ''     COMMENT '★当前版内容指纹 —— D7 变更判定唯一依据（hash 不变=同一文件=幂等）',
  `StoragePath`       varchar(512) NOT NULL DEFAULT ''     COMMENT '★MinIO 源路径（恒指当前活跃版 ⇒ 外部引用永不失效）',
  `VersionNumber`     int          NOT NULL DEFAULT 1      COMMENT '★替换递增；旧字节走 PathBuilder.Archive（_archive/{名}.v{n}）',

  -- 转换（枚举对齐 convertStatus.ts，⛔ 禁 converted）
  `ConvertStatus`     varchar(20)  NOT NULL DEFAULT 'none' COMMENT 'none/pending/converting/completed/failed/unsupported',
  `ConvertMessage`    varchar(1024) DEFAULT NULL           COMMENT '失败原因',
  `PreviewPdfPath`    varchar(512)  DEFAULT NULL           COMMENT 'PDF 产物（PathBuilder.Product 派生）',
  `MarkdownPath`      varchar(512)  DEFAULT NULL           COMMENT '★Markdown 产物（语义分析输入）',
  `MarkdownStatus`    varchar(20)  NOT NULL DEFAULT 'none' COMMENT 'none/pending/converting/completed/failed/unsupported',
  `MarkdownMessage`   varchar(1024) DEFAULT NULL,
  `ConvertDate`       datetime     DEFAULT NULL            COMMENT '双产物完成时间',

  -- 分析
  `AnalyzeStatus`     varchar(20)  NOT NULL DEFAULT 'pending' COMMENT 'pending/analyzing/analyzed/failed/skipped',
  `AnalyzeMessage`    varchar(1024) DEFAULT NULL           COMMENT '失败原因 / 跳过原因',
  `AnalyzeTime`       datetime     DEFAULT NULL            COMMENT '最近分析完成时间',

  -- 分析策略（36 号 §3.5 三层区分：只作用于 L2 数据来源层）
  `AnalyzePolicy`     varchar(20)  NOT NULL DEFAULT 'analyze' COMMENT 'analyze/skip/ignore（字典 ANALYZE_POLICY）',
  `PolicyReason`      varchar(200) NOT NULL DEFAULT ''     COMMENT '策略原因（字典 POLICY_REASON）或 AI 建议说明。⚠️ 原定 30 太短：实测 AI 建议 + LLM 返回的 policyReason 会超长，MySQL 严格模式直接报错 ⇒ 整条分析链路挂（2026-10-03）',
  `PolicySource`      varchar(10)  DEFAULT NULL            COMMENT 'ai（建议）/ manual（人工确认）（字典 POLICY_SOURCE）',
  `PolicyDecidedBy`   varchar(64)  DEFAULT NULL            COMMENT '留痕',
  `PolicyDecidedTime` datetime     DEFAULT NULL            COMMENT '留痕',

  `Remark`            varchar(500) DEFAULT NULL,
  `IsDeleted`         tinyint(1)   NOT NULL DEFAULT 0      COMMENT '软删除',
  `DeleteBy`          varchar(64)  DEFAULT NULL,
  `DeleteTime`        datetime     DEFAULT NULL,
  `IsValid`           int          NOT NULL DEFAULT 1      COMMENT '★1有效0无效（铁律九，⛔ 禁 Enable）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_ent_stage_path` (`EnterpriseCode`,`StageCode`,`RelFolderPath`(191),`FileName`(120)),
  KEY `idx_ent_stage` (`EnterpriseCode`,`StageCode`),
  KEY `idx_sha256` (`Sha256`),
  KEY `idx_upload_task` (`UploadTaskCode`),
  KEY `idx_analyze_status` (`AnalyzeStatus`),
  KEY `idx_policy` (`AnalyzePolicy`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='企业原始资料索引（L1 文件生命周期 + 当前活跃版 + 分析策略）';

-- ────────────────────────────────────────────────────────────────────────────
-- ② cert_enterprise_original_file_version —— D8 历史版本（只追加）
--
-- 对标物：cert_enterprise_file_version（实体 CertPlatform.Shared/Entities/Dir/
--         EnterpriseFileVersion.cs），哲学一致：旧版本物理留在 MinIO _archive/，
--         本表只追加记录元数据，行数永不减少。
-- ⚠️ 旧字节归档算法直接复用 PathBuilder.Archive(storagePath, versionNumber)，
--    ⛔ 不另写一套（第二套路径算法 = 历史上 5 套格式并存的根因）。
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_enterprise_original_file_version` (
  `Id`             bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID（⛔零语义）',
  `Code`           varchar(36)  NOT NULL                COMMENT '★业务键 GUID（唯一键）',
  `CreateTime`     datetime     NOT NULL                COMMENT '创建时间',
  `CreateBy`       varchar(64)  DEFAULT NULL            COMMENT '创建人Code',
  `UpdateTime`     datetime     DEFAULT NULL,
  `UpdateBy`       varchar(64)  DEFAULT NULL,

  `FileCode`       varchar(36)  NOT NULL DEFAULT ''     COMMENT '★父文件 Code → cert_enterprise_original_file.Code（⛔ 不用 Id）',
  `EnterpriseCode` varchar(36)  NOT NULL DEFAULT ''     COMMENT '企业 Code（冗余，方便按企业查）',
  `StageCode`      varchar(36)  NOT NULL DEFAULT ''     COMMENT '阶段 Code（冗余）',

  `VersionNumber`  int          NOT NULL DEFAULT 1      COMMENT '★被归档内容的版本号（与 PathBuilder.Archive(versionNumber) 对应）',
  `FileName`       varchar(300) NOT NULL DEFAULT ''     COMMENT '该版本文件名',
  `FileType`       varchar(20)  NOT NULL DEFAULT ''     COMMENT '该版本扩展名',
  `FileSize`       bigint       NOT NULL DEFAULT 0      COMMENT '该版本字节数',
  `Sha256`         varchar(64)  NOT NULL DEFAULT ''     COMMENT '该版指纹（审计：确认两版内容真的不同）',
  `StoragePath`    varchar(512) NOT NULL DEFAULT ''     COMMENT '★归档后的 MinIO 路径（…/_archive/{名}.v{n}）',
  `Reason`         varchar(500) DEFAULT NULL            COMMENT '替换原因（如：用户上传新文件覆盖）',

  `IsDeleted`      tinyint(1)   NOT NULL DEFAULT 0,
  `DeleteBy`       varchar(64)  DEFAULT NULL,
  `DeleteTime`     datetime     DEFAULT NULL,
  `IsValid`        int          NOT NULL DEFAULT 1      COMMENT '★1有效0无效（铁律九，⛔ 禁 Enable）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_ver` (`FileCode`,`VersionNumber`),
  KEY `idx_ent_stage` (`EnterpriseCode`,`StageCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='企业原始资料历史版本（只追加；旧字节物理留在 MinIO _archive/）';

-- ────────────────────────────────────────────────────────────────────────────
-- ③ cert_enterprise_original_upload_task —— 上传批次
--
-- 为什么必须有：五段式上传的 upload/cancel 要能「撤草稿行」，但 ① 表
-- ⛔ 不能拿批次号当唯一键（否则一批 N 个文件互相覆盖一个 TaskId），必须另立批次表。
-- 对标物：cert_upload_task + EnterpriseFileService.GetDraftRowAsync(fileCode, taskId)（:2030）。
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_enterprise_original_upload_task` (
  `Id`             bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID（⛔零语义）',
  `Code`           varchar(36)  NOT NULL                COMMENT '★批次业务键 GUID（= TaskId，唯一键）',
  `CreateTime`     datetime     NOT NULL,
  `CreateBy`       varchar(64)  DEFAULT NULL,
  `UpdateTime`     datetime     DEFAULT NULL,
  `UpdateBy`       varchar(64)  DEFAULT NULL,

  `EnterpriseCode` varchar(36)  NOT NULL DEFAULT ''     COMMENT '★企业Code',
  `StageCode`      varchar(36)  NOT NULL DEFAULT ''     COMMENT '★阶段Code',

  `TotalFiles`     int          NOT NULL DEFAULT 0      COMMENT '计划份数',
  `TotalSize`      bigint       NOT NULL DEFAULT 0      COMMENT '计划字节数',
  `SuccessCount`   int          NOT NULL DEFAULT 0      COMMENT '已完成份数',
  `SkipCount`      int          NOT NULL DEFAULT 0      COMMENT '幂等跳过份数（D7：同 hash）',
  `ReplaceCount`   int          NOT NULL DEFAULT 0      COMMENT '替换份数（D8：异 hash，版本+1）',
  `Status`         varchar(20)  NOT NULL DEFAULT 'draft' COMMENT 'draft/uploading/confirmed/cancelled/failed',
  `Message`        varchar(1024) DEFAULT NULL           COMMENT '失败原因',
  `ExpireTime`     datetime     DEFAULT NULL            COMMENT '草稿过期时间（清理用）',

  `IsDeleted`      tinyint(1)   NOT NULL DEFAULT 0,
  `DeleteBy`       varchar(64)  DEFAULT NULL,
  `DeleteTime`     datetime     DEFAULT NULL,
  `IsValid`        int          NOT NULL DEFAULT 1      COMMENT '★1有效0无效（铁律九）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_ent_stage_status` (`EnterpriseCode`,`StageCode`,`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='企业原始资料上传批次（五段式上传的批次定位与计数）';

-- ────────────────────────────────────────────────────────────────────────────
-- ④ cert_enterprise_doc_profile —— L3 画像
--
-- ⚠️ 本 DDL 逐字来自 33 号 §5.4（此前【只存在于 md，未落 scripts/db】——
--    20261002_doc_semantic_rule_V1.sql 只建了 cert_tag_dict + cert_standard_doc_contract）。
--    两处必须同步维护，⛔ 不得各改各的。
-- ⚠️ uk_file_ver + IsLatest = 只追加：换文件 → ProfileVersion+1，旧版 IsLatest=0 保留可审计。
-- ⚠️ 语义字段与标准侧 cert_standard_doc_contract 逐字对称 ⇒ 两侧共用一个 DTO。
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_enterprise_doc_profile` (
  `Id`                     bigint        NOT NULL AUTO_INCREMENT COMMENT '主键ID（⛔零语义）',
  `Code`                   varchar(36)   NOT NULL              COMMENT '★业务键 GUID（唯一键）',
  `CreateTime`             datetime      NOT NULL              COMMENT '创建时间',
  `CreateBy`               varchar(64)   DEFAULT NULL          COMMENT '创建人Code',
  `UpdateTime`             datetime      DEFAULT NULL,
  `UpdateBy`               varchar(64)   DEFAULT NULL,

  `OriginalFileCode`       varchar(36)   NOT NULL              COMMENT '★宿主 → cert_enterprise_original_file.Code（一文件一画像版本）',
  `EnterpriseCode`         varchar(36)   NOT NULL DEFAULT ''   COMMENT '★企业Code（⛔ 禁 NULL，13 号 P13 口径）',
  `OrgCode`                varchar(36)   NOT NULL DEFAULT ''   COMMENT '认证机构Code',
  `StageCode`              varchar(36)   NOT NULL DEFAULT ''   COMMENT '阶段Code（冗余）',
  `StandardCode`           varchar(36)   NOT NULL DEFAULT ''   COMMENT '标准Code（GUID，冗余；空=平台级/标准无关原始资料）',
  `FileName`               varchar(300)  NOT NULL DEFAULT ''   COMMENT '文件原名（冗余）',

  -- 与标准侧完全对称的语义字段（两侧共用同一 DTO）
  `TagsJson`               json          DEFAULT NULL          COMMENT '★受控标签多值，值 ∈ cert_tag_dict.TagCode',
  `TagsSource`             varchar(10)   DEFAULT NULL          COMMENT 'ai / manual / carried',
  `TagsReason`             varchar(1000) DEFAULT NULL,
  `TagsConfidence`         decimal(3,2)  DEFAULT NULL,
  `DocPurpose`             text                               COMMENT '★作用四段式：【是什么】【审核关注点】【来源口径】【包含信息】',
  `InfoItemsJson`          json          DEFAULT NULL          COMMENT '★第四段结构化 [{itemName,itemDesc,valueType,isKey}]',
  `DocPurposeSource`       varchar(10)   DEFAULT NULL          COMMENT 'ai / manual / carried',
  `DocPurposeConfidence`   decimal(3,2)  DEFAULT NULL,

  `DocCategory`            varchar(20)   DEFAULT NULL          COMMENT 'fixed / editable / hybrid（画像结论）',
  `Summary`                varchar(1000) DEFAULT NULL          COMMENT '摘要',
  `Keywords`               varchar(1000) DEFAULT NULL          COMMENT '关键词（逗号分隔，召回倒排）',
  `SuggestedStandardCodes` json          DEFAULT NULL          COMMENT 'AI 建议可服务的标准Code数组',
  `Confidence`             decimal(3,2)  DEFAULT NULL          COMMENT '画像整体置信度',
  `ProfileVersion`         int           NOT NULL DEFAULT 1    COMMENT '画像版本（重跑递增，只追加）',
  `IsLatest`               bit(1)        NOT NULL DEFAULT b'1' COMMENT '是否最新版本（查询过滤位）',

  `TypeGuess`              varchar(100)  DEFAULT NULL          COMMENT 'AI 猜测类别（供召回排序）',
  `FieldsJson`             json          DEFAULT NULL          COMMENT '字段数组（≤100 条）',
  `TablesJson`             json          DEFAULT NULL          COMMENT '表格数组（超 50 行截断）',
  `ProfileStatus`          varchar(20)   NOT NULL DEFAULT 'pending' COMMENT 'pending / processing / completed / failed / skipped',
  `ProfileMessage`         varchar(1024) DEFAULT NULL          COMMENT '失败原因',
  `DetectSource`           varchar(20)   NOT NULL DEFAULT 'ai' COMMENT 'ai / manual / fingerprint / rule / mixed',
  `ModelName`              varchar(100)  DEFAULT NULL          COMMENT '实际调用模型快照',
  `PromptCode`             varchar(36)   DEFAULT NULL          COMMENT '所用提示词Code → wf_prompt_template.Code',
  `PromptVersion`          int           NOT NULL DEFAULT 1,
  `PromptTokens`           int           NOT NULL DEFAULT 0,
  `CompletionTokens`       int           NOT NULL DEFAULT 0,
  `DurationMs`             int           NOT NULL DEFAULT 0,
  `SourceMarkdownPath`     varchar(512)  DEFAULT NULL          COMMENT '★分析输入 markdown 路径（快照）',
  `AnalyzeTime`            datetime      DEFAULT NULL          COMMENT '最近分析时间',
  `IsManualCorrected`      bit(1)        NOT NULL DEFAULT b'0' COMMENT '人工干预标记',
  `CorrectedBy`            varchar(64)   DEFAULT NULL,
  `CorrectedTime`          datetime      DEFAULT NULL,

  `Remark`                 varchar(500)  DEFAULT NULL,
  `IsDeleted`              tinyint(1)    NOT NULL DEFAULT 0,
  `DeleteBy`               varchar(64)   DEFAULT NULL,
  `DeleteTime`             datetime      DEFAULT NULL,
  `IsValid`                int           NOT NULL DEFAULT 1    COMMENT '★1有效0无效（铁律九，⛔ 禁 Enable）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_file_ver` (`OriginalFileCode`, `ProfileVersion`),
  KEY `idx_ent_std_stage` (`EnterpriseCode`, `StandardCode`, `StageCode`),
  KEY `idx_status` (`ProfileStatus`),
  KEY `idx_latest` (`IsLatest`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='企业资料画像（L3 层，标签+作用四段，与标准侧契约对称）';

-- ────────────────────────────────────────────────────────────────────────────
-- 五、受控字典（36 号 §3.6）
--
-- ⛔ ANALYZE_POLICY / DOC_CATEGORY / DOC_PURPOSE 【已由 20261002_doc_semantic_rule_V1.sql
--    建并执行完毕】，本脚本【不重复建】，只补真正缺的两个。
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `Sys_Dictionary` (`Code`, `DicName`, `DicNo`, `ParentCode`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`, `Remark`)
VALUES
  ('POLICY_REASON', '分析策略原因', 'policy_reason', '26b0f1d2ae6c11f1953796fd503fd974', 450, 1, 0, NOW(), 'seed_enterprise_original', '36 号 §3.5：skip/ignore 的受控原因；covered_by_params=值已在全局参数里定义'),
  ('POLICY_SOURCE', '分析策略来源', 'policy_source', '26b0f1d2ae6c11f1953796fd503fd974', 460, 1, 0, NOW(), 'seed_enterprise_original', '36 号 §5.2：ai=AI 建议（⛔ 未确认前不改 AnalyzePolicy）/ manual=人工确认')
ON DUPLICATE KEY UPDATE
  `DicName` = VALUES(`DicName`), `ParentCode` = VALUES(`ParentCode`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

INSERT INTO `Sys_DictionaryList` (`Code`, `DicCode`, `DicValue`, `DicName`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  (CONCAT('PR_', 'covered_by_params'), 'POLICY_REASON', 'covered_by_params', '值已在全局参数里定义（不重复提取）', 10, 1, 0, NOW(), 'seed_enterprise_original'),
  (CONCAT('PR_', 'irrelevant'),         'POLICY_REASON', 'irrelevant',         '与企业体系无关（可移出证据清单，须留痕）', 20, 1, 0, NOW(), 'seed_enterprise_original'),
  (CONCAT('PR_', 'duplicate'),          'POLICY_REASON', 'duplicate',          '与其他文件重复',                     30, 1, 0, NOW(), 'seed_enterprise_original'),
  (CONCAT('PR_', 'manual'),             'POLICY_REASON', 'manual',             '人工判定',                           40, 1, 0, NOW(), 'seed_enterprise_original'),
  (CONCAT('PS_', 'ai'),                 'POLICY_SOURCE', 'ai',                 'AI 建议（待人工确认）',              10, 1, 0, NOW(), 'seed_enterprise_original'),
  (CONCAT('PS_', 'manual'),             'POLICY_SOURCE', 'manual',             '人工确认',                           20, 1, 0, NOW(), 'seed_enterprise_original')
ON DUPLICATE KEY UPDATE
  `DicValue` = VALUES(`DicValue`), `DicName` = VALUES(`DicName`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

-- ────────────────────────────────────────────────────────────────────────────
-- 六、专家端菜单 MENU_AUD_11「企业原始资料管理」+ 角色授权
--
-- ⚠️ Sys_RoleMenu 【没有 Code 列】（实库 information_schema 确认：Id varchar(64) 主键
--    + RoleCode/MenuCode/OrderNo/CreateTime/CreateBy）⇒ 只能写 Id。
-- ⚠️ OrderNo=450 插在 资料库(400) 与 组织与成员(500) 之间，零改动既有行。
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`, `CreateTime`, `CreateBy`, `Tag`, `IsDeleted`, `IsValid`)
VALUES
  ('MENU_AUD_11', 'MENU_AUD_00', '企业原始资料管理', NULL, 'Folder',
   '企业散乱原始资料上传 → 转 PDF/Markdown → 语义分析 → L3 画像',
   450, '/enterprise-original', NOW(), 'seed_enterprise_original', 'auditor', 0, 1)
ON DUPLICATE KEY UPDATE
  `ParentCode` = VALUES(`ParentCode`),
  `MenuName`   = VALUES(`MenuName`),
  `Icon`       = VALUES(`Icon`),
  `Description`= VALUES(`Description`),
  `OrderNo`    = VALUES(`OrderNo`),
  `Url`        = VALUES(`Url`),
  `Tag`        = VALUES(`Tag`),
  `IsDeleted`  = 0,
  `IsValid`    = 1;

INSERT IGNORE INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
VALUES
  (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_AUDIT_CLIENT_ADMIN', 'MENU_AUD_11', 450, NOW(), 'seed_enterprise_original');

-- ============================================================================
-- 七、验证（⛔ 必须全部符合预期）
-- ============================================================================
-- ① 4 张表都建出来了（应为 4 行）
-- SELECT TABLE_NAME FROM information_schema.TABLES
--  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME LIKE 'cert_enterprise_original%'
--     OR TABLE_SCHEMA=DATABASE() AND TABLE_NAME='cert_enterprise_doc_profile';

-- ② ⛔ 铁律九：本批表【不允许】存在任何 Enable/enable 列（期望 0 行）
--    ⚠️ 必须用 CONVERT(... USING utf8mb4) COLLATE utf8mb4_bin —— information_schema
--       的排序规则大小写不敏感，直接 NOT REGEXP '^[A-Z]' 会【永远返回 0 行】（假阴性）。
-- SELECT TABLE_NAME, CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin AS Col
--   FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA=DATABASE()
--    AND TABLE_NAME IN ('cert_enterprise_original_file','cert_enterprise_original_file_version',
--                       'cert_enterprise_original_upload_task','cert_enterprise_doc_profile')
--    AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]';

-- ③ 列名必须全 PascalCase（期望 0 行；同 ② 的 COLLATE 铁律）
-- SELECT TABLE_NAME, CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin AS Col
--   FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA=DATABASE()
--    AND TABLE_NAME IN ('cert_enterprise_original_file','cert_enterprise_original_file_version',
--                       'cert_enterprise_original_upload_task','cert_enterprise_doc_profile')
--    AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]'
--    AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin <> 'Id';

-- ④ 唯一键确认：① 表只应有 uk_code，⛔ 不得有 uk_ent_stage_path（期望 1 行）
-- SELECT TABLE_NAME, INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) Cols
--   FROM information_schema.STATISTICS
--  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME LIKE 'cert_enterprise_original%'
--    AND NON_UNIQUE=0 GROUP BY TABLE_NAME, INDEX_NAME;

-- ⑤ 索引字节数确认（期望全部 <= 3072；idx_ent_stage_path = 144+144+191×4+120×4 = 1532）
--    ⚠️ information_schema.STATISTICS 【没有】CHAR_LENGTH 列，须 JOIN COLUMNS 取
--       CHARACTER_MAXIMUM_LENGTH。
-- SELECT s.INDEX_NAME, SUM(COALESCE(s.SUB_PART, c.CHARACTER_MAXIMUM_LENGTH)) * 4 AS bytes_est
--   FROM information_schema.STATISTICS s
--   JOIN information_schema.COLUMNS c
--     ON c.TABLE_SCHEMA = s.TABLE_SCHEMA AND c.TABLE_NAME = s.TABLE_NAME
--    AND c.COLUMN_NAME  = s.COLUMN_NAME
--  WHERE s.TABLE_SCHEMA = DATABASE() AND s.TABLE_NAME = 'cert_enterprise_original_file'
--  GROUP BY s.INDEX_NAME ORDER BY bytes_est DESC;

-- ⑥ 字典项（期望 2 + 4 + 2 = 8 行；ANALYZE_POLICY 3 项由旧脚本负责，此处不应重复）
-- SELECT DicCode, COUNT(*) FROM Sys_DictionaryList
--  WHERE DicCode IN ('POLICY_REASON','POLICY_SOURCE','ANALYZE_POLICY') GROUP BY DicCode;

-- ⑦ 菜单 + 授权（期望各 1 行）
-- SELECT Code, ParentCode, MenuName, Url, Tag, OrderNo FROM Sys_Menu WHERE Code='MENU_AUD_11';
-- SELECT RoleCode, MenuCode FROM Sys_RoleMenu WHERE MenuCode='MENU_AUD_11';