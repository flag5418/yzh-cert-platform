-- ============================================================
-- Sys_Role.ParentId 补默认值 V1
-- ------------------------------------------------------------
-- 背景（2026-10-07 实测）：
--   新架构「角色-人员管理」页面要开放角色增删改，后端走
--   TreeTableControllerBase.AddTreeNode → TreeEntity.Insert(entity)。
--   实体 Sys_Role **没有** ParentId 属性（新架构树键是 ParentCode），
--   SqlSugar 生成的 INSERT 不含该列；而 DB 侧：
--       `ParentId` int NOT NULL   ← 无 DEFAULT
--   且 @@sql_mode 含 STRICT_TRANS_TABLES →
--       ERROR 1364: Field 'ParentId' doesn't have a default value
--   ⇒ /api/Role/tree/add 必失败（单表 /add 同理）。
--
-- 取值依据：
--   存量 14 行中 12 行 ParentId = 0（其余 1、2 为历史 Vol 数据）；
--   新架构不读写该列（实体未声明、不参与树关系），
--   故 DEFAULT 0 与存量语义一致，不改任何已有行。
--
-- 生成: 2026-10-07 | 幂等: 可重复执行
-- 执行: mysql < 本文件（库 yzh_cert_platform）
-- ============================================================

ALTER TABLE `Sys_Role` ALTER COLUMN `ParentId` SET DEFAULT 0;

-- ------------------------------------------------------------
-- 验证 SQL（期望：PARENTID_DEFAULT = 0）
-- ------------------------------------------------------------
SELECT
  COLUMN_NAME,
  IS_NULLABLE,
  COLUMN_DEFAULT AS PARENTID_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME   = 'Sys_Role'
  AND COLUMN_NAME  = 'ParentId';

-- 冒烟（期望：插入成功后立即回滚，不留数据）
START TRANSACTION;
INSERT INTO Sys_Role (Code, RoleName, ParentCode, IsValid, IsDeleted, CreateTime)
VALUES ('__SMOKE_PARENTID__', 'ParentId 冒烟', NULL, 1, 0, NOW());
ROLLBACK;
