-- =================================================================--
-- 20261105_disaster_seed_minimal_V1.sql — 灾难恢复 · 最小种子数据
--
-- 场景：iCloud 同步灾难后数据库全空，CodeFirst 建表完成后填充最底限度数据，
--       让系统能登录（admin）+ 角色-用户关联 + 基础字典。
-- 幂等：脚本可重复跑（INSERT IGNORE 兜底）。
-- 密码：admin/123456，AES 密文通过以下方式生成
--       IV  = 0x000102030405060708090A0B0C0D0E0F
--       Key = appsettings.json PasswordSecret 前 16 字节 = "yzh_dev_secret_k"
--       明文 "123456" → AES-128-CBC → Base64 → 替换 +/ 为 _~
--       = "MtLleSqH5gOzl~tFS_qW8w=="
-- =================================================================--

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1) 机构根节点（Platform = 平台根节点，系统约定码）
INSERT IGNORE INTO Sys_Organization (Code, OrgName, OrgType, OrgLevel, OrgPath, Sort, IsValid, IsDeleted, CreateTime)
VALUES ('Platform', '映智汇认证平台', 'Platform', 1, '/Platform', 1, 1, 0, NOW());

-- 2) 角色 —— 超级管理员 + 审核员
INSERT IGNORE INTO Sys_Role (Code, RoleName, IsValid, IsDeleted, CreateTime) VALUES
('ROLE_SUPER_ADMIN', '超级管理员', 1, 0, NOW()),
('ROLE_AUDITOR',       '审核员',     1, 0, NOW());

-- 3) 用户 —— admin / 123456（UserPwd = AES 密文）
INSERT IGNORE INTO Sys_User (Code, UserName, UserPwd, UserTrueName, OrgCode, IsValid, IsDeleted, CreateTime)
VALUES ('U_ADMIN', 'admin', 'MtLleSqH5gOzl~tFS_qW8w==', '系统管理员', 'Platform', 1, 0, NOW());

-- 4) 角色-用户关联
INSERT IGNORE INTO Sys_RoleUser (Id, RoleCode, UserCode, OrderNo, CreateTime) VALUES
('RU_ADMIN', 'ROLE_SUPER_ADMIN', 'U_ADMIN', 1, NOW());

-- 5) 字典 —— 机构类型（Sys_Dictionary / Sys_DictionaryList）
--    列名 = DicName（显示文本）/ DicValue（取值）/ OrderNo（排序），无 DicText/Sort 列
INSERT IGNORE INTO Sys_Dictionary (Code, DicName, DicNo, Remark, IsValid, IsDeleted, CreateTime) VALUES
('DIC_ORG_TYPE', '机构类型', 'org_type', '平台内置机构分类字典', 1, 0, NOW());

INSERT IGNORE INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime) VALUES
('DL_ORG_PLATFORM',    'DIC_ORG_TYPE', '平台',              'Platform',    1, 1, 0, NOW()),
('DL_ORG_CERT_BODY',   'DIC_ORG_TYPE', '认证机构',           'CertBody',    2, 1, 0, NOW()),
('DL_ORG_VIRTUAL_ORG', 'DIC_ORG_TYPE', '虚拟企业/专家工作区', 'VirtualOrg',  3, 1, 0, NOW()),
('DL_ORG_ENTERPRISE',  'DIC_ORG_TYPE', '企业',              'Enterprise',  4, 1, 0, NOW());

SELECT '✅ 种子数据写入完成' AS result;
