# ============================================================================
# 体系认证平台 - 实体类自动生成脚本
# ============================================================================
# 用途：根据数据库表设计文档 V2.1 自动生成 C# 实体类
# 使用：./generate_entities.ps1
# ============================================================================

$baseDir = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/VOL.Entity/CertPlatform"

# 实体类定义数组
$entities = @(
    # 域 A：认证体系配置（13 张表）- 已创建 4 个，还需创建 9 个
    @{
        Name = "StandardPhaseConfig"
        Table = "cert_standard_phase_config"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "StandardCode"; Type = "string"; Column = "standard_code"; Required = $true; Length = 36 },
            @{ Name = "PhaseCode"; Type = "string"; Column = "phase_code"; Required = $true; Length = 36 },
            @{ Name = "RequiredClauses"; Type = "string"; Column = "required_clauses"; Required = $false },
            @{ Name = "RequiredFiles"; Type = "string"; Column = "required_files"; Required = $false },
            @{ Name = "Notes"; Type = "string"; Column = "notes"; Required = $false }
        )
    },
    @{
        Name = "DirectoryTemplate"
        Table = "cert_directory_template"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "ConfigCode"; Type = "string"; Column = "config_code"; Required = $true; Length = 36 },
            @{ Name = "ParentCode"; Type = "string"; Column = "parent_code"; Required = $false; Length = 36 },
            @{ Name = "FolderName"; Type = "string"; Column = "folder_name"; Required = $true; Length = 200 },
            @{ Name = "SortOrder"; Type = "int"; Column = "sort_order"; Required = $false }
        )
    },
    @{
        Name = "FileRequirement"
        Table = "cert_file_requirement"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "FolderCode"; Type = "string"; Column = "folder_code"; Required = $true; Length = 36 },
            @{ Name = "FileNameTemplate"; Type = "string"; Column = "file_name_template"; Required = $true; Length = 200 },
            @{ Name = "FileType"; Type = "string"; Column = "file_type"; Required = $true; Length = 50 },
            @{ Name = "IsRequired"; Type = "bool"; Column = "is_required"; Required = $false },
            @{ Name = "MaxSizeMB"; Type = "int"; Column = "max_size_mb"; Required = $false },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $false },
            @{ Name = "SortOrder"; Type = "int"; Column = "sort_order"; Required = $false }
        )
    },
    @{
        Name = "ExtractionRule"
        Table = "cert_extraction_rule"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "FileRequirementCode"; Type = "string"; Column = "file_requirement_code"; Required = $true; Length = 36 },
            @{ Name = "SkillCode"; Type = "string"; Column = "skill_code"; Required = $true; Length = 36 },
            @{ Name = "RuleType"; Type = "string"; Column = "rule_type"; Required = $true },
            @{ Name = "RuleConfig"; Type = "string"; Column = "rule_config"; Required = $true },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $false },
            @{ Name = "IsActive"; Type = "bool"; Column = "is_active"; Required = $false }
        )
    },
    @{
        Name = "ExtractionField"
        Table = "cert_extraction_field"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "RuleCode"; Type = "string"; Column = "rule_code"; Required = $true; Length = 36 },
            @{ Name = "SkillCode"; Type = "string"; Column = "skill_code"; Required = $false; Length = 36 },
            @{ Name = "FieldCode"; Type = "string"; Column = "field_code"; Required = $true; Length = 100 },
            @{ Name = "LabelTag"; Type = "string"; Column = "label_tag"; Required = $true; Length = 500 },
            @{ Name = "FieldName"; Type = "string"; Column = "field_name"; Required = $true; Length = 100 },
            @{ Name = "FieldType"; Type = "string"; Column = "field_type"; Required = $false },
            @{ Name = "EnumValues"; Type = "string"; Column = "enum_values"; Required = $false },
            @{ Name = "SortOrder"; Type = "int"; Column = "sort_order"; Required = $false }
        )
    },
    @{
        Name = "ValidationRule"
        Table = "cert_validation_rule"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "StandardCode"; Type = "string"; Column = "standard_code"; Required = $true; Length = 36 },
            @{ Name = "PhaseCode"; Type = "string"; Column = "phase_code"; Required = $true; Length = 36 },
            @{ Name = "ClauseCode"; Type = "string"; Column = "clause_code"; Required = $true; Length = 36 },
            @{ Name = "WorkflowCode"; Type = "string"; Column = "workflow_code"; Required = $true; Length = 36 },
            @{ Name = "RuleCode"; Type = "string"; Column = "rule_code"; Required = $true; Length = 50 },
            @{ Name = "RuleName"; Type = "string"; Column = "rule_name"; Required = $true; Length = 200 },
            @{ Name = "SeverityIfViolated"; Type = "string"; Column = "severity_if_violated"; Required = $true },
            @{ Name = "NcDescriptionTemplate"; Type = "string"; Column = "nc_description_template"; Required = $false },
            @{ Name = "IsActive"; Type = "bool"; Column = "is_active"; Required = $false }
        )
    },
    @{
        Name = "ValidationRuleSource"
        Table = "cert_validation_rule_source"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "RuleCode"; Type = "string"; Column = "rule_code"; Required = $true; Length = 36 },
            @{ Name = "FileRequirementCode"; Type = "string"; Column = "file_requirement_code"; Required = $true; Length = 36 },
            @{ Name = "SourcePath"; Type = "string"; Column = "source_path"; Required = $false; Length = 500 },
            @{ Name = "Notes"; Type = "string"; Column = "notes"; Required = $false }
        )
    },
    @{
        Name = "ReportTemplate"
        Table = "cert_report_template"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "CbCode"; Type = "string"; Column = "cb_code"; Required = $true; Length = 36 },
            @{ Name = "StandardCode"; Type = "string"; Column = "standard_code"; Required = $true; Length = 36 },
            @{ Name = "PhaseCode"; Type = "string"; Column = "phase_code"; Required = $true; Length = 36 },
            @{ Name = "TemplateName"; Type = "string"; Column = "template_name"; Required = $true; Length = 200 },
            @{ Name = "TemplateFilePath"; Type = "string"; Column = "template_file_path"; Required = $false; Length = 500 },
            @{ Name = "SectionConfig"; Type = "string"; Column = "section_config"; Required = $false },
            @{ Name = "IsDefault"; Type = "bool"; Column = "is_default"; Required = $false }
        )
    },
    @{
        Name = "ClauseExtractionRule"
        Table = "cert_clause_extraction_rule"
        Namespace = "Cert"
        Properties = @(
            @{ Name = "ClauseCode"; Type = "string"; Column = "clause_code"; Required = $true; Length = 36 },
            @{ Name = "WorkflowCode"; Type = "string"; Column = "workflow_code"; Required = $true; Length = 36 },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $false }
        )
    },

    # 域 B：企业档案（9 张表）
    @{
        Name = "Enterprise"
        Table = "ent_enterprise"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "Name"; Type = "string"; Column = "name"; Required = $true; Length = 200 },
            @{ Name = "ShortName"; Type = "string"; Column = "short_name"; Required = $false; Length = 100 },
            @{ Name = "CreditCode"; Type = "string"; Column = "credit_code"; Required = $false; Length = 50 },
            @{ Name = "LegalPerson"; Type = "string"; Column = "legal_person"; Required = $false; Length = 50 },
            @{ Name = "Address"; Type = "string"; Column = "address"; Required = $false },
            @{ Name = "CertScope"; Type = "string"; Column = "cert_scope"; Required = $false },
            @{ Name = "ContactName"; Type = "string"; Column = "contact_name"; Required = $false; Length = 50 },
            @{ Name = "ContactPhone"; Type = "string"; Column = "contact_phone"; Required = $false; Length = 20 },
            @{ Name = "ContactEmail"; Type = "string"; Column = "contact_email"; Required = $false; Length = 200 },
            @{ Name = "Status"; Type = "string"; Column = "status"; Required = $false },
            @{ Name = "ArchiveDate"; Type = "DateTime?"; Column = "archive_date"; Required = $false },
            @{ Name = "Notes"; Type = "string"; Column = "notes"; Required = $false }
        )
    },
    @{
        Name = "EnterprisePhase"
        Table = "ent_enterprise_phase"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "EnterpriseCode"; Type = "string"; Column = "enterprise_code"; Required = $true; Length = 36 },
            @{ Name = "PhaseCode"; Type = "string"; Column = "phase_code"; Required = $true; Length = 36 },
            @{ Name = "StandardCode"; Type = "string"; Column = "standard_code"; Required = $true; Length = 36 },
            @{ Name = "Status"; Type = "string"; Column = "status"; Required = $false },
            @{ Name = "StartedAt"; Type = "DateTime?"; Column = "started_at"; Required = $false },
            @{ Name = "CompletedAt"; Type = "DateTime?"; Column = "completed_at"; Required = $false }
        )
    },
    @{
        Name = "EnterpriseDocument"
        Table = "ent_enterprise_document"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "EnterpriseCode"; Type = "string"; Column = "enterprise_code"; Required = $true; Length = 36 },
            @{ Name = "PhaseCode"; Type = "string"; Column = "phase_code"; Required = $false; Length = 36 },
            @{ Name = "Scope"; Type = "string"; Column = "scope"; Required = $true },
            @{ Name = "TemplateFolderCode"; Type = "string"; Column = "template_folder_code"; Required = $false; Length = 36 },
            @{ Name = "ParentCode"; Type = "string"; Column = "parent_code"; Required = $false; Length = 36 },
            @{ Name = "FolderName"; Type = "string"; Column = "folder_name"; Required = $true; Length = 200 },
            @{ Name = "SortOrder"; Type = "int"; Column = "sort_order"; Required = $false }
        )
    },
    @{
        Name = "EnterpriseFile"
        Table = "ent_enterprise_file"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "FolderCode"; Type = "string"; Column = "folder_code"; Required = $true; Length = 36 },
            @{ Name = "FileName"; Type = "string"; Column = "file_name"; Required = $true; Length = 500 },
            @{ Name = "FileType"; Type = "string"; Column = "file_type"; Required = $true; Length = 50 },
            @{ Name = "FileSize"; Type = "long"; Column = "file_size"; Required = $true },
            @{ Name = "StoragePath"; Type = "string"; Column = "storage_path"; Required = $true; Length = 500 },
            @{ Name = "FileHash"; Type = "string"; Column = "file_hash"; Required = $false; Length = 64 },
            @{ Name = "CurrentVersion"; Type = "int"; Column = "current_version"; Required = $false },
            @{ Name = "Notes"; Type = "string"; Column = "notes"; Required = $false }
        )
    },
    @{
        Name = "FileVersion"
        Table = "ent_file_version"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "FileCode"; Type = "string"; Column = "file_code"; Required = $true; Length = 36 },
            @{ Name = "VersionNumber"; Type = "int"; Column = "version_number"; Required = $true },
            @{ Name = "FileSize"; Type = "long"; Column = "file_size"; Required = $true },
            @{ Name = "StoragePath"; Type = "string"; Column = "storage_path"; Required = $true; Length = 500 },
            @{ Name = "FileHash"; Type = "string"; Column = "file_hash"; Required = $true; Length = 64 },
            @{ Name = "ChangeNotes"; Type = "string"; Column = "change_notes"; Required = $false }
        )
    },
    @{
        Name = "FilePreCheckResult"
        Table = "ent_file_pre_check_result"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "FileCode"; Type = "string"; Column = "file_code"; Required = $true; Length = 36 },
            @{ Name = "VersionNumber"; Type = "int"; Column = "version_number"; Required = $true },
            @{ Name = "CheckType"; Type = "string"; Column = "check_type"; Required = $true },
            @{ Name = "CheckResult"; Type = "string"; Column = "check_result"; Required = $true },
            @{ Name = "Message"; Type = "string"; Column = "message"; Required = $false },
            @{ Name = "Detail"; Type = "string"; Column = "detail"; Required = $false },
            @{ Name = "CheckedAt"; Type = "DateTime"; Column = "checked_at"; Required = $true }
        )
    },
    @{
        Name = "FileComplianceCheck"
        Table = "ent_file_compliance_check"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "FileCode"; Type = "string"; Column = "file_code"; Required = $true; Length = 36 },
            @{ Name = "VersionNumber"; Type = "int"; Column = "version_number"; Required = $true },
            @{ Name = "RuleCode"; Type = "string"; Column = "rule_code"; Required = $true; Length = 36 },
            @{ Name = "WorkflowExecutionCode"; Type = "string"; Column = "workflow_execution_code"; Required = $false; Length = 36 },
            @{ Name = "CheckStatus"; Type = "string"; Column = "check_status"; Required = $true },
            @{ Name = "Message"; Type = "string"; Column = "message"; Required = $false },
            @{ Name = "Detail"; Type = "string"; Column = "detail"; Required = $false },
            @{ Name = "CheckedAt"; Type = "DateTime"; Column = "checked_at"; Required = $true }
        )
    },
    @{
        Name = "ExtractionResult"
        Table = "ent_extraction_result"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "FileCode"; Type = "string"; Column = "file_code"; Required = $true; Length = 36 },
            @{ Name = "VersionNumber"; Type = "int"; Column = "version_number"; Required = $true },
            @{ Name = "RuleCode"; Type = "string"; Column = "rule_code"; Required = $true; Length = 36 },
            @{ Name = "FieldCode"; Type = "string"; Column = "field_code"; Required = $true; Length = 36 },
            @{ Name = "LabelTag"; Type = "string"; Column = "label_tag"; Required = $false; Length = 500 },
            @{ Name = "ExtractedValue"; Type = "string"; Column = "extracted_value"; Required = $false },
            @{ Name = "Confidence"; Type = "decimal?"; Column = "confidence"; Required = $false },
            @{ Name = "PositionInfo"; Type = "string"; Column = "position_info"; Required = $false },
            @{ Name = "IsManualEdited"; Type = "bool"; Column = "is_manual_edited"; Required = $false },
            @{ Name = "ExtractedAt"; Type = "DateTime"; Column = "extracted_at"; Required = $true }
        )
    },
    @{
        Name = "TableExtractionResult"
        Table = "ent_table_extraction_result"
        Namespace = "Ent"
        Properties = @(
            @{ Name = "FileCode"; Type = "string"; Column = "file_code"; Required = $true; Length = 36 },
            @{ Name = "VersionNumber"; Type = "int"; Column = "version_number"; Required = $true },
            @{ Name = "RuleCode"; Type = "string"; Column = "rule_code"; Required = $true; Length = 36 },
            @{ Name = "TableIndex"; Type = "int"; Column = "table_index"; Required = $false },
            @{ Name = "ExtractedJson"; Type = "string"; Column = "extracted_json"; Required = $true },
            @{ Name = "Confidence"; Type = "decimal?"; Column = "confidence"; Required = $false },
            @{ Name = "PositionInfo"; Type = "string"; Column = "position_info"; Required = $false },
            @{ Name = "ExtractedAt"; Type = "DateTime"; Column = "extracted_at"; Required = $true }
        )
    },

    # 域 C：审核执行（6 张表）
    @{
        Name = "AuditTask"
        Table = "audit_task"
        Namespace = "Audit"
        Properties = @(
            @{ Name = "PhaseCode"; Type = "string"; Column = "phase_code"; Required = $true; Length = 36 },
            @{ Name = "TaskNumber"; Type = "string"; Column = "task_number"; Required = $true; Length = 50 },
            @{ Name = "AuditorId"; Type = "long"; Column = "auditor_id"; Required = $true },
            @{ Name = "Status"; Type = "string"; Column = "status"; Required = $false },
            @{ Name = "PlannedDate"; Type = "DateTime?"; Column = "planned_date"; Required = $false },
            @{ Name = "ActualStartDate"; Type = "DateTime?"; Column = "actual_start_date"; Required = $false },
            @{ Name = "ActualCompleteDate"; Type = "DateTime?"; Column = "actual_complete_date"; Required = $false },
            @{ Name = "AuditScope"; Type = "string"; Column = "audit_scope"; Required = $false },
            @{ Name = "Notes"; Type = "string"; Column = "notes"; Required = $false }
        )
    },
    @{
        Name = "ChecklistItem"
        Table = "audit_checklist_item"
        Namespace = "Audit"
        Properties = @(
            @{ Name = "TaskCode"; Type = "string"; Column = "task_code"; Required = $true; Length = 36 },
            @{ Name = "ClauseCode"; Type = "string"; Column = "clause_code"; Required = $true; Length = 36 },
            @{ Name = "AuditCriteria"; Type = "string"; Column = "audit_criteria"; Required = $false },
            @{ Name = "FindingDescription"; Type = "string"; Column = "finding_description"; Required = $false },
            @{ Name = "Conformity"; Type = "string"; Column = "conformity"; Required = $false },
            @{ Name = "NcsFound"; Type = "int"; Column = "ncs_found"; Required = $false },
            @{ Name = "CheckedBy"; Type = "long?"; Column = "checked_by"; Required = $false },
            @{ Name = "CheckedAt"; Type = "DateTime?"; Column = "checked_at"; Required = $false },
            @{ Name = "SortOrder"; Type = "int"; Column = "sort_order"; Required = $false }
        )
    },
    @{
        Name = "NonConformity"
        Table = "audit_nonconformity"
        Namespace = "Audit"
        Properties = @(
            @{ Name = "TaskCode"; Type = "string"; Column = "task_code"; Required = $true; Length = 36 },
            @{ Name = "ClauseCode"; Type = "string"; Column = "clause_code"; Required = $true; Length = 36 },
            @{ Name = "NcNumber"; Type = "string"; Column = "nc_number"; Required = $true; Length = 50 },
            @{ Name = "Severity"; Type = "string"; Column = "severity"; Required = $true },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $true },
            @{ Name = "RequirementRef"; Type = "string"; Column = "requirement_ref"; Required = $false },
            @{ Name = "EvidenceRef"; Type = "string"; Column = "evidence_ref"; Required = $false },
            @{ Name = "Status"; Type = "string"; Column = "status"; Required = $false },
            @{ Name = "SourceType"; Type = "string"; Column = "source_type"; Required = $false },
            @{ Name = "SourceCheckCode"; Type = "string"; Column = "source_check_code"; Required = $false; Length = 36 },
            @{ Name = "RuleCode"; Type = "string"; Column = "rule_code"; Required = $false; Length = 36 },
            @{ Name = "DueDate"; Type = "DateTime?"; Column = "due_date"; Required = $false },
            @{ Name = "OpenedBy"; Type = "long"; Column = "opened_by"; Required = $true },
            @{ Name = "OpenedAt"; Type = "DateTime"; Column = "opened_at"; Required = $true },
            @{ Name = "ClosedAt"; Type = "DateTime?"; Column = "closed_at"; Required = $false }
        )
    },
    @{
        Name = "AuditFinding"
        Table = "audit_finding"
        Namespace = "Audit"
        Properties = @(
            @{ Name = "ChecklistItemCode"; Type = "string"; Column = "checklist_item_code"; Required = $true; Length = 36 },
            @{ Name = "NcCode"; Type = "string"; Column = "nc_code"; Required = $false; Length = 36 },
            @{ Name = "SourceFileCode"; Type = "string"; Column = "source_file_code"; Required = $false; Length = 36 },
            @{ Name = "SourcePosition"; Type = "string"; Column = "source_position"; Required = $false; Length = 200 },
            @{ Name = "SourceContent"; Type = "string"; Column = "source_content"; Required = $false },
            @{ Name = "FindingType"; Type = "string"; Column = "finding_type"; Required = $true },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $true },
            @{ Name = "Confidence"; Type = "decimal?"; Column = "confidence"; Required = $false },
            @{ Name = "IsManual"; Type = "bool"; Column = "is_manual"; Required = $false }
        )
    },
    @{
        Name = "AuditEvidence"
        Table = "audit_evidence"
        Namespace = "Audit"
        Properties = @(
            @{ Name = "TaskCode"; Type = "string"; Column = "task_code"; Required = $true; Length = 36 },
            @{ Name = "ClauseCode"; Type = "string"; Column = "clause_code"; Required = $false; Length = 36 },
            @{ Name = "EvidenceType"; Type = "string"; Column = "evidence_type"; Required = $true },
            @{ Name = "StoragePath"; Type = "string"; Column = "storage_path"; Required = $true; Length = 500 },
            @{ Name = "FileHash"; Type = "string"; Column = "file_hash"; Required = $true; Length = 64 },
            @{ Name = "IsVoided"; Type = "bool"; Column = "is_voided"; Required = $false },
            @{ Name = "VoidedAt"; Type = "DateTime?"; Column = "voided_at"; Required = $false },
            @{ Name = "VoidedBy"; Type = "long?"; Column = "voided_by"; Required = $false },
            @{ Name = "CapturedAt"; Type = "DateTime?"; Column = "captured_at"; Required = $false },
            @{ Name = "CapturedBy"; Type = "long"; Column = "captured_by"; Required = $true },
            @{ Name = "Notes"; Type = "string"; Column = "notes"; Required = $false }
        )
    },
    @{
        Name = "Rectification"
        Table = "audit_rectification"
        Namespace = "Audit"
        Properties = @(
            @{ Name = "NcCode"; Type = "string"; Column = "nc_code"; Required = $true; Length = 36 },
            @{ Name = "Correction"; Type = "string"; Column = "correction"; Required = $true },
            @{ Name = "CorrectiveAction"; Type = "string"; Column = "corrective_action"; Required = $false },
            @{ Name = "EvidenceFiles"; Type = "string"; Column = "evidence_files"; Required = $false },
            @{ Name = "SubmittedBy"; Type = "long"; Column = "submitted_by"; Required = $true },
            @{ Name = "SubmittedAt"; Type = "DateTime"; Column = "submitted_at"; Required = $true },
            @{ Name = "VerifiedBy"; Type = "long?"; Column = "verified_by"; Required = $false },
            @{ Name = "VerifiedAt"; Type = "DateTime?"; Column = "verified_at"; Required = $false },
            @{ Name = "VerifyResult"; Type = "string"; Column = "verify_result"; Required = $false },
            @{ Name = "VerifyNotes"; Type = "string"; Column = "verify_notes"; Required = $false }
        )
    },

    # 域 D：报告生成（4 张表）
    @{
        Name = "ReportTask"
        Table = "rpt_report_task"
        Namespace = "Rpt"
        Properties = @(
            @{ Name = "PhaseCode"; Type = "string"; Column = "phase_code"; Required = $true; Length = 36 },
            @{ Name = "BasedOnAuditTaskCode"; Type = "string"; Column = "based_on_audit_task_code"; Required = $false; Length = 36 },
            @{ Name = "TemplateCode"; Type = "string"; Column = "template_code"; Required = $true; Length = 36 },
            @{ Name = "TaskNumber"; Type = "string"; Column = "task_number"; Required = $true; Length = 50 },
            @{ Name = "Status"; Type = "string"; Column = "status"; Required = $false },
            @{ Name = "GeneratedAt"; Type = "DateTime?"; Column = "generated_at"; Required = $false },
            @{ Name = "LockedAt"; Type = "DateTime?"; Column = "locked_at"; Required = $false },
            @{ Name = "LockedBy"; Type = "long?"; Column = "locked_by"; Required = $false }
        )
    },
    @{
        Name = "AuditReport"
        Table = "rpt_audit_report"
        Namespace = "Rpt"
        Properties = @(
            @{ Name = "TaskCode"; Type = "string"; Column = "task_code"; Required = $true; Length = 36 },
            @{ Name = "VersionNumber"; Type = "int"; Column = "version_number"; Required = $false },
            @{ Name = "ReportTitle"; Type = "string"; Column = "report_title"; Required = $true; Length = 500 },
            @{ Name = "FullContent"; Type = "string"; Column = "full_content"; Required = $false },
            @{ Name = "ExportPath"; Type = "string"; Column = "export_path"; Required = $false; Length = 500 },
            @{ Name = "EditedBy"; Type = "long?"; Column = "edited_by"; Required = $false }
        )
    },
    @{
        Name = "ReportSection"
        Table = "rpt_report_section"
        Namespace = "Rpt"
        Properties = @(
            @{ Name = "ReportCode"; Type = "string"; Column = "report_code"; Required = $true; Length = 36 },
            @{ Name = "ClauseCode"; Type = "string"; Column = "clause_code"; Required = $false; Length = 36 },
            @{ Name = "SectionName"; Type = "string"; Column = "section_name"; Required = $true; Length = 200 },
            @{ Name = "SectionContent"; Type = "string"; Column = "section_content"; Required = $false },
            @{ Name = "WorkflowCode"; Type = "string"; Column = "workflow_code"; Required = $false; Length = 36 },
            @{ Name = "SortOrder"; Type = "int"; Column = "sort_order"; Required = $false }
        )
    },
    @{
        Name = "ReportSectionSource"
        Table = "rpt_report_section_source"
        Namespace = "Rpt"
        Properties = @(
            @{ Name = "SectionCode"; Type = "string"; Column = "section_code"; Required = $true; Length = 36 },
            @{ Name = "SourceType"; Type = "string"; Column = "source_type"; Required = $true },
            @{ Name = "SourceCode"; Type = "string"; Column = "source_code"; Required = $false; Length = 36 },
            @{ Name = "SourceDescription"; Type = "string"; Column = "source_description"; Required = $false },
            @{ Name = "Confidence"; Type = "decimal?"; Column = "confidence"; Required = $false }
        )
    },

    # 域 E：系统基础（2 张补充表）
    @{
        Name = "SysLog"
        Table = "sys_log"
        Namespace = "Sys"
        Properties = @(
            @{ Name = "UserId"; Type = "long?"; Column = "user_id"; Required = $false },
            @{ Name = "Module"; Type = "string"; Column = "module"; Required = $true; Length = 50 },
            @{ Name = "Action"; Type = "string"; Column = "action"; Required = $true; Length = 100 },
            @{ Name = "TargetType"; Type = "string"; Column = "target_type"; Required = $false; Length = 50 },
            @{ Name = "TargetId"; Type = "long?"; Column = "target_id"; Required = $false },
            @{ Name = "Detail"; Type = "string"; Column = "detail"; Required = $false },
            @{ Name = "IpAddress"; Type = "string"; Column = "ip_address"; Required = $false; Length = 50 },
            @{ Name = "UserAgent"; Type = "string"; Column = "user_agent"; Required = $false; Length = 500 }
        )
    },
    @{
        Name = "SysConfig"
        Table = "sys_config"
        Namespace = "Sys"
        Properties = @(
            @{ Name = "ConfigKey"; Type = "string"; Column = "config_key"; Required = $true; Length = 100 },
            @{ Name = "ConfigValue"; Type = "string"; Column = "config_value"; Required = $true },
            @{ Name = "ValueType"; Type = "string"; Column = "value_type"; Required = $false },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $false },
            @{ Name = "IsSystem"; Type = "bool"; Column = "is_system"; Required = $false }
        )
    },

    # 域 F：工作流框架（4 张表）
    @{
        Name = "Skill"
        Table = "wf_skill"
        Namespace = "Wf"
        Properties = @(
            @{ Name = "SkillCode"; Type = "string"; Column = "skill_code"; Required = $true; Length = 100 },
            @{ Name = "SkillName"; Type = "string"; Column = "skill_name"; Required = $true; Length = 200 },
            @{ Name = "SkillType"; Type = "string"; Column = "skill_type"; Required = $true },
            @{ Name = "InputSchema"; Type = "string"; Column = "input_schema"; Required = $false },
            @{ Name = "OutputSchema"; Type = "string"; Column = "output_schema"; Required = $false },
            @{ Name = "EndpointConfig"; Type = "string"; Column = "endpoint_config"; Required = $false },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $false },
            @{ Name = "IsActive"; Type = "bool"; Column = "is_active"; Required = $false }
        )
    },
    @{
        Name = "FieldLabelMapping"
        Table = "wf_field_label_mapping"
        Namespace = "Wf"
        Properties = @(
            @{ Name = "LabelTag"; Type = "string"; Column = "label_tag"; Required = $true; Length = 500 },
            @{ Name = "FieldCode"; Type = "string"; Column = "field_code"; Required = $true; Length = 200 },
            @{ Name = "StandardCode"; Type = "string"; Column = "standard_code"; Required = $true; Length = 36 },
            @{ Name = "ScopeLevel"; Type = "string"; Column = "scope_level"; Required = $false; Length = 100 },
            @{ Name = "DocumentName"; Type = "string"; Column = "document_name"; Required = $false; Length = 200 },
            @{ Name = "FieldName"; Type = "string"; Column = "field_name"; Required = $false; Length = 100 },
            @{ Name = "DataType"; Type = "string"; Column = "data_type"; Required = $false; Length = 50 },
            @{ Name = "SkillCode"; Type = "string"; Column = "skill_code"; Required = $false; Length = 36 },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $false }
        )
    },
    @{
        Name = "WorkflowDefinition"
        Table = "wf_workflow_definition"
        Namespace = "Wf"
        Properties = @(
            @{ Name = "WorkflowCode"; Type = "string"; Column = "workflow_code"; Required = $true; Length = 100 },
            @{ Name = "WorkflowName"; Type = "string"; Column = "workflow_name"; Required = $true; Length = 200 },
            @{ Name = "WorkflowType"; Type = "string"; Column = "workflow_type"; Required = $true },
            @{ Name = "WorkflowConfig"; Type = "string"; Column = "workflow_config"; Required = $true },
            @{ Name = "Version"; Type = "int"; Column = "version"; Required = $false },
            @{ Name = "IsActive"; Type = "bool"; Column = "is_active"; Required = $false },
            @{ Name = "Description"; Type = "string"; Column = "description"; Required = $false }
        )
    },
    @{
        Name = "WorkflowExecutionLog"
        Table = "wf_workflow_execution_log"
        Namespace = "Wf"
        Properties = @(
            @{ Name = "WorkflowCode"; Type = "string"; Column = "workflow_code"; Required = $true; Length = 36 },
            @{ Name = "WorkflowVersion"; Type = "int"; Column = "workflow_version"; Required = $true },
            @{ Name = "BusinessType"; Type = "string"; Column = "business_type"; Required = $true },
            @{ Name = "BusinessId"; Type = "long"; Column = "business_id"; Required = $true },
            @{ Name = "NodeId"; Type = "string"; Column = "node_id"; Required = $true; Length = 50 },
            @{ Name = "SkillCode"; Type = "string"; Column = "skill_code"; Required = $true; Length = 100 },
            @{ Name = "InputData"; Type = "string"; Column = "input_data"; Required = $false },
            @{ Name = "OutputData"; Type = "string"; Column = "output_data"; Required = $false },
            @{ Name = "Status"; Type = "string"; Column = "status"; Required = $true },
            @{ Name = "ErrorMsg"; Type = "string"; Column = "error_msg"; Required = $false },
            @{ Name = "DurationMs"; Type = "int?"; Column = "duration_ms"; Required = $false },
            @{ Name = "StartedAt"; Type = "DateTime"; Column = "started_at"; Required = $true },
            @{ Name = "CompletedAt"; Type = "DateTime?"; Column = "completed_at"; Required = $false }
        )
    }
)

# 创建目录结构
$namespaces = @("Cert", "Ent", "Audit", "Rpt", "Sys", "Wf")
foreach ($ns in $namespaces) {
    $dir = Join-Path $baseDir $ns
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "✅ 创建目录: $dir"
    }
}

# 生成实体类
foreach ($entity in $entities) {
    $namespace = $entity.Namespace
    $name = $entity.Name
    $table = $entity.Table
    $properties = $entity.Properties
    
    $filePath = Join-Path $baseDir "$namespace\$name.cs"
    
    # 生成属性代码
    $propCodes = @()
    foreach ($prop in $properties) {
        $attrLines = @()
        
        if ($prop.Required) {
            $attrLines += '[Required]'
        }
        if ($prop.Length) {
            $attrLines += "[StringLength($($prop.Length))]"
        }
        $attrLines += "[Column(`"$($prop.Column)`")]"
        
        $attrStr = ($attrLines | Out-String).Trim()
        $propCodes += "$attrStr`n`t`t`tpublic $($prop.Type) $($prop.Name) { get; set; }"
    }
    
    $propsText = ($propCodes -join "`n`t`t")
    
    # 生成文件内容
    $content = @"
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.$namespace
{
    /// <summary>
    /// $name
    /// <para>表名：$table</para>
    /// <para>域：$namespace</para>
    /// </summary>
    [Table("$table")]
    public class $name : BaseEntity
    {
$propsText
    }
}
"@
    
    Set-Content -Path $filePath -Value $content -Encoding UTF8
    Write-Host "✅ 生成实体: $namespace.$name -> $table"
}

Write-Host "`n🎉 实体类生成完成！共生成 $($entities.Count) 个实体类。"
