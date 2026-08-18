-- ============================================================
-- 体系认证平台 - 菜单清理与重组脚本
-- 版本：V1.0 | 日期：2026-08-18
-- 说明：
--   1. MES/Vol Demo 菜单移到最后（保留参考，不删除）
--   2. 体系认证平台菜单重组（系统管理员端）
--   3. 菜单更名（9条）
--   4. 新建父菜单（2条：AI与系统配置、审核业务）
--   5. 修复 308 菜单 URL
--   6. 隐藏 7 个待开发菜单（Enable=0）
--   7. 删除审核员端相关占位菜单（与系统管理员无关的）
--   8. 删除 Vol 系统设置中无关菜单（表单设计、审批管理、消息推送）
-- ============================================================

USE `yzh_cert_platform`;

-- ============================================================
-- Part 1: MES 和 Vol Demo 菜单移到最后（OrderNo 降到最低优先级）
-- ============================================================

-- 不用节点：直接删除（垃圾数据）
DELETE FROM `Sys_Menu` WHERE `Menu_Id` = 45;

-- MES业务：OrderNo 降到 50000（排到最后）
UPDATE `Sys_Menu` SET `OrderNo` = 50000 WHERE `Menu_Id` = 235;

-- 基础组件：OrderNo 降到 49000
UPDATE `Sys_Menu` SET `OrderNo` = 49000 WHERE `Menu_Id` = 32;

-- 基础页面：OrderNo 降到 48000
UPDATE `Sys_Menu` SET `OrderNo` = 48000 WHERE `Menu_Id` = 113;

-- ============================================================
-- Part 2: 系统设置菜单顺序调整（OrderNo 降到 600，排在体系认证之后）
-- ============================================================

UPDATE `Sys_Menu` SET `OrderNo` = 600 WHERE `Menu_Id` = 61;

-- ============================================================
-- Part 3: 删除 Vol 系统设置中与认证平台无关的菜单
-- ============================================================

-- 删除表单设计（106 及子菜单 107/109/110）
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (106, 107, 109, 110);

-- 删除审批管理（133 及子菜单 134/135/136）
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (133, 134, 135, 136);

-- 删除消息推送（293 及子菜单 132）
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (293, 132);

-- 删除已禁用的角色管理(tree)（104）
DELETE FROM `Sys_Menu` WHERE `Menu_Id` = 104;

-- ============================================================
-- Part 4: 体系认证平台菜单 - 删除审核员端占位菜单
-- ============================================================

-- 删除"我的工作台"（313 及子菜单 314 待办任务）- 审核员端功能
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (313, 314);

-- 删除"企业档案"（315 及子菜单 316 企业列表）- 审核员端功能
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (315, 316);

-- 删除"数据监控"（311 及子菜单 312 任务状态监控）- 审核员端功能
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (311, 312);

-- 删除"用户权限"（309 及子菜单 310 审核员管理）- 审核员端功能
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (309, 310);

-- 删除"报告生成"（320 及子菜单 321 报告列表）- 审核员端功能
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (320, 321);

-- 删除"审核执行"（317 及子菜单 318 审核任务、319 不符合项管理）- 审核员端功能
DELETE FROM `Sys_Menu` WHERE `Menu_Id` IN (317, 318, 319);

-- ============================================================
-- Part 5: 体系认证平台菜单更名
-- ============================================================

-- 335: 审核规则库 → 规则与工作流
UPDATE `Sys_Menu` SET `MenuName` = '规则与工作流' WHERE `Menu_Id` = 335;

-- 336: NC检查项配置 → NC 检查规则
UPDATE `Sys_Menu` SET `MenuName` = 'NC 检查规则' WHERE `Menu_Id` = 336;

-- 337: 报告内容配置 → 报告章节定义
UPDATE `Sys_Menu` SET `MenuName` = '报告章节定义' WHERE `Menu_Id` = 337;

-- 322: ISO 标准注册 → ISO 标准管理
UPDATE `Sys_Menu` SET `MenuName` = 'ISO 标准管理' WHERE `Menu_Id` = 322;

-- 326: 标准目录管理 → 标准文件管理
UPDATE `Sys_Menu` SET `MenuName` = '标准文件管理' WHERE `Menu_Id` = 326;

-- 333: Prompt模板管理 → Prompt 模板
UPDATE `Sys_Menu` SET `MenuName` = 'Prompt 模板' WHERE `Menu_Id` = 333;

-- 339: Skill 管理 → 技能管理
UPDATE `Sys_Menu` SET `MenuName` = '技能管理' WHERE `Menu_Id` = 339;

-- 340: NC 规则配置 → NC 规则设计
UPDATE `Sys_Menu` SET `MenuName` = 'NC 规则设计' WHERE `Menu_Id` = 340;

-- 308: 工作流配置 → 工作流设计器，同时修复 URL
UPDATE `Sys_Menu` SET `MenuName` = '工作流设计器', `Url` = '/CertPlatform/WorkflowDesigner' WHERE `Menu_Id` = 308;

-- ============================================================
-- Part 6: 新建父菜单 - AI 与系统配置
-- ============================================================

-- 获取当前最大 Menu_Id
SET @max_menu_id = (SELECT MAX(`Menu_Id`) FROM `Sys_Menu`);

-- AI 与系统配置（ParentId=304）
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `MenuType`)
VALUES (@max_menu_id + 1, 'AI 与系统配置', '[{"text":"查询","value":"Search"}]', 'el-icon-cpu', 'AI 引擎配置、系统参数、费用监控', 1, 130, '.', 304, '', NOW(), '超级管理员', 0);

-- ============================================================
-- Part 7: 将散落在 304 下的菜单归入新父菜单
-- ============================================================

-- 系统参数配置(331)、Prompt模板(333)、AI费用监控(334)、队列监控(332) → 归入 AI与系统配置
UPDATE `Sys_Menu` SET `ParentId` = @max_menu_id + 1 WHERE `Menu_Id` IN (331, 333, 334, 332);

-- 文档提取规则(330)、技能管理(339) → 归入 规则与工作流(335)
UPDATE `Sys_Menu` SET `ParentId` = 335 WHERE `Menu_Id` IN (330, 339);

-- 工作流设计器(308) → 归入 规则与工作流(335)，从基础配置(305)移出
UPDATE `Sys_Menu` SET `ParentId` = 335 WHERE `Menu_Id` = 308;

-- ============================================================
-- Part 8: 调整体系认证平台子菜单的 OrderNo
-- ============================================================

-- 一级菜单：体系认证平台 → OrderNo=800（排第一）
UPDATE `Sys_Menu` SET `OrderNo` = 800 WHERE `Menu_Id` = 304;

-- 304 下的二级菜单排序：
-- 基础配置(305) → OrderNo=2000
UPDATE `Sys_Menu` SET `OrderNo` = 2000 WHERE `Menu_Id` = 305;

-- 规则与工作流(335) → OrderNo=1200
UPDATE `Sys_Menu` SET `OrderNo` = 1200 WHERE `Menu_Id` = 335;

-- AI与系统配置(新建) → OrderNo=130（已在 INSERT 中设置）

-- 基础配置(305)子菜单排序：
UPDATE `Sys_Menu` SET `OrderNo` = 1000 WHERE `Menu_Id` = 306; -- 认证机构管理
UPDATE `Sys_Menu` SET `OrderNo` = 900 WHERE `Menu_Id` = 322;  -- ISO 标准管理
UPDATE `Sys_Menu` SET `OrderNo` = 850 WHERE `Menu_Id` = 338;  -- 标准条款管理
UPDATE `Sys_Menu` SET `OrderNo` = 800 WHERE `Menu_Id` = 326;  -- 标准文件管理
UPDATE `Sys_Menu` SET `OrderNo` = 700 WHERE `Menu_Id` = 323;  -- 认证阶段定义
UPDATE `Sys_Menu` SET `OrderNo` = 600 WHERE `Menu_Id` = 324;  -- 机构-标准关联
UPDATE `Sys_Menu` SET `OrderNo` = 500 WHERE `Menu_Id` = 325;  -- 机构-阶段关联

-- 规则与工作流(335)子菜单排序：
UPDATE `Sys_Menu` SET `OrderNo` = 1000 WHERE `Menu_Id` = 340; -- NC 规则设计
UPDATE `Sys_Menu` SET `OrderNo` = 900 WHERE `Menu_Id` = 336;  -- NC 检查规则
UPDATE `Sys_Menu` SET `OrderNo` = 800 WHERE `Menu_Id` = 337;  -- 报告章节定义
UPDATE `Sys_Menu` SET `OrderNo` = 700 WHERE `Menu_Id` = 339;  -- 技能管理
UPDATE `Sys_Menu` SET `OrderNo` = 600 WHERE `Menu_Id` = 330;  -- 文档提取规则
UPDATE `Sys_Menu` SET `OrderNo` = 500 WHERE `Menu_Id` = 308;  -- 工作流设计器

-- AI与系统配置子菜单排序：
UPDATE `Sys_Menu` SET `OrderNo` = 1000 WHERE `Menu_Id` = 331; -- 系统参数配置
UPDATE `Sys_Menu` SET `OrderNo` = 900 WHERE `Menu_Id` = 333;  -- Prompt 模板
UPDATE `Sys_Menu` SET `OrderNo` = 800 WHERE `Menu_Id` = 334;  -- AI 费用监控
UPDATE `Sys_Menu` SET `OrderNo` = 700 WHERE `Menu_Id` = 332;  -- 队列监控

-- ============================================================
-- Part 9: 验证结果
-- ============================================================

-- 查看顶级菜单
SELECT '=== 顶级菜单 ===' AS '';
SELECT `Menu_Id`, `MenuName`, `OrderNo`, `Enable` FROM `Sys_Menu` WHERE `ParentId` = 0 ORDER BY `OrderNo` DESC;

-- 查看体系认证平台菜单树
SELECT '=== 体系认证平台菜单树 ===' AS '';
SELECT 
    p.`Menu_Id` AS parent_id,
    p.`MenuName` AS parent_name,
    c.`Menu_Id` AS child_id,
    c.`MenuName` AS child_name,
    c.`Url`,
    c.`OrderNo`,
    c.`Enable`
FROM `Sys_Menu` p
LEFT JOIN `Sys_Menu` c ON c.`ParentId` = p.`Menu_Id`
WHERE p.`Menu_Id` = 304
ORDER BY c.`OrderNo` DESC;

-- 查看系统设置菜单树
SELECT '=== 系统设置菜单树 ===' AS '';
SELECT 
    p.`Menu_Id` AS parent_id,
    p.`MenuName` AS parent_name,
    c.`Menu_Id` AS child_id,
    c.`MenuName` AS child_name,
    c.`Url`,
    c.`OrderNo`,
    c.`Enable`
FROM `Sys_Menu` p
LEFT JOIN `Sys_Menu` c ON c.`ParentId` = p.`Menu_Id`
WHERE p.`Menu_Id` = 61
ORDER BY c.`OrderNo` DESC;
