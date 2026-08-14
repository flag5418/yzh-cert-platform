-- =====================================================
-- 迁移脚本：cert_doc_field_def 增加 is_ai_recommended 列
-- 日期：2026-08-14
-- 说明：标识字段是否为AI推荐（1=AI推荐可自动提取，0=手动添加/审核员必填字段）
--       生成提取Prompt时，只纳入 is_ai_recommended=1 的字段
-- =====================================================

USE yzh_cert_platform;

-- MySQL 8.0 不支持 ADD COLUMN IF NOT EXISTS，使用存储过程安全添加
DROP PROCEDURE IF EXISTS add_column_if_not_exists;
DELIMITER //
CREATE PROCEDURE add_column_if_not_exists()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.COLUMNS 
        WHERE TABLE_SCHEMA = 'yzh_cert_platform' 
        AND TABLE_NAME = 'cert_doc_field_def' 
        AND COLUMN_NAME = 'is_ai_recommended'
    ) THEN
        ALTER TABLE cert_doc_field_def 
        ADD COLUMN is_ai_recommended TINYINT(1) DEFAULT 1 COMMENT '是否AI推荐字段(1=是,0=手动添加)';
    END IF;
END //
DELIMITER ;
CALL add_column_if_not_exists();
DROP PROCEDURE IF EXISTS add_column_if_not_exists;

-- 存量数据全部设为 AI 推荐（1），因为已有的字段都是 AI 分析产生的
UPDATE cert_doc_field_def SET is_ai_recommended = 1 WHERE is_ai_recommended IS NULL;
