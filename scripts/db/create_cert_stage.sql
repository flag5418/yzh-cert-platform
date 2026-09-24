-- ============================================================
-- cert_cert_stage: 认证阶段（全局基础资料）
-- 基于 ISO/IEC 17021-1:2015，9 个标准认证阶段
-- 创建时间: 2026-09-13
-- ============================================================

CREATE TABLE IF NOT EXISTS `cert_cert_stage` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `StageCode` varchar(50) NOT NULL COMMENT '阶段编码（如 AP/CR/SP/S1/S2/CD/CE/SV/RC）',
  `StageName` varchar(200) NOT NULL COMMENT '阶段名称',
  `Category` varchar(50) DEFAULT 'process' COMMENT '分类（process/audit/post-cert）',
  `SortOrder` int DEFAULT 0 COMMENT '排序号（1~9）',
  `Description` text COMMENT '阶段说明',
  `IsValid` tinyint(1) NOT NULL DEFAULT 1,
  `Status` varchar(50) DEFAULT 'active',
  `Remark` varchar(500) DEFAULT NULL,
  `CreateBy` varchar(50) DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_stage_code` (`StageCode`),
  KEY `idx_category` (`Category`),
  KEY `idx_sort_order` (`SortOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证阶段（全局基础资料）';

-- 预置 9 个标准阶段（基于 ISO/IEC 17021-1:2015）
INSERT INTO `cert_cert_stage` (`Code`, `StageCode`, `StageName`, `Category`, `SortOrder`, `Description`, `IsValid`) VALUES
('CERT_STAGE_01', 'AP',  '申请受理',   'process',     1, '接收认证申请，初步审查申请材料的完整性', 1),
('CERT_STAGE_02', 'CR',  '合同评审',   'process',     2, '评审认证申请，确定审核资源和可行性', 1),
('CERT_STAGE_03', 'SP',  '审核方案策划', 'process',     3, '策划审核方案，编制审核计划', 1),
('CERT_STAGE_04', 'S1',  '第一阶段审核', 'audit',      4, '文件审核和初步评估，确定第二阶段审核准备情况', 1),
('CERT_STAGE_05', 'S2',  '第二阶段审核', 'audit',      5, '现场审核，评估体系运行有效性', 1),
('CERT_STAGE_06', 'CD',  '认证决定',   'audit',      6, '根据审核结果做出认证决定', 1),
('CERT_STAGE_07', 'CE',  '颁发证书',   'audit',      7, '颁发认证证书', 1),
('CERT_STAGE_08', 'SV',  '监督审核',   'post-cert',  8, '证书有效期内定期监督审核', 1),
('CERT_STAGE_09', 'RC',  '再认证',     'post-cert',  9, '证书到期前的再认证审核', 1);
