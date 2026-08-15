-- ============================================================
-- 标准目录文件表新增 file_size 列（2026-08-15）
-- 背景：文件大小从未入库，页面显示 0K。
-- 修复：上传成功后由后端从 IFormFile.Length 记录（权威值，不依赖前端）。
-- 存量数据已通过 MinIO 对象实际大小回填（mc ls --json）。
-- ============================================================
ALTER TABLE cert_standard_directory_file
  ADD COLUMN file_size BIGINT NULL COMMENT '文件大小(字节)' AFTER FileType;
