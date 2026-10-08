-- =====================================================================================
-- 20261008_drop_cert_phase_definition_V1.sql —— Q4：删除孤儿表 cert_phase_definition
-- 【裁决 2026-10-08】cert_phase_definition 为重复实现的孤儿（0 行数据、无任何外键指向、
--   无路由/页面，页面已 2026-10-24 下线）⇒ 表 + 视图一并删除，阶段唯一实体 = cert_cert_stage。
-- 【同批已删除代码】PhaseDefinitionController.cs / Entities/Cert/PhaseDefinition.cs /
--   Assets/EntityConfigs/Foundation/PhaseDefinition.json / cert-share types 的 PhaseDefinition 接口。
-- 【保留说明】cert_standard_phase_config（0 行，无引用）不动；其 PhaseCode 语义改为指向
--   cert_cert_stage.Code（实体注释另行标注）。
-- 幂等：IF EXISTS。
-- =====================================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

DROP VIEW IF EXISTS v_cert_phase_definition;
DROP TABLE IF EXISTS cert_phase_definition;

-- ---------- 验证 SQL ----------
-- SELECT TABLE_NAME FROM information_schema.TABLES
--  WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME IN ('cert_phase_definition','v_cert_phase_definition');
--   期望：0 行（表/视图均已删除）
