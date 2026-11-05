-- =============================================================
-- 2026-11-05 条款管理菜单
-- · 在「基础资料 (MENU_00301)」下新增「条款管理」(MENU_ISO_CLAUSE)
-- · 路由：/cert/iso-clause ←→ CertAdmin 的 foundation/iso-clause/index.vue
-- · 权限：「仅管理员可见」（admin 角色）
-- =============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 幂等保护：重复执行不报错
INSERT INTO Sys_Menu (Code, Tag, ParentCode, MenuName, Url, Auth, Description, Icon, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT
    'MENU_ISO_CLAUSE',                      -- Code
    'admin',                                -- Tag
    'MENU_00301',                           -- ParentCode（基础资料）
    '条款管理',                              -- MenuName
    '/cert/iso-clause',                     -- Url
    '',                                     -- Auth（预留，当前无特殊限制）
    'ISO 标准条款的增删改查',                -- Description
    'Document',                             -- Icon
    850,                                    -- OrderNo（在条款配置 800 之后）
    1,                                      -- IsValid
    0,                                      -- IsDeleted
    NOW(3),                                 -- CreateTime
    'SYS_MENU_FIX'                          -- CreateBy
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM Sys_Menu WHERE Code = 'MENU_ISO_CLAUSE' OR Url = '/cert/iso-clause'
);

-- 验证
SELECT Code, MenuName, Url, OrderNo
FROM Sys_Menu
WHERE ParentCode = 'MENU_00301' AND IsDeleted = 0
ORDER BY OrderNo;
