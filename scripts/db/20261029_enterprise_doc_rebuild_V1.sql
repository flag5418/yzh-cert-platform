-- ============================================================================
-- 20261029_enterprise_doc_rebuild_V1.sql — 企业文档重建（去阶段，唯一一层）
-- 日期：2026-10-29 ｜ 幂等：可重复执行（CREATE TABLE IF NOT EXISTS）
-- 依据：docs/70-V2设计/36-企业资料上传重写-V1.md §四
--
-- 用户裁定（2026-10-29）：
--   ① 可以重建表，重新定义结构
--   ② 可以调整存储结构（已去掉阶段）
--   ③ 标准来源 = cert_audit_plan（一企业一有效计划）
--   ④ 上传不带标准；标准在「按标准条款提取」时才出现
--
-- 建 4 张表，⛔ 不动老表（cert_enterprise_original_* 保留到 P5 迁移后停用）：
--   cert_enterprise_doc               企业文档（唯一一层：无阶段 / 无标准 / 无槽位）
--   cert_enterprise_doc_version       文档历史版本
--   cert_enterprise_doc_upload_task   上传批次（撤草稿用）
--   cert_document_extraction          提取产物（文档 × 标准 = 一份 JSON）
--
-- ⚠️ 表名刻意避开已占用的 cert_enterprise_file_version（资料库归档版本）
--   与 cert_enterprise_original_*（老表）。
-- ⚠️ 铁律：UpdateTime 必须 NULL DEFAULT NULL —— BaseEntity.UpdateTime 是 DateTime?，
--    Insertable(entity) 会把它写进 INSERT，声明 NOT NULL 会在新增时炸成
--    「必填字段为空」（文案误导，实为 DB 约束）。
-- ⚠️ 铁律：RelFolderPath 512 字节，⛔ 不进唯一索引（3×512 超 3072B 上限）⇒ 用前缀长度。
-- ============================================================================

-- ── 1. 企业文档（唯一一层）──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_enterprise_doc` (
  `Id`             BIGINT       NOT NULL AUTO_INCREMENT,
  `Code`           VARCHAR(36)  NOT NULL                COMMENT '业务键（GUID）',
  `EnterpriseCode` VARCHAR(36)  NOT NULL                COMMENT 'cert_enterprise.Code',
  `OrgCode`        VARCHAR(36)  DEFAULT NULL            COMMENT '隔离冗余（VirtualOrg Code），⛔ 不作筛选条件',
  `UploadTaskCode` VARCHAR(36)  DEFAULT NULL            COMMENT '批次 cert_enterprise_doc_upload_task.Code',
  `RelFolderPath`  VARCHAR(512) NOT NULL DEFAULT ''     COMMENT '相对 {Ent}/ 的文件夹，空串=根',
  `FileName`       VARCHAR(300) NOT NULL                COMMENT '原始文件名（含扩展名）',
  `FileType`       VARCHAR(20)  NOT NULL DEFAULT ''     COMMENT '小写含点，如 .docx',
  `FileSize`       BIGINT       NOT NULL DEFAULT 0,
  `Sha256`         VARCHAR(64)  NOT NULL DEFAULT ''     COMMENT '★ 变更判定唯一依据（hash 不变=幂等跳过）',
  `StoragePath`    VARCHAR(512) NOT NULL DEFAULT ''     COMMENT '★ 恒指当前活跃版',
  `VersionNumber`  INT          NOT NULL DEFAULT 1      COMMENT '替换递增，旧字节走 _archive',
  `ConvertStatus`  VARCHAR(20)  NOT NULL DEFAULT 'none' COMMENT 'none/pending/converting/completed/failed/unsupported',
  `ConvertMessage` VARCHAR(1024) DEFAULT NULL,
  `PreviewPdfPath` VARCHAR(512) DEFAULT NULL            COMMENT 'PDF 产物（预览用）',
  `MarkdownPath`   VARCHAR(512) DEFAULT NULL            COMMENT 'Markdown 产物（原文，提取输入）',
  `MarkdownStatus` VARCHAR(20)  NOT NULL DEFAULT 'none' COMMENT '同 ConvertStatus 枚举',
  `MarkdownMessage` VARCHAR(1024) DEFAULT NULL,
  `ConvertDate`    DATETIME     DEFAULT NULL            COMMENT '双产物完成时间',
  `ExtractStatus`  VARCHAR(20)  NOT NULL DEFAULT 'none' COMMENT '聚合：none/partial/running/done/failed/skipped',
  `ExtractMessage` VARCHAR(1024) DEFAULT NULL,
  `ExtractTime`    DATETIME     DEFAULT NULL,
  `Remark`         VARCHAR(500) DEFAULT NULL,
  `IsValid`        INT          NOT NULL DEFAULT 1,
  `IsDeleted`      TINYINT(1)   NOT NULL DEFAULT 0,
  `CreateBy`       VARCHAR(64)  DEFAULT NULL,
  `CreateTime`     DATETIME     DEFAULT NULL,
  `UpdateBy`       VARCHAR(64)  DEFAULT NULL,
  `UpdateTime`     DATETIME     DEFAULT NULL            COMMENT '⚠️ 必须可空',
  `DeleteBy`       VARCHAR(64)  DEFAULT NULL,
  `DeleteTime`     DATETIME     DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_ent_path` (`EnterpriseCode`, `RelFolderPath`(64), `FileName`(64)),
  KEY `idx_sha256` (`Sha256`),
  KEY `idx_upload_task` (`UploadTaskCode`),
  KEY `idx_extract_status` (`ExtractStatus`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业文档（唯一一层，无阶段无标准）';

-- ── 2. 文档历史版本 ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_enterprise_doc_version` (
  `Id`             BIGINT       NOT NULL AUTO_INCREMENT,
  `Code`           VARCHAR(36)  NOT NULL,
  `FileCode`       VARCHAR(36)  NOT NULL                COMMENT '父文档 cert_enterprise_doc.Code（⛔ 不用 Id）',
  `EnterpriseCode` VARCHAR(36)  NOT NULL,
  `VersionNumber`  INT          NOT NULL DEFAULT 1,
  `FileName`       VARCHAR(300) NOT NULL,
  `FileType`       VARCHAR(20)  NOT NULL DEFAULT '',
  `FileSize`       BIGINT       NOT NULL DEFAULT 0,
  `Sha256`         VARCHAR(64)  NOT NULL DEFAULT '',
  `StoragePath`    VARCHAR(512) NOT NULL DEFAULT ''     COMMENT '归档路径 _archive/{名}.v{n}',
  `Reason`         VARCHAR(500) DEFAULT NULL,
  `IsValid`        INT          NOT NULL DEFAULT 1,
  `IsDeleted`      TINYINT(1)   NOT NULL DEFAULT 0,
  `CreateBy`       VARCHAR(64)  DEFAULT NULL,
  `CreateTime`     DATETIME     DEFAULT NULL,
  `UpdateBy`       VARCHAR(64)  DEFAULT NULL,
  `UpdateTime`     DATETIME     DEFAULT NULL,
  `DeleteBy`       VARCHAR(64)  DEFAULT NULL,
  `DeleteTime`     DATETIME     DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_ver` (`FileCode`, `VersionNumber`),
  KEY `idx_ent` (`EnterpriseCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业文档历史版本（只追加）';

-- ── 3. 上传批次 ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_enterprise_doc_upload_task` (
  `Id`             BIGINT       NOT NULL AUTO_INCREMENT,
  `Code`           VARCHAR(36)  NOT NULL,
  `EnterpriseCode` VARCHAR(36)  NOT NULL,
  `TotalFiles`     INT          NOT NULL DEFAULT 0      COMMENT '计划份数',
  `TotalSize`      BIGINT       NOT NULL DEFAULT 0      COMMENT '计划字节数',
  `SuccessCount`   INT          NOT NULL DEFAULT 0,
  `SkipCount`      INT          NOT NULL DEFAULT 0      COMMENT '幂等跳过（同 hash）',
  `ReplaceCount`   INT          NOT NULL DEFAULT 0      COMMENT '替换（异 hash）',
  `Status`         VARCHAR(20)  NOT NULL DEFAULT 'draft' COMMENT 'draft/uploading/confirmed/cancelled/failed',
  `Message`        VARCHAR(1024) DEFAULT NULL,
  `ExpireTime`     DATETIME     DEFAULT NULL            COMMENT '草稿过期（init 后超时未 confirm 即作废）',
  `IsValid`        INT          NOT NULL DEFAULT 1,
  `IsDeleted`      TINYINT(1)   NOT NULL DEFAULT 0,
  `CreateBy`       VARCHAR(64)  DEFAULT NULL,
  `CreateTime`     DATETIME     DEFAULT NULL,
  `UpdateBy`       VARCHAR(64)  DEFAULT NULL,
  `UpdateTime`     DATETIME     DEFAULT NULL,
  `DeleteBy`       VARCHAR(64)  DEFAULT NULL,
  `DeleteTime`     DATETIME     DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_ent_status` (`EnterpriseCode`, `Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业文档上传批次';

-- ── 4. 提取产物（文档 × 标准 = 一份 JSON）────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_document_extraction` (
  `Id`             BIGINT       NOT NULL AUTO_INCREMENT,
  `Code`           VARCHAR(36)  NOT NULL,
  `FileCode`       VARCHAR(36)  NOT NULL                COMMENT 'cert_enterprise_doc.Code',
  `StandardCode`   VARCHAR(36)  NOT NULL                COMMENT 'cert_iso_standard.Code（GUID）',
  `EnterpriseCode` VARCHAR(36)  NOT NULL                COMMENT '冗余，按企业查',
  `ClauseNumbers`  VARCHAR(500) NOT NULL DEFAULT ''     COMMENT '适用条款号，逗号分隔（FIND_IN_SET 精确匹配）',
  `ContentJson`    LONGTEXT     DEFAULT NULL            COMMENT '提取内容 {docMeta, clauses[]}',
  `ClauseCount`    INT          NOT NULL DEFAULT 0      COMMENT '切片条数',
  `SourceSha256`   VARCHAR(64)  NOT NULL DEFAULT ''     COMMENT '文档指纹，不变不重跑',
  `PromptCode`     VARCHAR(50)  NOT NULL DEFAULT ''     COMMENT '产出它的提示词 Code',
  `PromptVersion`  VARCHAR(20)  NOT NULL DEFAULT ''     COMMENT '产出它的提示词版本',
  `ExtractStatus`  VARCHAR(20)  NOT NULL DEFAULT 'pending' COMMENT 'pending/running/done/failed/skipped',
  `ExtractMessage` VARCHAR(1000) DEFAULT NULL           COMMENT '失败/跳过原因（⛔ 不许静默）',
  `ExtractedAt`    DATETIME     DEFAULT NULL,
  `IsStale`        TINYINT(1)   NOT NULL DEFAULT 0      COMMENT '文档被替换后置 1（待重跑）',
  `Sort`           INT          NOT NULL DEFAULT 0,
  `IsValid`        INT          NOT NULL DEFAULT 1,
  `IsDeleted`      TINYINT(1)   NOT NULL DEFAULT 0,
  `CreateBy`       VARCHAR(64)  DEFAULT NULL,
  `CreateTime`     DATETIME     DEFAULT NULL,
  `UpdateBy`       VARCHAR(64)  DEFAULT NULL,
  `UpdateTime`     DATETIME     DEFAULT NULL,
  `DeleteBy`       VARCHAR(64)  DEFAULT NULL,
  `DeleteTime`     DATETIME     DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_file_std` (`FileCode`, `StandardCode`),
  KEY `idx_std_clauses` (`StandardCode`, `ClauseNumbers`(120)),
  KEY `idx_ent` (`EnterpriseCode`),
  KEY `idx_status` (`ExtractStatus`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='文档提取产物（文档×标准=一份 JSON）';

-- ── 5. 验证 ─────────────────────────────────────────────────────────────────
SELECT '新表' AS Kind, TABLE_NAME AS Name, TABLE_ROWS AS Rows_
  FROM INFORMATION_SCHEMA.TABLES
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME IN ('cert_enterprise_doc', 'cert_enterprise_doc_version',
                      'cert_enterprise_doc_upload_task', 'cert_document_extraction')
 ORDER BY TABLE_NAME;

SELECT '老表未动' AS Kind, COUNT(*) AS OldFileRows
  FROM `cert_enterprise_original_file` WHERE `IsDeleted` = 0;
