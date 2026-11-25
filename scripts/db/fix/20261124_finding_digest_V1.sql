-- ============================================================================
-- 审核发现概括表（LLM 归纳缓存）—— cert_finding_digest
-- 日期：2026-11-24
-- 依据：用户裁定「生成新字段专门概括审核发现；展示概括 + 原文链接；原始文字保留」
-- 说明：
--   1) 「审核发现」面板的每条事实 = 企业文档提取产物（只读投影，不落 cert_evidence），
--      原文常达上千字。本表缓存「文档 × 条款」这段原文的 2-3 句 LLM 概括。
--   2) 缓存键 = SourceCode + Anchor + SnippetMd5（同文档同条款同原文跨检查项复用；
--      原文变化 = MD5 变 ⇒ 重新生成）。
--   3) 幂等：CREATE TABLE IF NOT EXISTS，重复执行不丢数据。
-- ============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `cert_finding_digest` (
  `Id`         BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`       VARCHAR(36)  NULL COMMENT '业务编码（GUID）',
  `SourceCode` VARCHAR(50)  NOT NULL DEFAULT '' COMMENT '来源编码（FileCode）',
  `Anchor`     VARCHAR(200) NOT NULL DEFAULT '' COMMENT '锚点（条款号 / 召回理由）',
  `SnippetMd5` VARCHAR(32)  NOT NULL DEFAULT '' COMMENT '原文摘录 MD5',
  `Digest`     TEXT         NULL COMMENT '概括（2-3 句）',
  `Model`      VARCHAR(50)  NULL COMMENT '生成用的模型名',
  `IsValid`    INT          NOT NULL DEFAULT 1 COMMENT '有效标志（1=启用 0=停用）',
  `IsDeleted`  TINYINT(1)   NOT NULL DEFAULT 0 COMMENT '软删除标记',
  `DeleteBy`   VARCHAR(64)  NULL COMMENT '删除人 Code',
  `DeleteTime` DATETIME     NULL COMMENT '删除时间',
  `CreateBy`   VARCHAR(64)  NULL COMMENT '创建人 Code',
  `CreateTime` DATETIME     NULL COMMENT '创建时间（本地时间）',
  `UpdateBy`   VARCHAR(64)  NULL COMMENT '更新人 Code',
  `UpdateTime` DATETIME     NULL COMMENT '更新时间（本地时间）',
  PRIMARY KEY (`Id`),
  KEY `idx_finding_digest_key` (`SourceCode`, `Anchor`, `SnippetMd5`),
  KEY `idx_finding_digest_code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核发现概括（LLM 归纳缓存）';
