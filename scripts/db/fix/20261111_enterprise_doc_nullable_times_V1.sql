-- ============================================================================
-- cert_enterprise_doc.ConvertDate / ExtractTime 改为可空（2026-11-11）
--
-- 现象：审核员端「企业原始资料管理」上传文件报
--       保存文件「01管理手册QMS.doc」失败：新增失败：必填字段为空
-- 根因：实体 EnterpriseDoc.ConvertDate/ExtractTime 均为 DateTime?，
--       但建表时被建成 NOT NULL 无默认值；上传登记阶段（字节还没转换）不写这两列
--       ⇒ MySQL 严格模式报 1364。它们表示「最近一次转换/提取时间」，转换前本就无值。
-- 影响：V2 新建的 cert_enterprise_doc（V1 表不动）。
-- 幂等：MODIFY 可重复执行。
-- ============================================================================
ALTER TABLE `cert_enterprise_doc`
    MODIFY COLUMN `ConvertDate` datetime NULL DEFAULT NULL COMMENT '最近一次转换完成时间（未转换=NULL）',
    MODIFY COLUMN `ExtractTime` datetime NULL DEFAULT NULL COMMENT '最近一次条款提取完成时间（未提取=NULL）';

-- 验证：期望两行都返回 YES
SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_enterprise_doc'
  AND COLUMN_NAME IN ('ConvertDate', 'ExtractTime');
