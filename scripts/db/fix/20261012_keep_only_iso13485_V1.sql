-- ============================================================================
-- 2026-10-12 · 标准收敛：只保留 ISO 13485，其余标准数据彻底删除
--
-- 用户指令（2026-10-12 原话）：
--   「现在我们所有的标准以13485为准，这里导入了正式的nc检查规则和报告，
--     其他的都可以彻底删除」
--
-- 保留基准（KEEP）：
--   StandardCode = 'iso13485'
--   cert_iso_standard.Code = 'dfca5483-f83c-4999-b773-3624de5f7cf7'
--   （「ISO 13485 医疗器械质量管理体系」，OrgCode = yzh-std-env）
--
-- 已导入（★ 保留，⛔ 不删）：
--   · NC 检查规则 cert_validation_rule      : 8 行（StandardCode=iso13485, PhaseCode=S2）
--   · 报告章节   cert_report_section        : 8 行（StandardCode=iso13485, PhaseCode=S2）
--
-- 删除范围（A 标准主数据 / B 企业级派生 / C 专家任务族），共约 1185 行 / 27 张表。
--
-- 备份：scripts/db/backup/backup_20261012_full_pre_13485_only.sql.gz（全库，2.6 MB）
-- 幂等：可重复执行（DELETE 带精确条件；二次执行影响 0 行）
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- A. 标准主数据（非 iso13485）
-- ────────────────────────────────────────────────────────────────────────────
-- A.1 目录子项（叶子）—— 668 文件 + 11 文件夹
DELETE FROM cert_standard_directory_file
 WHERE ConfigCode IN (SELECT Code FROM cert_standard_directory_config
                       WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7');
DELETE FROM cert_standard_directory_folder
 WHERE ConfigCode IN (SELECT Code FROM cert_standard_directory_config
                       WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7');
-- A.2 目录配置主表
DELETE FROM cert_standard_directory_config
 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
-- A.3 标准 × 机构
DELETE FROM cert_org_standard
 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
-- A.4 规则族（章节提取 / 文档提取 / 标准文档契约 / 校验 / 报告 / 提示词）
DELETE FROM cert_doc_extraction_rule
 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_standard_doc_contract
 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_validation_rule
 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_report_section
 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM wf_prompt_template
 WHERE StandardCode IS NOT NULL
   AND StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
-- A.5 标准文档模板
DELETE FROM cert_doc_template
 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
-- A.6 章节（叶子）→ 标准（主）
DELETE FROM cert_iso_clause
 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_iso_standard
 WHERE Code <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';

-- ────────────────────────────────────────────────────────────────────────────
-- B. 企业级派生数据（非 iso13485；含 1 个悬空 StandardCode 475da4fe…）
-- ────────────────────────────────────────────────────────────────────────────
DELETE FROM cert_table_extraction_result WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_extraction_result       WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_enterprise_doc_profile  WHERE StandardCode IS NULL
                                            OR StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_doc_fill_value          WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_doc_fill_log            WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_doc_normalize_action    WHERE StandardCode IS NOT NULL
                                            AND StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_enterprise_stage        WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';

-- ────────────────────────────────────────────────────────────────────────────
-- C. 专家任务族（3 个测试任务：S4冒烟/浏览器验证-复审NC）
--    叶子 → 主表顺序
-- ────────────────────────────────────────────────────────────────────────────
DELETE FROM cert_expert_task_log          WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_expert_task_queue_item
 WHERE QueueCode IN (SELECT Code FROM cert_expert_task_queue
                      WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7');
DELETE FROM cert_expert_nc_result         WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_expert_nc_item           WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_expert_task_data_gap     WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_expert_task_queue        WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';
DELETE FROM cert_expert_task
 WHERE Code IN (SELECT TaskCode FROM cert_expert_task_standard
                 WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7');
DELETE FROM cert_expert_task_standard     WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7';

-- ────────────────────────────────────────────────────────────────────────────
-- D. 体检（执行后自证 —— 取消注释单独跑，第 1 组必须全部返回 0）
-- ────────────────────────────────────────────────────────────────────────────
-- SELECT 'cert_iso_standard' t, COUNT(*) c FROM cert_iso_standard WHERE Code <> 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT 'cert_iso_clause', COUNT(*) FROM cert_iso_clause WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT 'cert_standard_directory_config', COUNT(*) FROM cert_standard_directory_config WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT 'cert_standard_directory_file', COUNT(*) FROM cert_standard_directory_file WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT 'cert_validation_rule', COUNT(*) FROM cert_validation_rule WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT 'cert_report_section', COUNT(*) FROM cert_report_section WHERE StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT 'cert_enterprise_doc_profile', COUNT(*) FROM cert_enterprise_doc_profile WHERE StandardCode IS NULL OR StandardCode <> 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT 'cert_expert_task', COUNT(*) FROM cert_expert_task;
--
-- -- 第 2 组：保留量（iso13485）
-- SELECT '标准' t, COUNT(*) c FROM cert_iso_standard WHERE Code = 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT '章节', COUNT(*) FROM cert_iso_clause WHERE StandardCode = 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT '目录配置', COUNT(*) FROM cert_standard_directory_config WHERE StandardCode = 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT '目录文件', COUNT(*) FROM cert_standard_directory_file WHERE StandardCode = 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT 'NC检查规则', COUNT(*) FROM cert_validation_rule WHERE StandardCode = 'dfca5483-f83c-4999-b773-3624de5f7cf7'
-- UNION ALL SELECT '报告章节', COUNT(*) FROM cert_report_section WHERE StandardCode = 'dfca5483-f83c-4999-b773-3624de5f7cf7';

-- ============================================================================
-- 反向（回滚）—— 数据已物理删除，需从全库备份恢复：
--   gunzip -c scripts/db/backup/backup_20261012_full_pre_13485_only.sql.gz \
--     | docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform
-- ============================================================================
