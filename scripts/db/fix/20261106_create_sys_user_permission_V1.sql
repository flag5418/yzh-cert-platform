-- ====================================================================
-- 20261106_create_sys_user_permission_V1.sql
--
-- 修复：Table 'yzh_cert_platform.sys_user_permission' doesn't exist
--
-- 背景：
--   2026-11-05 iCloud 同步灾难后数据库被 CodeFirst 全量重建
--   （Program.cs L345-346 DROP DATABASE + CREATE DATABASE）。
--   CodeFirst 的扫描条件是「继承 BaseEntity 的实体」，而
--   SysUserPermission / SysRoleApi / SysApi 三个实体不继承 BaseEntity
--   （yzh-core/Stand/Models/SysPermission.cs）⇒ 不会被自动建表。
--   其中 sys_api、sys_role_api 已由 2026-11-06 09:32 的处置建出，
--   唯独 sys_user_permission 未建 ⇒ 前端点「同步接口」时
--   PermissionCacheService.ExpandPermissionsAsync 执行
--   DELETE FROM sys_user_permission 直接抛 MySQL 1146。
--
-- 列名口径（PascalCase，与实体 / sys_role_api 现表结构一致）：
--   Id / UserCode / ApiCode / CreateTime / UpdateTime
--
-- 幂等：CREATE TABLE IF NOT EXISTS + SELECT 展开带 NOT EXISTS 去重。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1) 建表（列名与实体 SysUserPermission / 001 建表脚本对齐，PascalCase）
CREATE TABLE IF NOT EXISTS `sys_user_permission` (
  `Id`          bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `UserCode`    varchar(36)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '用户编码',
  `ApiCode`     varchar(64)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '接口编码',
  `CreateTime`  datetime     DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateTime`  datetime     DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_user_api` (`UserCode`, `ApiCode`),
  KEY `idx_user_code` (`UserCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='用户权限缓存表';

-- 2) 数据展开（等价 PermissionCacheService.ExpandPermissionsInnerAsync：
--    从 Sys_RoleUser + sys_role_api 推导；NOT EXISTS 保证幂等）
INSERT INTO `sys_user_permission` (`UserCode`, `ApiCode`, `CreateTime`)
SELECT DISTINCT ru.`UserCode`, ra.`ApiCode`, NOW()
  FROM `Sys_RoleUser` ru
  JOIN `sys_role_api` ra ON ra.`RoleCode` = ru.`RoleCode`
 WHERE NOT EXISTS (
       SELECT 1 FROM `sys_user_permission` up
        WHERE up.`UserCode` = ru.`UserCode`
          AND up.`ApiCode` = ra.`ApiCode`
 );

-- 3) 验评
--    ① 表结构（期望列名全部 PascalCase：Id/UserCode/ApiCode/CreateTime/UpdateTime）
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = 'yzh_cert_platform'
   AND TABLE_NAME = 'sys_user_permission'
 ORDER BY ORDINAL_POSITION;

--    ② 展开结果计数（当前 sys_role_api 为空 ⇒ 期望 0，仅确认不报错即可）
SELECT COUNT(*) AS user_permission_cnt FROM `sys_user_permission`;

--    ③ 关联一致性：展开表 ⊆ 角色授权推导（期望 0 行差异）
SELECT 'orphan_check' AS chk, COUNT(*) AS cnt
  FROM `sys_user_permission` up
 WHERE NOT EXISTS (
       SELECT 1 FROM `Sys_RoleUser` ru
         JOIN `sys_role_api` ra ON ra.`RoleCode` = ru.`RoleCode`
        WHERE ru.`UserCode` = up.`UserCode`
          AND ra.`ApiCode` = up.`ApiCode`
 );
