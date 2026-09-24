-- ============================================================
-- 001_skill_tables_V1.sql
-- Skill节点配置系统 - 数据库初始化脚本
-- 创建时间：2026-09-12 | 修订：2026-09-15（兼容 MySQL 8.0，移除 IF NOT EXISTS）
-- 说明：创建 wf_skill 主表 + 补齐 wf_skill_category 缺失列
-- 幂等性：可重复执行（存储过程判列存在）
-- ============================================================

USE yzh_cert_platform;

SET FOREIGN_KEY_CHECKS = 0;

-- Skill主表（CREATE TABLE IF NOT EXISTS 为 MySQL 标准语法，安全）
CREATE TABLE IF NOT EXISTS `wf_skill` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '唯一编码',
  `name` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Skill名称',
  `description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `category_code` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '分类编码',
  `skill_type` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'manual' COMMENT '类型：manual/api',
  `prompt_template` text COLLATE utf8mb4_general_ci COMMENT 'Prompt模板',
  `sort_order` int NOT NULL DEFAULT '0' COMMENT '排序',
  `is_valid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效 0=无效）',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `create_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_code` (`code`),
  KEY `idx_skill_category` (`category_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Skill主表';

-- wf_skill_category 补齐 is_valid 列
-- MySQL 8.0 不支持 ADD COLUMN IF NOT EXISTS（MariaDB 语法），改用存储过程判存
DROP PROCEDURE IF EXISTS `_mig_add_is_valid`;
DELIMITER $$
CREATE PROCEDURE `_mig_add_is_valid`()
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'wf_skill_category'
      AND COLUMN_NAME = 'is_valid'
  ) THEN
    ALTER TABLE `wf_skill_category`
      ADD COLUMN `is_valid` tinyint(1) NOT NULL DEFAULT 1 COMMENT '有效标志' AFTER `sort_order`;
  END IF;
END$$
DELIMITER ;

CALL `_mig_add_is_valid`();
DROP PROCEDURE `_mig_add_is_valid`;

SET FOREIGN_KEY_CHECKS = 1;
