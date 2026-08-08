-- ============================================================
-- 体系认证平台 - ISO标准与认证阶段重构 SQL 脚本
-- 
-- 设计决策（2026-08-07 确认）：
-- 1. ISO标准和认证阶段是全局基础资料，多机构复用
-- 2. 通过关联表实现机构-标准/阶段的多对多关系
-- 3. 新建机构时自动在 cert_org_stage 中插入全部阶段
-- 4. 关联操作：左树(机构) + 右checkbox表格，勾选即保存(Auto模式)
--
-- 执行方式：
--   docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform < this_file.sql
-- ============================================================

USE `yzh_cert_platform`;

-- ============================================================
-- Step 0: 字典数据
-- ============================================================

SET @cert_dict_id = (SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'cert_dict' LIMIT 1);

-- A. 认证阶段分类字典
INSERT IGNORE INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('认证阶段分类', 'stage_category', 1, @cert_dict_id, 40, NOW(), 1, '超级管理员');

SET @stage_cat_id = (SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'stage_category' LIMIT 1);

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `Remark`, `CreateDate`, `CreateID`, `Creator`) VALUES
('流程阶段', 'process', @stage_cat_id, 1, 10, '申请受理/合同评审等流程环节', NOW(), 1, '超级管理员'),
('审核阶段', 'audit',   @stage_cat_id, 1, 20, '一阶/二阶审核', NOW(), 1, '超级管理员'),
('证后阶段', 'post',    @stage_cat_id, 1, 30, '监督/再认证', NOW(), 1, '超级管理员')
ON DUPLICATE KEY UPDATE DicName = VALUES(DicName);

-- B. 认证阶段状态字典（复用 standard_status 的值）
INSERT IGNORE INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('认证阶段状态', 'stage_status', 1, @cert_dict_id, 41, NOW(), 1, '超级管理员');

SET @stage_status_id = (SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'stage_status' LIMIT 1);

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `Remark`, `CreateDate`, `CreateID`, `Creator`) VALUES
('启用',   'active',    @stage_status_id, 1, 10, '阶段可用', NOW(), 1, '超级管理员'),
('停用',   'inactive',  @stage_status_id, 1, 20, '阶段不可用', NOW(), 1, '超级管理员')
ON DUPLICATE KEY UPDATE DicName = VALUES(DicName);

-- C. ISO 标准分类字典
INSERT IGNORE INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('ISO标准分类', 'iso_category', 1, @cert_dict_id, 42, NOW(), 1, '超级管理员');

SET @iso_cat_id = (SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'iso_category' LIMIT 1);

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `Remark`, `CreateDate`, `CreateID`, `Creator`) VALUES
('质量管理', 'quality',     @iso_cat_id, 1, 10, 'ISO 9001 等', NOW(), 1, '超级管理员'),
('环境管理', 'environment', @iso_cat_id, 1, 20, 'ISO 14001 等', NOW(), 1, '超级管理员'),
('职业健康安全', 'safety',      @iso_cat_id, 1, 30, 'ISO 45001 等', NOW(), 1, '超级管理员'),
('信息安全', 'info',        @iso_cat_id, 1, 40, 'ISO 27001 等', NOW(), 1, '超级管理员'),
('食品安全', 'food',        @iso_cat_id, 1, 50, 'ISO 22000 等', NOW(), 1, '超级管理员'),
('医疗器械', 'medical',     @iso_cat_id, 1, 60, 'ISO 13485 等', NOW(), 1, '超级管理员'),
('能源管理', 'energy',      @iso_cat_id, 1, 70, 'ISO 50001 等', NOW(), 1, '超级管理员')
ON DUPLICATE KEY UPDATE DicName = VALUES(DicName);

SELECT '✅ 字典数据完成' AS Result;

-- ============================================================
-- Step 1: cert_cert_stage — 认证阶段（全局基础资料）
-- 基于 ISO/IEC 17021-1:2015 规定的 7 个核心阶段 + 2 个扩展阶段
-- ============================================================

DROP TABLE IF EXISTS `cert_cert_stage`;

CREATE TABLE `cert_cert_stage` (
    `Id`          BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键',
    `Code`        VARCHAR(50) NOT NULL COMMENT '业务编码(自动生成)',
    `StageCode`   VARCHAR(50) NOT NULL COMMENT '阶段编码(STAGE-01 ~ STAGE-09)',
    `StageName`   VARCHAR(200) NOT NULL COMMENT '阶段名称',
    `SortOrder`   INT DEFAULT 0 COMMENT '排序号(决定流程顺序)',
    `Category`    VARCHAR(50) DEFAULT 'process' COMMENT '分类: process=流程阶段, audit=审核类型, post=证后',
    `Status`      VARCHAR(50) DEFAULT 'active' COMMENT '状态: active/inactive',
    `Remark`      VARCHAR(500) DEFAULT '' COMMENT '备注',
    
    -- YZHBaseEntity 审计字段
    `OrgCode`     VARCHAR(50) DEFAULT '' COMMENT '多租户编码',
    `Enable`       TINYINT(1) DEFAULT 1 COMMENT '启用状态: 1=启用 0=禁用/已删除',
    `CreateID`     INT DEFAULT NULL COMMENT '创建人ID',
    `Creator`      VARCHAR(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate`   DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID`     INT DEFAULT NULL COMMENT '修改人ID',
    `Modifier`     VARCHAR(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate`   DATETIME DEFAULT NULL COMMENT '修改时间',
    `DeleteID`     INT DEFAULT NULL COMMENT '删除人ID',
    `Deleter`      VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime`   DATETIME DEFAULT NULL COMMENT '删除时间',
    
    UNIQUE KEY `uk_code` (`Code`),
    INDEX `idx_sort` (`SortOrder`),
    INDEX `idx_category` (`Category`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='认证阶段(全局基础资料, 基于 ISO/IEC 17021-1)';

-- 插入 9 个标准阶段数据
INSERT INTO `cert_cert_stage` (`Code`, `StageCode`, `StageName`, `SortOrder`, `Category`, `Status`, `Creator`) VALUES
('STAGE-2026080701', 'STAGE-01', '申请受理',       10, 'process', 'active', '系统'),
('STAGE-2026080702', 'STAGE-02', '合同评审',       20, 'process', 'active', '系统'),
('STAGE-2026080703', 'STAGE-03', '审核方案策划',     30, 'process', 'active', '系统'),
('STAGE-2026080704', 'STAGE-04', '第一阶段审核',     40, 'audit',   'active', '系统'),
('STAGE-2026080705', 'STAGE-05', '第二阶段审核',     50, 'audit',   'active', '系统'),
('STAGE-2026080706', 'STAGE-06', '认证决定',         60, 'process', 'active', '系统'),
('STAGE-2026080707', 'STAGE-07', '颁发证书',         70, 'post',    'active', '系统'),
('STAGE-2026080708', 'STAGE-08', '监督审核',         80, 'post',    'active', '系统'),
('STAGE-2026080709', 'STAGE-09', '再认证',           90, 'post',    'active', '系统');

SELECT CONCAT('✅ cert_cert_stage 完成, 插入 ', ROW_COUNT(), ' 条记录') AS Result;

-- ============================================================
-- Step 2: cert_org_standard — 机构-标准关联表（多对多）
-- ============================================================

DROP TABLE IF EXISTS `cert_org_standard`;

CREATE TABLE `cert_org_standard` (
    `Id`          BIGINT PRIMARY KEY AUTO_INCREMENT,
    `CbCode`      VARCHAR(50) NOT NULL COMMENT '认证机构编码(cert_certification_body.Code)',
    `StdId`       BIGINT NOT NULL COMMENT '标准ID(cert_iso_standard.Id)',
    `StdCode`     VARCHAR(100) NOT NULL COMMENT '标准编号(冗余,方便查询)',
    `EnabledAt`   DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '启用时间',
    `Remark`      VARCHAR(500) DEFAULT '' COMMENT '备注',
    
    INDEX `idx_cbcode` (`CbCode`),
    INDEX `idx_stdid` (`StdId`),
    UNIQUE KEY `uk_org_std` (`CbCode`, `StdId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='机构-标准关联表(多对多)';

-- 迁移现有数据：将现有 cert_iso_standard 表中的 CbCode 关系迁移过来
INSERT IGNORE INTO `cert_org_standard` (`CbCode`, `StdId`, `StdCode`, `EnabledAt`)
SELECT DISTINCT 
    isd.CbCode, 
    isd.Id AS StdId, 
    COALESCE(isd.StandardCode, '') AS StdCode,
    NOW()
FROM `cert_iso_standard` isd
WHERE isd.CbCode IS NOT NULL AND isd.CbCode != '' AND isd.Enable = 1;

SELECT CONCAT('✅ cert_org_standard 完成, 迁入 ', ROW_COUNT(), ' 条历史关联') AS Result;

-- ============================================================
-- Step 3: cert_org_stage — 机构-阶段关联表（多对多）
-- 新建机构时自动在此表中插入全部阶段记录
-- ============================================================

DROP TABLE IF EXISTS `cert_org_stage`;

CREATE TABLE `cert_org_stage` (
    `Id`          BIGINT PRIMARY KEY AUTO_INCREMENT,
    `CbCode`      VARCHAR(50) NOT NULL COMMENT '认证机构编码(cert_certification_body.Code)',
    `StageId`     BIGINT NOT NULL COMMENT '阶段ID(cert_cert_stage.Id)',
    `StageCode`   VARCHAR(50) NOT NULL COMMENT '阶段编码(冗余)',
    `EnabledAt`   DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '启用时间',
    `Remark`      VARCHAR(500) DEFAULT '' COMMENT '备注',
    
    INDEX `idx_cbcode` (`CbCode`),
    INDEX `idx_stageid` (`StageId`),
    UNIQUE KEY `uq_org_stage` (`CbCode`, `StageId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='机构-阶段关联表(多对多, 默认全选)';

-- 为现有机构初始化全部阶段（默认全选策略）
INSERT IGNORE INTO `cert_org_stage` (`CbCode`, `StageId`, `StageCode`, `EnabledAt`)
SELECT 
    cb.Code AS CbCode,
    cs.Id AS StageId,
    cs.StageCode,
    NOW()
FROM `cert_certification_body` cb
CROSS JOIN `cert_cert_stage` cs
WHERE cb.Enable = 1 AND cs.Enable = 1;

SELECT CONCAT('✅ cert_org_stage 完成, 初始化 ', ROW_COUNT(), ' 条机构-阶段关联') AS Result;

-- ============================================================
-- Step 4: 移除 cert_iso_standard 的 CbCode 字段
-- （现在标准是全局基础资料，不再属于某个机构）
-- ============================================================

-- 先检查是否有 CbCode 列
SELECT COUNT(*) INTO @has_column 
FROM information_schema.COLUMNS 
WHERE TABLE_SCHEMA = 'yzh_cert_platform' 
  AND TABLE_NAME = 'cert_iso_standard' 
  AND COLUMN_NAME = 'CbCode';

-- 如果有则删除（数据已迁移到 cert_org_standard）
SET @sql = IF(@has_column > 0, 
    'ALTER TABLE `cert_iso_standard` DROP COLUMN `CbCode`',
    'SELECT "CbCode 列不存在，跳过" AS info'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT '✅ cert_iso_standard.CbCode 字段已移除（或本来就不存在）' AS Result;

-- ============================================================
-- Step 5: 给 cert_iso_standard 补充缺失字段
-- ============================================================

SET @has_desc = (SELECT COUNT(*) FROM information_schema.COLUMNS 
    WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_iso_standard' AND COLUMN_NAME = 'Description');
SET @has_cat = (SELECT COUNT(*) FROM information_schema.COLUMNS 
    WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_iso_standard' AND COLUMN_NAME = 'Category');

IF @has_desc = 0 THEN
    ALTER TABLE `cert_iso_standard` ADD COLUMN `Description` TEXT COMMENT '标准描述';
END IF;

IF @has_cat = 0 THEN
    ALTER TABLE `cert_iso_standard` ADD COLUMN `Category` VARCHAR(50) DEFAULT 'quality' COMMENT '分类: quality/safety/environment/info/food/medical';
END IF;

-- 更新现有数据的 Category
UPDATE `cert_iso_standard` SET Category = 'medical' WHERE StandardCode LIKE '%13485%' OR StandardCode LIKE '%13488%';
UPDATE `cert_iso_standard` SET Category = 'quality' WHERE Category IS NULL OR Category = '';

SELECT '✅ cert_iso_standard 补充字段完成' AS Result;

-- ============================================================
-- 验证
-- ============================================================

SELECT '=== 验证：认证阶段 ===' AS Info;
SELECT Id, StageCode, StageName, SortOrder, Category, Status FROM `cert_cert_stage` ORDER BY SortOrder;

SELECT '=== 验证：机构-标准关联 ===' AS Info;
SELECT os.Id, os.CbCode, cb.Name AS OrgName, os.StdCode, os.EnabledAt 
FROM `cert_org_standard` os LEFT JOIN `cert_certification_body` cb ON os.CbCode = cb.Code LIMIT 10;

SELECT '=== 验证：机构-阶段关联 ===' AS Info;
SELECT ostage.Id, ostage.CbCode, cb.Name AS OrgName, cs.StageName, ostage.StageCode
FROM `cert_org_stage` ostage 
LEFT JOIN `cert_certification_body` cb ON ostage.CbCode = cb.Code
LEFT JOIN `cert_cert_stage` cs ON ostage.StageId = cs.Id
ORDER BY cb.Name, cs.SortOrder
LIMIT 20;

SELECT '=== 全部完成 ✅ ===' AS FinalResult;
