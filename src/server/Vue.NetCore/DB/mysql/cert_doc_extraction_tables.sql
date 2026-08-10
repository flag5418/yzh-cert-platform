-- ========================================================
-- 文档数据提取系统 - 数据库表结构
-- 创建时间: 2026-08-10
-- 版本: V1.1 - 使用Code关联
-- ========================================================

-- --------------------------------------------------------
-- 1. 文档提取规则主表
-- --------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_extraction_rule` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) NOT NULL COMMENT '规则编码（唯一标识）',
  `file_code` varchar(100) NOT NULL COMMENT '文件编码（关联标准目录文件）',
  `skill` varchar(50) NOT NULL COMMENT '技能类型：word/excel/pdf',
  `prompt` text COMMENT '提取Prompt',
  `is_valid` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否验证通过：0-否 1-是',
  `verify_message` varchar(500) DEFAULT NULL COMMENT '验证结果信息',
  `sample_data` json DEFAULT NULL COMMENT '验证时提取的样本数据（JSON格式）',
  `status` varchar(20) NOT NULL DEFAULT 'none' COMMENT '规则状态：none/configured/failed',
  `remark` varchar(500) DEFAULT NULL COMMENT '备注',
  `create_id` int DEFAULT NULL COMMENT '创建人ID',
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_id` int DEFAULT NULL COMMENT '修改人ID',
  `update_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '修改时间',
  `org_code` varchar(50) DEFAULT NULL COMMENT '机构编码（多租户）',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_file_code` (`file_code`),
  KEY `idx_status` (`status`),
  KEY `idx_org_code` (`org_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文档提取规则主表';

-- --------------------------------------------------------
-- 2. 文档字段定义表
-- --------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_field_def` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) NOT NULL COMMENT '字段定义编码（唯一标识）',
  `rule_code` varchar(100) NOT NULL COMMENT '规则编码（关联cert_doc_extraction_rule.code）',
  `field_name` varchar(100) NOT NULL COMMENT '字段名称',
  `field_code` varchar(100) NOT NULL COMMENT '字段编码（用于工作流引用）',
  `data_type` varchar(20) NOT NULL DEFAULT 'string' COMMENT '数据类型：string/number/date/boolean',
  `description` varchar(500) DEFAULT NULL COMMENT '字段描述（AI提取依据）',
  `is_manual` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否需手动补充：0-否 1-是',
  `sort_order` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `remark` varchar(500) DEFAULT NULL COMMENT '备注',
  `create_id` int DEFAULT NULL COMMENT '创建人ID',
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_id` int DEFAULT NULL COMMENT '修改人ID',
  `update_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '修改时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  KEY `idx_rule_code` (`rule_code`),
  KEY `idx_field_code` (`field_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文档字段定义表';

-- --------------------------------------------------------
-- 3. 文档表格定义表
-- --------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_table_def` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) NOT NULL COMMENT '表格定义编码（唯一标识）',
  `rule_code` varchar(100) NOT NULL COMMENT '规则编码（关联cert_doc_extraction_rule.code）',
  `table_name` varchar(100) NOT NULL COMMENT '表格名称',
  `table_code` varchar(100) NOT NULL COMMENT '表格编码（用于工作流引用）',
  `description` varchar(500) DEFAULT NULL COMMENT '表格描述（AI提取依据）',
  `sort_order` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `remark` varchar(500) DEFAULT NULL COMMENT '备注',
  `create_id` int DEFAULT NULL COMMENT '创建人ID',
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_id` int DEFAULT NULL COMMENT '修改人ID',
  `update_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '修改时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  KEY `idx_rule_code` (`rule_code`),
  KEY `idx_table_code` (`table_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文档表格定义表';

-- --------------------------------------------------------
-- 4. 文档表格字段定义表
-- --------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_doc_table_field_def` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) NOT NULL COMMENT '表格字段定义编码（唯一标识）',
  `table_code` varchar(100) NOT NULL COMMENT '表格编码（关联cert_doc_table_def.code）',
  `column_name` varchar(100) NOT NULL COMMENT '列名称',
  `column_code` varchar(100) NOT NULL COMMENT '列编码',
  `data_type` varchar(20) NOT NULL DEFAULT 'string' COMMENT '数据类型：string/number/date',
  `sort_order` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `remark` varchar(500) DEFAULT NULL COMMENT '备注',
  `create_id` int DEFAULT NULL COMMENT '创建人ID',
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_id` int DEFAULT NULL COMMENT '修改人ID',
  `update_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '修改时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  KEY `idx_table_code` (`table_code`),
  KEY `idx_column_code` (`column_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文档表格字段定义表';

-- --------------------------------------------------------
-- 5. AI配置表
-- --------------------------------------------------------
CREATE TABLE IF NOT EXISTS `cert_ai_config` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) NOT NULL COMMENT '配置编码（唯一标识）',
  `provider` varchar(50) NOT NULL DEFAULT 'qwen' COMMENT 'AI提供商：qwen/deepseek等',
  `api_key` varchar(500) NOT NULL COMMENT 'API Key（加密存储）',
  `model` varchar(100) NOT NULL DEFAULT 'qwen-turbo' COMMENT '模型名称',
  `temperature` float NOT NULL DEFAULT '0.7' COMMENT '温度参数',
  `max_tokens` int NOT NULL DEFAULT '4096' COMMENT '最大Token数',
  `is_enabled` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否启用：0-否 1-是',
  `remark` varchar(500) DEFAULT NULL COMMENT '备注',
  `create_id` int DEFAULT NULL COMMENT '创建人ID',
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_id` int DEFAULT NULL COMMENT '修改人ID',
  `update_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '修改时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI配置表';

-- --------------------------------------------------------
-- 6. 插入默认AI配置
-- --------------------------------------------------------
INSERT INTO `cert_ai_config` (`code`, `provider`, `api_key`, `model`, `temperature`, `max_tokens`, `is_enabled`, `remark`, `create_date`)
VALUES ('default-qwen-config', 'qwen', 'LTAI5tAg9TpuMJbxxn16V4dk', 'qwen-turbo', 0.7, 4096, 1, '默认千问配置', NOW())
ON DUPLICATE KEY UPDATE `update_date` = NOW();

-- --------------------------------------------------------
-- 7. 添加数据字典（技能类型）- 使用正确的列名
-- --------------------------------------------------------
INSERT INTO `Sys_Dictionary` (`DicNo`, `DicName`, `OrderNo`, `Remark`, `Enable`, `CreateDate`, `ParentId`) 
SELECT 'doc_skill', '文档提取技能类型', 1, '文档提取技能类型', 1, NOW(), 87
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `Sys_Dictionary` WHERE `DicNo` = 'doc_skill');

-- 获取刚插入的字典ID
SET @doc_skill_id = (SELECT `Dic_ID` FROM `Sys_Dictionary` WHERE `DicNo` = 'doc_skill');

-- 技能类型字典项
INSERT INTO `Sys_DictionaryList` (`Dic_ID`, `DicName`, `DicValue`, `OrderNo`, `Remark`, `Enable`, `CreateDate`)
VALUES
  (@doc_skill_id, 'Word文档提取', 'word', 1, '使用NPOI提取Word文档', 1, NOW()),
  (@doc_skill_id, 'Excel表格提取', 'excel', 2, '使用NPOI提取Excel表格', 1, NOW()),
  (@doc_skill_id, 'PDF文档提取', 'pdf', 3, '提取PDF文档文本内容', 1, NOW())
ON DUPLICATE KEY UPDATE `DicName` = VALUES(`DicName`);

-- --------------------------------------------------------
-- 8. 添加数据字典（规则状态）
-- --------------------------------------------------------
INSERT INTO `Sys_Dictionary` (`DicNo`, `DicName`, `OrderNo`, `Remark`, `Enable`, `CreateDate`, `ParentId`)
SELECT 'rule_status', '文档规则状态', 1, '文档提取规则状态', 1, NOW(), 87
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `Sys_Dictionary` WHERE `DicNo` = 'rule_status');

-- 获取刚插入的字典ID
SET @rule_status_id = (SELECT `Dic_ID` FROM `Sys_Dictionary` WHERE `DicNo` = 'rule_status');

-- 规则状态字典项
INSERT INTO `Sys_DictionaryList` (`Dic_ID`, `DicName`, `DicValue`, `OrderNo`, `Remark`, `Enable`, `CreateDate`)
VALUES
  (@rule_status_id, '未验证', '0', 1, '规则尚未验证', 1, NOW()),
  (@rule_status_id, '验证通过', '1', 2, '规则验证成功', 1, NOW()),
  (@rule_status_id, '验证失败', '2', 3, '规则验证失败', 1, NOW())
ON DUPLICATE KEY UPDATE `DicName` = VALUES(`DicName`);

