-- 2026-11-10 V3 知识层：标准原文 Markdown 落在标准行上（单一事实源）
-- 目的：上传标准文件 → 转 Markdown → 存路径 → 供「自动生成条款」与「文档内容提取」共同消费
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

ALTER TABLE cert_iso_standard
  ADD COLUMN MarkdownPath VARCHAR(500) NULL COMMENT '标准原文 Markdown 存储路径（MinIO key，/standard-source/...）' AFTER Description,
  ADD COLUMN MarkdownChars INT NOT NULL DEFAULT 0 COMMENT 'Markdown 字符数（0=未生成）' AFTER MarkdownPath,
  ADD COLUMN MarkdownSourceFile VARCHAR(255) NULL COMMENT '生成该 Markdown 的原始文件名' AFTER MarkdownChars,
  ADD COLUMN MarkdownUpdatedTime DATETIME NULL COMMENT 'Markdown 最近生成时间' AFTER MarkdownSourceFile;

SELECT Code, StandardCode, StandardName, MarkdownPath, MarkdownChars
FROM cert_iso_standard WHERE IsDeleted = 0 ORDER BY Sort;
