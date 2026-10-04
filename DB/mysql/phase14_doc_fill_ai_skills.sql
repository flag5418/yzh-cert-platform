-- =============================================================================
-- 文档填写 · AI 数据来源 Skill 注册（39 号 §六 / §七 / §八 / §2.3）
-- 日期：2026-10-04  作者：seed_doc_fill_ai_skill
--
-- ★ 幂等：全部用 `AS new ... ON DUPLICATE KEY UPDATE`，可重复执行。
--
-- ★ 本次补的是 phase13 明确「待实现后再补」的 3 个 AI 来源：
--     src_semantic  —— AI 语义改写（把模板里的标准原文按企业实际情况改写）
--     src_ai_field  —— AI 单元格填写（★ 本系统最核心的能力）
--     src_ai_table  —— AI 表格填写
--   代码已就位（2026-10-04）：
--     CertPlatform.Admin/Services/Workflow/Skills/Fill/Ai/{SrcSemantic, SrcAiField, SrcAiTable}Skill.cs
--   ⛔ `src_dict` **不注册** —— 39 号 §九定为二期，提前注册会让
--      `SkillExecutor.ResolveType` 返回 null ⇒ 运行期报「无法找到类型」。
--
-- ★ 注册一个 Skill 必须同时动 3 处（漏任一处都会静默失败）：
--     ① Sys_DictionaryList —— 技能分类字典（否则 skill 管理页左树挂不上节点，界面上「看不见」）
--     ② wf_skill           —— 技能主体
--     ③ wf_skill_reflection —— 反射登记（★ ClassPath 必须**逐字**正确，写错一个字只在运行期炸）
-- =============================================================================

-- ① 技能分类字典（幂等重放 phase13 的 3 项，使本脚本可独立执行） ------------------
--    字典：Sys_Dictionary.DicNo='skill_category'（Code=63a1d466b99d11f1877ec60431dd7713）
INSERT INTO `Sys_DictionaryList`
  (`Code`, `DicCode`, `DicName`, `DicValue`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('doccat_doc_src_scalar', '63a1d466b99d11f1877ec60431dd7713', '文档取值·标量', 'doc_src_scalar', 60, 1, 0, NOW(), 'seed_doc_fill_ai_skill'),
  ('doccat_doc_src_table',  '63a1d466b99d11f1877ec60431dd7713', '文档取值·表格', 'doc_src_table',  61, 1, 0, NOW(), 'seed_doc_fill_ai_skill'),
  ('doccat_doc_fill',       '63a1d466b99d11f1877ec60431dd7713', '文档填写',       'doc_fill',       62, 1, 0, NOW(), 'seed_doc_fill_ai_skill')
AS new
ON DUPLICATE KEY UPDATE
  `DicName` = new.`DicName`,
  `DicValue` = new.`DicValue`,
  `OrderNo` = new.`OrderNo`,
  `IsValid` = 1,
  `IsDeleted` = 0;


-- ② wf_skill 注册（3 个 AI 来源） ----------------------------------------------
--    ⚠️ SortOrder 110/111/150 是**给 AI 类留的段**：标量来源占 100~109、
--       表格来源占 150~159 ⇒ 以后再插同类不会打乱现有顺序。
INSERT INTO `wf_skill`
  (`Code`, `SkillCode`, `Name`, `SkillType`, `CategoryCode`, `SideEffect`, `ReturnType`,
   `Description`, `IsActive`, `OutputStrict`, `Version`, `SortOrder`, `Icon`, `Color`,
   `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('SK_SRC_SEMANTIC', 'src_semantic', 'AI 语义改写', 'method', 'doc_src_scalar', 0, 'json',
   '按企业实际情况改写标准原文（不依赖企业文档）。', 1, 1, '1.0', 110, 'el-icon-magic-stick', '#409EFF', 1, 0, NOW(), 'seed_doc_fill_ai_skill'),

  ('SK_SRC_AI_FIELD', 'src_ai_field', 'AI 单元格填写', 'method', 'doc_src_scalar', 0, 'json',
   '提示词 + 已过滤企业文档 → 动态分析出单元格值。', 1, 1, '1.0', 111, 'el-icon-cpu', '#409EFF', 1, 0, NOW(), 'seed_doc_fill_ai_skill'),

  ('SK_SRC_AI_TABLE', 'src_ai_table', 'AI 表格填写', 'method', 'doc_src_table', 0, 'json',
   '提示词 + 已过滤企业文档 → 动态分析出表格数据。', 1, 1, '1.0', 150, 'el-icon-grid', '#409EFF', 1, 0, NOW(), 'seed_doc_fill_ai_skill')
AS new
ON DUPLICATE KEY UPDATE
  `Name` = new.`Name`,
  `SkillType` = new.`SkillType`,
  `CategoryCode` = new.`CategoryCode`,
  `SideEffect` = new.`SideEffect`,
  `ReturnType` = new.`ReturnType`,
  `Description` = new.`Description`,
  `IsActive` = 1,
  `SortOrder` = new.`SortOrder`,
  `Icon` = new.`Icon`,
  `Color` = new.`Color`,
  `IsValid` = 1,
  `IsDeleted` = 0;


-- ③ wf_skill_reflection 反射登记（★ ClassPath 逐字正确，否则运行期「无法找到类型」） --
--    ⚠️ 命名空间含 `.Fill.Ai` 段（本批 3 个 Skill 放在 Fill/Ai/ 子目录，
--       与 phase13 的 `.Fill` 段**不同** —— 这是本批最容易写错的地方）。
INSERT INTO `wf_skill_reflection`
  (`Code`, `SkillCode`, `ClassPath`, `MethodName`, `Status`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('SKR_SRC_SEMANTIC', 'src_semantic',
   'CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai.SrcSemanticSkill', 'ExecuteAsync', 'active', 1, 0, NOW(), 'seed_doc_fill_ai_skill'),

  ('SKR_SRC_AI_FIELD', 'src_ai_field',
   'CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai.SrcAiFieldSkill', 'ExecuteAsync', 'active', 1, 0, NOW(), 'seed_doc_fill_ai_skill'),

  ('SKR_SRC_AI_TABLE', 'src_ai_table',
   'CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai.SrcAiTableSkill', 'ExecuteAsync', 'active', 1, 0, NOW(), 'seed_doc_fill_ai_skill')
AS new
ON DUPLICATE KEY UPDATE
  `ClassPath` = new.`ClassPath`,
  `MethodName` = new.`MethodName`,
  `Status` = 'active',
  `IsValid` = 1,
  `IsDeleted` = 0;


-- ④ 执行后自检（应输出 3 / 3 / 3；再跑 ⑤ 看总数应为 7 / 7） -----------------------
SELECT '① 技能分类项（本批依赖）' AS item, COUNT(*) AS cnt
  FROM `Sys_DictionaryList`
 WHERE `DicCode` = '63a1d466b99d11f1877ec60431dd7713'
   AND `DicValue` IN ('doc_src_scalar', 'doc_src_table', 'doc_fill') AND `IsDeleted` = 0
UNION ALL
SELECT '② wf_skill AI 来源', COUNT(*)
  FROM `wf_skill` WHERE `SkillCode` IN ('src_semantic','src_ai_field','src_ai_table') AND `IsDeleted` = 0
UNION ALL
SELECT '③ wf_skill_reflection AI 来源', COUNT(*)
  FROM `wf_skill_reflection` WHERE `SkillCode` IN ('src_semantic','src_ai_field','src_ai_table') AND `IsDeleted` = 0
UNION ALL
SELECT '④ wf_skill 填写类合计（应为 7）', COUNT(*)
  FROM `wf_skill`
 WHERE `SkillCode` IN ('src_global_param','src_manual','fill_cell','fill_table',
                       'src_semantic','src_ai_field','src_ai_table') AND `IsDeleted` = 0
UNION ALL
SELECT '⑤ reflection 填写类合计（应为 7）', COUNT(*)
  FROM `wf_skill_reflection`
 WHERE `SkillCode` IN ('src_global_param','src_manual','fill_cell','fill_table',
                       'src_semantic','src_ai_field','src_ai_table') AND `IsDeleted` = 0;
