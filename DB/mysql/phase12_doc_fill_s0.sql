-- ============================================================================
-- phase12 · 标准文档填写规则（37 号）—— S0 收尾
--
-- 依据：docs/20-体系认证/03-详细设计/05-企业资料规范化/
--        37-标准文档填写规则设计-V1.md
--          §4.3        _template/ 存储段（PathBuilder 三处改造，代码侧已完成）
--          §4.5(2) A   补 cert_standard_doc_contract.FixedDocSubtype（Q-13 建议 A）
--          §二          菜单（Q-4 建议 B：MENU_00218，因 00217 已被「标准核心字段」占用）
--        14 号 §D16/R7  FixedDocSubtype 列（逐字指定）
--
-- 内容：
--   ① cert_standard_doc_contract 补 FixedDocSubtype 列
--   ② 菜单 MENU_00218「标准文档填写规则」→ /business/doc-fill-rule，父 MENU_00302（规则定义）
--   ③ 角色授权（对齐 MENU_00217：ROLE_SUPER_ADMIN + ROLE_ADMIN）
--
-- ★ 幂等：本脚本可重复执行。
--   MySQL 8.0 无 `ADD COLUMN IF NOT EXISTS`（那是 MariaDB 语法）⇒ 用
--   information_schema 判定 + 预处理语句动态执行。
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 \
--         yzh_cert_platform < DB/mysql/phase12_doc_fill_s0.sql
-- ============================================================================

SET NAMES utf8mb4;

-- ----------------------------------------------------------------------------
-- ① cert_standard_doc_contract.FixedDocSubtype
--    fixed 文档的二级拆分（37 号 §4.5(2) A / 14 号 §D16）
--    ⚠️ 非 fixed 行忽略此列；默认值 'enterprise_provided' 保证存量行有合法值
-- ----------------------------------------------------------------------------
SET @ddl := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE `cert_standard_doc_contract` ADD COLUMN `FixedDocSubtype` varchar(20) NOT NULL DEFAULT ''enterprise_provided'' COMMENT ''【fixed 专用】enterprise_provided=企业提交原件(OCR抽值回流全局参数) / platform_generated=平台自动生成(纯规则,不走LLM)。非 fixed 行忽略此列'' AFTER `DocCategory`',
    'SELECT ''[skip] FixedDocSubtype 已存在'' AS msg')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_standard_doc_contract'
    AND COLUMN_NAME  = 'FixedDocSubtype'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ----------------------------------------------------------------------------
-- ② 菜单 MENU_00218
--    OrderNo=350：排在「文档提取规则」（MENU_00208, 300）之后 —— 先有提取规则，再有填写规则
-- ----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`,
   `CreateTime`, `CreateBy`, `UpdateTime`, `UpdateBy`, `Tag`, `IsDeleted`, `IsValid`)
VALUES
  ('MENU_00218', 'MENU_00302', '标准文档填写规则', NULL, 'Document',
   '为标准文档标注锚点并配置「字段填写规则 / 全文填写规则」，产出可填充的空白模板',
   350, '/business/doc-fill-rule',
   NOW(), 'seed_doc_fill_rule', NOW(), 'seed_doc_fill_rule', 'admin', 0, 1)
AS new
ON DUPLICATE KEY UPDATE
  `ParentCode`  = new.`ParentCode`,
  `MenuName`    = new.`MenuName`,
  `Icon`        = new.`Icon`,
  `Description` = new.`Description`,
  `OrderNo`     = new.`OrderNo`,
  `Url`         = new.`Url`,
  `UpdateTime`  = NOW(),
  `UpdateBy`    = 'seed_doc_fill_rule',
  `IsDeleted`   = 0,
  `IsValid`     = 1;

-- ----------------------------------------------------------------------------
-- ③ 角色授权（对齐 MENU_00217 的 ROLE_SUPER_ADMIN + ROLE_ADMIN）
--    Id 用 MD5 派生 ⇒ 确定性 + 可重复执行（Sys_RoleMenu.Id 是 varchar(64) PK）
-- ----------------------------------------------------------------------------
INSERT INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
SELECT CONCAT('RM_', MD5(CONCAT('MENU_00218|', r.RoleCode))),
       r.RoleCode, 'MENU_00218', 1800, NOW(), 'seed_doc_fill_rule'
  FROM (SELECT 'ROLE_SUPER_ADMIN' AS RoleCode
        UNION ALL
        SELECT 'ROLE_ADMIN') r
 WHERE NOT EXISTS (
   SELECT 1 FROM `Sys_RoleMenu` x
    WHERE x.RoleCode = r.RoleCode AND x.MenuCode = 'MENU_00218'
 );

-- ----------------------------------------------------------------------------
-- 验证（执行后逐段检查输出）
-- ----------------------------------------------------------------------------
SELECT '[1] FixedDocSubtype' AS step, COLUMN_NAME, COLUMN_TYPE, COLUMN_DEFAULT, IS_NULLABLE
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = 'cert_standard_doc_contract'
   AND COLUMN_NAME = 'FixedDocSubtype';

SELECT '[2] 菜单' AS step, `Code`, `ParentCode`, `MenuName`, `Url`, `OrderNo`, `IsValid`
  FROM `Sys_Menu` WHERE `Code` = 'MENU_00218';

SELECT '[3] 授权' AS step, `RoleCode`, `MenuCode`, `OrderNo`
  FROM `Sys_RoleMenu` WHERE `MenuCode` = 'MENU_00218' ORDER BY `RoleCode`;

SELECT '[4] 规则定义菜单全览（确认编号无冲突）' AS step,
       `Code`, `MenuName`, `Url`, `OrderNo`
  FROM `Sys_Menu`
 WHERE `IsDeleted` = 0 AND `ParentCode` = 'MENU_00302'
 ORDER BY `OrderNo`;
