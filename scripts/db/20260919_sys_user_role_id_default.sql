-- ============================================================
-- 20260919_sys_user_role_id_default.sql
-- 目标：修复「机构-人员管理 → 新增人员」必然失败的问题
--
-- 现象：
--   POST /api/Organization/add 返回
--     新增失败：Field 'Role_Id' doesn't have a default value
--
-- 原因：
--   Role_Id 是 YZH 框架遗留的「单角色」外键列（见 all_tables_ddl.sql 中 Sys_User 的 DDL：
--   `Role_Id` int NOT NULL，且无 DEFAULT）。原 sys_user 视图 v_sys_user 依赖它关联 sys_role。
--   新模型改为通过 Sys_RoleUser 做「用户-角色」多对多，因此
--   YZH.Core.Api/Models/Users/Sys_User.cs 里【故意没有】Role_Id 属性 ——
--   SqlSugar 生成 INSERT 时不会带上该列，而列又是 NOT NULL 且无默认值，于是必然报错。
--
-- 依据：
--   库中已有 4 个用户的 Role_Id = 0（即历史上就是「无遗留单角色」的写法），
--   本脚本沿用该既有约定，只补一个 DEFAULT 0，不改语义、不动数据、不加实体属性。
--
-- 影响：
--   仅放宽 INSERT 路径；已有行与 v_sys_user 视图行为不变（左连接，NULL/0 均安全）。
--   新增人员的角色授权请走「角色-人员管理」（Sys_RoleUser）。
--
-- 幂等性：重复执行安全（MODIFY COLUMN 幂等）。
-- ============================================================

ALTER TABLE `Sys_User`
    MODIFY COLUMN `Role_Id` INT NOT NULL DEFAULT 0
        COMMENT '遗留单角色字段（新模型用 Sys_RoleUser 多对多，此处保留默认 0）';

-- 校验：默认值应变成 0，且 NOT NULL 保持不变
-- SHOW COLUMNS FROM `Sys_User` LIKE 'Role_Id';
-- 期望：Field=Role_Id  Type=int  Null=NO  Default=0
