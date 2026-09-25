-- ============================================================================
-- verify_phase_migration.sql — 认证阶段定义迁移 · 验证查询（★ 只读）
-- ============================================================================
-- 由 run_phase_migration.sh 第 3 步调用。
-- 数据来源：视图 `v_cert_phase_definition`（列名为视图自身定义，非表列）。
-- ============================================================================

SELECT phase_code,
       phase_name,
       sequence_order,
       is_valid
  FROM v_cert_phase_definition
 ORDER BY sequence_order;
