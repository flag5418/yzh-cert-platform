-- ============================================================
-- 修复业务表缺失的唯一索引（对齐代码中的 [UniqueField] 特性）
--
-- 背景：
--   代码中已为实体属性声明 [UniqueField] 特性，但部分数据库表
--   未同步创建对应的 UNIQUE 约束，导致重复数据可被插入。
--
-- 本脚本：
--   1. 清理 cert_iso_standard / cert_iso_clause 中的重复数据
--   2. 为所有缺失唯一约束的表补充 UNIQUE INDEX
--
-- 执行前：已确认以下表有重复数据：
--   cert_iso_standard.standard_name='1'（2条垃圾数据）
--   cert_iso_clause: (standard_code='ISO13485-CODE',clause_number='1') 2条
--   cert_iso_clause: (standard_code='a504...',clause_number='2') 2条
-- ============================================================

-- ════════════════════════════════════════════════════════════
-- 第一步：清理重复数据
-- ════════════════════════════════════════════════════════════

-- cert_iso_standard.standard_name='1' 有两条：
--   id=2088459357187608576 (standard_code='11', version_year=2026)
--   id=2092136391839453184 (standard_code='1', version_year=2015)
-- 两者均为测试垃圾数据，删除 id 较小的（后插入的）
DELETE FROM cert_iso_standard WHERE id = 2092136391839453184;

-- cert_iso_clause 重复组1：(ISO13485-CODE, clause_number='1')
--   id=36 parent_code='' title='范围(已编辑)' sort_order=0
--   id=38 parent_code='52ef...' title='1' sort_order=0
-- 保留 id=36（有实际内容），删除 id=38（垃圾数据）
DELETE FROM cert_iso_clause WHERE id = 38;

-- cert_iso_clause 重复组2：(a504..., clause_number='2')
--   id=41 title='2' sort_order=0
--   id=43 title='2' sort_order=2
-- 保留 id=41（先插入的），删除 id=43
DELETE FROM cert_iso_clause WHERE id = 43;

-- ════════════════════════════════════════════════════════════
-- 第二步：补充缺失的唯一索引
-- ════════════════════════════════════════════════════════════

-- ------------------------------------------------------------
-- 1. cert_iso_standard — [UniqueField("标准名称")] → standard_name
--    已有：uk_org_std_ver(standard_code, version_year)
--    缺失：standard_name 单字段唯一
-- ------------------------------------------------------------
ALTER TABLE cert_iso_standard
    ADD UNIQUE INDEX uk_standard_name (standard_name);

-- ------------------------------------------------------------
-- 2. cert_iso_clause — [UniqueField("条款编号", WithFields={"StandardCode"})] → clause_number + standard_code
--    缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_iso_clause
    ADD UNIQUE INDEX uk_std_clause (standard_code, clause_number);

-- ------------------------------------------------------------
-- 3. cert_certification_body — [UniqueField("机构名称")] → name
--    已有：cb_code 唯一
--    缺失：name 单字段唯一
-- ------------------------------------------------------------
ALTER TABLE cert_certification_body
    ADD UNIQUE INDEX uk_name (name);

-- ------------------------------------------------------------
-- 4. cert_report_template — [UniqueField("模板名称", WithFields={"CbCode","StandardCode","PhaseCode"})]
--    缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_report_template
    ADD UNIQUE INDEX uk_tpl_name (cb_code, standard_code, phase_code, template_name);

-- ------------------------------------------------------------
-- 5. cert_directory_template — [UniqueField("文件夹名称", WithFields={"ConfigCode","ParentCode"})]
--    缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_directory_template
    ADD UNIQUE INDEX uk_dir_folder (config_code, parent_code, folder_name);

-- ------------------------------------------------------------
-- 6. cert_file_requirement — [UniqueField("文件名称", WithFields={"FolderCode"})]
--    缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_file_requirement
    ADD UNIQUE INDEX uk_folder_file (folder_code, file_name_template);

-- ------------------------------------------------------------
-- 7. cert_extraction_rule — [UniqueField("技能编码", WithFields={"FileRequirementCode"})]
--    缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_extraction_rule
    ADD UNIQUE INDEX uk_file_skill (file_requirement_code, skill_code);

-- ------------------------------------------------------------
-- 8. cert_doc_field_def — [UniqueField("字段编码", WithFields={"RuleCode"})]
--    缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_doc_field_def
    ADD UNIQUE INDEX uk_rule_field (rule_code, field_code);

-- ------------------------------------------------------------
-- 9. cert_doc_table_def — [UniqueField("表格编码", WithFields={"RuleCode"})]
--    缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_doc_table_def
    ADD UNIQUE INDEX uk_rule_table (rule_code, table_code);

-- ------------------------------------------------------------
-- 10. cert_doc_table_field_def — [UniqueField("列编码", WithFields={"TableCode"})]
--     缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_doc_table_field_def
    ADD UNIQUE INDEX uk_table_column (table_code, column_code);

-- ------------------------------------------------------------
-- 11. cert_ai_config — [UniqueField("模型", WithFields={"Provider"})]
--     缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_ai_config
    ADD UNIQUE INDEX uk_provider_model (provider, model);

-- ------------------------------------------------------------
-- 12. cert_org_standard — [UniqueField("机构编码", WithFields={"StdCode"})] → org_code + standard_code
--     缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_org_standard
    ADD UNIQUE INDEX uk_org_std (org_code, standard_code);

-- ------------------------------------------------------------
-- 13. cert_org_stage — [UniqueField("机构编码", WithFields={"StdCode","StageCode"})]
--     缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE cert_org_stage
    ADD UNIQUE INDEX uk_org_std_stage (org_code, standard_code, phase_code);

-- ------------------------------------------------------------
-- 14. rpt_report_section — [UniqueField("章节名称", WithFields={"ReportCode"})]
--     缺失：联合唯一索引
-- ------------------------------------------------------------
ALTER TABLE rpt_report_section
    ADD UNIQUE INDEX uk_report_section (report_code, section_name);

-- ------------------------------------------------------------
-- 15. ent_file_version — [UniqueField("文件编码", WithFields={"VersionNumber"})]
--     已有：uk_file_version(file_code, version_number) ✓ 无需补充
-- ------------------------------------------------------------

-- ------------------------------------------------------------
-- 16. rpt_audit_report — [UniqueField("报告编号")]
--     已有：report_number 唯一 ✓ 无需补充
-- ------------------------------------------------------------

-- ------------------------------------------------------------
-- 17. audit_task — [UniqueField("任务编号")]
--     已有：task_number 唯一 ✓ 无需补充
-- ------------------------------------------------------------

-- ------------------------------------------------------------
-- 18. cert_auditor_profile — [UniqueField("用户ID")] + [UniqueField("审核员资格证号")]
--     已有：uk_user_id + auditor_no ✓ 无需补充
-- ------------------------------------------------------------

-- ------------------------------------------------------------
-- 19. ent_enterprise — [UniqueField("企业编号")] + [UniqueField("统一社会信用代码")]
--     已有：enterprise_no + credit_code ✓ 无需补充
-- ------------------------------------------------------------

-- ------------------------------------------------------------
-- 20. wf_skill — [UniqueField("技能编码")]
--     已有：uk_skill_code ✓ 无需补充
-- ------------------------------------------------------------

-- ------------------------------------------------------------
-- 21. wf_workflow_definition — [UniqueField("工作流编码")]
--     已有：uk_workflow_code ✓ 无需补充
-- ------------------------------------------------------------

-- ------------------------------------------------------------
-- 22. wf_prompt_template — [UniqueField("Prompt编码")]
--     已有：uk_prompt_code ✓ 无需补充
-- ------------------------------------------------------------

-- ════════════════════════════════════════════════════════════
-- 验证：执行后检查所有唯一索引
-- ════════════════════════════════════════════════════════════
SELECT TABLE_NAME, INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',') AS COLS
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA='yzh_cert_platform'
  AND TABLE_NAME LIKE 'cert_%'
  AND NON_UNIQUE=0
  AND INDEX_NAME != 'PRIMARY'
GROUP BY TABLE_NAME, INDEX_NAME
ORDER BY TABLE_NAME, INDEX_NAME;
