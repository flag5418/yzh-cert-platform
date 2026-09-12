-- =====================================================
-- 统一有效标志字段 IsValid 架构改造
-- 日期: 2026-09-11
-- 说明: Sys_Organization 和 Sys_User 增加 IsValid 字段
--       作为统一的有效标志（1=有效，0=无效）
--       与 BaseEntity.IsValid 架构对齐
-- =====================================================

USE yzh_cert_platform;

-- 1. Sys_Organization 增加 IsValid 字段
ALTER TABLE `Sys_Organization`
ADD COLUMN `IsValid` tinyint(4) NOT NULL DEFAULT 1 COMMENT '有效标志: 1=有效, 0=无效' AFTER `Enable`;

-- 2. Sys_User 增加 IsValid 字段
ALTER TABLE `Sys_User`
ADD COLUMN `IsValid` tinyint(4) NOT NULL DEFAULT 1 COMMENT '有效标志: 1=有效, 0=无效' AFTER `Enable`;

-- 3. 初始化：将现有记录的 IsValid 设置为 1（有效）
UPDATE `Sys_Organization` SET `IsValid` = 1 WHERE `IsValid` IS NULL;
UPDATE `Sys_User` SET `IsValid` = 1 WHERE `IsValid` IS NULL;

-- 4. 可选：与 Enable 保持同步（Enable=0 的记录，IsValid 也设为 0）
-- 注意：根据业务需求决定是否启用
-- UPDATE `Sys_Organization` SET `IsValid` = 0 WHERE `Enable` = 0;
-- UPDATE `Sys_User` SET `IsValid` = 0 WHERE `Enable` = 0;
