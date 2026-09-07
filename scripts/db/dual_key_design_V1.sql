-- ============================================================
-- 双键设计 V1：核心表 Code 字段 + Tag 分类
-- 日期：2026-09-06
-- 目标：为 Sys_User, Sys_Role, Sys_Menu, Sys_RoleAuth 添加 Code 稳定标识符
-- ============================================================

-- --------------------------------------------------------
-- 1. Sys_User 添加 Code 字段
-- --------------------------------------------------------
ALTER TABLE sys_user ADD COLUMN Code varchar(50) NULL COMMENT '业务唯一编码' AFTER User_Id;
CREATE UNIQUE INDEX uk_user_code ON sys_user(Code);

-- 为现有数据生成 Code（格式：USER_ + 6位零填充ID）
UPDATE sys_user SET Code = CONCAT('USER_', LPAD(User_Id, 6, '0')) WHERE Code IS NULL;

-- --------------------------------------------------------
-- 2. Sys_Role 添加 Code 字段
-- --------------------------------------------------------
ALTER TABLE Sys_Role ADD COLUMN Code varchar(50) NULL COMMENT '业务唯一编码' AFTER Role_Id;
CREATE UNIQUE INDEX uk_role_code ON Sys_Role(Code);

-- 为现有数据生成 Code
UPDATE Sys_Role SET Code = CONCAT('ROLE_', LPAD(Role_Id, 6, '0')) WHERE Code IS NULL;

-- 手动修正核心角色编码（更语义化）
UPDATE Sys_Role SET Code = 'ROLE_SUPER_ADMIN' WHERE Role_Id = 1;
UPDATE Sys_Role SET Code = 'ROLE_ADMIN' WHERE Role_Id = 10;
UPDATE Sys_Role SET Code = 'ROLE_OPS' WHERE Role_Id = 13;
UPDATE Sys_Role SET Code = 'ROLE_CONFIG' WHERE Role_Id = 14;
UPDATE Sys_Role SET Code = 'ROLE_QUALITY' WHERE Role_Id = 15;
UPDATE Sys_Role SET Code = 'ROLE_AUDIT_CLIENT_ADMIN' WHERE Role_Id = 200;

-- --------------------------------------------------------
-- 3. Sys_Menu 添加 Code 字段 + Tag 分类字段
-- --------------------------------------------------------
ALTER TABLE Sys_Menu ADD COLUMN Code varchar(50) NULL COMMENT '业务唯一编码' AFTER Menu_Id;
ALTER TABLE Sys_Menu ADD COLUMN Tag varchar(20) NULL COMMENT '菜单分类标签：admin/auditor/enterprise/common' AFTER MenuType;
CREATE UNIQUE INDEX uk_menu_code ON Sys_Menu(Code);

-- 为现有数据生成 Code（格式：MENU_ + 6位零填充ID）
UPDATE Sys_Menu SET Code = CONCAT('MENU_', LPAD(Menu_Id, 6, '0')) WHERE Code IS NULL;

-- 手动修正核心菜单编码（使用 Menu_Id 唯一标识）
-- 系统设置（根节点）
UPDATE Sys_Menu SET Code = 'MENU_SYSTEM_ROOT', Tag = 'admin' WHERE Menu_Id = 61;
-- 系统设置子菜单
UPDATE Sys_Menu SET Code = 'MENU_USER_MGMT', Tag = 'admin' WHERE Menu_Id = 9;  -- 用户管理（启用）
UPDATE Sys_Menu SET Code = 'MENU_USER_MGMT_DISABLE', Tag = 'admin' WHERE Menu_Id = 2;  -- 用户管理（禁用）
UPDATE Sys_Menu SET Code = 'MENU_ROLE_MGMT', Tag = 'admin' WHERE Menu_Id = 3;
UPDATE Sys_Menu SET Code = 'MENU_LOG', Tag = 'admin' WHERE Menu_Id = 6;
UPDATE Sys_Menu SET Code = 'MENU_MENU_SETTING', Tag = 'admin' WHERE Menu_Id = 62;
UPDATE Sys_Menu SET Code = 'MENU_DICT', Tag = 'admin' WHERE Menu_Id = 63;
UPDATE Sys_Menu SET Code = 'MENU_AUTH', Tag = 'admin' WHERE Menu_Id = 71;
UPDATE Sys_Menu SET Code = 'MENU_ORG', Tag = 'admin' WHERE Menu_Id = 142;

-- 认证平台（根节点）
UPDATE Sys_Menu SET Code = 'MENU_CERT_ROOT', Tag = 'common' WHERE Menu_Id = 304;
-- 认证平台子菜单
UPDATE Sys_Menu SET Code = 'MENU_CERT_BODY', Tag = 'common' WHERE Menu_Id = 306;
UPDATE Sys_Menu SET Code = 'MENU_ISO_STD', Tag = 'common' WHERE Menu_Id = 322;
UPDATE Sys_Menu SET Code = 'MENU_CERT_STAGE', Tag = 'common' WHERE Menu_Id = 323;
UPDATE Sys_Menu SET Code = 'MENU_ORG_STD', Tag = 'common' WHERE Menu_Id = 324;
UPDATE Sys_Menu SET Code = 'MENU_ORG_STAGE', Tag = 'common' WHERE Menu_Id = 325;
UPDATE Sys_Menu SET Code = 'MENU_STD_FILE', Tag = 'common' WHERE Menu_Id = 326;
UPDATE Sys_Menu SET Code = 'MENU_DOC_RULE', Tag = 'common' WHERE Menu_Id = 330;
UPDATE Sys_Menu SET Code = 'MENU_SYS_CONFIG', Tag = 'common' WHERE Menu_Id = 331;
UPDATE Sys_Menu SET Code = 'MENU_QUEUE', Tag = 'common' WHERE Menu_Id = 332;
UPDATE Sys_Menu SET Code = 'MENU_PROMPT', Tag = 'common' WHERE Menu_Id = 333;
UPDATE Sys_Menu SET Code = 'MENU_AI_COST', Tag = 'common' WHERE Menu_Id = 334;
UPDATE Sys_Menu SET Code = 'MENU_NC_RULE', Tag = 'common' WHERE Menu_Id = 336;
UPDATE Sys_Menu SET Code = 'MENU_RPT_SECTION', Tag = 'common' WHERE Menu_Id = 337;
UPDATE Sys_Menu SET Code = 'MENU_STD_CLAUSE', Tag = 'common' WHERE Menu_Id = 338;
UPDATE Sys_Menu SET Code = 'MENU_SKILL', Tag = 'common' WHERE Menu_Id = 339;
UPDATE Sys_Menu SET Code = 'MENU_NC_DESIGN', Tag = 'common' WHERE Menu_Id = 340;
UPDATE Sys_Menu SET Code = 'MENU_RPT_DESIGN', Tag = 'common' WHERE Menu_Id = 342;

-- 设置现有菜单默认 Tag（后续可按需调整）
UPDATE Sys_Menu SET Tag = 'common' WHERE Tag IS NULL;

-- --------------------------------------------------------
-- 4. Sys_RoleAuth 添加 Code 关联字段
-- --------------------------------------------------------
ALTER TABLE Sys_RoleAuth ADD COLUMN MenuCode varchar(50) NULL COMMENT '菜单编码' AFTER Menu_Id;
ALTER TABLE Sys_RoleAuth ADD COLUMN RoleCode varchar(50) NULL COMMENT '角色编码' AFTER Role_Id;
ALTER TABLE Sys_RoleAuth ADD COLUMN UserCode varchar(50) NULL COMMENT '用户编码' AFTER User_Id;

-- 回填 Code 数据（基于现有 ID 关联）
UPDATE Sys_RoleAuth ra
JOIN Sys_Menu m ON ra.Menu_Id = m.Menu_Id
SET ra.MenuCode = m.Code;

UPDATE Sys_RoleAuth ra
JOIN Sys_Role r ON ra.Role_Id = r.Role_Id
SET ra.RoleCode = r.Code;

UPDATE Sys_RoleAuth ra
JOIN sys_user u ON ra.User_Id = u.User_Id
SET ra.UserCode = u.Code;

-- --------------------------------------------------------
-- 5. 创建关联索引（提升 Code 查询性能）
-- --------------------------------------------------------
CREATE INDEX idx_roleauth_menu_code ON Sys_RoleAuth(MenuCode);
CREATE INDEX idx_roleauth_role_code ON Sys_RoleAuth(RoleCode);
CREATE INDEX idx_roleauth_user_code ON Sys_RoleAuth(UserCode);

-- --------------------------------------------------------
-- 6. 验证数据完整性
-- --------------------------------------------------------
SELECT 'Sys_User' AS TableName, COUNT(*) AS TotalRows, COUNT(Code) AS HasCode FROM sys_user
UNION ALL
SELECT 'Sys_Role', COUNT(*), COUNT(Code) FROM Sys_Role
UNION ALL
SELECT 'Sys_Menu', COUNT(*), COUNT(Code) FROM Sys_Menu
UNION ALL
SELECT 'Sys_RoleAuth', COUNT(*), COUNT(MenuCode) FROM Sys_RoleAuth;
