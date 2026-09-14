-- ============================================================
-- 修复 ent_enterprise 表：列名统一 + 补齐缺失列
-- 创建时间：2026-09-13
-- ============================================================

-- ──── 1. 重命名 snake_case 列 → PascalCase ────
ALTER TABLE `ent_enterprise` CHANGE `creator` `CreateBy` varchar(50);
ALTER TABLE `ent_enterprise` CHANGE `create_date` `CreateTime` datetime;
ALTER TABLE `ent_enterprise` CHANGE `modifier` `UpdateBy` varchar(50);
ALTER TABLE `ent_enterprise` CHANGE `modify_date` `UpdateTime` datetime;
ALTER TABLE `ent_enterprise` CHANGE `deleter` `DeleteBy` varchar(50);
ALTER TABLE `ent_enterprise` CHANGE `delete_time` `DeleteTime` datetime;
ALTER TABLE `ent_enterprise` CHANGE `delete_by` `DeleteBy2` varchar(50);  -- 临时避免重名

-- 删除多余的 DeleteBy2（原 delete_by 列）
ALTER TABLE `ent_enterprise` DROP COLUMN `DeleteBy2`;

-- 修复 status → Status
ALTER TABLE `ent_enterprise` CHANGE `status` `Status` varchar(50);

-- ──── 2. 补齐缺失的业务列 ────
ALTER TABLE `ent_enterprise` ADD COLUMN `EnterpriseNo` varchar(20) NOT NULL COMMENT '企业编号' AFTER `OrgCode`;
ALTER TABLE `ent_enterprise` ADD COLUMN `Province` varchar(50) COMMENT '省份' AFTER `Address`;
ALTER TABLE `ent_enterprise` ADD COLUMN `City` varchar(50) COMMENT '城市' AFTER `Province`;
ALTER TABLE `ent_enterprise` ADD COLUMN `IndustryType` varchar(100) COMMENT '行业类型' AFTER `City`;
ALTER TABLE `ent_enterprise` ADD COLUMN `EmployeeCount` int COMMENT '员工人数' AFTER `IndustryType`;

-- 添加唯一索引
ALTER TABLE `ent_enterprise` ADD UNIQUE INDEX `uk_enterprise_no` (`EnterpriseNo`);

-- ──── 3. 验证 ────
SELECT 'ent_enterprise 修复完成' AS result;
SHOW COLUMNS FROM `ent_enterprise`;
