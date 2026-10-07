-- ══════════════════════════════════════════════════════════════════════════
-- 20261006 · cert_doc_template 增加「试填预览」2 列（PreviewPdfPath / PreviewTime）
-- ══════════════════════════════════════════════════════════════════════════
-- 权威依据（⛔ 不要凭记忆改）：
--   落点裁定 → docs/.../05-企业资料规范化/52-标准文档标准化-实施TODO清单-V1.md §12.4
--   链路口径 → 同文 §12.2（① NPOI 填模板 → ② IFileConvertCore 转 PDF → ③ 自己上传）
--   保留段   → CertPlatform.Shared/Storage/PathBuilder.cs（PreviewSegment = "_preview"）
--
-- 背景（2026-10-05 用户逐字）：
--   「1\a，2\可以存储」⇒ ① 试填产物落点 = 新增保留段 `_preview/`（**选 A**）
--                       ② 试填 PDF 路径 **可以存储**
--   （§12.4 同时把 B7/B8 整体降为 TODO，2026-10-06 用户重新点名「先完善自动填充和预览」⇒ 解除）
--
-- 语义：
--   PreviewPdfPath = 最近一次「试填」生成的 PDF 在 MinIO 的路径
--     · 由 PathBuilder.PreviewFromTemplate() 派生 —— 把 `_template` 段换成 `_preview` 段
--     · 形如 …/{Folder}/_preview/{模板名}.docx.pdf
--     · ★ **固定 key**：重复试填覆盖同一个对象 ⇒ 空间占用恒定（用户口径「节约后台空间」）
--     · 空 = 从未试填过（前端据此禁用「填充后预览」视图）
--   PreviewTime    = 最近一次试填时间（供页面显示「上次试填：…」）
--     ⚠️ 不复用 BaseEntity.UpdateTime —— 那个列被「扫描 / 发布 / 改配置」共用，
--        会显示成「上次改配置的时间」，是误导。
--
-- ⛔ 不得挤占 cert_standard_directory_file.PreviewPdfPath：
--   那是「**源文件**的预览 PDF」的位置（实测 185 行在用，`52` §12.3 已警告）。
--   「试填产物」来自**空白模板**，与「源文件预览」是两个概念 ⇒ 落在本表（另一张表）。
--
-- 幂等：可重复执行（先探测列是否存在）
-- ══════════════════════════════════════════════════════════════════════════

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ──────────────────────────────────────────────────────────────────────────
-- ① PreviewPdfPath —— 试填预览 PDF 的 MinIO 路径
-- ──────────────────────────────────────────────────────────────────────────
SET @exist := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_doc_template'
    AND COLUMN_NAME  = 'PreviewPdfPath'
);

SET @sql := IF(@exist = 0,
  'ALTER TABLE cert_doc_template
     ADD COLUMN PreviewPdfPath VARCHAR(512) NULL
     COMMENT ''试填预览PDF路径 → PathBuilder.PreviewFromTemplate()（_preview/ 段下，固定key覆盖）；空=从未试填''
     AFTER StoragePath',
  'SELECT ''PreviewPdfPath 已存在，跳过'' AS msg');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ──────────────────────────────────────────────────────────────────────────
-- ② PreviewTime —— 最近一次试填时间
-- ──────────────────────────────────────────────────────────────────────────
SET @exist := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_doc_template'
    AND COLUMN_NAME  = 'PreviewTime'
);

SET @sql := IF(@exist = 0,
  'ALTER TABLE cert_doc_template
     ADD COLUMN PreviewTime DATETIME NULL
     COMMENT ''最近一次试填时间（⛔ 不复用 UpdateTime：那列被扫描/发布/改配置共用）''
     AFTER PreviewPdfPath',
  'SELECT ''PreviewTime 已存在，跳过'' AS msg');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ──────────────────────────────────────────────────────────────────────────
-- 校验（⛔ 必须 2 行，列名 PascalCase 逐字一致）
-- ──────────────────────────────────────────────────────────────────────────
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME   = 'cert_doc_template'
  AND COLUMN_NAME IN ('PreviewPdfPath', 'PreviewTime')
ORDER BY ORDINAL_POSITION;

SELECT COUNT(*) AS total,
       SUM(PreviewPdfPath IS NOT NULL) AS has_preview
FROM cert_doc_template;
