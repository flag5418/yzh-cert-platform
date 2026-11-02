-- ====================================================================
-- 脚本名称: 20261030_audit_prompt_templates_V1.sql
-- 创建认证检查闭环必备 3 套提示词模板到 wf_prompt_template
-- PromptCode = PromptType（唯一键，Version=1）
--
-- ⚠️ wf_prompt_template 表实际列：
--    Code / OrgCode(null) / PromptCode / PromptName / PromptType /
--    SkillTarget / StandardCode / Template / ModelName / MaxTokens /
--    Temperature / Description / Version / IsActive / CreateBy /
--    UpdateBy / DeleteBy / IsValid / IsDeleted 等（不含 Status/CreateTime）
-- ★ 幂等（ON DUPLICATE KEY UPDATE）；Platform 级资源 OrgCode=NULL
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1. audit_check — 符合性分析
INSERT INTO `wf_prompt_template`
  (`Code`, `OrgCode`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`, `Template`, `ModelName`, `MaxTokens`, `Temperature`, `Description`, `Version`, `IsActive`, `IsValid`, `IsDeleted`, `CreateBy`)
VALUES
  (REPLACE(UUID(),'-',''), NULL, 'audit_check', '认证检查符合性分析', 'audit_check', 'CertCheckEngine', NULL,
   '作为认证审核专家，请根据以下条款要求与已挂接证据，对检查项进行符合性判定。\n\n条款信息：{{ClauseInfo}}\n\n检查项要求：{{CheckRequirement}}\n\n已挂接证据：{{EvidenceList}}\n\n请输出 JSON（不要 markdown）：\n{"conclusion":"conformant|minor_nc|major_nc|observation","summary":"判定依据摘要","recommendation":"整改建议","confidence":0-100}',
   'claude-sonnet-4-5-20250929', 4096, 0.30,
   '认证检查符合性分析主流程（V2 独立认证检查闭环）', 1, 1, 1, 0, 'seed_audit_check')
ON DUPLICATE KEY UPDATE
  `Version` = `Version` + 1,
  `Template` = VALUES(`Template`),
  `ModelName` = VALUES(`ModelName`),
  `UpdateTime` = CURRENT_TIMESTAMP;

-- 2. nc_suggest — 不符合项描述与处置建议
INSERT INTO `wf_prompt_template`
  (`Code`, `OrgCode`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`, `Template`, `ModelName`, `MaxTokens`, `Temperature`, `Description`, `Version`, `IsActive`, `IsValid`, `IsDeleted`, `CreateBy`)
VALUES
  (REPLACE(UUID(),'-',''), NULL, 'nc_suggest', 'NC 不符合项描述生成', 'nc_suggest', 'CertNcService', NULL,
   '作为认证审核专家，请根据以下不符合项信息生成 NC 不符合项描述与处置建议。\n\n不符合项背景：{{NcContext}}\n\n条款原文：{{ClauseText}}\n\n检查发现：{{FindingDesc}}\n\n请输出 JSON（不要 markdown）：\n{"fact_desc":"不符合事实描述","requirement_ref":"违反条款原文引用","severity":"major|minor","corrective_req":"整改要求","due_days":30}',
   'claude-sonnet-4-5-20250929', 2048, 0.50,
   'NC 不符合项描述与处置建议生成（V2 独立认证检查闭环）', 1, 1, 1, 0, 'seed_audit_check')
ON DUPLICATE KEY UPDATE
  `Version` = `Version` + 1,
  `Template` = VALUES(`Template`),
  `ModelName` = VALUES(`ModelName`),
  `UpdateTime` = CURRENT_TIMESTAMP;

-- 3. evidence_link — 证据关联分析
INSERT INTO `wf_prompt_template`
  (`Code`, `OrgCode`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`, `Template`, `ModelName`, `MaxTokens`, `Temperature`, `Description`, `Version`, `IsActive`, `IsValid`, `IsDeleted`, `CreateBy`)
VALUES
  (REPLACE(UUID(),'-',''), NULL, 'evidence_link', '证据关联度分析', 'evidence_link', 'CertEvidenceService', NULL,
   '作为认证审核专家，请评估以下候选证据与当前检查项的关联度。\n\n检查项：{{CheckItemDesc}}\n\n条款要求：{{ClauseRequirement}}\n\n候选证据列表：{{CandidateEvidence}}\n\n请输出 JSON（不要 markdown）：\n{"relevant":[{"code":"证据Code","score":0-100,"reason":"关联理由"}]}',
   'claude-sonnet-4-5-20250929', 2048, 0.30,
   '证据与检查项关联度评分（V2 独立认证检查闭环）', 1, 1, 1, 0, 'seed_audit_check')
ON DUPLICATE KEY UPDATE
  `Version` = `Version` + 1,
  `Template` = VALUES(`Template`),
  `ModelName` = VALUES(`ModelName`),
  `UpdateTime` = CURRENT_TIMESTAMP;

-- ④ 自检
SELECT '--- 提示词模板注册确认（audit_check / nc_suggest / evidence_link）---' AS `check`;
SELECT PromptCode, PromptName, Version, ModelName, IsActive
FROM `wf_prompt_template`
WHERE `PromptCode` IN ('audit_check', 'nc_suggest', 'evidence_link');
