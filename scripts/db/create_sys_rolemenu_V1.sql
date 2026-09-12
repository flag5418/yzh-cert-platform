-- ============================================================
-- 角色-菜单关联表 V1
-- 日期：2026-09-12
-- 目标：为「角色-菜单」功能提供 Code 关联表（与 Sys_RoleUser 完全对称）
-- 设计原则：Code 是唯一业务键，所有关联必须用 Code
--   关联方式：
--     RoleCode → Sys_Role.Code
--     MenuCode → Sys_Menu.Code
-- ============================================================

-- ------------------------------------------------------------
-- 1. 建表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Sys_RoleMenu (
    Id          varchar(64)  NOT NULL COMMENT '主键',
    RoleCode    varchar(64)  NOT NULL COMMENT '角色编码（Sys_Role.Code）',
    MenuCode    varchar(50)  NOT NULL COMMENT '菜单编码（Sys_Menu.Code）',
    OrderNo     int          NULL     COMMENT '排序号',
    CreateTime  datetime     DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    CreateBy    varchar(64)  NULL     COMMENT '创建人',
    PRIMARY KEY (Id),
    UNIQUE KEY uk_role_menu (RoleCode, MenuCode),
    KEY idx_rm_role (RoleCode),
    KEY idx_rm_menu (MenuCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='角色-菜单关联表';

-- ------------------------------------------------------------
-- 2. 迁移历史数据（来自 Sys_RoleAuth）
--    只迁移「角色级」且能匹配到现存菜单的行，避免脏数据。
--    Sys_RoleAuth 中指向已删除菜单（MES/Demo）的行会被自动忽略。
-- ------------------------------------------------------------
INSERT IGNORE INTO Sys_RoleMenu (Id, RoleCode, MenuCode, CreateTime, CreateBy)
SELECT REPLACE(UUID(), '-', ''), ra.RoleCode, m.Code, NOW(), 'migration'
FROM Sys_RoleAuth ra
JOIN Sys_Menu m ON ra.Menu_Id = m.Menu_Id
WHERE ra.User_Id IS NULL
  AND ra.RoleCode IS NOT NULL AND ra.RoleCode <> ''
  AND m.Code IS NOT NULL AND m.Code <> ''
GROUP BY ra.RoleCode, m.Code;

-- ------------------------------------------------------------
-- 3. 验证
-- ------------------------------------------------------------
SELECT COUNT(*) AS migrated_rows FROM Sys_RoleMenu;
SELECT RoleCode, COUNT(*) AS menu_count FROM Sys_RoleMenu GROUP BY RoleCode ORDER BY menu_count DESC;
