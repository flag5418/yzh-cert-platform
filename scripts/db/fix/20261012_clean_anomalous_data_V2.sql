-- =====================================================================================
--  2026-10-12 数据清理（第二轮）—— 「与逻辑异常」判据
-- =====================================================================================
--  背景：
--    用户裁定「所有的标准以 ISO 13485 为准」（第一轮已执行 20261012_keep_only_iso13485_V1.sql）。
--    本轮处置第一轮遗留的 ⛔ 未动表，判据**不是**「是不是通用表」，而是用户的原话：
--      「如果发现执行的时候，这些数据和我们的逻辑有异常，可以彻底删除」
--    ⇒ 只删【与程序逻辑冲突 / 指向已不存在实体 / 测试残留】的行；
--      【正确的全局主数据】一律保留（删了 13485 自己也跑不起来）。
--
--  ★ 保留（**不是** 异常，是 13485 自己依赖的基础设施）：
--      · cert_cert_stage  14 条真实阶段（AP/CAN/CD/CE/CR/RC/REV/S1/S2/SA/SP/SUS/SV/TT）
--        —— 实证被 EnterpriseStageController / FillParamValueController / DocumentFillController /
--           EnterpriseNormalizeController / ExpertTaskService 读取；删了阶段选择器全空。
--      · cert_org_stage   8 条（绑定活认证机构 906e8b2a… 尚龙认证）
--      · cert_fill_param_def 8 条活参数（企业资料参数字典，seed_fill_param）
--      · cert_doc_fill_prompt 2 条（正文待补，属「待补清单」不是异常）
--      · cert_conclusion_rule / cert_nc_judge_prompt / cert_nc_conclude_prompt 各 1 条（后台标准级，已迁 yzh-std-env）
--
--  ⛔ 删除（**是** 异常）：
--      A. 3 个测试企业 + 全部派生行（含 105 个原始文件登记行）
--      B. 2 个测试企业的机构节点
--      C. 7 个指向已软删机构的孤儿用户
--      D. 38 行已软删机构垃圾
--      E. 1 行已软删测试阶段（__T__测试阶段改）
--      F. 9 行已软删镜像参数（有意软删，物理清除）
--
--  执行前必备份：scripts/db/backup/backup_20261012_full_pre_anomaly_clean.sql.gz
--  幂等：重复执行结果一致（DELETE 命中 0 行不报错）
-- =====================================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------------------------------------------------------------------------
-- A. 测试企业派生行（先叶子后主行）
--    测试企业 Code：TEST-ENT-0001 / 42833c3d…（G4测试企业甲，持有 105 文件）/ e1ac353d…（测试企业b）
-- ---------------------------------------------------------------------------
DELETE FROM cert_enterprise_file_op_log              WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_enterprise_file_version             WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_enterprise_original_file_version   WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_enterprise_original_upload_task     WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_enterprise_original_file            WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_enterprise_stage                    WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_fill_param_value                    WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_doc_fill_value                      WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_doc_fill_log                        WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_doc_ai_suggestion                   WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_doc_normalize_action                WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_enterprise_doc_profile              WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_extraction_change_log               WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_expert_task_data_gap                WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_expert_report_section_item          WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_expert_report_result                WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_expert_nc_result                    WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_expert_nc_item                      WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_expert_task                         WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_application                         WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_standard_directory_config           WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM cert_standard_directory_file             WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');
DELETE FROM wf_execution_task                        WHERE EnterpriseCode IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');

-- A2. 测试企业主行
DELETE FROM cert_enterprise WHERE Code IN ('TEST-ENT-0001','42833c3d3fe241b1bcfa707f6b92e4c3','e1ac353d133f4cffbf8b01f43bf0fe7e');

-- ---------------------------------------------------------------------------
-- B. 测试企业的机构节点（Enterprise 类型，OrgCode 指向上面被删的 cert_enterprise.Code）
--    da4623f4… = G4测试企业甲   cd35dcf2… = 测试企业b
-- ---------------------------------------------------------------------------
DELETE FROM sys_organization
 WHERE Code IN ('da4623f4746146f8bc4dffb44648e2c5','cd35dcf228df43fd9ea8d83d2be0faac');

-- ---------------------------------------------------------------------------
-- C. 孤儿用户：OrgCode 指向已软删机构（全表扫描实测：仅 sys_user 命中，共 7 行）
--    admin 用户 USER_000001 的 OrgCode 为 NULL ⇒ **不在**删除范围
-- ---------------------------------------------------------------------------
DELETE FROM sys_user
 WHERE OrgCode IN (SELECT Code FROM (SELECT Code FROM sys_organization WHERE IsDeleted = 1) x);

-- ---------------------------------------------------------------------------
-- D. 已软删机构垃圾（实测 40 行；全表扫描确认除 sys_user 外零引用）
-- ---------------------------------------------------------------------------
DELETE FROM sys_organization WHERE IsDeleted = 1;

-- ---------------------------------------------------------------------------
-- E. 已软删测试阶段（__T__测试阶段改）
-- ---------------------------------------------------------------------------
DELETE FROM cert_cert_stage WHERE IsDeleted = 1;

-- ---------------------------------------------------------------------------
-- F. 已软删镜像参数（9 条「档案镜像参数」，2026-10-09 有意软删 ⇒ 物理清除）
-- ---------------------------------------------------------------------------
DELETE FROM cert_fill_param_def WHERE IsDeleted = 1;

-- =====================================================================================
--  自证（期望值）
-- =====================================================================================
-- SELECT 'cert_enterprise'              t, COUNT(*) FROM cert_enterprise                -- 0   （实测 ✅）
-- UNION ALL SELECT 'sys_organization 活',      COUNT(*) FROM sys_organization WHERE IsDeleted=0  -- 10  （实测 ✅）
-- UNION ALL SELECT 'sys_organization 软删',    COUNT(*) FROM sys_organization WHERE IsDeleted=1  -- 0   （实测 ✅）
-- UNION ALL SELECT 'cert_cert_stage 活',       COUNT(*) FROM cert_cert_stage   WHERE IsDeleted=0 -- 14  （实测 ✅）
-- UNION ALL SELECT 'cert_fill_param_def 活',   COUNT(*) FROM cert_fill_param_def WHERE IsDeleted=0 -- 8   （实测 ✅）
-- UNION ALL SELECT 'cert_fill_param_def 软删', COUNT(*) FROM cert_fill_param_def WHERE IsDeleted=1 -- 0   （实测 ✅）
-- UNION ALL SELECT 'sys_user',                 COUNT(*) FROM sys_user                            -- 17  （实测 ✅）
-- UNION ALL SELECT 'cert_enterprise_original_file', COUNT(*) FROM cert_enterprise_original_file -- 0   （实测 ✅）
-- UNION ALL SELECT '孤儿用户',                 COUNT(*) FROM sys_user u LEFT JOIN sys_organization o ON o.Code=u.OrgCode WHERE o.Code IS NULL;  -- 11（NULL OrgCode 的平台/测试账号，见报告「未动」）
--
-- =====================================================================================
--  回滚
-- =====================================================================================
--   恢复整库：gunzip -c scripts/db/backup/backup_20261012_full_pre_anomaly_clean.sql.gz | \
--             docker exec -i yzh-mysql mysql -uroot -p*** yzh_cert_platform
-- =====================================================================================
