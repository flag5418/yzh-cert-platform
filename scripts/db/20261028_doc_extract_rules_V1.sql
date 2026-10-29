-- ============================================================================
-- 20261028_doc_extract_rules_V1.sql
-- 文档提取规则表 —— 标准 Markdown 知识库 + 提取提示词
--
-- 设计依据：docs/70-V2设计/28-文档建模与执行引擎设计-V3.md §二
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 yzh_cert_platform < 本文件
--
-- ⚠️ 幂等：全部 IF NOT EXISTS
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- 一、建表 cert_doc_extract_rules
-- ────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_doc_extract_rules` (
  `Id`              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`            VARCHAR(50)  NOT NULL COMMENT '业务键',
  `StandardCode`    VARCHAR(36)  NOT NULL COMMENT '标准 Code（cert_iso_standard.Code GUID）',
  `StandardName`    VARCHAR(200) NOT NULL COMMENT '标准名称（冗余，方便展示）',
  `MarkdownContent` MEDIUMTEXT   NULL COMMENT '国标 Markdown 全文（清洗后）',
  `PromptContent`   MEDIUMTEXT   NULL COMMENT '提取提示词（每标准一份）',
  `PromptVariables` JSON         NULL COMMENT '提示词变量列表',
  `Version`         INT          NOT NULL DEFAULT 1 COMMENT '版本号（每次发布 +1）',
  `IsPublished`     TINYINT(1)   NOT NULL DEFAULT 0 COMMENT '当前是否发布（1=发布）',
  `Sort`            INT          NOT NULL DEFAULT 0 COMMENT '排序号',
  `IsValid`         INT          NOT NULL DEFAULT 1 COMMENT '有效标志',
  `Remark`          VARCHAR(500) NULL COMMENT '备注',
  `CreateBy`        VARCHAR(50)  NULL,
  `CreateTime`      DATETIME     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`        VARCHAR(50)  NULL,
  `UpdateTime`      DATETIME     NULL,
  `DeleteBy`        VARCHAR(50)  NULL,
  `DeleteTime`      DATETIME     NULL,
  `IsDeleted`       TINYINT(1)   NOT NULL DEFAULT 0,
  `Status`          VARCHAR(50)  NULL COMMENT '业务状态（保留）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_standard` (`StandardCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='文档提取规则（每标准一行：Markdown 知识库 + 提取提示词）';

-- ────────────────────────────────────────────────────────────────────────────
-- 二、索引
-- ────────────────────────────────────────────────────────────────────────────
-- uk_code / uk_standard 已在此表定义时创建，无需额外 ALTER
