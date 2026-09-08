-- ============================================================
-- Sys_Organization 组织机构表
-- 基础字段，支持树形结构（ParentCode 构建层级）
-- 预初始化三类根节点：平台管理/认证机构/虚拟机构
-- ============================================================

CREATE TABLE IF NOT EXISTS `Sys_Organization` (
    `Id` VARCHAR(64) NOT NULL COMMENT '主键 GUID',
    `Code` VARCHAR(64) NOT NULL COMMENT '业务编码（GUID，唯一）',
    `OrgName` VARCHAR(200) NOT NULL COMMENT '机构名称',
    `OrgCode` VARCHAR(100) DEFAULT NULL COMMENT '机构业务编号（对外展示）',
    `ParentCode` VARCHAR(64) DEFAULT NULL COMMENT '父机构编码（根节点为 NULL）',
    `OrgType` VARCHAR(50) DEFAULT NULL COMMENT '机构类型：Platform/CertBody/VirtualOrg/Dept',
    `OrgLevel` INT DEFAULT 1 COMMENT '机构层级（1=根，2=一级子机构）',
    `OrgPath` VARCHAR(500) DEFAULT NULL COMMENT '机构路径（如：/rootCode/parentCode/code）',
    `LeaderName` VARCHAR(50) DEFAULT NULL COMMENT '负责人姓名',
    `LeaderPhone` VARCHAR(20) DEFAULT NULL COMMENT '负责人电话',
    `Sort` INT DEFAULT 0 COMMENT '排序号',
    `Enable` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用（1=启用，0=禁用）',
    `Remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
    `CreateTime` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `CreateBy` VARCHAR(64) DEFAULT NULL COMMENT '创建人 Code',
    `UpdateTime` DATETIME DEFAULT NULL COMMENT '更新时间',
    `UpdateBy` VARCHAR(64) DEFAULT NULL COMMENT '更新人 Code',
    `DeleteTime` DATETIME DEFAULT NULL COMMENT '删除时间（软删除）',
    `DeleteBy` VARCHAR(64) DEFAULT NULL COMMENT '删除人 Code',
    `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '软删除标志',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_parent_code` (`ParentCode`),
    KEY `idx_org_type` (`OrgType`),
    KEY `idx_enable` (`Enable`),
    KEY `idx_is_deleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='组织机构表';

-- ============================================================
-- 预初始化三类根节点
-- ============================================================

-- 1. 平台管理组（系统管理员/维护人员）
INSERT INTO `Sys_Organization` (`Id`, `Code`, `OrgName`, `OrgCode`, `ParentCode`, `OrgType`, `OrgLevel`, `OrgPath`, `Sort`, `Enable`, `Remark`)
VALUES (REPLACE(UUID(),'-'', ''), 'org_platform_root', '平台管理组', 'PLATFORM', NULL, 'Platform', 1, '/org_platform_root', 1, 1, '系统预置-平台管理层');

-- 2. 体系认证机构（真实认证的机构）
INSERT INTO `Sys_Organization` (`Id`, `Code`, `OrgName`, `OrgCode`, `ParentCode`, `OrgType`, `OrgLevel`, `OrgPath`, `Sort`, `Enable`, `Remark`)
VALUES (REPLACE(UUID(),'-', ''), 'org_certbody_root', '认证机构', 'CERTBODY', NULL, 'CertBody', 1, '/org_certbody_root', 2, 1, '系统预置-认证机构层');

-- 3. 虚拟机构（审核员注册形成的企业/审核机构）
INSERT INTO `Sys_Organization` (`Id`, `Code`, `OrgName`, `OrgCode`, `ParentCode`, `OrgType`, `OrgLevel`, `OrgPath`, `Sort`, `Enable`, `Remark`)
VALUES (REPLACE(UUID(),'-', ''), 'org_virtual_root', '虚拟机构', 'VIRTUAL', NULL, 'VirtualOrg', 1, '/org_virtual_root', 3, 1, '系统预置-虚拟机构层（审核员/企业用户）');
