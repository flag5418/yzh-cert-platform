-- ============================================================
-- 体系认证平台 - Phase 2: 数据字典初始化脚本
-- 说明: 创建认证平台专用的数据字典和字典项
-- 执行顺序: 在 Phase 1 脚本执行后运行本脚本
-- ============================================================

USE `yzh_cert_platform`;

-- ============================================================
-- Step 1: 创建认证平台字典根节点
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('认证平台字典', 'cert_dict', 1, 0, 200, NOW(), 1, '超级管理员');

SET @cert_dict_id = LAST_INSERT_ID();

SELECT CONCAT('✅ 认证平台字典根节点创建成功，ID=', @cert_dict_id) AS Status;

-- ============================================================
-- Step 2: 认证类型字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('认证类型', 'CertType', 1, @cert_dict_id, 10, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

-- 字典项
INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`) VALUES
('质量管理体系认证', 'QMS', @dict_id, 1, 1, NOW(), 1, '超级管理员'),
('环境管理体系认证', 'EMS', @dict_id, 1, 2, NOW(), 1, '超级管理员'),
('职业健康安全管理体系认证', 'OHSAS', @dict_id, 1, 3, NOW(), 1, '超级管理员'),
('信息安全管理体系认证', 'ISMS', @dict_id, 1, 4, NOW(), 1, '超级管理员');

SELECT '✅ 认证类型字典创建完成 (4 项)' AS Status;

-- ============================================================
-- Step 3: 审核阶段字典（5 个核心阶段）
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('审核阶段', 'audit_phase', 1, @cert_dict_id, 20, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`) VALUES
('申请受理', 'application_review', @dict_id, 1, 10, NOW(), 1, '超级管理员'),
('文件评审', 'document_review', @dict_id, 1, 20, NOW(), 1, '超级管理员'),
('一阶段审核', 'stage1_audit', @dict_id, 1, 30, NOW(), 1, '超级管理员'),
('二阶段审核', 'stage2_audit', @dict_id, 1, 40, NOW(), 1, '超级管理员'),
('认证决定', 'certification_decision', @dict_id, 1, 50, NOW(), 1, '超级管理员'),
('证书颁发', 'certificate_issuance', @dict_id, 1, 60, NOW(), 1, '超级管理员');

SELECT '✅ 审核阶段字典创建完成 (6 项)' AS Status;

-- ============================================================
-- Step 4: 证书状态字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('证书状态', 'cert_status', 1, @cert_dict_id, 30, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`) VALUES
('有效', 'valid', @dict_id, 1, 10, NOW(), 1, '超级管理员'),
('暂停', 'suspended', @dict_id, 1, 20, NOW(), 1, '超级管理员'),
('撤销', 'revoked', @dict_id, 1, 30, NOW(), 1, '超级管理员'),
('过期', 'expired', @dict_id, 1, 40, NOW(), 1, '超级管理员'),
('待颁发', 'pending_issuance', @dict_id, 1, 5, NOW(), 1, '超级管理员');

SELECT '✅ 证书状态字典创建完成 (5 项)' AS Status;

-- ============================================================
-- Step 5: 审核结论字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('审核结论', 'audit_conclusion', 1, @cert_dict_id, 40, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`) VALUES
('通过（推荐认证）', 'pass', @dict_id, 1, 10, NOW(), 1, '超级管理员'),
('通过（带不符合项）', 'pass_with_nc', @dict_id, 1, 20, NOW(), 1, '超级管理员'),
('不通过', 'fail', @dict_id, 1, 30, NOW(), 1, '超级管理员'),
('待改进后复审', 'improvement_required', @dict_id, 1, 40, NOW(), 1, '超级管理员'),
('取消审核', 'cancelled', @dict_id, 1, 50, NOW(), 1, '超级管理员');

SELECT '✅ 审核结论字典创建完成 (5 项)' AS Status;

-- ============================================================
-- Step 6: 不符合项严重程度字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('不符合项严重程度', 'nc_severity', 1, @cert_dict_id, 50, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `Remark`, `CreateDate`, `CreateID`, `Creator`) VALUES
('严重不符合', 'major', @dict_id, 1, 10, '直接影响QMS有效性或产品质量安全', NOW(), 1, '超级管理员'),
('一般不符合', 'minor', @dict_id, 1, 20, '属于孤立问题，不影响体系整体', NOW(), 1, '超级管理员'),
('轻微不符合', 'observation', @dict_id, 1, 30, '改进建议性质，可作为预防措施', NOW(), 1, '超级管理员'),
('观察项', 'suggestion', @dict_id, 1, 40, '非不符合，仅是最佳实践建议', NOW(), 1, '超级管理员');

SELECT '✅ 不符合项严重程度字典创建完成 (4 项)' AS Status;

-- ============================================================
-- Step 7: ISO 标准类型字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('标准类型', 'standard_type', 1, @cert_dict_id, 60, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `Remark`, `CreateDate`, `CreateID`, `Creator`) VALUES
('ISO 9001 质量管理体系', 'ISO9001', @dict_id, 1, 10, '适用于所有行业', NOW(), 1, '超级管理员'),
('ISO 13485 医疗器械质量管理体系', 'ISO13485', @dict_id, 1, 20, '医疗器械行业专用（案例使用）', NOW(), 1, '超级管理员'),
('ISO 14001 环境管理体系', 'ISO14001', @dict_id, 1, 30, '环境管理', NOW(), 1, '超级管理员'),
('ISO 27001 信息安全管理体系', 'ISO27001', @dict_id, 1, 40, '信息安全', NOW(), 1, '超级管理员'),
('ISO 45001 职业健康安全管理体系', 'ISO45001', @dict_id, 1, 50, '职业健康安全', NOW(), 1, '超级管理员'),
('IATF 16949 汽车行业质量管理', 'IATF16949', @dict_id, 1, 60, '汽车行业', NOW(), 1, '超级管理员');

SELECT '✅ 标准类型字典创建完成 (6 项)' AS Status;

-- ============================================================
-- Step 8: 申请状态字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('申请状态', 'application_status', 1, @cert_dict_id, 70, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`) VALUES
('草稿', 'draft', @dict_id, 1, 10, NOW(), 1, '超级管理员'),
('已提交', 'submitted', @dict_id, 1, 20, NOW(), 1, '超级管理员'),
('受理中', 'accepted', @dict_id, 1, 30, NOW(), 1, '超级管理员'),
('文件评审中', 'doc_reviewing', @dict_id, 1, 35, NOW(), 1, '超级管理员'),
('审核中', 'auditing', @dict_id, 1, 40, NOW(), 1, '超级管理员'),
('已完成（通过）', 'completed_pass', @dict_id, 1, 50, NOW(), 1, '超级管理员'),
('已完成（未通过）', 'completed_fail', @dict_id, 1, 55, NOW(), 1, '超级管理员'),
('已拒绝', 'rejected', @dict_id, 1, 60, NOW(), 1, '超级管理员'),
('已取消', 'cancelled', @dict_id, 1, 70, NOW(), 1, '超级管理员');

SELECT '✅ 申请状态字典创建完成 (9 项)' AS Status;

-- ============================================================
-- Step 9: 任务状态字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('任务状态', 'task_status', 1, @cert_dict_id, 80, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`) VALUES
('待分配', 'pending_assignment', @dict_id, 1, 10, NOW(), 1, '超级管理员'),
('待开始', 'pending_start', @dict_id, 1, 20, NOW(), 1, '超级管理员'),
('进行中', 'in_progress', @dict_id, 1, 30, NOW(), 1, '超级管理员'),
('已完成', 'completed', @dict_id, 1, 40, NOW(), 1, '超级管理员'),
('已暂停', 'paused', @dict_id, 1, 50, NOW(), 1, '超级管理员'),
('已取消', 'cancelled', @dict_id, 1, 60, NOW(), 1, '超级管理员');

SELECT '✅ 任务状态字典创建完成 (6 项)' AS Status;

-- ============================================================
-- Step 10: 机构状态字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('机构状态', 'org_status', 1, @cert_dict_id, 90, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`) VALUES
('正常运营', 'active', @dict_id, 1, 10, NOW(), 1, '超级管理员'),
('暂停业务', 'suspended', @dict_id, 1, 20, NOW(), 1, '超级管理员'),
('注销', 'cancelled', @dict_id, 1, 30, NOW(), 1, '超级管理员'),
('整改中', 'rectification', @dict_id, 1, 40, NOW(), 1, '超级管理员');

SELECT '✅ 机构状态字典创建完成 (4 项)' AS Status;

-- ============================================================
-- Step 11: 报告模板类型字典
-- ============================================================

INSERT INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('报告模板类型', 'report_template_type', 1, @cert_dict_id, 100, NOW(), 1, '超级管理员');

SET @dict_id = LAST_INSERT_ID();

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `Remark`, `CreateDate`, `CreateID`, `Creator`) VALUES
('审核报告（一阶段）', 'audit_report_stage1', @dict_id, 1, 10, '一阶段审核报告模板', NOW(), 1, '超级管理员'),
('审核报告（二阶段）', 'audit_report_stage2', @dict_id, 1, 20, '二阶段审核报告模板', NOW(), 1, '超级管理员'),
('审核报告（综合）', 'audit_report_combined', @dict_id, 1, 30, '综合审核报告模板', NOW(), 1, '超级管理员'),
('认证证书', 'certificate', @dict_id, 1, 40, '认证证书模板', NOW(), 1, '超级管理员'),
('不符合项报告', 'nc_report', @dict_id, 1, 50, '不符合项汇总报告', NOW(), 1, '超级管理员');

SELECT '✅ 报告模板类型字典创建完成 (5 项)' AS Status;

-- ============================================================
-- 总结
-- ============================================================

SELECT 
    COUNT(DISTINCT d.Dic_ID) AS total_dictionaries,
    COUNT(l.DicList_ID) AS total_items
FROM Sys_Dictionary d
LEFT JOIN Sys_DictionaryList l ON d.Dic_ID = l.Dic_ID
WHERE d.ParentId = @cert_dict_id;

SELECT '🎉 数据字典初始化完成！' AS Status;
SELECT '共创建 11 个字典分类，54 个字典项' AS summary;
