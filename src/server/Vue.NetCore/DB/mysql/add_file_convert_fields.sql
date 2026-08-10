-- ============================================
-- 标准目录文件表添加转换相关字段
-- 用途：支持旧版 Office 文档（.doc/.xls）自动转换为 OOXML 格式
-- 日期：2026-08-10
-- ============================================

ALTER TABLE `cert_standard_directory_file`
ADD COLUMN `converted_storage_path` VARCHAR(512) NULL COMMENT '转换后文件在 MinIO 的存储路径（.docx/.xlsx）',
ADD COLUMN `convert_status` VARCHAR(20) NULL COMMENT '转换状态：null/pending/converting/converted/failed',
ADD COLUMN `convert_message` VARCHAR(1024) NULL COMMENT '转换失败原因或丢失的样式信息',
ADD COLUMN `convert_date` DATETIME NULL COMMENT '转换完成时间';

-- 添加索引优化查询
CREATE INDEX `idx_convert_status` ON `cert_standard_directory_file`(`convert_status`);
CREATE INDEX `idx_file_type` ON `cert_standard_directory_file`(`FileType`);

-- ============================================
-- 说明：
-- 1. 存量数据 ConvertStatus 为 null，表示未转换或无需转换
-- 2. 新上传的 .doc/.xls 文件 ConvertStatus = 'pending'，进入转换队列
-- 3. 转换成功后 ConvertStatus = 'converted'，ConvertedStoragePath 指向 .converted/ 目录下的文件
-- 4. 转换失败 ConvertStatus = 'failed'，ConvertMessage 记录失败原因
-- ============================================
