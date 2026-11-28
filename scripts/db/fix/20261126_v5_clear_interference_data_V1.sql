-- ================================================================
-- V5 干扰数据清理（2026-11-26）
-- 目的：清除 V3 时代残留的「文档提取 / 条款属性提取 / 证据链路产物」，
--       为 V5「指纹 + 特征匹配」链路从零重建让路。
-- 依据：docs/80-v4/24-23号设计可行性与风险评估-V1.md
-- 备份：scripts/db/backup/yzh_cert_platform_full_20261126_113131.sql.gz（全量 136 表）
-- 保留（源数据，⛔ 不清）：
--   cert_iso_clause（标准条款**存活行**）/ cert_audit_plan_detail（审核计划）/
--   cert_enterprise_doc + DocProfileJson（企业文档与画像）
-- 幂等：TRUNCATE 可重复执行。
--
-- ④ 追加（2026-11-26）：cert_iso_clause 的 526 条 IsDeleted=1 **死副本**——
--    11-12「条款重建」历史快照（重建脚本先软删旧行、再用同 Code 重插，
--    Code 为稳定码 SHA256("cert-clause-v1|…") 重建后不变 ⇒ 同名 Code 存活+软删并存）。
--    引擎全按 IsDeleted=0 过滤、业务零影响，但同码并存是"Code 唯一"隐患，故物理清除。
--    ⛔ 只删 IsDeleted=1，存活 320 条一律不动。
-- 执行：docker exec -i yzh-mysql mysql -uroot -pCHANGE_ME \
--        --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- ================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
SET FOREIGN_KEY_CHECKS = 0;

-- ① 文档提取信息（每份文档的 ContentJson / ModeOutput）
TRUNCATE TABLE cert_document_extraction;

-- ② 条款提取信息（EAV 长表：10 个 AttrCode × 240 条款）
TRUNCATE TABLE cert_clause_attr;

-- ③ 证据发现 / 证据生成产物（检查结果 / 分析 / 审核发现摘要）
TRUNCATE TABLE cert_check_result;
TRUNCATE TABLE cert_check_analysis;
TRUNCATE TABLE cert_finding_digest;

-- ④ 条款死副本（IsDeleted=1 的历史重建快照，⛔ 不动存活行）
DELETE FROM cert_iso_clause WHERE IsDeleted = 1;

SET FOREIGN_KEY_CHECKS = 1;

-- ── 验证（期望：前 5 行 = 0；cert_iso_clause_dead = 0）──
SELECT 'cert_document_extraction' AS tbl, COUNT(*) AS rows_left FROM cert_document_extraction
UNION ALL SELECT 'cert_clause_attr',       COUNT(*) FROM cert_clause_attr
UNION ALL SELECT 'cert_check_result',      COUNT(*) FROM cert_check_result
UNION ALL SELECT 'cert_check_analysis',    COUNT(*) FROM cert_check_analysis
UNION ALL SELECT 'cert_finding_digest',    COUNT(*) FROM cert_finding_digest
UNION ALL SELECT 'cert_iso_clause_dead',   COUNT(*) FROM cert_iso_clause WHERE IsDeleted = 1;
