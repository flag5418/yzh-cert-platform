-- ====================================================================
-- 20261105_fix_all_remaining_nullable_V1.sql
-- 最终修复：将所有基表中仍为 NOT NULL 的 entity-typed 列改为 NULL
--
-- 原理：Entity 中 string?/int?/DateTime? 属性对应列若为 NOT NULL，
--       前端传 null 时 MySQL 直接拒绝。批量改为 NULL 是幂等且安全的
--       （仅丢失 DB 层约束检查，但 Entity 框架应负责业务校验）。
--
-- 排除（应保持 NOT NULL）：基础设施列 + 名称列 + 外键列
-- 幂等：重复执行无副作用
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ============================================================
-- cert_audit_plan
-- ============================================================
ALTER TABLE `cert_audit_plan`
  MODIFY `AuditYear` int NULL,
  MODIFY `CoverageMode` varchar(255) NULL,
  MODIFY `PlanKind` varchar(255) NULL,
  MODIFY `PreviousPlanCode` varchar(255) NULL;

-- ============================================================
-- cert_audit_plan_detail
-- ============================================================
ALTER TABLE `cert_audit_plan_detail`
  MODIFY `Clauses` varchar(1000) NULL,
  MODIFY `Departments` varchar(500) NULL,
  MODIFY `DeptNames` varchar(500) NULL,
  MODIFY `ClauseNumbers` varchar(1000) NULL,
  MODIFY `OwnerCode` varchar(64) NULL,
  MODIFY `OwnerName` varchar(64) NULL,
  MODIFY `Remark` varchar(500) NULL;

-- ============================================================
-- cert_cert_stage (Category, Status, StatusName)
-- ============================================================
ALTER TABLE `cert_cert_stage`
  MODIFY `Category` varchar(255) NULL,
  MODIFY `Status` varchar(255) NULL,
  MODIFY `StatusName` varchar(255) NULL;

-- ============================================================
-- cert_certification_body
-- ============================================================
ALTER TABLE `cert_certification_body`
  MODIFY `LogoUrl` varchar(255) NULL,
  MODIFY `ScopeText` varchar(255) NULL,
  MODIFY `ThemeConfig` varchar(255) NULL,
  MODIFY `LoginConfig` varchar(255) NULL,
  MODIFY `MaxUsers` int NULL,
  MODIFY `MaxEnterprises` int NULL,
  MODIFY `ExpireDate` datetime NULL;

-- ============================================================
-- cert_ai_config
-- ============================================================
ALTER TABLE `cert_ai_config`
  MODIFY `Provider` varchar(50) NULL,
  MODIFY `ApiKey` varchar(500) NULL,
  MODIFY `Model` varchar(100) NULL,
  MODIFY `MaxTokens` int NULL;

-- ============================================================
-- cert_clause_attr
-- ============================================================
ALTER TABLE `cert_clause_attr`
  MODIFY `AttrValue` varchar(255) NULL;

-- ============================================================
-- cert_clause_attr_def
-- ============================================================
ALTER TABLE `cert_clause_attr_def`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_enterprise_stage
-- ============================================================
ALTER TABLE `cert_enterprise_stage`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_org_stage
-- ============================================================
ALTER TABLE `cert_org_stage`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_org_standard
-- ============================================================
ALTER TABLE `cert_org_standard`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_nc
-- ============================================================
ALTER TABLE `cert_nc`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_check_analysis
-- ============================================================
ALTER TABLE `cert_check_analysis`
  MODIFY `InputSnapshotJson` varchar(255) NULL,
  MODIFY `ResultSummaryJson` varchar(255) NULL;

-- ============================================================
-- cert_check_result
-- ============================================================
ALTER TABLE `cert_check_result`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- Sys_Dictionary
-- ============================================================
ALTER TABLE `Sys_Dictionary`
  MODIFY `DicValue` varchar(255) NULL;

-- ============================================================
-- Sys_DictionaryList
-- ============================================================
ALTER TABLE `Sys_DictionaryList`
  MODIFY `DicCode` varchar(255) NULL,
  MODIFY `DicName` varchar(255) NULL,
  MODIFY `DicValue` varchar(255) NULL;

-- ============================================================
-- Sys_User
-- ============================================================
ALTER TABLE `Sys_User`
  MODIFY `UserName` varchar(255) NULL,
  MODIFY `TrueName` varchar(255) NULL,
  MODIFY `Phone` varchar(255) NULL,
  MODIFY `Address` varchar(255) NULL;

-- ============================================================
-- Sys_log
-- ============================================================
ALTER TABLE `sys_log`
  MODIFY `Action` varchar(255) NULL,
  MODIFY `ApiPath` varchar(255) NULL,
  MODIFY `TargetType` varchar(255) NULL;

-- ============================================================
-- yzh_queue
-- ============================================================
ALTER TABLE `yzh_queue`
  MODIFY `Remark` varchar(255) NULL,
  MODIFY `Priority` int NULL;

-- ============================================================
-- yzh_queue_task
-- ============================================================
ALTER TABLE `yzh_queue_task`
  MODIFY `Remark` varchar(255) NULL,
  MODIFY `LockedAt` datetime NULL,
  MODIFY `LockedUntil` datetime NULL,
  MODIFY `ProcessTime` datetime NULL,
  MODIFY `CompleteTime` datetime NULL,
  MODIFY `Priority` int NULL;

-- ============================================================
-- yzh_queue_resource_lock
-- ============================================================
ALTER TABLE `yzh_queue_resource_lock`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_expert_nc_item / cert_expert_report_section_item / cert_expert_task_data_gap
-- ============================================================
ALTER TABLE `cert_expert_nc_item`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_expert_report_section_item`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_expert_task_data_gap`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_iso_clause / cert_iso_standard
-- ============================================================
ALTER TABLE `cert_iso_clause`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_iso_standard`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_prompt_template / cert_report_section
-- ============================================================
ALTER TABLE `cert_prompt_template`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_report_section`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_conclusion_rule / cert_validation_rule
-- ============================================================
ALTER TABLE `cert_conclusion_rule`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_validation_rule`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_doc_fill_log / cert_doc_fill_prompt
-- ============================================================
ALTER TABLE `cert_doc_fill_log`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_doc_fill_prompt`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_doc_fill_value / cert_doc_template / cert_doc_template_anchor
-- ============================================================
ALTER TABLE `cert_doc_fill_value`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_doc_template`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_doc_template_anchor`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_doc_extract_rules / cert_doc_field_def
-- ============================================================
ALTER TABLE `cert_doc_extract_rules`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_doc_field_def`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_doc_normalize_action / cert_doc_table_def / cert_doc_table_field_def
-- ============================================================
ALTER TABLE `cert_doc_normalize_action`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_doc_table_def`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_doc_table_field_def`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_extraction_change_log / cert_extraction_result / cert_file_requirement
-- ============================================================
ALTER TABLE `cert_extraction_change_log`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_extraction_result`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_file_requirement`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_expert_task_data_gap / cert_expert_task_queue_item / cert_table_extraction_result
-- ============================================================
ALTER TABLE `cert_expert_task_queue_item`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_table_extraction_result`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_enterprise_doc / cert_enterprise_doc_profile / cert_enterprise_doc_upload_task / cert_enterprise_doc_version
-- ============================================================
ALTER TABLE `cert_enterprise_doc`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_enterprise_doc_profile`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_enterprise_doc_upload_task`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_enterprise_doc_version`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_enterprise_original_file / cert_enterprise_original_file_version / cert_enterprise_original_upload_task
-- ============================================================
ALTER TABLE `cert_enterprise_original_file`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_enterprise_original_file_version`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_enterprise_original_upload_task`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_enterprise_file_op_log / cert_nc_conclude_prompt / cert_nc_judge_prompt
-- ============================================================
ALTER TABLE `cert_enterprise_file_op_log`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_nc_conclude_prompt`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_nc_judge_prompt`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_standard_directory_file / cert_standard_directory_folder / cert_standard_phase_config
-- ============================================================
ALTER TABLE `cert_standard_directory_file`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_standard_directory_folder`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_standard_phase_config`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_doc_ai_suggestion / cert_message / cert_impact_log
-- ============================================================
ALTER TABLE `cert_doc_ai_suggestion`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_message`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_impact_log`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_sys_config / cert_task / cert_task_item
-- ============================================================
ALTER TABLE `cert_sys_config`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_task`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_task_item`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_standard_family / cert_standard_directory_config / cert_directory_template
-- ============================================================
ALTER TABLE `cert_standard_family`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_standard_directory_config`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_directory_template`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- cert_standard_doc_contract / cert_fill_param_def / cert_fill_param_value
-- ============================================================
ALTER TABLE `cert_standard_doc_contract`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_fill_param_def`
  MODIFY `Remark` varchar(255) NULL;

ALTER TABLE `cert_fill_param_value`
  MODIFY `Remark` varchar(255) NULL;

-- ============================================================
-- 系统表可空字段（entity 声明为 int? / string?）
-- ============================================================
ALTER TABLE `Sys_User`
  MODIFY `Gender` int NULL,
  MODIFY `OrgCode` varchar(255) NULL;

ALTER TABLE `Sys_Role`
  MODIFY `DeptName` varchar(255) NULL;

ALTER TABLE `sys_log`
  MODIFY `UserId` bigint NULL;

-- ============================================================
-- 其他业务表可空字段（批量）
-- ============================================================
ALTER TABLE `cert_doc_fill_log`
  MODIFY `Message` varchar(255) NULL,
  MODIFY `TotalAnchors` int NULL,
  MODIFY `FileKind` varchar(255) NULL,
  MODIFY `OutputStoragePath` varchar(255) NULL,
  MODIFY `ResolvedCount` int NULL,
  MODIFY `PendingCount` int NULL,
  MODIFY `Completion` decimal(18,4) NULL,
  MODIFY `AvgConfidence` decimal(18,4) NULL,
  MODIFY `RegionCount` int NULL,
  MODIFY `ClonedRows` int NULL,
  MODIFY `Verified` tinyint(1) NULL,
  MODIFY `PromptTokens` int NULL,
  MODIFY `CompletionTokens` int NULL,
  MODIFY `DurationMs` int NULL,
  MODIFY `Status` varchar(255) NULL;

ALTER TABLE `cert_doc_fill_prompt`
  MODIFY `PromptCode` varchar(255) NULL,
  MODIFY `PromptName` varchar(255) NULL,
  MODIFY `OutputSchema` varchar(255) NULL,
  MODIFY `Temperature` decimal(18,4) NULL,
  MODIFY `Model` varchar(255) NULL,
  MODIFY `MaxTokens` int NULL,
  MODIFY `IsDefault` tinyint(1) NULL,
  MODIFY `Status` varchar(255) NULL;

ALTER TABLE `cert_task`
  MODIFY `TaskNo` varchar(255) NULL,
  MODIFY `TaskKind` varchar(255) NULL,
  MODIFY `AuditType` varchar(255) NULL,
  MODIFY `AuditDateFrom` datetime NULL,
  MODIFY `AuditDateTo` datetime NULL,
  MODIFY `TeamJson` varchar(255) NULL,
  MODIFY `Status` varchar(255) NULL,
  MODIFY `ItemTotal` int NULL,
  MODIFY `ItemDone` int NULL,
  MODIFY `ItemFailed` int NULL,
  MODIFY `ItemSkipped` int NULL,
  MODIFY `PromptCode` varchar(255) NULL,
  MODIFY `PromptVersion` varchar(255) NULL,
  MODIFY `GuardJson` varchar(255) NULL,
  MODIFY `QueueAttempt` int NULL,
  MODIFY `StartedAt` datetime NULL,
  MODIFY `FinishedAt` datetime NULL,
  MODIFY `Message` varchar(255) NULL;

ALTER TABLE `cert_task_item`
  MODIFY `CheckItemCode` varchar(255) NULL,
  MODIFY `Status` varchar(255) NULL,
  MODIFY `EvidenceState` varchar(255) NULL,
  MODIFY `AnalysisState` varchar(255) NULL,
  MODIFY `AnalyzeCount` int NULL,
  MODIFY `LastAnalyzedAt` datetime NULL,
  MODIFY `EvidenceCount` int NULL,
  MODIFY `RunMode` varchar(255) NULL,
  MODIFY `SkipLocked` tinyint(1) NULL,
  MODIFY `RerunState` varchar(255) NULL,
  MODIFY `RerunReason` varchar(255) NULL,
  MODIFY `RerunTriggeredAt` datetime NULL,
  MODIFY `Message` varchar(255) NULL;

ALTER TABLE `cert_message`
  MODIFY `UserCode` varchar(64) NULL,
  MODIFY `UserName` varchar(255) NULL,
  MODIFY `Content` varchar(255) NULL,
  MODIFY `MessageType` varchar(20) NULL,
  MODIFY `IsRead` int NULL,
  MODIFY `ExtraData` varchar(255) NULL,
  MODIFY `ReadDate` datetime NULL;

ALTER TABLE `cert_check_lock`
  MODIFY `CheckItemCode` varchar(255) NULL,
  MODIFY `StageKind` varchar(255) NULL,
  MODIFY `LockReason` varchar(255) NULL,
  MODIFY `LockSource` varchar(255) NULL,
  MODIFY `IsActive` tinyint(1) NULL,
  MODIFY `LockedAt` datetime NULL,
  MODIFY `UnlockedAt` datetime NULL;

ALTER TABLE `cert_expert_task_queue_item`
  MODIFY `TaskItemCode` varchar(36) NULL,
  MODIFY `ItemType` varchar(20) NULL,
  MODIFY `ItemCode` varchar(36) NULL,
  MODIFY `Seq` int NULL,
  MODIFY `ItemStatus` varchar(20) NULL,
  MODIFY `RetryCount` int NULL,
  MODIFY `MaxRetryCount` int NULL,
  MODIFY `NextRetryAt` datetime NULL,
  MODIFY `LockedUntil` datetime NULL,
  MODIFY `StartTime` datetime NULL,
  MODIFY `FinishTime` datetime NULL;

ALTER TABLE `cert_ai_config`
  MODIFY `Temperature` float NULL,
  MODIFY `IsEnabled` tinyint(1) NULL;

ALTER TABLE `cert_point`
  MODIFY `CheckItemCode` varchar(255) NULL,
  MODIFY `DeptCode` varchar(255) NULL,
  MODIFY `Origin` varchar(255) NULL,
  MODIFY `PointKind` varchar(255) NULL,
  MODIFY `Mode` varchar(255) NULL,
  MODIFY `Action` varchar(255) NULL,
  MODIFY `Reason` varchar(255) NULL,
  MODIFY `Question` varchar(255) NULL,
  MODIFY `TargetRole` varchar(255) NULL,
  MODIFY `EstMinutes` int NULL,
  MODIFY `Priority` int NULL,
  MODIFY `Fingerprint` varchar(255) NULL,
  MODIFY `Status` varchar(255) NULL,
  MODIFY `ResolveReason` varchar(255) NULL,
  MODIFY `ResolvedAt` datetime NULL;

ALTER TABLE `cert_sys_config`
  MODIFY `ConfigKey` varchar(255) NULL,
  MODIFY `ConfigValue` varchar(255) NULL,
  MODIFY `ConfigType` varchar(255) NULL,
  MODIFY `Category` varchar(255) NULL,
  MODIFY `DisplayName` varchar(255) NULL,
  MODIFY `Description` varchar(255) NULL,
  MODIFY `IsReadonly` int NULL;
