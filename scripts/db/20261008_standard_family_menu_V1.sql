-- ============================================================================
-- 20261008_standard_family_menu_V1.sql
-- 目的：三层体系（体系→族→版本）的「标准管理」菜单
--
-- 裁决（2026-10-08「统一，都支持」· 菜单名撞车选 a）：
--   ① 原 `MENU_00201 标准管理`(/cert/iso-standard) 改名「标准条款管理」——
--      它实际管的是「标准 → 条款」两层，不是三层体系
--   ② 新增 `MENU_00219 标准管理`(/cert/standard-manage) —— 三层只读树 + 族 CRUD
--
-- ⚠️ 执行后必须跑 ./scripts/db/verify/sync_menu_urls.sh 刷新菜单快照，
--    否则守卫 R12（路由↔菜单一致性）基于过期数据。
-- 执行：docker exec -i -e MYSQL_PWD='Yzh123456.' yzh-mysql mysql -uroot \
--        --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- 幂等：按 Code 判重
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ----------------------------------------------------------------------------
-- 1. 原菜单改名：标准管理 → 标准条款管理
-- ----------------------------------------------------------------------------
UPDATE Sys_Menu
   SET MenuName   = '标准条款管理',
       Description = '标准（去岁数化）→ 条款 左树右表管理；版本实体即本页树节点',
       UpdateTime  = NOW(),
       UpdateBy    = 'standard-family-20261008'
 WHERE Code = 'MENU_00201'
   AND MenuName <> '标准条款管理';

-- ----------------------------------------------------------------------------
-- 2. 新增「标准管理」（三层体系入口）
--    ParentCode = MENU_00301（业务管理/基础资料）
--    OrderNo    = 150 —— 排在 认证机构管理(100) 之后、标准条款管理(200) 之前
-- ----------------------------------------------------------------------------
INSERT INTO Sys_Menu
       (Code, ParentCode, MenuName, Auth, Icon, Description, OrderNo, Url,
        CreateTime, CreateBy, Tag, IsDeleted, IsValid)
SELECT 'MENU_00219', 'MENU_00301', '标准管理', NULL, 'Folder',
       '体系(iso_category 字典) → 标准族(cert_standard_family) → 版本(cert_iso_standard) 三层只读树 + 族维护',
       150, '/cert/standard-manage',
       NOW(), 'standard-family-20261008', 'admin', 0, 1
  FROM DUAL
 WHERE NOT EXISTS (SELECT 1 FROM Sys_Menu WHERE Code = 'MENU_00219');

-- ----------------------------------------------------------------------------
-- 3. 验证
-- ----------------------------------------------------------------------------
SELECT '== 验证1：基础资料子菜单顺序 ==' AS t;
SELECT Code, MenuName, Url, OrderNo, Tag, IsValid, IsDeleted
  FROM Sys_Menu
 WHERE ParentCode = 'MENU_00301' AND IsDeleted = 0
 ORDER BY OrderNo;

SELECT '== 验证2：两条 Url 均唯一 ==' AS t;
SELECT Url, COUNT(*) c FROM Sys_Menu
 WHERE IsDeleted = 0 AND Url IN ('/cert/standard-manage', '/cert/iso-standard')
 GROUP BY Url;

-- ============================================================================
-- 回滚：
--   DELETE FROM Sys_Menu WHERE Code='MENU_00219';
--   UPDATE Sys_Menu SET MenuName='标准管理', Description=NULL WHERE Code='MENU_00201';
--   然后 ./scripts/db/verify/sync_menu_urls.sh
-- ============================================================================
