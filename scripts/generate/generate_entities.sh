#!/bin/bash
# ============================================================================
# 体系认证平台 - 实体类自动生成脚本（Bash 版本）
# ============================================================================
BASE_DIR="/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/VOL.Entity/CertPlatform"

generate_entity() {
    local namespace=$1
    local name=$2
    local table=$3
    local properties=$4
    
    local file_path="${BASE_DIR}/${namespace}/${name}.cs"
    
    cat > "$file_path" << EOF
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.${namespace}
{
    /// <summary>
    /// ${name}
    /// <para>表名：${table}</para>
    /// </summary>
    [Table("${table}")]
    public class ${name} : BaseEntity
    {
${properties}
    }
}
EOF

    echo "✅ 生成: ${namespace}.${name}"
}

# 域 A 剩余实体
generate_entity "Cert" "DirectoryTemplate" "cert_directory_template" '
    [Required][StringLength(36)][Column("config_code")]
    public string ConfigCode { get; set; }
    [StringLength(36)][Column("parent_code")]
    public string ParentCode { get; set; }
    [Required][StringLength(200)][Column("folder_name")]
    public string FolderName { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;
'

generate_entity "Cert" "FileRequirement" "cert_file_requirement" '
    [Required][StringLength(36)][Column("folder_code")]
    public string FolderCode { get; set; }
    [Required][StringLength(200)][Column("file_name_template")]
    public string FileNameTemplate { get; set; }
    [Required][StringLength(50)][Column("file_type")]
    public string FileType { get; set; }
    [Column("is_required")]
    public bool IsRequired { get; set; } = true;
    [Column("max_size_mb")]
    public int MaxSizeMB { get; set; } = 10;
    [Column("description")]
    public string Description { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;
'

generate_entity "Cert" "ExtractionRule" "cert_extraction_rule" '
    [Required][StringLength(36)][Column("file_requirement_code")]
    public string FileRequirementCode { get; set; }
    [Required][StringLength(36)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Required][Column("rule_type")]
    public string RuleType { get; set; }
    [Required][Column("rule_config")]
    public string RuleConfig { get; set; }
    [Column("description")]
    public string Description { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
'

generate_entity "Cert" "ExtractionField" "cert_extraction_field" '
    [Required][StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [StringLength(36)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Required][StringLength(100)][Column("field_code")]
    public string FieldCode { get; set; }
    [Required][StringLength(500)][Column("label_tag")]
    public string LabelTag { get; set; }
    [Required][StringLength(100)][Column("field_name")]
    public string FieldName { get; set; }
    [Column("field_type")]
    public string FieldType { get; set; } = "string";
    [Column("enum_values")]
    public string EnumValues { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;
'

generate_entity "Cert" "ValidationRule" "cert_validation_rule" '
    [Required][StringLength(36)][Column("standard_code")]
    public string StandardCode { get; set; }
    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Required][StringLength(36)][Column("workflow_code")]
    public string WorkflowCode { get; set; }
    [Required][StringLength(50)][Column("rule_code")]
    public string RuleCode { get; set; }
    [Required][StringLength(200)][Column("rule_name")]
    public string RuleName { get; set; }
    [Required][Column("severity_if_violated")]
    public string SeverityIfViolated { get; set; }
    [Column("nc_description_template")]
    public string NcDescriptionTemplate { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
'

generate_entity "Cert" "ValidationRuleSource" "cert_validation_rule_source" '
    [Required][StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [Required][StringLength(36)][Column("file_requirement_code")]
    public string FileRequirementCode { get; set; }
    [StringLength(500)][Column("source_path")]
    public string SourcePath { get; set; }
    [Column("notes")]
    public string Notes { get; set; }
'

generate_entity "Cert" "ReportTemplate" "cert_report_template" '
    [Required][StringLength(36)][Column("cb_code")]
    public string CbCode { get; set; }
    [Required][StringLength(36)][Column("standard_code")]
    public string StandardCode { get; set; }
    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][StringLength(200)][Column("template_name")]
    public string TemplateName { get; set; }
    [StringLength(500)][Column("template_file_path")]
    public string TemplateFilePath { get; set; }
    [Column("section_config")]
    public string SectionConfig { get; set; }
    [Column("is_default")]
    public bool IsDefault { get; set; } = false;
'

generate_entity "Cert" "ClauseExtractionRule" "cert_clause_extraction_rule" '
    [Required][StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Required][StringLength(36)][Column("workflow_code")]
    public string WorkflowCode { get; set; }
    [Column("description")]
    public string Description { get; set; }
'

# 域 B：企业档案（9 张表）
generate_entity "Ent" "Enterprise" "ent_enterprise" '
    [Required][StringLength(200)][Column("name")]
    public string Name { get; set; }
    [StringLength(100)][Column("short_name")]
    public string ShortName { get; set; }
    [StringLength(50)][Column("credit_code")]
    public string CreditCode { get; set; }
    [StringLength(50)][Column("legal_person")]
    public string LegalPerson { get; set; }
    [Column("address")]
    public string Address { get; set; }
    [Column("cert_scope")]
    public string CertScope { get; set; }
    [StringLength(50)][Column("contact_name")]
    public string ContactName { get; set; }
    [StringLength(20)][Column("contact_phone")]
    public string ContactPhone { get; set; }
    [StringLength(200)][Column("contact_email")]
    public string ContactEmail { get; set; }
    [Column("status")]
    public string Status { get; set; } = "active";
    [Column("archive_date")]
    public DateTime? ArchiveDate { get; set; }
    [Column("notes")]
    public string Notes { get; set; }
'

generate_entity "Ent" "EnterprisePhase" "ent_enterprise_phase" '
    [Required][StringLength(36)][Column("enterprise_code")]
    public string EnterpriseCode { get; set; }
    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][StringLength(36)][Column("standard_code")]
    public string StandardCode { get; set; }
    [Column("status")]
    public string Status { get; set; } = "pending";
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
'

generate_entity "Ent" "EnterpriseDocument" "ent_enterprise_document" '
    [Required][StringLength(36)][Column("enterprise_code")]
    public string EnterpriseCode { get; set; }
    [StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][Column("scope")]
    public string Scope { get; set; }
    [StringLength(36)[Column("template_folder_code")]
    public string TemplateFolderCode { get; set; }
    [StringLength(36)][Column("parent_code")]
    public string ParentCode { get; set; }
    [Required][StringLength(200)][Column("folder_name")]
    public string FolderName { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;
'

generate_entity "Ent" "EnterpriseFile" "ent_enterprise_file" '
    [Required][StringLength(36)][Column("folder_code")]
    public string FolderCode { get; set; }
    [Required][StringLength(500)][Column("file_name")]
    public string FileName { get; set; }
    [Required][StringLength(50)][Column("file_type")]
    public string FileType { get; set; }
    [Required][Column("file_size")]
    public long FileSize { get; set; }
    [Required][StringLength(500)][Column("storage_path")]
    public string StoragePath { get; set; }
    [StringLength(64)][Column("file_hash")]
    public string FileHash { get; set; }
    [Column("current_version")]
    public int CurrentVersion { get; set; } = 1;
    [Column("notes")]
    public string Notes { get; set; }
'

generate_entity "Ent" "FileVersion" "ent_file_version" '
    [Required][StringLength(36)][Column("file_code")]
    public string FileCode { get; set; }
    [Required][Column("version_number")]
    public int VersionNumber { get; set; }
    [Required][Column("file_size")]
    public long FileSize { get; set; }
    [Required][StringLength(500)][Column("storage_path")]
    public string StoragePath { get; set; }
    [Required][StringLength(64)][Column("file_hash")]
    public string FileHash { get; set; }
    [Column("change_notes")]
    public string ChangeNotes { get; set; }
'

generate_entity "Ent" "FilePreCheckResult" "ent_file_pre_check_result" '
    [Required][StringLength(36)][Column("file_code")]
    public string FileCode { get; set; }
    [Required][Column("version_number")]
    public int VersionNumber { get; set; }
    [Required][Column("check_type")]
    public string CheckType { get; set; }
    [Required][Column("check_result")]
    public string CheckResult { get; set; }
    [Column("message")]
    public string Message { get; set; }
    [Column("detail")]
    public string Detail { get; set; }
    [Required][Column("checked_at")]
    public DateTime CheckedAt { get; set; }
'

generate_entity "Ent" "FileComplianceCheck" "ent_file_compliance_check" '
    [Required][StringLength(36)][Column("file_code")]
    public string FileCode { get; set; }
    [Required][Column("version_number")]
    public int VersionNumber { get; set; }
    [Required][StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [StringLength(36)][Column("workflow_execution_code")]
    public string WorkflowExecutionCode { get; set; }
    [Required][Column("check_status")]
    public string CheckStatus { get; set; }
    [Column("message")]
    public string Message { get; set; }
    [Column("detail")]
    public string Detail { get; set; }
    [Required][Column("checked_at")]
    public DateTime CheckedAt { get; set; }
'

generate_entity "Ent" "ExtractionResult" "ent_extraction_result" '
    [Required][StringLength(36)][Column("file_code")]
    public string FileCode { get; set; }
    [Required][Column("version_number")]
    public int VersionNumber { get; set; }
    [Required][StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [Required][StringLength(36)][Column("field_code")]
    public string FieldCode { get; set; }
    [StringLength(500)][Column("label_tag")]
    public string LabelTag { get; set; }
    [Column("extracted_value")]
    public string ExtractedValue { get; set; }
    [Column("confidence")]
    public decimal? Confidence { get; set; }
    [Column("position_info")]
    public string PositionInfo { get; set; }
    [Column("is_manual_edited")]
    public bool IsManualEdited { get; set; } = false;
    [Required][Column("extracted_at")]
    public DateTime ExtractedAt { get; set; }
'

generate_entity "Ent" "TableExtractionResult" "ent_table_extraction_result" '
    [Required][StringLength(36)][Column("file_code")]
    public string FileCode { get; set; }
    [Required][Column("version_number")]
    public int VersionNumber { get; set; }
    [Required][StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [Column("table_index")]
    public int TableIndex { get; set; } = 1;
    [Required][Column("extracted_json")]
    public string ExtractedJson { get; set; }
    [Column("confidence")]
    public decimal? Confidence { get; set; }
    [Column("position_info")]
    public string PositionInfo { get; set; }
    [Required][Column("extracted_at")]
    public DateTime ExtractedAt { get; set; }
'

# 域 C：审核执行（6 张表）
generate_entity "Audit" "AuditTask" "audit_task" '
    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][StringLength(50)][Column("task_number")]
    public string TaskNumber { get; set; }
    [Required][Column("auditor_id")]
    public long AuditorId { get; set; }
    [Column("status")]
    public string Status { get; set; } = "pending";
    [Column("planned_date")]
    public DateTime? PlannedDate { get; set; }
    [Column("actual_start_date")]
    public DateTime? ActualStartDate { get; set; }
    [Column("actual_complete_date")]
    public DateTime? ActualCompleteDate { get; set; }
    [Column("audit_scope")]
    public string AuditScope { get; set; }
    [Column("notes")]
    public string Notes { get; set; }
'

generate_entity "Audit" "ChecklistItem" "audit_checklist_item" '
    [Required][StringLength(36)][Column("task_code")]
    public string TaskCode { get; set; }
    [Required][StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Column("audit_criteria")]
    public string AuditCriteria { get; set; }
    [Column("finding_description")]
    public string FindingDescription { get; set; }
    [Column("conformity")]
    public string Conformity { get; set; } = "pending";
    [Column("ncs_found")]
    public int NcsFound { get; set; } = 0;
    [Column("checked_by")]
    public long? CheckedBy { get; set; }
    [Column("checked_at")]
    public DateTime? CheckedAt { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;
'

generate_entity "Audit" "NonConformity" "audit_nonconformity" '
    [Required][StringLength(36)][Column("task_code")]
    public string TaskCode { get; set; }
    [Required][StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Required][StringLength(50)][Column("nc_number")]
    public string NcNumber { get; set; }
    [Required][Column("severity")]
    public string Severity { get; set; }
    [Required][Column("description")]
    public string Description { get; set; }
    [Column("requirement_ref")]
    public string RequirementRef { get; set; }
    [Column("evidence_ref")]
    public string EvidenceRef { get; set; }
    [Column("status")]
    public string Status { get; set; } = "open";
    [Column("source_type")]
    public string SourceType { get; set; } = "manual";
    [StringLength(36)][Column("source_check_code")]
    public string SourceCheckCode { get; set; }
    [StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [Column("due_date")]
    public DateTime? DueDate { get; set; }
    [Required][Column("opened_by")]
    public long OpenedBy { get; set; }
    [Required][Column("opened_at")]
    public DateTime OpenedAt { get; set; }
    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }
'

generate_entity "Audit" "AuditFinding" "audit_finding" '
    [Required][StringLength(36)][Column("checklist_item_code")]
    public string ChecklistItemCode { get; set; }
    [StringLength(36)][Column("nc_code")]
    public string NcCode { get; set; }
    [StringLength(36)[Column("source_file_code")]
    public string SourceFileCode { get; set; }
    [StringLength(200)][Column("source_position")]
    public string SourcePosition { get; set; }
    [Column("source_content")]
    public string SourceContent { get; set; }
    [Required][Column("finding_type")]
    public string FindingType { get; set; }
    [Required][Column("description")]
    public string Description { get; set; }
    [Column("confidence")]
    public decimal? Confidence { get; set; }
    [Column("is_manual")]
    public bool IsManual { get; set; } = false;
'

generate_entity "Audit" "AuditEvidence" "audit_evidence" '
    [Required][StringLength(36)][Column("task_code")]
    public string TaskCode { get; set; }
    [StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Required][Column("evidence_type")]
    public string EvidenceType { get; set; }
    [Required][StringLength(500)][Column("storage_path")]
    public string StoragePath { get; set; }
    [Required][StringLength(64)][Column("file_hash")]
    public string FileHash { get; set; }
    [Column("is_voided")]
    public bool IsVoided { get; set; } = false;
    [Column("voided_at")]
    public DateTime? VoidedAt { get; set; }
    [Column("voided_by")]
    public long? VoidedBy { get; set; }
    [Column("captured_at")]
    public DateTime? CapturedAt { get; set; }
    [Required][Column("captured_by")]
    public long CapturedBy { get; set; }
    [Column("notes")]
    public string Notes { get; set; }
'

generate_entity "Audit" "Rectification" "audit_rectification" '
    [Required][StringLength(36)][Column("nc_code")]
    public string NcCode { get; set; }
    [Required][Column("correction")]
    public string Correction { get; set; }
    [Column("corrective_action")]
    public string CorrectiveAction { get; set; }
    [Column("evidence_files")]
    public string EvidenceFiles { get; set; }
    [Required][Column("submitted_by")]
    public long SubmittedBy { get; set; }
    [Required][Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }
    [Column("verified_by")]
    public long? VerifiedBy { get; set; }
    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }
    [Column("verify_result")]
    public string VerifyResult { get; set; }
    [Column("verify_notes")]
    public string VerifyNotes { get; set; }
'

# 域 D：报告生成（4 张表）
generate_entity "Rpt" "ReportTask" "rpt_report_task" '
    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [StringLength(36)][Column("based_on_audit_task_code")]
    public string BasedOnAuditTaskCode { get; set; }
    [Required][StringLength(36)][Column("template_code")]
    public string TemplateCode { get; set; }
    [Required][StringLength(50)][Column("task_number")]
    public string TaskNumber { get; set; }
    [Column("status")]
    public string Status { get; set; } = "draft";
    [Column("generated_at")]
    public DateTime? GeneratedAt { get; set; }
    [Column("locked_at")]
    public DateTime? LockedAt { get; set; }
    [Column("locked_by")]
    public long? LockedBy { get; set; }
'

generate_entity "Rpt" "AuditReport" "rpt_audit_report" '
    [Required][StringLength(36)][Column("task_code")]
    public string TaskCode { get; set; }
    [Column("version_number")]
    public int VersionNumber { get; set; } = 1;
    [Required][StringLength(500)[Column("report_title")]
    public string ReportTitle { get; set; }
    [Column("full_content")]
    public string FullContent { get; set; }
    [StringLength(500)[Column("export_path")]
    public string ExportPath { get; set; }
    [Column("edited_by")]
    public long? EditedBy { get; set; }
'

generate_entity "Rpt" "ReportSection" "rpt_report_section" '
    [Required][StringLength(36)][Column("report_code")]
    public string ReportCode { get; set; }
    [StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Required][StringLength(200)][Column("section_name")]
    public string SectionName { get; set; }
    [Column("section_content")]
    public string SectionContent { get; set; }
    [StringLength(36)][Column("workflow_code")]
    public string WorkflowCode { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;
'

generate_entity "Rpt" "ReportSectionSource" "rpt_report_section_source" '
    [Required][StringLength(36)][Column("section_code")]
    public string SectionCode { get; set; }
    [Required][Column("source_type")]
    public string SourceType { get; set; }
    [StringLength(36)][Column("source_code")]
    public string SourceCode { get; set; }
    [Column("source_description")]
    public string SourceDescription { get; set; }
    [Column("confidence")]
    public decimal? Confidence { get; set; }
'

# 域 E：系统基础（2 张补充表）
generate_entity "Sys" "SysLog" "sys_log" '
    [Column("user_id")]
    public long? UserId { get; set; }
    [Required][StringLength(50)][Column("module")]
    public string Module { get; set; }
    [Required][StringLength(100)][Column("action")]
    public string Action { get; set; }
    [StringLength(50)][Column("target_type")]
    public string TargetType { get; set; }
    [Column("target_id")]
    public long? TargetId { get; set; }
    [Column("detail")]
    public string Detail { get; set; }
    [StringLength(50)][Column("ip_address")]
    public string IpAddress { get; set; }
    [StringLength(500)][Column("user_agent")]
    public string UserAgent { get; set; }
'

generate_entity "Sys" "SysConfig" "sys_config" '
    [Required][StringLength(100)][Column("config_key")]
    public string ConfigKey { get; set; }
    [Required][Column("config_value")]
    public string ConfigValue { get; set; }
    [Column("value_type")]
    public string ValueType { get; set; } = "string";
    [Column("description")]
    public string Description { get; set; }
    [Column("is_system")]
    public bool IsSystem { get; set; } = false;
'

# 域 F：工作流框架（4 张表）
generate_entity "Wf" "Skill" "wf_skill" '
    [Required][StringLength(100)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Required][StringLength(200)][Column("skill_name")]
    public string SkillName { get; set; }
    [Required][Column("skill_type")]
    public string SkillType { get; set; }
    [Column("input_schema")]
    public string InputSchema { get; set; }
    [Column("output_schema")]
    public string OutputSchema { get; set; }
    [Column("endpoint_config")]
    public string EndpointConfig { get; set; }
    [Column("description")]
    public string Description { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
'

generate_entity "Wf" "FieldLabelMapping" "wf_field_label_mapping" '
    [Required][StringLength(500)][Column("label_tag")]
    public string LabelTag { get; set; }
    [Required][StringLength(200)][Column("field_code")]
    public string FieldCode { get; set; }
    [Required][StringLength(36)][Column("standard_code")]
    public string StandardCode { get; set; }
    [StringLength(100)][Column("scope_level")]
    public string ScopeLevel { get; set; }
    [StringLength(200)[Column("document_name")]
    public string DocumentName { get; set; }
    [StringLength(100)[Column("field_name")]
    public string FieldName { get; set; }
    [StringLength(50)[Column("data_type")]
    public string DataType { get; set; }
    [StringLength(36)[Column("skill_code")]
    public string SkillCode { get; set; }
    [Column("description")]
    public string Description { get; set; }
'

generate_entity "Wf" "WorkflowDefinition" "wf_workflow_definition" '
    [Required][StringLength(100)][Column("workflow_code")]
    public string WorkflowCode { get; set; }
    [Required][StringLength(200)][Column("workflow_name")]
    public string WorkflowName { get; set; }
    [Required][Column("workflow_type")]
    public string WorkflowType { get; set; }
    [Required][Column("workflow_config")]
    public string WorkflowConfig { get; set; }
    [Column("version")]
    public int Version { get; set; } = 1;
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    [Column("description")]
    public string Description { get; set; }
'

generate_entity "Wf" "WorkflowExecutionLog" "wf_workflow_execution_log" '
    [Required][StringLength(36)][Column("workflow_code")]
    public string WorkflowCode { get; set; }
    [Required][Column("workflow_version")]
    public int WorkflowVersion { get; set; }
    [Required][Column("business_type")]
    public string BusinessType { get; set; }
    [Required][Column("business_id")]
    public long BusinessId { get; set; }
    [Required][StringLength(50)][Column("node_id")]
    public string NodeId { get; set; }
    [Required][StringLength(100)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Column("input_data")]
    public string InputData { get; set; }
    [Column("output_data")]
    public string OutputData { get; set; }
    [Required][Column("status")]
    public string Status { get; set; }
    [Column("error_msg")]
    public string ErrorMsg { get; set; }
    [Column("duration_ms")]
    public int? DurationMs { get; set; }
    [Required][Column("started_at")]
    public DateTime StartedAt { get; set; }
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
'

echo ""
echo "🎉 实体类生成完成！"
echo "📊 统计信息："
find "$BASE_DIR" -name "*.cs" -type f | wc -l | xargs echo "   总文件数:"
du -sh "$BASE_DIR" | awk '{print "   总大小: " $1}'
