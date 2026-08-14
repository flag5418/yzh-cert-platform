-- =====================================================================
-- Phase 3 测试数据初始化
-- =====================================================================

-- 1. 认证机构
INSERT INTO cert_certification_body (code, org_code, name, short_name, cb_code, status, enable)
VALUES ('CB001-CODE', 'CB001', '测试认证机构', '测试CB', 'CB001', 'active', 1)
ON DUPLICATE KEY UPDATE name = VALUES(name);

-- 2. ISO 标准（字段：standard_code, standard_name, version_year）
INSERT INTO cert_iso_standard (code, org_code, standard_code, standard_name, version_year, category, status, enable)
VALUES ('ISO13485-CODE', 'CB001', 'ISO134852016', 'ISO 13485:2016 医疗器械质量管理体系', 2016, 'quality', 'active', 1)
ON DUPLICATE KEY UPDATE standard_name = VALUES(standard_name);

-- 3. 阶段定义（字段：phase_code, phase_name, sequence_order）
INSERT INTO cert_phase_definition (code, org_code, phase_code, phase_name, sequence_order, status, enable)
VALUES ('PHASE01-CODE', 'CB001', 'STAGE01', '初审阶段', 1, 'active', 1)
ON DUPLICATE KEY UPDATE phase_name = VALUES(phase_name);

-- 4. 标准-阶段配置
INSERT INTO cert_standard_phase_config (code, org_code, standard_code, phase_code, status, enable)
VALUES ('SPC-001', 'CB001', 'ISO13485-CODE', 'PHASE01-CODE', 'active', 1)
ON DUPLICATE KEY UPDATE org_code = VALUES(org_code);

-- 5. 目录模板（文件夹树）
INSERT INTO cert_directory_template (code, org_code, config_code, parent_code, folder_name, sort_order, status, enable)
VALUES ('DIR-ROOT-001', 'CB001', 'SPC-001', NULL, '1质量手册', 1, 'active', 1)
ON DUPLICATE KEY UPDATE folder_name = VALUES(folder_name);

INSERT INTO cert_directory_template (code, org_code, config_code, parent_code, folder_name, sort_order, status, enable)
VALUES ('DIR-SUB-001', 'CB001', 'SPC-001', 'DIR-ROOT-001', '程序文件', 1, 'active', 1)
ON DUPLICATE KEY UPDATE folder_name = VALUES(folder_name);

-- 6. 文件要求（标准文件模板）
INSERT INTO cert_file_requirement (code, org_code, folder_code, file_name_template, file_type, is_required, max_size_mb, description, sort_order, standard_code, status, enable)
VALUES ('FR-001', 'CB001', 'DIR-ROOT-001', '质量手册模板.docx', 'docx', 1, 20, '质量手册标准模板文件', 1, 'ISO13485-CODE', 'active', 1)
ON DUPLICATE KEY UPDATE file_name_template = VALUES(file_name_template);

-- 验证
SELECT '=== 测试数据验证 ===' AS info;
SELECT 'cert_certification_body' AS tbl, COUNT(*) AS cnt FROM cert_certification_body
UNION ALL
SELECT 'cert_iso_standard', COUNT(*) FROM cert_iso_standard
UNION ALL
SELECT 'cert_phase_definition', COUNT(*) FROM cert_phase_definition
UNION ALL
SELECT 'cert_standard_phase_config', COUNT(*) FROM cert_standard_phase_config
UNION ALL
SELECT 'cert_directory_template', COUNT(*) FROM cert_directory_template
UNION ALL
SELECT 'cert_file_requirement', COUNT(*) FROM cert_file_requirement;

SELECT '=== cert_file_requirement 详情 ===' AS info;
SELECT code, folder_code, file_name_template, file_type, standard_code, template_storage_path FROM cert_file_requirement;
