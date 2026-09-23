-- ============================================================
-- 全量表结构 DDL（基准定义）
-- 更新：2026-09-23
--
-- ★ 字符集/排序规则铁律（`项目全局规则.md` §十六 铁律八）：
--   全库统一 `utf8mb4` + `utf8mb4_general_ci`。
--   ⛔ 禁止 utf8mb3（旧 utf8，无法存 4 字节字符如 emoji）；
--   ⛔ 禁止只写 `DEFAULT CHARSET=utf8mb4` 而不写 COLLATE —— 那会落到
--      **utf8mb4 字符集自身的默认排序规则 `utf8mb4_0900_ai_ci`**，与库内既有表不一致，
--      跨表「列 vs 列」关联时报 ERROR 1267 Illegal mix of collations。
--
-- ★ 2026-09-23 修正记录：
--   本文件原有 40 处 `CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci`（`Remark` 列），
--   是全库 37 个 utf8mb3 列的**源头**。已全部改为 `utf8mb4_general_ci`。
-- ============================================================

-- 连接排序规则：决定视图内「字面量派生列」的 collation，勿删
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ============================================================
-- Table: audit_checklist_item
-- ============================================================
CREATE TABLE `audit_checklist_item` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属审核任务编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应条款编码',
  `AuditCriteria` text COLLATE utf8mb4_general_ci COMMENT '审核准则（标准条款原文）',
  `FindingDescription` text COLLATE utf8mb4_general_ci COMMENT '审核发现描述',
  `Conformity` enum('pending','conform','nonconform','observation','na') COLLATE utf8mb4_general_ci DEFAULT 'pending' COMMENT '判定结果',
  `NcsFound` int DEFAULT '0' COMMENT '发现NC数量',
  `CheckedBy` bigint DEFAULT NULL COMMENT '检查人ID',
  `CheckedAt` datetime DEFAULT NULL COMMENT '检查时间',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_conformity` (`Conformity`),
  CONSTRAINT `fk_checklist_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_checklist_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='检查表条目'


-- ============================================================
-- Table: audit_evidence
-- ============================================================
CREATE TABLE `audit_evidence` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属审核任务编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '关联条款编码',
  `EvidenceType` enum('photo','audio','screenshot','video','document','other') COLLATE utf8mb4_general_ci NOT NULL COMMENT '证据类型',
  `StoragePath` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'MinIO存储路径',
  `FileHash` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'SHA256哈希',
  `IsVoided` tinyint(1) DEFAULT '0' COMMENT '是否废弃',
  `VoidedAt` datetime DEFAULT NULL COMMENT '废弃时间',
  `VoidedBy` bigint DEFAULT NULL COMMENT '废弃操作人ID',
  `CapturedAt` datetime DEFAULT NULL COMMENT '采集时间',
  `CapturedBy` bigint NOT NULL COMMENT '采集人ID',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_evidence_type` (`EvidenceType`),
  KEY `idx_is_voided` (`IsVoided`),
  CONSTRAINT `fk_evidence_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_evidence_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核证据'


-- ============================================================
-- Table: audit_finding
-- ============================================================
CREATE TABLE `audit_finding` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ChecklistItemCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '检查表条目编码',
  `NcCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '关联NC编码',
  `SourceFileCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源文件编码',
  `SourcePosition` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源位置（页码/行号/列号）',
  `SourceContent` text COLLATE utf8mb4_general_ci COMMENT '来源内容摘录',
  `FindingType` enum('conform','discrepancy','comment') COLLATE utf8mb4_general_ci NOT NULL COMMENT '发现类型',
  `Description` text COLLATE utf8mb4_general_ci NOT NULL COMMENT '描述',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
  `IsManual` tinyint(1) DEFAULT '0' COMMENT '是否人工添加',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_checklist_item_code` (`ChecklistItemCode`),
  KEY `idx_nc_code` (`NcCode`),
  KEY `idx_source_file_code` (`SourceFileCode`),
  KEY `idx_finding_type` (`FindingType`),
  CONSTRAINT `fk_finding_checklist` FOREIGN KEY (`ChecklistItemCode`) REFERENCES `audit_checklist_item` (`Code`),
  CONSTRAINT `fk_finding_file` FOREIGN KEY (`SourceFileCode`) REFERENCES `ent_enterprise_file` (`Code`),
  CONSTRAINT `fk_finding_nc` FOREIGN KEY (`NcCode`) REFERENCES `audit_nonconformity` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核发现明细'


-- ============================================================
-- Table: audit_nonconformity
-- ============================================================
CREATE TABLE `audit_nonconformity` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属审核任务编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应条款编码',
  `NcNumber` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'NC编号',
  `Severity` enum('major','minor','observation') COLLATE utf8mb4_general_ci NOT NULL COMMENT '严重度',
  `Description` text COLLATE utf8mb4_general_ci NOT NULL COMMENT 'NC描述（不符合事实）',
  `RequirementRef` text COLLATE utf8mb4_general_ci COMMENT '违反的标准要求原文',
  `EvidenceRef` text COLLATE utf8mb4_general_ci COMMENT '客观证据引用',
  `SourceType` enum('auto_rule','manual') COLLATE utf8mb4_general_ci DEFAULT 'manual' COMMENT 'NC来源：规则自动触发 / 手动创建',
  `SourceCheckCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '触发的合规检查记录编码',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '触发的校验规则编码',
  `DueDate` date DEFAULT NULL COMMENT '整改截止日期',
  `OpenedBy` bigint NOT NULL COMMENT '开具人ID',
  `OpenedAt` datetime NOT NULL COMMENT '开具时间',
  `ClosedAt` datetime DEFAULT NULL COMMENT '关闭时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_nc_number` (`NcNumber`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_severity` (`Severity`),
  KEY `idx_status` (`status`),
  KEY `idx_source_type` (`SourceType`),
  KEY `fk_nc_sourcecheck` (`SourceCheckCode`),
  KEY `fk_nc_rule` (`RuleCode`),
  CONSTRAINT `fk_nc_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_nc_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`),
  CONSTRAINT `fk_nc_sourcecheck` FOREIGN KEY (`SourceCheckCode`) REFERENCES `ent_file_compliance_check` (`Code`),
  CONSTRAINT `fk_nc_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='不符合项(NC)'


-- ============================================================
-- Table: audit_project
-- ============================================================
CREATE TABLE `audit_project` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` char(36) NOT NULL,
  `ProjectNo` varchar(50) NOT NULL COMMENT '项目编号',
  `ApplicationCode` varchar(36) NOT NULL COMMENT '关联申请编码',
  `CurrentPhase` varchar(30) DEFAULT 'application_review' COMMENT '当前阶段',
  `ProjectManagerCode` varchar(50) DEFAULT NULL,
  `PlannedStartDate` date DEFAULT NULL COMMENT '计划开始日期',
  `PlannedEndDate` date DEFAULT NULL COMMENT '计划结束日期',
  `ActualEndDate` date DEFAULT NULL COMMENT '实际结束日期',
  `status` varchar(20) DEFAULT NULL,
  `Remark` text,
  `create_date` datetime DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `create_by` varchar(50) DEFAULT NULL,
  `update_by` varchar(50) DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_project_no` (`ProjectNo`),
  KEY `idx_application_code` (`ApplicationCode`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='审核项目表'


-- ============================================================
-- Table: audit_rectification
-- ============================================================
CREATE TABLE `audit_rectification` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `NcCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '关联NC编码',
  `Correction` text COLLATE utf8mb4_general_ci NOT NULL COMMENT '纠正措施描述',
  `CorrectiveAction` text COLLATE utf8mb4_general_ci COMMENT '纠正措施（根因分析+防再发生）',
  `EvidenceFiles` json DEFAULT NULL COMMENT '整改证据文件路径列表',
  `SubmittedBy` bigint NOT NULL COMMENT '提交人ID',
  `SubmittedAt` datetime NOT NULL COMMENT '提交时间',
  `VerifiedBy` bigint DEFAULT NULL COMMENT '复核人ID',
  `VerifiedAt` datetime DEFAULT NULL COMMENT '复核时间',
  `VerifyResult` enum('approved','rejected') COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '复核结果',
  `VerifyNotes` text COLLATE utf8mb4_general_ci COMMENT '复核意见',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_nc_code` (`NcCode`),
  KEY `idx_verify_result` (`VerifyResult`),
  CONSTRAINT `fk_rect_nc` FOREIGN KEY (`NcCode`) REFERENCES `audit_nonconformity` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='整改记录'


-- ============================================================
-- Table: audit_task
-- ============================================================
CREATE TABLE `audit_task` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属企业阶段编码',
  `TaskNumber` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务编号',
  `AuditorCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `PlannedDate` date DEFAULT NULL COMMENT '计划审核日期',
  `ActualStartDate` date DEFAULT NULL COMMENT '实际开始日期',
  `ActualCompleteDate` date DEFAULT NULL COMMENT '实际完成日期',
  `AuditScope` text COLLATE utf8mb4_general_ci COMMENT '审核范围描述',
  `create_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_task_number` (`TaskNumber`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_status` (`status`),
  CONSTRAINT `fk_task_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核任务'


-- ============================================================
-- Table: cert_ai_config
-- ============================================================
CREATE TABLE `cert_ai_config` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '配置编码（唯一标识）',
  `OrgCode` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `provider` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'qwen' COMMENT 'AI提供商：qwen/deepseek等',
  `api_key` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'API Key（加密存储）',
  `model` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'qwen-turbo' COMMENT '模型名称',
  `temperature` float NOT NULL DEFAULT '0.7' COMMENT '温度参数',
  `max_tokens` int NOT NULL DEFAULT '4096' COMMENT '最大Token数',
  `is_enabled` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否启用：0-否 1-是',
  `Remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) DEFAULT '1',
  `status` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `Sort` int DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_provider_model` (`provider`,`model`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI配置表'


-- ============================================================
-- Table: cert_ai_usage_log
-- ============================================================
CREATE TABLE `cert_ai_usage_log` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `call_id` varchar(64) NOT NULL COMMENT '调用唯一ID(GUID)',
  `business_type` varchar(50) NOT NULL DEFAULT 'doc_extraction' COMMENT '业务类型',
  `business_ref` varchar(100) DEFAULT NULL COMMENT '业务关联（如文件编码）',
  `skill` varchar(50) DEFAULT NULL COMMENT '技能名称：analyze/verify',
  `provider` varchar(50) DEFAULT NULL COMMENT '模型提供商：qwen/deepseek',
  `model` varchar(100) DEFAULT NULL COMMENT '模型名称：qwen-turbo/qwen-plus 等',
  `prompt_tokens` int DEFAULT '0' COMMENT '输入 token 数',
  `completion_tokens` int DEFAULT '0' COMMENT '输出 token 数',
  `total_tokens` int DEFAULT '0' COMMENT '总 token 数',
  `cost_usd` decimal(10,6) DEFAULT '0.000000' COMMENT '本次费用（美元）',
  `duration_ms` bigint DEFAULT '0' COMMENT '耗时（毫秒）',
  `success` tinyint(1) DEFAULT '1' COMMENT '是否成功',
  `error_message` varchar(500) DEFAULT NULL COMMENT '失败原因',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP COMMENT '调用时间',
  `CreateBy` varchar(50) DEFAULT NULL,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除',
  `status` varchar(50) NOT NULL DEFAULT 'active',
  `Remark` varchar(500) DEFAULT NULL,
  `code` varchar(64) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_call_id` (`call_id`),
  UNIQUE KEY `code` (`code`),
  KEY `idx_create_date` (`CreateTime`),
  KEY `idx_model` (`model`),
  KEY `idx_success` (`success`)
) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='AI 调用日志（用于费用统计）'


-- ============================================================
-- Table: cert_application
-- ============================================================
CREATE TABLE `cert_application` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` char(36) NOT NULL,
  `ApplicationNo` varchar(50) NOT NULL COMMENT '申请编号',
  `CbCode` varchar(36) NOT NULL COMMENT '认证机构编码',
  `StandardCode` varchar(36) NOT NULL COMMENT '标准编码',
  `EnterpriseCode` varchar(36) NOT NULL COMMENT '企业编码',
  `CertType` varchar(20) NOT NULL COMMENT '认证类型(QMS/EMS等)',
  `ScopeText` text COMMENT '认证范围描述',
  `status` varchar(30) DEFAULT NULL,
  `SubmitTime` datetime DEFAULT NULL COMMENT '提交时间',
  `AcceptTime` datetime DEFAULT NULL COMMENT '受理时间',
  `CompleteTime` datetime DEFAULT NULL COMMENT '完成时间',
  `Remark` text,
  `CreateBy` varchar(50) DEFAULT NULL,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_application_no` (`ApplicationNo`),
  KEY `idx_cb_code` (`CbCode`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='认证申请表'


-- ============================================================
-- Table: cert_auditor_profile
-- ============================================================
CREATE TABLE `cert_auditor_profile` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(36) NOT NULL COMMENT '审核员编码(GUID)',
  `user_code` varchar(50) DEFAULT NULL,
  `OrgCode` varchar(50) NOT NULL COMMENT '所属认证机构编码',
  `auditor_no` varchar(50) NOT NULL COMMENT '审核员资格证号',
  `auditor_name` varchar(100) NOT NULL COMMENT '审核员姓名',
  `phone` varchar(20) NOT NULL COMMENT '手机号',
  `email` varchar(200) DEFAULT NULL COMMENT '邮箱',
  `qualification` json DEFAULT NULL COMMENT '审核资质(标准类型+级别)',
  `expertise_areas` json DEFAULT NULL COMMENT '专业领域(行业分类)',
  `status` varchar(20) NOT NULL DEFAULT 'active' COMMENT 'active/inactive/suspended',
  `CreateBy` varchar(50) DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除',
  `Remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  UNIQUE KEY `auditor_no` (`auditor_no`),
  KEY `idx_org_code` (`OrgCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='审核员资质档案'


-- ============================================================
-- Table: cert_certification_body
-- ============================================================
CREATE TABLE `cert_certification_body` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT '1',
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `name` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `short_name` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `cb_code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `contact_name` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `contact_phone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `legal_person` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `contact_email` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `address` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `logo_url` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `scope_text` text COLLATE utf8mb4_general_ci,
  `theme_config` text COLLATE utf8mb4_general_ci,
  `login_config` text COLLATE utf8mb4_general_ci,
  `max_users` int DEFAULT '100',
  `max_enterprises` int DEFAULT '1000',
  `expire_date` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_name` (`name`),
  UNIQUE KEY `uk_cb_code` (`cb_code`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证机构'


-- ============================================================
-- Table: cert_clause_extraction_rule
-- ============================================================
CREATE TABLE `cert_clause_extraction_rule` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '条款编码',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '关联的提取工作流编码',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '规则集说明',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_workflow_code` (`WorkflowCode`),
  CONSTRAINT `fk_clauseext_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_clauseext_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='条款提取规则'


-- ============================================================
-- Table: cert_directory_template
-- ============================================================
CREATE TABLE `cert_directory_template` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ConfigCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属标准-阶段配置编码',
  `ParentCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父文件夹编码（树形结构）',
  `FolderName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件夹名称',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_config_code` (`ConfigCode`),
  KEY `idx_parent_code` (`ParentCode`),
  CONSTRAINT `fk_dirtemplate_config` FOREIGN KEY (`ConfigCode`) REFERENCES `cert_standard_phase_config` (`Code`),
  CONSTRAINT `fk_dirtemplate_parent` FOREIGN KEY (`ParentCode`) REFERENCES `cert_directory_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件目录模板'


-- ============================================================
-- Table: cert_doc_extraction_rule
-- ============================================================
CREATE TABLE `cert_doc_extraction_rule` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '规则编码（唯一标识）',
  `file_code` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `standard_file_code` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '规则键：实际文件 FileCode 或文件要求模板 Code',
  `standard_code` varchar(36) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '标准编码(冗余)',
  `phase_code` varchar(36) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '阶段编码(冗余)',
  `skill` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '技能类型：word/excel/pdf',
  `prompt` text COLLATE utf8mb4_unicode_ci COMMENT '提取Prompt',
  `DocIsValid` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否验证通过：0-否 1-是',
  `verify_message` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '验证结果信息',
  `sample_data` json DEFAULT NULL COMMENT '验证时提取的样本数据（JSON格式）',
  `doc_content` longtext COLLATE utf8mb4_unicode_ci COMMENT '提取的文档内容缓存',
  `status` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'none' COMMENT '规则状态：none/configured/failed',
  `Remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_standard_file_code` (`standard_file_code`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB AUTO_INCREMENT=2088559573848952833 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文档提取规则主表'


-- ============================================================
-- Table: cert_doc_field_def
-- ============================================================
CREATE TABLE `cert_doc_field_def` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '字段定义编码（唯一标识）',
  `rule_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '规则编码（关联cert_doc_extraction_rule.code）',
  `field_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '字段名称',
  `field_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '字段编码（用于工作流引用）',
  `data_type` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'string' COMMENT '数据类型：string/number/date/boolean',
  `description` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '字段描述（AI提取依据）',
  `is_manual` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否需手动补充：0-否 1-是',
  `Sort` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `Remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `is_ai_recommended` tinyint(1) DEFAULT '1' COMMENT '是否AI推荐字段(1=是,0=手动添加)',
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'active' COMMENT '业务状态',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_rule_field` (`rule_code`,`field_code`),
  KEY `idx_rule_code` (`rule_code`),
  KEY `idx_field_code` (`field_code`)
) ENGINE=InnoDB AUTO_INCREMENT=40 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文档字段定义表'


-- ============================================================
-- Table: cert_doc_table_def
-- ============================================================
CREATE TABLE `cert_doc_table_def` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '表格定义编码（唯一标识）',
  `rule_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '规则编码（关联cert_doc_extraction_rule.code）',
  `table_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '表格名称',
  `table_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '表格编码（用于工作流引用）',
  `description` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '表格描述（AI提取依据）',
  `Sort` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `Remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'active' COMMENT '业务状态',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_rule_table` (`rule_code`,`table_code`),
  KEY `idx_rule_code` (`rule_code`),
  KEY `idx_table_code` (`table_code`)
) ENGINE=InnoDB AUTO_INCREMENT=22 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文档表格定义表'


-- ============================================================
-- Table: cert_doc_table_field_def
-- ============================================================
CREATE TABLE `cert_doc_table_field_def` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '表格字段定义编码（唯一标识）',
  `table_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '表格编码（关联cert_doc_table_def.code）',
  `column_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '列名称',
  `column_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '列编码',
  `data_type` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'string' COMMENT '数据类型：string/number/date',
  `Sort` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `Remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'active' COMMENT '业务状态',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_table_column` (`table_code`,`column_code`),
  KEY `idx_table_code` (`table_code`),
  KEY `idx_column_code` (`column_code`)
) ENGINE=InnoDB AUTO_INCREMENT=66 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文档表格字段定义表'


-- ============================================================
-- Table: cert_enterprise
-- ============================================================
CREATE TABLE `cert_enterprise` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` char(36) NOT NULL,
  `Name` varchar(200) NOT NULL,
  `ShortName` varchar(100) DEFAULT NULL,
  `CreditCode` varchar(50) NOT NULL COMMENT '统一社会信用代码',
  `LegalPerson` varchar(100) DEFAULT NULL,
  `ContactName` varchar(100) DEFAULT NULL,
  `ContactPhone` varchar(20) DEFAULT NULL,
  `ContactEmail` varchar(200) DEFAULT NULL,
  `province` varchar(50) DEFAULT NULL,
  `city` varchar(50) DEFAULT NULL,
  `Address` varchar(500) DEFAULT NULL,
  `IndustryType` varchar(100) DEFAULT NULL COMMENT '行业类型',
  `EmployeeCount` int DEFAULT NULL COMMENT '员工人数',
  `status` tinyint DEFAULT NULL,
  `OrgCode` varchar(50) DEFAULT NULL COMMENT '所属机构编码',
  `Remark` text,
  `CreateBy` varchar(50) DEFAULT NULL,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_credit_code` (`CreditCode`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='企业信息表'


-- ============================================================
-- Table: cert_file_requirement
-- ============================================================
CREATE TABLE `cert_file_requirement` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FolderCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属文件夹编码',
  `FileNameTemplate` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件名称模板',
  `FileType` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '允许的文件类型（pdf/docx/xlsx/png 等）',
  `IsRequired` tinyint(1) DEFAULT '1' COMMENT '是否必须提供',
  `MaxSizeMB` int DEFAULT '10' COMMENT '最大文件大小（MB）',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '文件说明/要求描述',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_folder_code` (`FolderCode`),
  CONSTRAINT `fk_filereq_folder` FOREIGN KEY (`FolderCode`) REFERENCES `cert_directory_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件要求'


-- ============================================================
-- Table: cert_iso_clause
-- ============================================================
CREATE TABLE `cert_iso_clause` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属标准编码',
  `ParentCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父条款编码（树形结构）',
  `ClauseNumber` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '条款编号（如 7.1、7.1.1）',
  `Title` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '条款标题',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '条款原文或摘要',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_parent_code` (`ParentCode`),
  KEY `idx_clause_number` (`ClauseNumber`)
) ENGINE=InnoDB AUTO_INCREMENT=44 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准条款'


-- ============================================================
-- Table: cert_iso_standard
-- ============================================================
CREATE TABLE `cert_iso_standard` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `cb_code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `standard_code` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `standard_name` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `version_year` int DEFAULT NULL,
  `category` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'quality',
  `description` text COLLATE utf8mb4_general_ci,
  `parent_code` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父节点编码（ISO标准扁平结构，始终null=根节点）',
  `is_leaf` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否叶子节点（ISO标准无子节点，始终true）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_cb_code` (`cb_code`),
  KEY `idx_standard_code` (`standard_code`),
  CONSTRAINT `fk_cert_iso_standard_cb_code` FOREIGN KEY (`cb_code`) REFERENCES `cert_certification_body` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='ISO标准'


-- ============================================================
-- Table: cert_message
-- ============================================================
CREATE TABLE `cert_message` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `user_code` varchar(50) DEFAULT NULL,
  `user_name` varchar(50) DEFAULT NULL,
  `title` varchar(200) NOT NULL,
  `content` text,
  `message_type` varchar(50) DEFAULT 'system',
  `is_read` tinyint(1) DEFAULT '0',
  `extra_data` json DEFAULT NULL,
  `related_code` varchar(100) DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `CreateBy` varchar(50) DEFAULT NULL,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `read_date` datetime DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT NULL,
  `status` varchar(50) DEFAULT NULL,
  `Remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_is_read` (`is_read`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci


-- ============================================================
-- Table: cert_org_stage
-- ============================================================
CREATE TABLE `cert_org_stage` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL,
  `OrgCode` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `standard_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phase_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `IsValid` tinyint(1) DEFAULT '1',
  `status` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_org_std_stage` (`OrgCode`,`standard_code`,`phase_code`),
  KEY `idx_org_code` (`OrgCode`),
  KEY `idx_standard_code` (`standard_code`),
  KEY `idx_phase_code` (`phase_code`)
) ENGINE=InnoDB AUTO_INCREMENT=13 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci


-- ============================================================
-- Table: cert_org_standard
-- ============================================================
CREATE TABLE `cert_org_standard` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL,
  `OrgCode` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `standard_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `IsValid` tinyint(1) DEFAULT '1',
  `status` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_org_std` (`OrgCode`,`standard_code`),
  KEY `idx_org_code` (`OrgCode`),
  KEY `idx_standard_code` (`standard_code`)
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci


-- ============================================================
-- Table: cert_phase_definition
-- ============================================================
CREATE TABLE `cert_phase_definition` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `code` varchar(36) NOT NULL COMMENT '业务编码(Guid)，内部关联键',
  `phase_code` varchar(20) NOT NULL COMMENT '阶段编码(S1/S2/Surv1/Surv2/Recert)，业务标识',
  `phase_name` varchar(100) NOT NULL COMMENT '中文名称',
  `sequence_order` int NOT NULL DEFAULT '0' COMMENT '顺序(1=S1, 2=S2, 3=一监, 4=二监, 5=再认证)',
  `description` text COMMENT '阶段说明',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '有效标志(1=启用, 0=停用)',
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy` varchar(50) DEFAULT NULL COMMENT '创建人编码',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(50) DEFAULT NULL COMMENT '更新人编码',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(50) DEFAULT NULL COMMENT '删除人编码',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '软删除标志',
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  UNIQUE KEY `phase_code` (`phase_code`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_phase_code` (`phase_code`),
  KEY `idx_is_valid` (`IsValid`),
  KEY `idx_is_deleted` (`IsDeleted`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='认证阶段定义（通用五阶段，ISO 17021）'


-- ============================================================
-- Table: cert_report_template
-- ============================================================
CREATE TABLE `cert_report_template` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `CbCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证机构编码',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',
  `PhaseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TemplateName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '模板名称',
  `TemplateFilePath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '空白文档文件路径（MinIO）',
  `SectionConfig` json DEFAULT NULL COMMENT '报告章节配置（含每章节的 workflow_id、clause_id 映射）',
  `IsDefault` tinyint(1) DEFAULT '0' COMMENT '是否默认模板',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_cb_code` (`CbCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  CONSTRAINT `fk_rpttmpl_cb` FOREIGN KEY (`CbCode`) REFERENCES `cert_certification_body` (`Code`),
  CONSTRAINT `fk_rpttmpl_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_phase_definition` (`code`),
  CONSTRAINT `fk_rpttmpl_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告模板'


-- ============================================================
-- Table: cert_standard_directory_config
-- ============================================================
CREATE TABLE `cert_standard_directory_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `StandardCode` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `PhaseCode` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `RootFolderName` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('draft','active','archived') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status_field` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `Enable_field` tinyint(1) DEFAULT '1',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_directory_code` (`DirectoryCode`),
  UNIQUE KEY `uk_standard_phase` (`StandardCode`,`PhaseCode`)
) ENGINE=InnoDB AUTO_INCREMENT=14 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci


-- ============================================================
-- Table: cert_standard_directory_file
-- ============================================================
CREATE TABLE `cert_standard_directory_file` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL,
  `FileCode` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `FolderCode` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `FileName` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `FileType` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `file_size` bigint DEFAULT NULL COMMENT '文件大小(字节)',
  `FilePattern` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsRequired` tinyint(1) DEFAULT '1',
  `MaxFileSizeMB` int DEFAULT '10',
  `Description` text COLLATE utf8mb4_unicode_ci,
  `SortOrder` int DEFAULT '0',
  `ExtractionEnabled` tinyint(1) DEFAULT '0',
  `ExtractionRules` json DEFAULT NULL,
  `PreCheckRequired` tinyint(1) DEFAULT '1',
  `ComplianceRequired` tinyint(1) DEFAULT '0',
  `status` enum('draft','active','archived') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status_field` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `Enable_field` tinyint(1) DEFAULT '1',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_unicode_ci,
  `TaskId` varchar(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT '1',
  `UploadStatus` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `StoragePath` varchar(512) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `FullPath` varchar(1024) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `converted_storage_path` varchar(512) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `convert_status` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `convert_message` varchar(1024) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `convert_date` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_file_code` (`FileCode`),
  KEY `idx_folder_code` (`FolderCode`),
  KEY `idx_directory_code` (`DirectoryCode`)
) ENGINE=InnoDB AUTO_INCREMENT=1026 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci


-- ============================================================
-- Table: cert_standard_directory_folder
-- ============================================================
CREATE TABLE `cert_standard_directory_folder` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL,
  `FolderCode` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ParentCode` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `FolderName` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `Depth` int DEFAULT '1',
  `SortOrder` int DEFAULT '0',
  `status` enum('draft','active','archived') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status_field` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `Enable_field` tinyint(1) DEFAULT '1',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_unicode_ci,
  `TaskId` varchar(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT '0',
  `FullPath` varchar(1024) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_folder_code` (`FolderCode`),
  KEY `idx_directory_code` (`DirectoryCode`),
  KEY `idx_parent_code` (`ParentCode`)
) ENGINE=InnoDB AUTO_INCREMENT=115 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci


-- ============================================================
-- Table: cert_standard_phase_config
-- ============================================================
CREATE TABLE `cert_standard_phase_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',
  `PhaseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `RequiredClauses` json DEFAULT NULL COMMENT '此阶段需检查的条款编码列表',
  `RequiredFiles` json DEFAULT NULL COMMENT '此阶段必需的文件清单编码列表',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_standard_phase` (`StandardCode`,`PhaseCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  CONSTRAINT `fk_spconfig_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_phase_definition` (`code`),
  CONSTRAINT `fk_spconfig_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准-阶段配置'


-- ============================================================
-- Table: cert_sys_config
-- ============================================================
CREATE TABLE `cert_sys_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `ConfigKey` varchar(100) NOT NULL,
  `ConfigValue` varchar(500) DEFAULT NULL,
  `ConfigType` varchar(20) DEFAULT NULL,
  `Category` varchar(50) NOT NULL,
  `DisplayName` varchar(100) DEFAULT NULL,
  `Description` varchar(500) DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序',
  `IsReadonly` tinyint DEFAULT '0',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(64) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeleteBy` varchar(64) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除',
  `Status` varchar(50) NOT NULL DEFAULT 'active',
  `Remark` varchar(500) DEFAULT NULL,
  `Code` varchar(64) DEFAULT NULL,
  `CreateBy` varchar(64) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_config_key` (`ConfigKey`),
  UNIQUE KEY `code` (`Code`),
  KEY `idx_category` (`Category`)
) ENGINE=InnoDB AUTO_INCREMENT=26 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='全局系统参数配置'


-- ============================================================
-- Table: cert_upload_task
-- ============================================================
CREATE TABLE `cert_upload_task` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `TaskId` varchar(64) NOT NULL,
  `DirectoryCode` varchar(128) NOT NULL,
  `TotalFiles` int NOT NULL DEFAULT '0',
  `TotalSize` bigint NOT NULL DEFAULT '0',
  `SuccessCount` int NOT NULL DEFAULT '0',
  `status` varchar(20) DEFAULT NULL,
  `CreateBy` varchar(64) DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) DEFAULT NULL,
  `DeleteBy` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `ExpireTime` datetime DEFAULT NULL,
  `code` varchar(64) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UK_TaskId` (`TaskId`),
  UNIQUE KEY `code` (`code`)
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='上传任务追踪表'


-- ============================================================
-- Table: cert_validation_rule
-- ============================================================
CREATE TABLE `cert_validation_rule` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '适用标准编码',
  `PhaseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应条款编码',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '关联的工作流定义编码',
  `RuleCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '规则编码',
  `RuleName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '规则名称',
  `SeverityIfViolated` enum('major','minor','observation') COLLATE utf8mb4_general_ci NOT NULL COMMENT '触发时的NC严重度',
  `NcDescriptionTemplate` text COLLATE utf8mb4_general_ci COMMENT 'NC描述模板',
  `IsActive` tinyint(1) DEFAULT '1' COMMENT '是否启用',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_rule_code` (`RuleCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_workflow_code` (`WorkflowCode`),
  CONSTRAINT `fk_valrule_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_valrule_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_phase_definition` (`code`),
  CONSTRAINT `fk_valrule_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`),
  CONSTRAINT `fk_valrule_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='校验规则'


-- ============================================================
-- Table: cert_validation_rule_source
-- ============================================================
CREATE TABLE `cert_validation_rule_source` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '校验规则编码',
  `FileRequirementCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '溯源文件类型编码',
  `SourcePath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '溯源路径（文件内位置描述）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_rule_code` (`RuleCode`),
  KEY `idx_filereq_code` (`FileRequirementCode`),
  CONSTRAINT `fk_valsource_filereq` FOREIGN KEY (`FileRequirementCode`) REFERENCES `cert_file_requirement` (`Code`),
  CONSTRAINT `fk_valsource_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='校验规则溯源'


-- ============================================================
-- Table: ent_enterprise
-- ============================================================
CREATE TABLE `ent_enterprise` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `create_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Name` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业全称',
  `ShortName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '简称',
  `CreditCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '统一社会信用代码',
  `LegalPerson` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '法人代表',
  `Address` text COLLATE utf8mb4_general_ci COMMENT '企业地址',
  `CertScope` text COLLATE utf8mb4_general_ci COMMENT '认证范围描述',
  `ContactName` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对接人姓名',
  `ContactPhone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对接人电话',
  `ContactEmail` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对接人邮箱',
  `ArchiveDate` date DEFAULT NULL COMMENT '归档日期',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_credit_code` (`CreditCode`),
  KEY `idx_name` (`Name`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业'


-- ============================================================
-- Table: ent_enterprise_document
-- ============================================================
CREATE TABLE `ent_enterprise_document` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属企业编码',
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '所属阶段编码（scope=phase时必填）',
  `Scope` enum('enterprise_base','phase') COLLATE utf8mb4_general_ci NOT NULL COMMENT '资料层级：共享层 / 隔离层',
  `TemplateFolderCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对应的模板文件夹编码',
  `ParentCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父文件夹编码（树形结构）',
  `FolderName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件夹名称',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_parent_code` (`ParentCode`),
  KEY `idx_scope` (`Scope`),
  KEY `fk_edoc_template` (`TemplateFolderCode`),
  CONSTRAINT `fk_edoc_enterprise` FOREIGN KEY (`EnterpriseCode`) REFERENCES `ent_enterprise` (`Code`),
  CONSTRAINT `fk_edoc_parent` FOREIGN KEY (`ParentCode`) REFERENCES `ent_enterprise_document` (`Code`),
  CONSTRAINT `fk_edoc_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`),
  CONSTRAINT `fk_edoc_template` FOREIGN KEY (`TemplateFolderCode`) REFERENCES `cert_directory_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文档目录'


-- ============================================================
-- Table: ent_enterprise_file
-- ============================================================
CREATE TABLE `ent_enterprise_file` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FolderCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属文件夹编码',
  `FileName` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件名',
  `FileType` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件类型（pdf/docx/xlsx/png/jpg）',
  `FileSize` bigint NOT NULL COMMENT '文件大小（bytes）',
  `StoragePath` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'MinIO存储路径',
  `FileHash` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '文件SHA256哈希（增量审核依据）',
  `CurrentVersion` int DEFAULT '1' COMMENT '当前版本号',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_folder_code` (`FolderCode`),
  KEY `idx_file_hash` (`FileHash`),
  CONSTRAINT `fk_efile_folder` FOREIGN KEY (`FolderCode`) REFERENCES `ent_enterprise_document` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文件'


-- ============================================================
-- Table: ent_enterprise_phase
-- ============================================================
CREATE TABLE `ent_enterprise_phase` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属企业编码',
  `PhaseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证标准编码',
  `StartedAt` datetime DEFAULT NULL COMMENT '开始时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_ent_phase_std` (`EnterpriseCode`,`PhaseCode`,`StandardCode`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_status` (`status`),
  CONSTRAINT `fk_ephase_enterprise` FOREIGN KEY (`EnterpriseCode`) REFERENCES `ent_enterprise` (`Code`),
  CONSTRAINT `fk_ephase_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_phase_definition` (`code`),
  CONSTRAINT `fk_ephase_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业阶段'


-- ============================================================
-- Table: ent_extraction_result
-- ============================================================
CREATE TABLE `ent_extraction_result` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '提取的源文件编码',
  `VersionNumber` int NOT NULL COMMENT '提取的文件版本',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '使用的提取规则编码',
  `FieldCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应的提取字段编码',
  `LabelTag` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字段标签冗余（便于查询）',
  `ExtractedValue` text COLLATE utf8mb4_general_ci COMMENT '提取的值',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
  `PositionInfo` json DEFAULT NULL COMMENT '位置信息（页码/行号/列号/单元格）',
  `IsManualEdited` tinyint(1) DEFAULT '0' COMMENT '是否被人工修改',
  `ExtractedAt` datetime NOT NULL COMMENT '提取时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_code` (`FileCode`),
  KEY `idx_rule_code` (`RuleCode`),
  KEY `idx_field_code` (`FieldCode`),
  KEY `idx_label_tag` (`LabelTag`),
  CONSTRAINT `fk_extres_field` FOREIGN KEY (`FieldCode`) REFERENCES `cert_extraction_field` (`Code`),
  CONSTRAINT `fk_extres_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_extraction_rule` (`Code`),
  CONSTRAINT `k_extres_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档提取结果'


-- ============================================================
-- Table: ent_file_compliance_check
-- ============================================================
CREATE TABLE `ent_file_compliance_check` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '被检查的文件编码',
  `VersionNumber` int NOT NULL COMMENT '检查的文件版本',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '触发的校验规则编码',
  `WorkflowExecutionCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '工作流执行记录编码',
  `CheckStatus` enum('pass','fail','warning','blocked') COLLATE utf8mb4_general_ci NOT NULL COMMENT '检查结果',
  `Message` text COLLATE utf8mb4_general_ci COMMENT '检查信息',
  `Detail` json DEFAULT NULL COMMENT '详细信息（含具体位置、偏离描述）',
  `CheckedAt` datetime NOT NULL COMMENT '检查时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_code` (`FileCode`),
  KEY `idx_rule_code` (`RuleCode`),
  KEY `idx_check_status` (`CheckStatus`),
  KEY `fk_compliance_wexec` (`WorkflowExecutionCode`),
  CONSTRAINT `fk_compliance_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`),
  CONSTRAINT `fk_compliance_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`),
  CONSTRAINT `fk_compliance_wexec` FOREIGN KEY (`WorkflowExecutionCode`) REFERENCES `wf_workflow_execution_log` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件合规检查'


-- ============================================================
-- Table: ent_file_pre_check_result
-- ============================================================
CREATE TABLE `ent_file_pre_check_result` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '被检查的文件编码',
  `VersionNumber` int NOT NULL COMMENT '检查的文件版本',
  `CheckType` enum('readability','clarity','format','completeness') COLLATE utf8mb4_general_ci NOT NULL COMMENT '检查类型',
  `CheckResult` enum('pass','warning','block') COLLATE utf8mb4_general_ci NOT NULL COMMENT '检查结果',
  `Message` text COLLATE utf8mb4_general_ci COMMENT '检查信息',
  `Detail` json DEFAULT NULL COMMENT '详细信息（DPI值、倾斜角度、缺页数等）',
  `CheckedAt` datetime NOT NULL COMMENT '检查时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_code` (`FileCode`),
  KEY `idx_check_type` (`CheckType`),
  KEY `idx_check_result` (`CheckResult`),
  CONSTRAINT `fk_precheck_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='资料质量预审结果'


-- ============================================================
-- Table: ent_file_version
-- ============================================================
CREATE TABLE `ent_file_version` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '源文件编码',
  `VersionNumber` int NOT NULL COMMENT '版本号（从1开始递增）',
  `FileSize` bigint NOT NULL COMMENT '版本文件大小',
  `StoragePath` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'MinIO存储路径',
  `FileHash` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'SHA256哈希',
  `ChangeNotes` text COLLATE utf8mb4_general_ci COMMENT '变更说明',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_file_version` (`FileCode`,`VersionNumber`),
  KEY `idx_file_code` (`FileCode`),
  CONSTRAINT `fk_fver_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件版本'


-- ============================================================
-- Table: ent_table_extraction_result
-- ============================================================
CREATE TABLE `ent_table_extraction_result` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '提取的源文件编码',
  `VersionNumber` int NOT NULL COMMENT '提取的文件版本',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '使用的提取规则编码',
  `TableIndex` int DEFAULT '1' COMMENT '文档中第几个表格',
  `ExtractedJson` json NOT NULL COMMENT '表格内容（JSON）',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度',
  `PositionInfo` json DEFAULT NULL COMMENT '表格在文档中的位置信息',
  `ExtractedAt` datetime NOT NULL COMMENT '提取时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_code` (`FileCode`),
  KEY `idx_rule_code` (`RuleCode`),
  CONSTRAINT `fk_tableext_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`),
  CONSTRAINT `fk_tableext_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_extraction_rule` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='表格提取结果'


-- ============================================================
-- Table: FormCollectionObject
-- ============================================================
CREATE TABLE `FormCollectionObject` (
  `FormCollectionId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `FormId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Title` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `FormData` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  PRIMARY KEY (`FormCollectionId`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: FormDesignOptions
-- ============================================================
CREATE TABLE `FormDesignOptions` (
  `FormId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Title` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DaraggeOptions` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `FormOptions` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `FormConfig` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `FormFields` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `TableConfig` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  PRIMARY KEY (`FormId`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: rpt_audit_report
-- ============================================================
CREATE TABLE `rpt_audit_report` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属报告任务编码',
  `VersionNumber` int DEFAULT '1' COMMENT '报告版本号',
  `ReportTitle` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT '报告标题',
  `FullContent` mediumtext COLLATE utf8mb4_general_ci COMMENT '报告完整内容（Markdown/HTML）',
  `ExportPath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '导出的PDF/Word文件路径',
  `EditedBy` bigint DEFAULT NULL COMMENT '最后编辑人ID',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_task_code` (`TaskCode`),
  CONSTRAINT `fk_report_task` FOREIGN KEY (`TaskCode`) REFERENCES `rpt_report_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告正文'


-- ============================================================
-- Table: rpt_report_section
-- ============================================================
CREATE TABLE `rpt_report_section` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `create_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ReportCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属报告编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对应条款编码（可空，概述/结论章节不映射条款）',
  `SectionName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '章节名称',
  `SectionContent` text COLLATE utf8mb4_general_ci COMMENT '章节填充内容',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '生成此章节的工作流编码',
  `SortOrder` int DEFAULT '0' COMMENT '章节排序',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_report_code` (`ReportCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_workflow_code` (`WorkflowCode`),
  CONSTRAINT `fk_section_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_section_report` FOREIGN KEY (`ReportCode`) REFERENCES `rpt_audit_report` (`Code`),
  CONSTRAINT `fk_section_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告章节内容'


-- ============================================================
-- Table: rpt_report_section_source
-- ============================================================
CREATE TABLE `rpt_report_section_source` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `SectionCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属报告章节编码',
  `SourceType` enum('extraction','finding','nc','manual','template','compliance') COLLATE utf8mb4_general_ci NOT NULL COMMENT '来源类型',
  `SourceCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源记录的编码（根据source_type指向不同表）',
  `SourceDescription` text COLLATE utf8mb4_general_ci COMMENT '来源描述',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT '可信度',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_section_code` (`SectionCode`),
  KEY `idx_source_type` (`SourceType`),
  CONSTRAINT `fk_src_section` FOREIGN KEY (`SectionCode`) REFERENCES `rpt_report_section` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告内容溯源'


-- ============================================================
-- Table: rpt_report_task
-- ============================================================
CREATE TABLE `rpt_report_task` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属企业阶段编码',
  `BasedOnAuditTaskCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '基于的审核任务编码',
  `TemplateCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '使用的报告模板编码',
  `TaskNumber` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务编号',
  `GeneratedAt` datetime DEFAULT NULL COMMENT '生成时间',
  `LockedAt` datetime DEFAULT NULL COMMENT '锁定时间',
  `LockedBy` bigint DEFAULT NULL COMMENT '锁定人ID',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_task_number` (`TaskNumber`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_audit_task_code` (`BasedOnAuditTaskCode`),
  KEY `idx_template_code` (`TemplateCode`),
  KEY `idx_status` (`status`),
  CONSTRAINT `fk_rpttask_audit` FOREIGN KEY (`BasedOnAuditTaskCode`) REFERENCES `audit_task` (`Code`),
  CONSTRAINT `fk_rpttask_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`),
  CONSTRAINT `fk_rpttask_template` FOREIGN KEY (`TemplateCode`) REFERENCES `cert_report_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告任务'


-- ============================================================
-- Table: SellOrder
-- ============================================================
CREATE TABLE `SellOrder` (
  `Order_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `OrderType` int NOT NULL,
  `TranNo` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `SellNo` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Qty` int NOT NULL,
  `AuditDate` datetime DEFAULT NULL,
  `AuditStatus` int NOT NULL,
  `AuditId` int DEFAULT NULL,
  `Auditor` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  PRIMARY KEY (`Order_Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: SellOrderList
-- ============================================================
CREATE TABLE `SellOrderList` (
  `OrderList_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Order_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `ProductName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `MO` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Qty` int NOT NULL,
  `Weight` decimal(18,2) DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  PRIMARY KEY (`OrderList_Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: sys_api
-- ============================================================
CREATE TABLE `sys_api` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(64) NOT NULL COMMENT '接口编码（SHA256 Hash）',
  `method` varchar(10) NOT NULL COMMENT 'HTTP方法: GET/POST/PUT/DELETE',
  `path` varchar(200) NOT NULL COMMENT '接口路径',
  `group_path` varchar(200) NOT NULL COMMENT '业务分组（如 系统管理/用户管理）',
  `name` varchar(200) NOT NULL COMMENT '接口名称',
  `author` varchar(50) DEFAULT NULL COMMENT '负责人（可选）',
  `enable` tinyint(1) DEFAULT '1' COMMENT '是否启用（0=禁用，前端不展示）',
  `create_date` datetime DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_api_code` (`code`),
  KEY `idx_api_group` (`group_path`),
  KEY `idx_api_path` (`path`)
) ENGINE=InnoDB AUTO_INCREMENT=240 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='接口表'


-- ============================================================
-- Table: Sys_City
-- ============================================================
CREATE TABLE `Sys_City` (
  `CityId` int NOT NULL AUTO_INCREMENT,
  `CityCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CityName` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ProvinceCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`CityId`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=346 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: sys_config
-- ============================================================
CREATE TABLE `sys_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateID` int DEFAULT NULL COMMENT '创建人ID',
  `Creator` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人姓名',
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
  `Modifier` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '修改人姓名',
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
  `Deleter` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人姓名',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `Enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ConfigKey` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '参数键',
  `ConfigValue` text COLLATE utf8mb4_general_ci NOT NULL COMMENT '参数值',
  `ValueType` enum('string','number','boolean','json') COLLATE utf8mb4_general_ci DEFAULT 'string' COMMENT '值类型',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '参数说明',
  `IsSystem` tinyint(1) DEFAULT '0' COMMENT '是否系统级（不可删除）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_config_key` (`ConfigKey`),
  KEY `idx_value_type` (`ValueType`),
  KEY `idx_is_system` (`IsSystem`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='系统参数'


-- ============================================================
-- Table: Sys_Dictionary
-- ============================================================
CREATE TABLE `Sys_Dictionary` (
  `Dic_ID` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '稳定标识（随机唯一，关联键）',
  `Config` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateDate` datetime DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DBServer` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `DbSql` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `DicName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DicNo` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字典编码（可选，仅用于显示）',
  `Enable` tinyint NOT NULL,
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '删除标志（1=已删除）',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人',
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父节点 Code（根节点为 NULL）',
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  PRIMARY KEY (`Dic_ID`) USING BTREE,
  UNIQUE KEY `uk_sys_dictionary_code` (`Code`),
  UNIQUE KEY `uk_sys_dictionary_dicno` (`DicNo`),
  KEY `idx_sys_dictionary_pcode` (`ParentCode`)
) ENGINE=InnoDB AUTO_INCREMENT=151 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_DictionaryList
-- ============================================================
CREATE TABLE `Sys_DictionaryList` (
  `DicList_ID` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '稳定标识（随机唯一，定位键）',
  `CreateDate` datetime DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicValue` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属字典 Code（= Sys_Dictionary.Code）',
  `Enable` tinyint DEFAULT NULL,
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '删除标志（1=已删除）',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人',
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `Color` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`DicList_ID`) USING BTREE,
  UNIQUE KEY `uk_sys_dictionarylist_code` (`Code`),
  KEY `idx_sys_dictionarylist_dicode` (`DicCode`)
) ENGINE=InnoDB AUTO_INCREMENT=688 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: sys_log
-- ============================================================
CREATE TABLE `sys_log` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateID` int DEFAULT NULL COMMENT '创建人ID',
  `Creator` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人姓名',
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
  `Modifier` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '修改人姓名',
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
  `Deleter` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人姓名',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `Enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `UserId` bigint DEFAULT NULL COMMENT '操作用户ID',
  `Module` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '操作模块',
  `Action` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '操作动作',
  `TargetType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '操作对象类型（表名）',
  `TargetId` bigint DEFAULT NULL COMMENT '操作对象ID',
  `Detail` json DEFAULT NULL COMMENT '操作详情（变更前后值等）',
  `IpAddress` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '操作IP',
  `UserAgent` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '用户代理',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_user_id` (`UserId`),
  KEY `idx_module` (`Module`),
  KEY `idx_action` (`Action`),
  KEY `idx_create_time` (`CreateDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='系统日志'


-- ============================================================
-- Table: Sys_Menu
-- ============================================================
CREATE TABLE `Sys_Menu` (
  `Menu_Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `ParentCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT '0',
  `MenuName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Auth` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `Icon` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Description` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Url` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateDate` datetime DEFAULT NULL,
  `Creator` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `Modifier` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Tag` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '菜单分类标签：admin/auditor/enterprise/common',
  PRIMARY KEY (`Menu_Id`) USING BTREE,
  UNIQUE KEY `uk_menu_code` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=219 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_Organization
-- ============================================================
CREATE TABLE `Sys_Organization` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `OrgName` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `OrgCode` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ParentCode` varchar(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `OrgType` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'Dept',
  `OrgLevel` int DEFAULT NULL,
  `OrgPath` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `LeaderName` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `LeaderPhone` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `Sort` int DEFAULT '0',
  `Enable` tinyint DEFAULT '1',
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志: 1=有效, 0=无效',
  `Remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreateDate` datetime DEFAULT CURRENT_TIMESTAMP,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteID` int DEFAULT NULL,
  `Deleter` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Code` (`Code`),
  KEY `idx_parent_code` (`ParentCode`),
  KEY `idx_org_code` (`OrgCode`),
  KEY `idx_enable` (`Enable`)
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci


-- ============================================================
-- Table: Sys_Province
-- ============================================================
CREATE TABLE `Sys_Province` (
  `ProvinceId` int NOT NULL AUTO_INCREMENT,
  `ProvinceCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `ProvinceName` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `RegionCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`ProvinceId`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=44 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_QuartzLog
-- ============================================================
CREATE TABLE `Sys_QuartzLog` (
  `LogId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `TaskName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '任务名称',
  `ElapsedTime` int DEFAULT NULL COMMENT '耗时(秒)',
  `StratDate` datetime DEFAULT NULL COMMENT '开始时间',
  `EndDate` datetime DEFAULT NULL COMMENT '结束时间',
  `Result` int DEFAULT NULL COMMENT '执行结果',
  `ResponseContent` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '返回内容',
  `ErrorMsg` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  PRIMARY KEY (`LogId`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_QuartzOptions
-- ============================================================
CREATE TABLE `Sys_QuartzOptions` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `TaskName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务名称',
  `GroupName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务分组',
  `CronExpression` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Corn表达式',
  `Method` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '请求方式',
  `ApiUrl` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT 'Url地址',
  `AuthKey` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AuthValue` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Describe` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '描述',
  `LastRunTime` datetime DEFAULT NULL COMMENT '最后执行执行',
  `Status` int DEFAULT NULL COMMENT '运行状态',
  `PostData` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT 'post参数',
  `TimeOut` int DEFAULT NULL COMMENT '超时时间(秒)',
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL COMMENT '创建时间',
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_Role
-- ============================================================
CREATE TABLE `Sys_Role` (
  `Role_Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父节点编码（树结构，根节点为 NULL）',
  `CreateDate` datetime DEFAULT NULL,
  `Creator` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeptName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Dept_Id` int DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `Modifier` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `ParentId` int NOT NULL,
  `RoleName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Role_Id`) USING BTREE,
  UNIQUE KEY `uk_role_code` (`Code`),
  KEY `idx_role_parent_code` (`ParentCode`)
) ENGINE=InnoDB AUTO_INCREMENT=301 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: sys_role_api
-- ============================================================
CREATE TABLE `sys_role_api` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `role_code` varchar(50) NOT NULL COMMENT '角色编码',
  `api_code` varchar(64) NOT NULL COMMENT '接口编码',
  `create_date` datetime DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_role_api` (`role_code`,`api_code`),
  KEY `idx_api_code` (`api_code`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='角色-接口关联表'


-- ============================================================
-- Table: Sys_RoleAuth
-- ============================================================
CREATE TABLE `Sys_RoleAuth` (
  `Auth_Id` int NOT NULL AUTO_INCREMENT,
  `AuthValue` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `Creator` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `Menu_Id` int NOT NULL,
  `MenuCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '菜单编码',
  `Modifier` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `ModifyDate` datetime DEFAULT NULL,
  `Role_Id` int DEFAULT NULL,
  `RoleCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '角色编码',
  `User_Id` int DEFAULT NULL,
  `UserCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '用户编码',
  PRIMARY KEY (`Auth_Id`) USING BTREE,
  KEY `idx_roleauth_menu_code` (`MenuCode`),
  KEY `idx_roleauth_role_code` (`RoleCode`),
  KEY `idx_roleauth_user_code` (`UserCode`)
) ENGINE=InnoDB AUTO_INCREMENT=368 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_RoleMenu
-- ============================================================
CREATE TABLE `Sys_RoleMenu` (
  `Id` varchar(64) NOT NULL COMMENT '主键',
  `RoleCode` varchar(64) NOT NULL COMMENT '角色编码（Sys_Role.Code）',
  `MenuCode` varchar(50) NOT NULL COMMENT '菜单编码（Sys_Menu.Code）',
  `OrderNo` int DEFAULT NULL COMMENT '排序号',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy` varchar(64) DEFAULT NULL COMMENT '创建人',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_role_menu` (`RoleCode`,`MenuCode`),
  KEY `idx_rm_role` (`RoleCode`),
  KEY `idx_rm_menu` (`MenuCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='角色-菜单关联表'


-- ============================================================
-- Table: Sys_RoleUser
-- ============================================================
CREATE TABLE `Sys_RoleUser` (
  `Id` varchar(64) NOT NULL,
  `RoleCode` varchar(64) NOT NULL COMMENT '角色编码',
  `UserCode` varchar(64) NOT NULL COMMENT '用户编码',
  `OrderNo` int DEFAULT NULL COMMENT '排序号',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `CreateBy` varchar(64) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_role_user` (`RoleCode`,`UserCode`),
  KEY `idx_role_code` (`RoleCode`),
  KEY `idx_user_code` (`UserCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='角色-用户关联表'


-- ============================================================
-- Table: Sys_TableColumn
-- ============================================================
CREATE TABLE `Sys_TableColumn` (
  `ColumnId` int NOT NULL AUTO_INCREMENT,
  `ApiInPut` int DEFAULT NULL,
  `ApiIsNull` int DEFAULT NULL,
  `ApiOutPut` int DEFAULT NULL,
  `ColSize` int DEFAULT NULL,
  `ColumnCNName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ColumnName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ColumnType` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `ColumnWidth` int DEFAULT NULL,
  `Columnformat` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DropNo` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `EditColNo` int DEFAULT NULL,
  `EditRowNo` int DEFAULT NULL,
  `EditType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` int DEFAULT NULL,
  `IsColumnData` int DEFAULT NULL,
  `IsDisplay` int DEFAULT NULL,
  `IsImage` int DEFAULT NULL,
  `IsKey` int DEFAULT NULL,
  `IsNull` int DEFAULT NULL,
  `IsReadDataset` int DEFAULT NULL,
  `Maxlength` int DEFAULT NULL,
  `Modifier` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Script` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `SearchColNo` int DEFAULT NULL,
  `SearchRowNo` int DEFAULT NULL,
  `SearchType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sortable` int DEFAULT NULL,
  `TableName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Table_Id` int DEFAULT NULL,
  `Placeholder` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AddDefaultValue` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UploadOption` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SearchDateRange` int DEFAULT NULL,
  `SearchDefaultValue` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CustomValidate` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsUnique` int DEFAULT NULL,
  `SummaryType` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `HeaderFilter` int DEFAULT NULL,
  `TextAlign` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ShowOverflowTooltip` int DEFAULT NULL,
  `FixedColumn` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CalcColumn` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Text1` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Text2` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`ColumnId`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=1828 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_TableInfo
-- ============================================================
CREATE TABLE `Sys_TableInfo` (
  `Table_Id` int NOT NULL AUTO_INCREMENT,
  `CnName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ColumnCNName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DBServer` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `DataTableType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DetailCnName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DetailName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `EditorType` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` int DEFAULT NULL,
  `ExpressField` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FolderName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Namespace` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `ParentId` int DEFAULT NULL,
  `RichText` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SortName` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `TableName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `TableTrueName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UploadField` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UploadMaxCount` int DEFAULT NULL,
  `AsyncApi` int DEFAULT NULL,
  `Text1` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Text2` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `QuickQueryFields` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ShowDetail` int DEFAULT NULL,
  `FixedSearch` int DEFAULT NULL,
  `MainKeyField` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Table_Id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=89 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_User
-- ============================================================
CREATE TABLE `Sys_User` (
  `User_Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `Role_Id` int NOT NULL,
  `RoleName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `PhoneNo` varchar(11) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Remark` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Tel` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UserName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `UserPwd` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UserTrueName` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DeptName` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Dept_Id` int DEFAULT NULL,
  `Email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint NOT NULL,
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志: 1=有效, 0=无效',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '软删除标记',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人Code',
  `UserType` tinyint NOT NULL DEFAULT '10' COMMENT '用户类型：1=超级管理员, 10=总管理员, 13=运维人员, 14=配置人员, 15=质量专员, 20=审核管理员, 21=审核组长, 22=普通审核员, 30=企业账号',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构编码（多租户隔离），NULL表示平台管理层',
  `OrgId` bigint DEFAULT NULL COMMENT '机构ID，关联cert_org_config.id',
  `ParentUserId` int DEFAULT NULL COMMENT '上级用户ID，用于企业子账号或审核员层级',
  `Gender` int DEFAULT NULL,
  `HeadImageUrl` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `IsRegregisterPhone` int DEFAULT NULL,
  `LastLoginDate` datetime DEFAULT NULL,
  `LastModifyPwdDate` datetime DEFAULT NULL,
  `Address` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AppType` int DEFAULT NULL,
  `AuditDate` datetime DEFAULT NULL,
  `AuditStatus` int DEFAULT NULL,
  `Auditor` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Token` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateID` int DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `Creator` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Mobile` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Modifier` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `DeptIds` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `wechat_openid` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '微信 OpenID',
  `wechat_unionid` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '微信 UnionID',
  PRIMARY KEY (`User_Id`) USING BTREE,
  UNIQUE KEY `uk_wechat_openid` (`wechat_openid`),
  UNIQUE KEY `uk_wechat_unionid` (`wechat_unionid`),
  UNIQUE KEY `uk_user_code` (`Code`),
  KEY `idx_sys_user_org_code` (`OrgCode`),
  KEY `idx_sys_user_user_type` (`UserType`),
  KEY `IX_Sys_User_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB AUTO_INCREMENT=3384 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: sys_user_permission
-- ============================================================
CREATE TABLE `sys_user_permission` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `user_code` varchar(36) NOT NULL COMMENT '用户编码',
  `api_code` varchar(64) NOT NULL COMMENT '接口编码',
  `create_date` datetime DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_user_api` (`user_code`,`api_code`),
  KEY `idx_user_code` (`user_code`)
) ENGINE=InnoDB AUTO_INCREMENT=64 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户权限缓存表'


-- ============================================================
-- Table: Sys_UserDepartment
-- ============================================================
CREATE TABLE `Sys_UserDepartment` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `UserId` int NOT NULL,
  `DepartmentId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Enable` int NOT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_WorkFlow
-- ============================================================
CREATE TABLE `Sys_WorkFlow` (
  `WorkFlow_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `WorkName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '流程名称',
  `WorkTable` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '表名',
  `WorkTableName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '功能菜单',
  `NodeConfig` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '节点信息',
  `LineConfig` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '连接配置',
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '备注',
  `Weight` int DEFAULT NULL COMMENT '权重',
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `AuditingEdit` int DEFAULT NULL,
  PRIMARY KEY (`WorkFlow_Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_WorkFlowStep
-- ============================================================
CREATE TABLE `Sys_WorkFlowStep` (
  `WorkStepFlow_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlow_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '流程主表id',
  `StepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '流程节点Id',
  `StepName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '节点名称',
  `StepType` int DEFAULT NULL COMMENT '节点类型(1=按用户审批,2=按角色审批)',
  `StepValue` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '审批用户id或角色id',
  `OrderId` int DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '备注',
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `NextStepIds` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `ParentId` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `AuditRefuse` int DEFAULT NULL,
  `AuditBack` int DEFAULT NULL,
  `AuditMethod` int DEFAULT NULL,
  `SendMail` int DEFAULT NULL,
  `Filters` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `StepAttrType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Weight` int DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_WorkFlowTable
-- ============================================================
CREATE TABLE `Sys_WorkFlowTable` (
  `WorkFlowTable_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlow_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WorkName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WorkTableKey` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '表主键id',
  `WorkTable` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '表名',
  `WorkTableName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '表中文名',
  `CurrentOrderId` int DEFAULT NULL,
  `AuditStatus` int DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `CurrentStepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_WorkFlowTableAuditLog
-- ============================================================
CREATE TABLE `Sys_WorkFlowTableAuditLog` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlowTable_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WorkFlowTableStep_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AuditId` int DEFAULT NULL,
  `Auditor` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AuditStatus` int DEFAULT NULL,
  `AuditResult` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `AuditDate` datetime DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateDate` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: Sys_WorkFlowTableStep
-- ============================================================
CREATE TABLE `Sys_WorkFlowTableStep` (
  `Sys_WorkFlowTableStep_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlowTable_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlow_Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepType` int DEFAULT NULL,
  `StepValue` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `OrderId` int DEFAULT NULL,
  `AuditId` int DEFAULT NULL COMMENT '审核人id',
  `Auditor` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '审核人',
  `AuditStatus` int DEFAULT NULL COMMENT '审核状态',
  `AuditDate` datetime DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  `StepAttrType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ParentId` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `NextStepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Weight` int DEFAULT NULL,
  PRIMARY KEY (`Sys_WorkFlowTableStep_Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: TestDb
-- ============================================================
CREATE TABLE `TestDb` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `TestDbName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `TestDbContent` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: TestService
-- ============================================================
CREATE TABLE `TestService` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DbName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DbContent` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Modifier` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `ModifyID` int DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC


-- ============================================================
-- Table: v_cert_phase_definition
-- ============================================================
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `yzh_cert_platform`.`v_cert_phase_definition` AS select `p`.`id` AS `id`,`p`.`code` AS `code`,`p`.`phase_code` AS `phase_code`,`p`.`phase_name` AS `phase_name`,`p`.`sequence_order` AS `sequence_order`,`p`.`description` AS `description`,`p`.`is_valid` AS `is_valid`,`p`.`create_time` AS `create_time`,`p`.`create_by` AS `create_by`,`p`.`update_time` AS `update_time`,`p`.`update_by` AS `update_by`,`p`.`delete_time` AS `delete_time`,`p`.`delete_by` AS `delete_by`,`p`.`is_deleted` AS `is_deleted`,(case `p`.`is_valid` when 1 then '启用' else '停用' end) AS `status_name` from `yzh_cert_platform`.`cert_phase_definition` `p` where (`p`.`is_deleted` = 0)	utf8mb4	utf8mb4_0900_ai_ci


-- ============================================================
-- Table: v_cert_stage
-- ============================================================
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `yzh_cert_platform`.`v_cert_stage` AS select `yzh_cert_platform`.`s`.`id` AS `id`,`yzh_cert_platform`.`s`.`code` AS `code`,`yzh_cert_platform`.`s`.`phase_code` AS `phase_code`,`yzh_cert_platform`.`s`.`phase_name` AS `phase_name`,`yzh_cert_platform`.`s`.`description` AS `description`,`yzh_cert_platform`.`s`.`sort_order` AS `sort_order`,`yzh_cert_platform`.`s`.`category` AS `category`,`yzh_cert_platform`.`cat`.`DicName` AS `CategoryName`,`yzh_cert_platform`.`s`.`status` AS `status`,(case `yzh_cert_platform`.`s`.`status` when 'active' then '启用' when 'inactive' then '停用' else `yzh_cert_platform`.`s`.`status` end) AS `StatusName`,`yzh_cert_platform`.`s`.`remark` AS `remark`,`yzh_cert_platform`.`s`.`enable` AS `enable`,`yzh_cert_platform`.`s`.`create_id` AS `create_id`,`yzh_cert_platform`.`s`.`creator` AS `creator`,`yzh_cert_platform`.`s`.`create_date` AS `create_date`,`yzh_cert_platform`.`s`.`modify_id` AS `modify_id`,`yzh_cert_platform`.`s`.`modifier` AS `modifier`,`yzh_cert_platform`.`s`.`modify_date` AS `modify_date`,`yzh_cert_platform`.`s`.`delete_id` AS `delete_id`,`yzh_cert_platform`.`s`.`deleter` AS `deleter`,`yzh_cert_platform`.`s`.`delete_time` AS `delete_time` from (`yzh_cert_platform`.`cert_cert_stage` `s` left join `yzh_cert_platform`.`sys_dictionarylist` `cat` on(((`yzh_cert_platform`.`cat`.`DicValue` = (`yzh_cert_platform`.`s`.`category` collate utf8mb4_unicode_ci)) and (`yzh_cert_platform`.`cat`.`Dic_ID` = (select `yzh_cert_platform`.`sys_dictionary`.`Dic_ID` from `yzh_cert_platform`.`sys_dictionary` where (`yzh_cert_platform`.`sys_dictionary`.`DicNo` = 'stage_category') limit 1)))))	utf8mb4	utf8mb4_0900_ai_ci


-- ============================================================
-- Table: v_certification_body
-- ============================================================
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `yzh_cert_platform`.`v_certification_body` AS select `cb`.`Id` AS `Id`,`cb`.`Code` AS `Code`,`cb`.`OrgCode` AS `OrgCode`,`cb`.`status` AS `status`,(case `cb`.`status` when 'active' then '启用' else `cb`.`status` end) AS `StatusName`,`yzh_cert_platform`.`cb`.`enable` AS `enable`,`yzh_cert_platform`.`cb`.`Sort` AS `Sort`,`yzh_cert_platform`.`cb`.`Remark` AS `Remark`,`yzh_cert_platform`.`cb`.`create_id` AS `create_id`,`yzh_cert_platform`.`cb`.`creator` AS `creator`,`yzh_cert_platform`.`cb`.`create_date` AS `create_date`,`yzh_cert_platform`.`cb`.`modify_id` AS `modify_id`,`yzh_cert_platform`.`cb`.`modifier` AS `modifier`,`yzh_cert_platform`.`cb`.`modify_date` AS `modify_date`,`yzh_cert_platform`.`cb`.`delete_id` AS `delete_id`,`yzh_cert_platform`.`cb`.`deleter` AS `deleter`,`yzh_cert_platform`.`cb`.`delete_time` AS `delete_time`,`yzh_cert_platform`.`cb`.`name` AS `name`,`yzh_cert_platform`.`cb`.`short_name` AS `short_name`,`yzh_cert_platform`.`cb`.`cb_code` AS `cb_code`,`yzh_cert_platform`.`cb`.`contact_name` AS `contact_name`,`yzh_cert_platform`.`cb`.`contact_phone` AS `contact_phone`,`yzh_cert_platform`.`cb`.`legal_person` AS `legal_person`,`yzh_cert_platform`.`cb`.`contact_email` AS `contact_email`,`yzh_cert_platform`.`cb`.`address` AS `address`,`yzh_cert_platform`.`cb`.`logo_url` AS `logo_url`,`yzh_cert_platform`.`cb`.`scope_text` AS `scope_text`,`yzh_cert_platform`.`cb`.`theme_config` AS `theme_config`,`yzh_cert_platform`.`cb`.`login_config` AS `login_config`,`yzh_cert_platform`.`cb`.`max_users` AS `max_users`,`yzh_cert_platform`.`cb`.`max_enterprises` AS `max_enterprises`,`yzh_cert_platform`.`cb`.`expire_date` AS `expire_date` from `yzh_cert_platform`.`cert_certification_body` `cb`	utf8mb4	utf8mb4_0900_ai_ci


-- ============================================================
-- Table: v_iso_standard
-- ============================================================
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `yzh_cert_platform`.`v_iso_standard` AS select `s`.`Id` AS `Id`,`s`.`Code` AS `Code`,`s`.`OrgCode` AS `OrgCode`,`s`.`cb_code` AS `cb_code`,`cb`.`short_name` AS `CbName`,`s`.`standard_code` AS `standard_code`,`s`.`standard_name` AS `standard_name`,`s`.`version_year` AS `version_year`,`s`.`category` AS `category`,`cat`.`DicName` AS `CategoryName`,`s`.`description` AS `description`,`s`.`status` AS `status`,(case `s`.`status` when 'active' then '启用' when 'inactive' then '停用' else `s`.`status` end) AS `StatusName`,`yzh_cert_platform`.`s`.`creator` AS `creator`,`yzh_cert_platform`.`s`.`create_by` AS `create_by`,`yzh_cert_platform`.`s`.`create_date` AS `create_date`,`yzh_cert_platform`.`s`.`modifier` AS `modifier`,`yzh_cert_platform`.`s`.`update_by` AS `update_by`,`yzh_cert_platform`.`s`.`modify_date` AS `modify_date`,`yzh_cert_platform`.`s`.`deleter` AS `deleter`,`yzh_cert_platform`.`s`.`delete_by` AS `delete_by`,`yzh_cert_platform`.`s`.`delete_time` AS `delete_time`,`yzh_cert_platform`.`s`.`enable` AS `enable`,`yzh_cert_platform`.`s`.`Sort` AS `Sort`,`yzh_cert_platform`.`s`.`Remark` AS `Remark` from ((`yzh_cert_platform`.`cert_iso_standard` `s` left join `yzh_cert_platform`.`cert_certification_body` `cb` on((`yzh_cert_platform`.`s`.`cb_code` = `yzh_cert_platform`.`cb`.`Code`))) left join `yzh_cert_platform`.`sys_dictionarylist` `cat` on(((`yzh_cert_platform`.`cat`.`DicCode` = 'iso_category') and (`yzh_cert_platform`.`cat`.`DicValue` = `yzh_cert_platform`.`s`.`category`))))	utf8mb4	utf8mb4_0900_ai_ci


-- ============================================================
-- Table: v_sys_user
-- ============================================================
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `yzh_cert_platform`.`v_sys_user` AS select `u`.`User_Id` AS `User_Id`,`u`.`Code` AS `Code`,`u`.`UserName` AS `UserName`,`u`.`UserTrueName` AS `UserTrueName`,`u`.`UserPwd` AS `UserPwd`,`u`.`Role_Id` AS `Role_Id`,`u`.`Enable` AS `Enable`,`u`.`IsValid` AS `IsValid`,`u`.`IsDeleted` AS `IsDeleted`,`u`.`DeleteTime` AS `DeleteTime`,`u`.`DeleteBy` AS `DeleteBy`,`u`.`OrgCode` AS `OrgCode`,`u`.`Gender` AS `Gender`,`u`.`PhoneNo` AS `PhoneNo`,`u`.`Email` AS `Email`,`u`.`HeadImageUrl` AS `HeadImageUrl`,`u`.`Address` AS `Address`,`u`.`Remark` AS `Remark`,`u`.`LastLoginDate` AS `LastLoginDate`,`u`.`LastModifyPwdDate` AS `LastModifyPwdDate`,`u`.`OrderNo` AS `OrderNo`,`u`.`Token` AS `Token`,`u`.`CreateID` AS `CreateID`,`u`.`CreateDate` AS `CreateDate`,`u`.`Creator` AS `Creator`,`u`.`Modifier` AS `Modifier`,`u`.`ModifyDate` AS `ModifyDate`,`u`.`ModifyID` AS `ModifyID`,`r`.`RoleName` AS `RoleName`,`o`.`OrgName` AS `OrgName` from ((`yzh_cert_platform`.`sys_user` `u` left join `yzh_cert_platform`.`sys_role` `r` on((`u`.`Role_Id` = `r`.`Role_Id`))) left join `yzh_cert_platform`.`sys_organization` `o` on((`u`.`OrgCode` = (`o`.`Code` collate utf8mb4_unicode_ci))))	utf8mb4	utf8mb4_0900_ai_ci


-- ============================================================
-- Table: v_workflow
-- ============================================================
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `yzh_cert_platform`.`v_workflow` AS select `w`.`Id` AS `Id`,`w`.`Code` AS `Code`,`w`.`OrgCode` AS `OrgCode`,`w`.`WorkflowCode` AS `WorkflowCode`,`w`.`WorkflowName` AS `WorkflowName`,`w`.`WorkflowType` AS `WorkflowType`,(case `w`.`WorkflowType` when 'extraction' then '提取' when 'validation' then '审核' when 'report' then '报告' else `w`.`WorkflowType` end) AS `WorkflowTypeName`,`w`.`WorkflowConfig` AS `WorkflowConfig`,`w`.`Version` AS `Version`,`w`.`IsActive` AS `IsActive`,(case `w`.`IsActive` when 1 then '启用' when 0 then '停用' else cast(`w`.`IsActive` as char charset utf8mb4) end) AS `IsActiveName`,`w`.`Description` AS `Description`,`w`.`status` AS `status`,(case `w`.`status` when 'active' then '启用' when 'inactive' then '停用' else `w`.`status` end) AS `StatusName`,`w`.`Sort` AS `Sort`,`w`.`Remark` AS `Remark`,`w`.`enable` AS `enable`,`yzh_cert_platform`.`w`.`create_id` AS `create_id`,`yzh_cert_platform`.`w`.`creator` AS `creator`,`yzh_cert_platform`.`w`.`create_date` AS `create_date`,`yzh_cert_platform`.`w`.`modify_id` AS `modify_id`,`yzh_cert_platform`.`w`.`modifier` AS `modifier`,`yzh_cert_platform`.`w`.`modify_date` AS `modify_date`,`yzh_cert_platform`.`w`.`delete_id` AS `delete_id`,`yzh_cert_platform`.`w`.`deleter` AS `deleter`,`yzh_cert_platform`.`w`.`delete_time` AS `delete_time` from `yzh_cert_platform`.`wf_workflow_definition` `w`	utf8mb4	utf8mb4_0900_ai_ci


-- ============================================================
-- Table: wf_execution_task
-- ============================================================
CREATE TABLE `wf_execution_task` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人姓名',
  `create_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '修改人姓名',
  `update_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人姓名',
  `delete_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `sort` int DEFAULT '0' COMMENT '排序号',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `task_type` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务类型：TEST | NC_CHECK | REPORT_GENERATE',
  `task_status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'queued' COMMENT '执行状态：queued|executing|completed|failed|cancelled',
  `config_snapshot` json NOT NULL COMMENT '执行时的工作流配置快照（从cert_validation_rule.rule_json锁定）',
  `rule_code` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'cert_validation_rule.rule_code（配置来源）',
  `enterprise_code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '企业编码（运行时绑定）',
  `phase_code` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '审核阶段',
  `queue_code` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'yzh_queue.queue_code（关联队列层）',
  `cache_keys` text COLLATE utf8mb4_general_ci COMMENT '预热的缓存键列表（JSON数组，任务级缓存方案）',
  `result_summary` json DEFAULT NULL COMMENT '执行结果摘要（end节点输出）',
  `error_message` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '失败原因',
  `started_at` datetime DEFAULT NULL COMMENT '开始执行时间',
  `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
  `duration_ms` int DEFAULT NULL COMMENT '执行耗时(ms)',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  KEY `idx_type_status` (`task_type`,`task_status`),
  KEY `idx_rule` (`rule_code`),
  KEY `idx_enterprise` (`enterprise_code`),
  KEY `idx_phase` (`phase_code`),
  KEY `idx_queue` (`queue_code`),
  KEY `idx_status` (`task_status`)
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流执行任务（TEST/NC_CHECK/REPORT_GENERATE）'


-- ============================================================
-- Table: wf_execution_task_item
-- ============================================================
CREATE TABLE `wf_execution_task_item` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人姓名',
  `create_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '修改人姓名',
  `update_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人姓名',
  `delete_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `sort` int DEFAULT '0' COMMENT '排序号',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `task_code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'wf_execution_task.code（所属任务）',
  `rule_code` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'cert_validation_rule.rule_code（关联配置定义）',
  `item_type` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'NC_CHECK | REPORT_GENERATE',
  `item_status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'queued' COMMENT 'queued|executing|completed|failed|cancelled',
  `is_success` tinyint DEFAULT NULL COMMENT '业务成功标志：1=成功 0=失败 NULL=未完成',
  `cache_keys` text COLLATE utf8mb4_general_ci COMMENT '预热的缓存键列表（JSON数组，任务级缓存方案）',
  `result_summary` json DEFAULT NULL COMMENT '本项执行结果（end节点输出）',
  `error_message` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '失败原因',
  `started_at` datetime DEFAULT NULL COMMENT '开始执行时间',
  `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
  `duration_ms` int DEFAULT NULL COMMENT '执行耗时(ms)',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_task_rule` (`task_code`,`rule_code`),
  KEY `idx_task` (`task_code`),
  KEY `idx_task_status` (`task_code`,`item_status`),
  KEY `idx_rule` (`rule_code`)
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='执行项（一个NC检查项 / 一个报告章节）'


-- ============================================================
-- Table: wf_field_label_mapping
-- ============================================================
CREATE TABLE `wf_field_label_mapping` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `LabelTag` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT '字段标签，如 [ISO9001_企业基础资料_营业执照_企业名称]',
  `FieldCode` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '字段编码，如 iso9001.ent_base.biz_lic.Name',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属标准编码',
  `ScopeLevel` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '层级路径，如 企业基础资料/营业执照',
  `DocumentName` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '所属文档名称',
  `FieldName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字段名称',
  `DataType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '数据类型',
  `SkillCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '提取此字段的Skill编码',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_label_tag` (`LabelTag`),
  KEY `idx_field_code` (`FieldCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_skill_code` (`SkillCode`),
  CONSTRAINT `fk_flm_skill` FOREIGN KEY (`SkillCode`) REFERENCES `wf_skill` (`Code`),
  CONSTRAINT `fk_flm_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='字段标签映射'


-- ============================================================
-- Table: wf_node_execution
-- ============================================================
CREATE TABLE `wf_node_execution` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人姓名',
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '修改人姓名',
  `modify_date` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人姓名',
  `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `sort` int DEFAULT '0' COMMENT '排序号',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `task_code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'wf_execution_task.code',
  `item_code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'wf_execution_task_item.code',
  `node_id` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '节点ID（前端生成的 classCode_n序号）',
  `node_type` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'start|end|skill|ai_node|logic|branch|docField|docTable',
  `node_title` varchar(128) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '节点名称快照',
  `skill_code` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'Skill编码（功能节点）',
  `exec_status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'pending' COMMENT 'pending|executing|completed|failed|skipped',
  `output_json` json DEFAULT NULL COMMENT '节点输出（所有端口的JSON）',
  `error_message` varchar(1000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '执行错误信息',
  `started_at` datetime DEFAULT NULL COMMENT '开始执行时间',
  `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
  `execution_time_ms` int DEFAULT NULL COMMENT '执行耗时(ms)',
  `is_reused` tinyint DEFAULT '0' COMMENT '0=新执行 1=复用了历史结果',
  `create_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_task_item_node` (`task_code`,`item_code`,`node_id`),
  KEY `idx_task_item` (`task_code`,`item_code`),
  KEY `idx_task` (`task_code`),
  KEY `idx_node_id` (`node_id`),
  KEY `idx_exec_status` (`exec_status`)
) ENGINE=InnoDB AUTO_INCREMENT=67 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='节点执行状态（跨路径复用的核心载体）'


-- ============================================================
-- Table: wf_prompt_template
-- ============================================================
CREATE TABLE `wf_prompt_template` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键',
  `code` varchar(100) NOT NULL COMMENT '全局唯一编码（GUID）',
  `org_code` varchar(50) DEFAULT NULL COMMENT '多租户组织编码',
  `creator` varchar(50) DEFAULT NULL COMMENT '创建人姓名',
  `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) DEFAULT NULL COMMENT '修改人姓名',
  `modify_date` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) DEFAULT NULL COMMENT '删除人姓名',
  `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) DEFAULT 'active' COMMENT '实体启用状态',
  `enable` tinyint(1) DEFAULT '1' COMMENT '实体启用标记',
  `sort` int DEFAULT '0' COMMENT '排序',
  `remark` varchar(500) DEFAULT NULL COMMENT '备注',
  `prompt_code` varchar(100) NOT NULL COMMENT '提示词编码（如 analyze_word_v1）',
  `prompt_name` varchar(200) NOT NULL COMMENT '提示词名称',
  `prompt_type` varchar(50) NOT NULL COMMENT '类型：analyze/extract/verify/validate/report',
  `skill_target` varchar(50) DEFAULT NULL COMMENT '适用技能：word/excel/pdf/all',
  `template` mediumtext COMMENT '提示词模板（支持占位符）',
  `description` text COMMENT '说明',
  `version` int NOT NULL DEFAULT '1' COMMENT '版本号',
  `is_active` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否当前生效',
  `last_test_result` text COMMENT '最后测试结果（JSON）',
  `create_by` varchar(50) DEFAULT NULL,
  `update_by` varchar(50) DEFAULT NULL,
  `delete_by` varchar(50) DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_prompt_code` (`prompt_code`),
  KEY `idx_prompt_type` (`prompt_type`),
  KEY `idx_prompt_active` (`is_active`,`prompt_type`)
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Prompt模板表'


-- ============================================================
-- Table: wf_skill_api
-- ============================================================
CREATE TABLE `wf_skill_api` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `skill_code` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '所属 Skill（与 wf_skill.skill_code 同 collation）',
  `url` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `http_method` varchar(10) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'POST',
  `headers` text COLLATE utf8mb4_unicode_ci COMMENT '请求头 JSON（值可含 $sys. 引用）',
  `auth_config` text COLLATE utf8mb4_unicode_ci COMMENT '鉴权 JSON: {"type":"bearer","tokenSource":"$sys.XXX"}——密钥不落库',
  `param_mapping` text COLLATE utf8mb4_unicode_ci COMMENT '参数映射: {"输入项名":"请求参数名"}',
  `response_mapping` text COLLATE utf8mb4_unicode_ci COMMENT '响应解析: {"输出项名":"$.data.xxx"}',
  `timeout_seconds` int NOT NULL DEFAULT '30',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `creator` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_api` (`skill_code`),
  CONSTRAINT `fk_api_skill` FOREIGN KEY (`skill_code`) REFERENCES `wf_skill` (`skill_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='api 型 Skill 信息（1:1，预留）'


-- ============================================================
-- Table: wf_skill_category
-- ============================================================
CREATE TABLE `wf_skill_category` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `category_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '分类编码（与 wf_skill.category 对应）',
  `category_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '分类名称',
  `icon` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '图标',
  `color` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '颜色',
  `sort_order` int NOT NULL DEFAULT '0' COMMENT '排序',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `creator` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_category_code` (`category_code`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Skill 分类（基础资料维护：面板分组 + 页面左侧导航）'


-- ============================================================
-- Table: wf_skill_input
-- ============================================================
CREATE TABLE `wf_skill_input` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `skill_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `input_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `input_label` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `input_type` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'text' COMMENT 'text/number/date/boolean/enum/field_ref/table_ref/json',
  `enum_values` text COLLATE utf8mb4_unicode_ci,
  `is_required` tinyint(1) NOT NULL DEFAULT '0',
  `default_value` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `bind_mode` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'LinkOrConstant' COMMENT '绑定模式：Link/LinkOrConstant/Enum',
  `enum_source` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '字典编码（BindMode=Enum 时必填），对应 Sys_Dictionary.DicNo',
  `sort_order` int NOT NULL DEFAULT '0',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `creator` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_input` (`skill_code`,`input_name`),
  KEY `idx_skill_input_skill` (`skill_code`)
) ENGINE=InnoDB AUTO_INCREMENT=40 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Skill 输入表单模板（画布生成输入表单用，非硬校验）'


-- ============================================================
-- Table: wf_skill_output
-- ============================================================
CREATE TABLE `wf_skill_output` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `skill_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `output_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `output_type` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'json' COMMENT 'string/number/date/boolean/json',
  `output_prompt` text COLLATE utf8mb4_unicode_ci COMMENT '输出解读提示词（解释器组装用）',
  `description` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `sort_order` int NOT NULL DEFAULT '0',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `creator` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_output` (`skill_code`,`output_name`),
  KEY `idx_skill_output_skill` (`skill_code`)
) ENGINE=InnoDB AUTO_INCREMENT=36 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='强约束 Skill 输出契约（output_strict=1 时解释器强校验）'


-- ============================================================
-- Table: wf_skill_reflection
-- ============================================================
CREATE TABLE `wf_skill_reflection` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `skill_code` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '所属 Skill（与 wf_skill.skill_code 同 collation）',
  `class_path` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '反射的地址（类型全名）',
  `method_name` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ExecuteAsync' COMMENT '反射的方法',
  `param_binding` text COLLATE utf8mb4_unicode_ci COMMENT '参数绑定 JSON: {"输入项名":"方法参数名或顺序"}',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `creator` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `create_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_reflection` (`skill_code`),
  UNIQUE KEY `uk_class_method` (`class_path`,`method_name`),
  CONSTRAINT `fk_reflection_skill` FOREIGN KEY (`skill_code`) REFERENCES `wf_skill` (`skill_code`)
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='method 型 Skill 反射信息（1:1）'


-- ============================================================
-- Table: wf_workflow_definition
-- ============================================================
CREATE TABLE `wf_workflow_definition` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `WorkflowCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流编码',
  `WorkflowName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流名称',
  `WorkflowType` enum('extraction','validation','report') COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流类型',
  `WorkflowConfig` json NOT NULL COMMENT '工作流DAG配置（节点+边+参数）',
  `Version` int DEFAULT '1' COMMENT '版本号',
  `IsActive` tinyint(1) DEFAULT '1' COMMENT '是否启用',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `create_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `update_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `delete_by` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_workflow_code` (`WorkflowCode`),
  KEY `idx_workflow_type` (`WorkflowType`),
  KEY `idx_is_active` (`IsActive`),
  KEY `idx_version` (`Version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流定义'


-- ============================================================
-- Table: wf_workflow_execution_log
-- ============================================================
CREATE TABLE `wf_workflow_execution_log` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `creator` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `modifier` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `deleter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流定义编码',
  `Workflowversion` int NOT NULL COMMENT '执行时的工作流版本',
  `BusinessType` enum('audit_task','report_task','file_upload') COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务场景类型',
  `BusinessId` bigint NOT NULL COMMENT '关联的业务ID（审核任务ID/报告任务ID/文件ID）',
  `NodeId` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '节点ID',
  `SkillCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '执行的Skill',
  `InputData` json DEFAULT NULL COMMENT '实际输入数据',
  `OutputData` json DEFAULT NULL COMMENT '实际输出数据',
  `ErrorMsg` text COLLATE utf8mb4_general_ci COMMENT '错误信息',
  `DurationMs` int DEFAULT NULL COMMENT '耗时（毫秒）',
  `StartedAt` datetime NOT NULL COMMENT '开始时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_workflow_code` (`WorkflowCode`),
  KEY `idx_business_type` (`BusinessType`),
  KEY `idx_business_id` (`BusinessId`),
  KEY `idx_node_id` (`NodeId`),
  KEY `idx_status` (`status`),
  KEY `idx_started_at` (`StartedAt`),
  CONSTRAINT `fk_wlog_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流执行日志'


-- ============================================================
-- Table: yzh_field_config
-- ============================================================
CREATE TABLE `yzh_field_config` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `page_key` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `field_name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `field_alias` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `xs_flag` tinyint DEFAULT '1',
  `column_sxh` int DEFAULT '0',
  `column_title` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `column_width` int DEFAULT '120',
  `column_fixed` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `sortable` tinyint DEFAULT '1',
  `column_formatter` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `show_overflow` tinyint DEFAULT '1',
  `align` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT 'left',
  `bc_flag` tinyint DEFAULT '1',
  `form_title` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `control_type` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT 'input',
  `grid_row` int DEFAULT '0',
  `grid_col` int DEFAULT '0',
  `grid_row_span` int DEFAULT '1',
  `grid_col_span` int DEFAULT '1',
  `required` tinyint DEFAULT '0',
  `maxlength` int DEFAULT '0',
  `placeholder` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `default_value` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `readonly` tinyint DEFAULT '0',
  `disabled` tinyint DEFAULT '0',
  `precision` int DEFAULT NULL,
  `min_val` decimal(18,6) DEFAULT NULL,
  `max_val` decimal(18,6) DEFAULT NULL,
  `textarea_rows` int DEFAULT '3',
  `data_key` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `remote_url` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `group_index` int DEFAULT '0',
  `search_flag` tinyint DEFAULT '0',
  `search_title` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `search_placeholder` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `search_control_type` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `search_width` int DEFAULT '180',
  `org_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `created_at` datetime DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `code` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  UNIQUE KEY `uk_page_field` (`page_key`,`field_name`,`org_code`),
  KEY `idx_page_key` (`page_key`),
  KEY `idx_field_name` (`field_name`)
) ENGINE=InnoDB AUTO_INCREMENT=49 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci


-- ============================================================
-- Table: yzh_page_config
-- ============================================================
CREATE TABLE `yzh_page_config` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `page_key` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `page_title` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `entity_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `table_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `controller_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `key_field` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'Id',
  `key_field_type` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT 'number',
  `sort_field` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `sort_order` varchar(5) COLLATE utf8mb4_unicode_ci DEFAULT 'desc',
  `dialog_width` int DEFAULT '960',
  `dialog_max_height` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT '85vh',
  `dialog_label_width` int DEFAULT '120',
  `row_height` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT 'default',
  `stripe` tinyint DEFAULT '1',
  `show_row_number` tinyint DEFAULT '1',
  `search_mode` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT 'fixed',
  `visible_buttons` text COLLATE utf8mb4_unicode_ci,
  `show_action_column` tinyint DEFAULT '1',
  `checkbox_selection` tinyint DEFAULT '1',
  `incremental_update` tinyint DEFAULT '1',
  `org_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `is_active` tinyint DEFAULT '1',
  `created_at` datetime DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `remark` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT '',
  `code` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  UNIQUE KEY `uk_page_org` (`page_key`,`org_code`),
  KEY `idx_page_key` (`page_key`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci


-- ============================================================
-- Table: yzh_queue
-- ============================================================
CREATE TABLE `yzh_queue` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(36) NOT NULL COMMENT '全局唯一编码(GUID)，表间关联用',
  `queue_code` varchar(64) NOT NULL COMMENT '队列业务编码：Q-{yyyyMMdd}-{6位随机}',
  `queue_type` varchar(30) NOT NULL COMMENT '队列类型：file_convert/auto_verify/report_generate',
  `queue_name` varchar(200) DEFAULT NULL COMMENT '队列名称（人话）',
  `scope_key` varchar(200) DEFAULT NULL COMMENT '范围键（按类型约定格式，如 file_convert=机构|标准|阶段）',
  `scope_info` json DEFAULT NULL COMMENT '冗余展示数据 JSON',
  `source_type` varchar(30) DEFAULT NULL COMMENT '来源类型：upload_task/verify_req/report_req',
  `source_id` varchar(64) DEFAULT NULL COMMENT '来源ID：上传任务taskId等',
  `status` varchar(20) NOT NULL DEFAULT 'pending' COMMENT 'pending/running/completed/failed/cancelled',
  `total_count` int DEFAULT '0' COMMENT '子任务总数',
  `pending_count` int DEFAULT '0',
  `processing_count` int DEFAULT '0',
  `completed_count` int DEFAULT '0',
  `failed_count` int DEFAULT '0',
  `cancelled_count` int DEFAULT '0',
  `progress` int DEFAULT '0' COMMENT '0-100',
  `start_time` datetime DEFAULT NULL,
  `end_time` datetime DEFAULT NULL,
  `remark` varchar(500) DEFAULT NULL,
  `org_code` varchar(50) DEFAULT NULL COMMENT '机构编码（多租户）',
  `creator` varchar(50) DEFAULT NULL,
  `create_date` datetime DEFAULT CURRENT_TIMESTAMP,
  `modifier` varchar(50) DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `create_by` varchar(50) DEFAULT NULL,
  `update_by` varchar(50) DEFAULT NULL,
  `delete_by` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_queue_code` (`queue_code`),
  UNIQUE KEY `uk_source` (`source_type`,`source_id`),
  KEY `idx_scope_status` (`queue_type`,`scope_key`,`status`),
  KEY `idx_status` (`status`),
  KEY `idx_create_date` (`create_date`)
) ENGINE=InnoDB AUTO_INCREMENT=35 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='yzh队列主表（通用队列中心）'


-- ============================================================
-- Table: yzh_queue_resource_lock
-- ============================================================
CREATE TABLE `yzh_queue_resource_lock` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(36) NOT NULL COMMENT '全局唯一编码(GUID)',
  `queue_code` varchar(64) NOT NULL COMMENT '所属队列编码',
  `resource_table` varchar(50) NOT NULL COMMENT '资源表名（如 cert_standard_directory_file / 任意业务表）',
  `resource_code` varchar(200) NOT NULL COMMENT '资源唯一编码',
  `resource_name` varchar(200) DEFAULT NULL COMMENT '资源名称快照',
  `task_no` int DEFAULT NULL COMMENT '占用该资源的子任务序号（NULL=队列级锁）',
  `status` varchar(20) DEFAULT 'locked' COMMENT 'locked/released',
  `active_key` varchar(260) DEFAULT NULL COMMENT '活跃锁键:{resource_table}|{resource_code}，释放时置NULL；uk_active唯一索引实现同一资源同时仅一个活跃锁',
  `create_time` datetime DEFAULT CURRENT_TIMESTAMP,
  `release_time` datetime DEFAULT NULL,
  `expire_at` datetime DEFAULT NULL COMMENT '锁租约安全网',
  `org_code` varchar(50) DEFAULT NULL,
  `creator` varchar(50) DEFAULT NULL,
  `create_date` datetime DEFAULT CURRENT_TIMESTAMP,
  `modifier` varchar(50) DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `create_by` varchar(50) DEFAULT NULL,
  `update_by` varchar(50) DEFAULT NULL,
  `delete_by` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_active` (`active_key`),
  KEY `idx_queue` (`queue_code`,`status`),
  KEY `idx_locked` (`resource_table`,`resource_code`,`status`),
  KEY `idx_expire` (`status`,`expire_at`)
) ENGINE=InnoDB AUTO_INCREMENT=2393 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='yzh队列资源锁定表'


-- ============================================================
-- Table: yzh_queue_task
-- ============================================================
CREATE TABLE `yzh_queue_task` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(36) NOT NULL COMMENT '全局唯一编码(GUID)',
  `queue_code` varchar(64) NOT NULL COMMENT '所属队列编码(yzh_queue.queue_code)',
  `task_type` varchar(30) NOT NULL COMMENT '任务类型：file_convert/auto_verify/report_generate',
  `payload` text COMMENT '业务数据 JSON（file_convert={fileCode,fileName,sourcePath,targetPath,convertType}）',
  `status` varchar(20) NOT NULL DEFAULT 'pending' COMMENT 'pending/processing/completed/failed/cancelled',
  `error_type` varchar(20) DEFAULT NULL COMMENT '错误分类: retryable(可重试)/permanent(永久)',
  `error_message` varchar(2000) DEFAULT NULL COMMENT '错误信息',
  `retry_count` int DEFAULT '0',
  `max_retry_count` int DEFAULT '3',
  `next_retry_at` datetime DEFAULT NULL COMMENT '下次重试时间(指数退避+抖动)',
  `locked_until` datetime DEFAULT NULL COMMENT '领取租约到期时间(worker续期,到期可被重新领取)',
  `locked_at` datetime DEFAULT NULL COMMENT '领取时间',
  `locked_by` varchar(100) DEFAULT NULL COMMENT '领取 Worker 标识',
  `process_time` datetime DEFAULT NULL COMMENT '开始处理时间',
  `complete_time` datetime DEFAULT NULL COMMENT '完成/失败/取消时间',
  `create_time` datetime DEFAULT NULL COMMENT '入队时间',
  `task_id` varchar(64) DEFAULT NULL COMMENT '来源批次ID（如上传任务taskId）',
  `user_code` varchar(50) DEFAULT NULL,
  `user_name` varchar(100) DEFAULT NULL COMMENT '发起用户名',
  `org_code` varchar(50) DEFAULT NULL COMMENT '机构编码',
  `priority` int DEFAULT '0' COMMENT '优先级（0=普通，10=高优先）',
  `lock_codes` varchar(500) DEFAULT NULL COMMENT '本任务持有的资源锁编码(逗号分隔，对应 yzh_queue_resource_lock.code)',
  `creator` varchar(50) DEFAULT NULL,
  `create_date` datetime DEFAULT CURRENT_TIMESTAMP,
  `modifier` varchar(50) DEFAULT NULL,
  `modify_date` datetime DEFAULT NULL,
  `deleter` varchar(50) DEFAULT NULL,
  `delete_time` datetime DEFAULT NULL,
  `create_by` varchar(50) DEFAULT NULL,
  `update_by` varchar(50) DEFAULT NULL,
  `delete_by` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  KEY `idx_queue` (`queue_code`),
  KEY `idx_claim` (`status`,`next_retry_at`),
  KEY `idx_lease` (`status`,`locked_until`),
  KEY `idx_task_id` (`task_id`),
  KEY `idx_type` (`task_type`)
) ENGINE=InnoDB AUTO_INCREMENT=2358 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='yzh队列子任务表（通用任务）'


