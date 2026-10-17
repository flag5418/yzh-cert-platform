-- ============================================================================
-- 20261016_del_nc_conclusion_calc_v1_V1.sql
-- 目的：删除 v1 工作流结论技能 `nc_conclusion_calc`（已被 `nc_conclusion_v2` 取代）。
--   ① wf_skill            Code = 'nc_conclusion_calc'      → 软删（IsDeleted=1 / IsValid=0）
--   ② wf_skill_reflection SkillCode = 'nc_conclusion_calc'  → 软删（IsDeleted=1 / IsValid=0）
--   （代码侧同步删除：NcConclusionCalcSkill.cs / NcConclusionCalculator.cs /
--     NcConclusionCalculatorTests.cs；NcConclusionCalculator.cs 中 v2 仍需的
--     DimensionResult / DimensionConformity / DimensionResultParser 已迁至
--     NcDimensionContracts.cs 保留。）
-- 说明：
--   · 幂等 —— 重复执行 0 行变更。
--   · 先备份到 `_bak_`（前缀 `_bak_` 已被 verify_createby.sql 排除）。
--   · 审计人字段 DeleteBy 用脚本标记 'manual_fix_20261016'（白名单前缀 `manual_fix`，见
--     scripts/db/verify/verify_createby.sql）。
--   · ⛔ 铁律九：本脚本绝不新增/使用 Enable 列，启用字段唯一 source = IsValid。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── ① 备份（幂等：已存在则重建结构+数据快照）────────────────────────────────
DROP TABLE IF EXISTS yzh_cert_platform._bak_nc_conclusion_calc_20261016;
CREATE TABLE yzh_cert_platform._bak_nc_conclusion_calc_20261016 AS
SELECT * FROM yzh_cert_platform.wf_skill WHERE Code = 'nc_conclusion_calc';

DROP TABLE IF EXISTS yzh_cert_platform._bak_nc_conclusion_calc_refl_20261016;
CREATE TABLE yzh_cert_platform._bak_nc_conclusion_calc_refl_20261016 AS
SELECT * FROM yzh_cert_platform.wf_skill_reflection WHERE SkillCode = 'nc_conclusion_calc';

-- ── ② 软删（幂等：条件带 IsDeleted=0，重跑影响 0 行）────────────────────────
UPDATE yzh_cert_platform.wf_skill
SET IsDeleted  = 1,
    IsValid    = 0,
    IsActive   = 0,
    DeleteTime = NOW(),
    DeleteBy   = 'manual_fix_20261016'
WHERE Code = 'nc_conclusion_calc' AND IsDeleted = 0;

UPDATE yzh_cert_platform.wf_skill_reflection
SET IsDeleted  = 1,
    IsValid    = 0,
    DeleteTime = NOW(),
    DeleteBy   = 'manual_fix_20261016'
WHERE SkillCode = 'nc_conclusion_calc' AND IsDeleted = 0;

-- ── ③ 验证（期望：①② = 0，③ ≥ 1）──────────────────────────────────────────
SELECT '① wf_skill 有效残留(期望0)'          AS item, COUNT(*) AS cnt
  FROM yzh_cert_platform.wf_skill WHERE Code = 'nc_conclusion_calc' AND IsDeleted = 0
UNION ALL
SELECT '② reflection 有效残留(期望0)',        COUNT(*)
  FROM yzh_cert_platform.wf_skill_reflection WHERE SkillCode = 'nc_conclusion_calc' AND IsDeleted = 0
UNION ALL
SELECT '③ nc_conclusion_v2 仍有效(期望>=1)', COUNT(*)
  FROM yzh_cert_platform.wf_skill WHERE Code = 'nc_conclusion_v2' AND IsDeleted = 0;
