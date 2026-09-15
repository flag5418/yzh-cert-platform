-- ============================================================================
-- 20260915_validation_rule_alter.sql
-- 修复：NC 规则（cert_validation_rule）新增报 400 —— PhaseCode 外键约束错误
--
-- 背景：
--   - 表单/树使用 cert_cert_stage.StageCode（AP/S1/S2/CD/CE/SV/RC...）作为阶段标识
--   - 实体注释也声明 PhaseCode 关联 cert_cert_stage.Code
--   - 但旧外键 fk_valrule_phase 指向 cert_phase_definition.Code（GUID）
--   - cert_phase_definition 仅覆盖 S1/S2/Surv1/Surv2/Recert，
--     AP/CR/SP/CD/CE/SV/RC 无对应记录 → 插入即违反外键（400 Bad Request）
--
-- 处理：
--   - 删除错误外键 fk_valrule_phase
--   - 保留 idx_phase_code 普通索引（查询过滤仍高效）
--
-- 前置检查：
--   SELECT COUNT(*) FROM cert_validation_rule;  -- 当前为 0，无数据迁移
-- ============================================================================

USE yzh_cert_platform;

ALTER TABLE cert_validation_rule
  DROP FOREIGN KEY fk_valrule_phase;
