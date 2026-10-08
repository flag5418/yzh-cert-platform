-- =====================================================================================
-- 20261008_stage_code_unify_V1.sql  —— 阶段关联键统一为 Code(GUID) + 清除测试阶段行
-- 【裁决】
--   Q2：所有阶段关联一律存 cert_cert_stage.Code（GUID），⛔ 不存业务码（jd01/03 这类 slug）
--   Q3：删除测试阶段行 jd01 初审 / jd02 预审 / 03 复审（先迁移全部引用，再删除）
-- 【映射（一次性、可追溯；旧值仅此三行）】
--   jd01 → 9777866c836b4a719040c402781bbd9e  (S2 第二阶段审核)
--   jd02 → fc7e10becf4c4b32ae454f1eeccf2eef  (S1 第一阶段审核)
--     03 → 654c6c4014b64593887bf7c9bce8d6a6  (RC 再认证审核)
--   依据：初审的主体现场环节 = 第二阶段审核；预审（文件预审）≈ 第一阶段审核；复审 = 再认证审核
-- 【影响】cert_cert_stage 17 → 14 行；引用表 22 张、约 1460 行 GUID 换值 + cert_org_stage 5 行 slug→GUID
-- 幂等：重复执行 UPDATE/DELETE 命中 0 行
-- =====================================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- §1 GUID 引用迁移（旧阶段 Code → 新标准阶段 Code） ----------
-- cert_doc_extraction_rule.StageCode
UPDATE `cert_doc_extraction_rule` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_doc_extraction_rule` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_doc_extraction_rule` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_doc_fill_log.StageCode
UPDATE `cert_doc_fill_log` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_doc_fill_log` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_doc_fill_log` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_doc_fill_value.StageCode
UPDATE `cert_doc_fill_value` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_doc_fill_value` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_doc_fill_value` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_doc_template.StageCode
UPDATE `cert_doc_template` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_doc_template` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_doc_template` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_enterprise_doc_profile.StageCode
UPDATE `cert_enterprise_doc_profile` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_enterprise_doc_profile` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_enterprise_doc_profile` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_enterprise_file_op_log.StageCode
UPDATE `cert_enterprise_file_op_log` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_enterprise_file_op_log` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_enterprise_file_op_log` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_enterprise_original_file.StageCode
UPDATE `cert_enterprise_original_file` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_enterprise_original_file` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_enterprise_original_file` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_enterprise_original_file_version.StageCode
UPDATE `cert_enterprise_original_file_version` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_enterprise_original_file_version` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_enterprise_original_file_version` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_enterprise_original_upload_task.StageCode
UPDATE `cert_enterprise_original_upload_task` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_enterprise_original_upload_task` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_enterprise_original_upload_task` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_enterprise_stage.StageCode
UPDATE `cert_enterprise_stage` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_enterprise_stage` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_enterprise_stage` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_expert_nc_item.StageCode
UPDATE `cert_expert_nc_item` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_expert_nc_item` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_expert_nc_item` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_expert_nc_result.StageCode
UPDATE `cert_expert_nc_result` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_expert_nc_result` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_expert_nc_result` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_expert_task.StageCode
UPDATE `cert_expert_task` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_expert_task` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_expert_task` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_expert_task_data_gap.StageCode
UPDATE `cert_expert_task_data_gap` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_expert_task_data_gap` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_expert_task_data_gap` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_extraction_result.StageCode
UPDATE `cert_extraction_result` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_extraction_result` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_extraction_result` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_report_section.PhaseCode
UPDATE `cert_report_section` SET `PhaseCode`='9777866c836b4a719040c402781bbd9e' WHERE `PhaseCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_report_section` SET `PhaseCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `PhaseCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_report_section` SET `PhaseCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `PhaseCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_standard_directory_config.StageCode
UPDATE `cert_standard_directory_config` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_standard_directory_config` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_standard_directory_config` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_standard_directory_file.StageCode
UPDATE `cert_standard_directory_file` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_standard_directory_file` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_standard_directory_file` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_standard_doc_contract.StageCode
UPDATE `cert_standard_doc_contract` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_standard_doc_contract` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_standard_doc_contract` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_table_extraction_result.StageCode
UPDATE `cert_table_extraction_result` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_table_extraction_result` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_table_extraction_result` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_validation_rule.PhaseCode
UPDATE `cert_validation_rule` SET `PhaseCode`='9777866c836b4a719040c402781bbd9e' WHERE `PhaseCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_validation_rule` SET `PhaseCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `PhaseCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_validation_rule` SET `PhaseCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `PhaseCode`='29c1bcc3a18942e1865b2497a0262504';

-- wf_execution_task.PhaseCode
UPDATE `wf_execution_task` SET `PhaseCode`='9777866c836b4a719040c402781bbd9e' WHERE `PhaseCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `wf_execution_task` SET `PhaseCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `PhaseCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `wf_execution_task` SET `PhaseCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `PhaseCode`='29c1bcc3a18942e1865b2497a0262504';

-- audit_task.PhaseCode
UPDATE `audit_task` SET `PhaseCode`='9777866c836b4a719040c402781bbd9e' WHERE `PhaseCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `audit_task` SET `PhaseCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `PhaseCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `audit_task` SET `PhaseCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `PhaseCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_conclusion_rule.PhaseCode
UPDATE `cert_conclusion_rule` SET `PhaseCode`='9777866c836b4a719040c402781bbd9e' WHERE `PhaseCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_conclusion_rule` SET `PhaseCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `PhaseCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_conclusion_rule` SET `PhaseCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `PhaseCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_doc_ai_suggestion.StageCode
UPDATE `cert_doc_ai_suggestion` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_doc_ai_suggestion` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_doc_ai_suggestion` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_doc_normalize_action.StageCode
UPDATE `cert_doc_normalize_action` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_doc_normalize_action` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_doc_normalize_action` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_expert_report_result.StageCode
UPDATE `cert_expert_report_result` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_expert_report_result` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_expert_report_result` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_expert_report_section_item.StageCode
UPDATE `cert_expert_report_section_item` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_expert_report_section_item` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_expert_report_section_item` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_extraction_change_log.StageCode
UPDATE `cert_extraction_change_log` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_extraction_change_log` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_extraction_change_log` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_fill_param_def.StageCode
UPDATE `cert_fill_param_def` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_fill_param_def` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_fill_param_def` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- cert_fill_param_value.StageCode
UPDATE `cert_fill_param_value` SET `StageCode`='9777866c836b4a719040c402781bbd9e' WHERE `StageCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `cert_fill_param_value` SET `StageCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `StageCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `cert_fill_param_value` SET `StageCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `StageCode`='29c1bcc3a18942e1865b2497a0262504';

-- rpt_report_task.PhaseCode
UPDATE `rpt_report_task` SET `PhaseCode`='9777866c836b4a719040c402781bbd9e' WHERE `PhaseCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `rpt_report_task` SET `PhaseCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `PhaseCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `rpt_report_task` SET `PhaseCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `PhaseCode`='29c1bcc3a18942e1865b2497a0262504';

-- z_deprecated_cert_report_template.PhaseCode
UPDATE `z_deprecated_cert_report_template` SET `PhaseCode`='9777866c836b4a719040c402781bbd9e' WHERE `PhaseCode`='c42582d5f95f4318a1a5548eee7ed463';
UPDATE `z_deprecated_cert_report_template` SET `PhaseCode`='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE `PhaseCode`='fa7931c884d04637bb2e356a94dae450';
UPDATE `z_deprecated_cert_report_template` SET `PhaseCode`='654c6c4014b64593887bf7c9bce8d6a6' WHERE `PhaseCode`='29c1bcc3a18942e1865b2497a0262504';

-- ---------- §2 cert_org_stage：业务码 → Code（Q2 统一口径） ----------
UPDATE cert_org_stage SET StageCode='9777866c836b4a719040c402781bbd9e' WHERE StageCode='jd01';
UPDATE cert_org_stage SET StageCode='fc7e10becf4c4b32ae454f1eeccf2eef' WHERE StageCode='jd02';
UPDATE cert_org_stage SET StageCode='654c6c4014b64593887bf7c9bce8d6a6' WHERE StageCode='03';

-- ---------- §3 删除测试阶段行（引用已在 §1/§2 迁移完毕；FK: cert_validation_rule / cert_report_section → Code） ----------
DELETE FROM cert_cert_stage WHERE StageCode IN ('jd01','jd02','03');

-- ---------- §4 列注释纠正（列名保留 StageCode，值 = cert_cert_stage.Code） ----------
ALTER TABLE cert_org_stage MODIFY COLUMN StageCode varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段 Code → cert_cert_stage.Code（GUID，2026-10-08 Q2 统一，⛔ 非业务码 jd01/03）';
ALTER TABLE cert_enterprise_stage MODIFY COLUMN StageCode varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '★阶段 Code → cert_cert_stage.Code（GUID，⛔ 不是业务码 StageCode slug）';

-- ---------- §5 验证 SQL ----------
-- ① 阶段主表应为 14 行、且不含 jd01/jd02/03：
-- SELECT StageCode,StageName,Category FROM cert_cert_stage WHERE IsDeleted=0 ORDER BY Category,SortOrder;
-- ② 全库不得再有旧 GUID / 业务码（逐表跑 §1/§2 的 WHERE，期望全 0）：
-- SELECT COUNT(*) FROM cert_enterprise_stage WHERE StageCode IN ('c42582d5f95f4318a1a5548eee7ed463','fa7931c884d04637bb2e356a94dae450','29c1bcc3a18942e1865b2497a0262504');
-- SELECT COUNT(*) FROM cert_org_stage WHERE StageCode IN ('jd01','jd02','03');
-- ③ 关联分布：
-- SELECT StageCode,COUNT(*) FROM cert_org_stage GROUP BY StageCode;
-- SELECT StageCode,COUNT(*) FROM cert_enterprise_stage GROUP BY StageCode;
