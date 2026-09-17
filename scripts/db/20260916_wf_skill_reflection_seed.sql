-- ============================================================
-- 20260916_wf_skill_reflection_seed.sql
-- 工作流引擎迁移（旧 vol.api → 新 certplatform-api）— Skill 反射种子数据
--
-- 说明：
--   * CertSkillRegistry 按 skill_code 查询 wf_skill_reflection（enable=1 AND IsDeleted=0）
--     反射执行 class_path.method_name；查不到时回退 DI 容器中的 ISkillNode
--   * llm_extract 为 DI 注册式（ISkillNode），无需反射登记
--   * 参数绑定：SkillExecutor 按方法参数名从节点 Inputs 直接取值，无需 param_binding
--
-- 幂等性：
--   * wf_skill            → INSERT ... ON DUPLICATE KEY UPDATE（依赖 uk_skill_code）
--   * wf_skill_reflection → UPDATE 存量 + INSERT ... ON DUPLICATE KEY UPDATE（依赖 uk_skill_reflection）
--
-- 真实表结构对照（2026-09-17 SHOW CREATE TABLE 校准）：
--   wf_skill            唯一键 uk_code(`Code`) + uk_skill_code(`skill_code`)，启用列为 is_active
--   wf_skill_reflection 唯一键 uk_skill_reflection(`skill_code`) + uk_class_method(`class_path`,`method_name`)
-- ============================================================

USE yzh_cert_platform;

-- ------------------------------------------------------------
-- 1. wf_skill 主表补录缺省的 3 个 Skill（compare/assemble 已由前端创建，跳过）
--    注意列名：Code 与 skill_code 双唯一键需同时给值；启用列为 is_active
-- ------------------------------------------------------------
INSERT INTO `wf_skill`
  (`Code`, `skill_code`, `name`, `description`, `skill_type`, `is_active`, `sort_order`, `is_valid`, `create_date`, `status`)
VALUES
  ('get_field',   'get_field',   '获取字段值', '按字段编码和企业编码查询已提取的文档字段值（ent_extraction_result）', 'method', 1, 30, 1, NOW(), 'active'),
  ('get_table',   'get_table',   '获取表格数据', '按表格编码和企业编码查询已提取的表格数据（ent_table_extraction_result）', 'method', 1, 40, 1, NOW(), 'active'),
  ('llm_extract', 'llm_extract', 'LLM提取', '渲染提示词 → 调 LLM → 解析结构化 JSON 输出（DI 注册式，无需反射登记）', 'method', 1, 50, 1, NOW(), 'active')
ON DUPLICATE KEY UPDATE
  `name`        = VALUES(`name`),
  `description` = VALUES(`description`),
  `is_active`   = 1,
  `is_valid`    = 1,
  `modify_date` = NOW();

-- ------------------------------------------------------------
-- 2. wf_skill_reflection：
--    a) 存量 compare/assemble 仍指向旧程序集 YZH.Core.Skills.*（新后端不含该程序集，
--       反射必然失败）→ 统一 UPDATE 为新架构类型全名
--    b) get_field / get_table 为新增登记
-- ------------------------------------------------------------
UPDATE `wf_skill_reflection`
SET `class_path`  = 'CertPlatform.Admin.Services.Workflow.Skills.CompareSkill',
    `method_name` = 'ExecuteAsync',
    `enable`      = 1,
    `status`      = 'active',
    `modify_date` = NOW()
WHERE `skill_code` = 'compare' AND `IsDeleted` = 0;

UPDATE `wf_skill_reflection`
SET `class_path`  = 'CertPlatform.Admin.Services.Workflow.Skills.AssembleSkill',
    `method_name` = 'ExecuteAsync',
    `enable`      = 1,
    `status`      = 'active',
    `modify_date` = NOW()
WHERE `skill_code` = 'assemble' AND `IsDeleted` = 0;

INSERT INTO `wf_skill_reflection`
  (`code`, `skill_code`, `class_path`, `method_name`, `param_binding`, `enable`, `create_date`, `status`, `IsDeleted`)
VALUES
  (REPLACE(UUID(), '-', ''), 'get_field', 'CertPlatform.Admin.Services.Workflow.Skills.GetFieldSkill', 'ExecuteAsync', NULL, 1, NOW(), 'active', 0),
  (REPLACE(UUID(), '-', ''), 'get_table', 'CertPlatform.Admin.Services.Workflow.Skills.GetTableSkill', 'ExecuteAsync', NULL, 1, NOW(), 'active', 0)
ON DUPLICATE KEY UPDATE
  `class_path`  = VALUES(`class_path`),
  `method_name` = VALUES(`method_name`),
  `enable`      = 1,
  `status`      = 'active',
  `modify_date` = NOW();

-- ------------------------------------------------------------
-- 3. 校验查询（执行后人工确认）
-- ------------------------------------------------------------
-- SELECT s.skill_code, s.name, s.is_active, r.class_path, r.method_name, r.enable
-- FROM wf_skill s
-- LEFT JOIN wf_skill_reflection r ON r.skill_code = s.skill_code AND r.IsDeleted = 0
-- WHERE s.skill_code IN ('compare','assemble','get_field','get_table','llm_extract')
-- ORDER BY s.skill_code;
--
-- 预期：5 行；compare/assemble/get_field/get_table 的 class_path 均为
--       CertPlatform.Admin.Services.Workflow.Skills.*，enable=1；
--       llm_extract 的 r.* 为 NULL（DI 回退通道）。
