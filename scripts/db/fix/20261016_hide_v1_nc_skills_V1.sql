-- =====================================================================
-- 20261016_hide_v1_nc_skills_V1.sql
-- 用途：隐藏 v1 NC 三件套中剩余的两个（**仅隐藏，不删代码**）
-- 依据：docs/20-体系认证/03-详细设计/01-系统管理/工作流管理/04-设计器实施与验证/
--       NC结论判定V2-judge链路简化与运行上下文自动注入-改造设计-V1.md §7.4
-- 裁决：用户 2026-10-16「仅隐藏，不删代码」
-- =====================================================================
-- 背景：
--   v1 三件套 = nc_conclusion_calc / nc_validate / build_nc_prompt。
--   nc_conclusion_calc 已于 2026-10-16 软删（scripts/db/fix/20261016_del_nc_conclusion_calc_v1_V1.sql）。
--   本次把剩余两个 **仅隐藏**：wf_skill.IsValid = 0
--     ⇒ 画布节点库（POST /api/Admin/Workflow/WfSkill/filter）自动过滤 IsValid=1 ⇒ 不再出现；
--     ⇒ 技能管理列表同样不再列出（SkillTreeTableController 亦按 IsValid=1）。
--   ⛔ 不删 C# 类、不动 wf_skill_reflection（执行期反射登记保留，回退容易）；
--   ⛔ 不设 IsDeleted（与「删除」区分，符合铁律九：启停唯一字段 = IsValid）。
-- 回退：UPDATE wf_skill SET IsValid = 1 WHERE Code IN ('nc_validate','build_nc_prompt');
-- 幂等：重复执行结果一致。
-- =====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

UPDATE `wf_skill`
SET `IsValid`    = 0,
    `UpdateTime` = NOW(),
    `UpdateBy`   = 'manual_fix_20261016'
WHERE `Code` IN ('nc_validate', 'build_nc_prompt')
  AND `IsDeleted` = 0;

-- ── 校验：两项都必须不在「节点库可见集」内（期望 0）────────────────────
SELECT COUNT(*) AS `C_v1残留可见_期望0`
FROM `wf_skill`
WHERE `Code` IN ('nc_validate', 'build_nc_prompt')
  AND `IsValid` = 1 AND `IsDeleted` = 0;

-- ── 现状回显（供人工核对）──────────────────────────────────────────────
SELECT `Code`, `Name`, `IsValid`, `IsDeleted`, `IsUseForNcConfig`, `IsUseForReport`
FROM `wf_skill`
WHERE `Code` IN ('nc_conclusion_calc', 'nc_validate', 'build_nc_prompt', 'nc_conclusion_v2')
ORDER BY `Id`;
