-- ============================================================================
-- 20261028_doc_extract_rule_menu_V1.sql
-- 菜单注册：提取提示词管理
--
-- 设计依据：docs/70-V2设计/28-文档建模与执行引擎设计-V3.md §六
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 yzh_cert_platform < 本文件
--
-- 幂等：INSERT ... ON DUPLICATE KEY UPDATE
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 菜单：提取提示词管理（MENU_00222，新增）
-- 父节点 = MENU_00302（规则定义），排在 NC 规则设计(100) 之后、文档语义规则(250) 之前
INSERT INTO `sys_menu` (`Code`, `ParentCode`, `MenuName`, `Url`, `Icon`, `OrderNo`, `Tag`, `IsValid`, `CreateTime`)
VALUES ('MENU_00222', 'MENU_00302', '提取提示词管理', '/business/doc-extract-rule', 'el-icon-document', 150, 'admin', 1, NOW())
ON DUPLICATE KEY UPDATE
  `Url` = VALUES(`Url`),
  `Icon` = VALUES(`Icon`),
  `OrderNo` = VALUES(`OrderNo`),
  `Tag` = VALUES(`Tag`),
  `UpdateTime` = NOW();

-- 验证
SELECT Code, MenuName, Url, ParentCode, OrderNo FROM sys_menu WHERE Code = 'MENU_00222';
