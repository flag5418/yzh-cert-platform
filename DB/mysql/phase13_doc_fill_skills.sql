-- =============================================================================
-- 文档填写 Skill 注册（38 号 §六 / 39 号 §2.3）
-- 日期：2026-10-03  作者：seed_doc_fill_skill
--
-- ★ 幂等：全部用 `AS new ... ON DUPLICATE KEY UPDATE`，可重复执行。
--   （MySQL 8.0 无 `ADD COLUMN IF NOT EXISTS`，本脚本无 DDL，故不涉及。）
--
-- ★ 为什么必须补「技能分类」字典：
--   `39` 号 §2.3 给填写 Skill 定的 `CategoryCode` 是 `doc_src_scalar` / `doc_src_table` /
--   `doc_fill`，但**库里 `skill_category` 字典只有 5 项**（data_access/data_process/
--   ai_judge/ai_generate/output）⇒ 不补的话 skill 管理页左树**挂不上这些节点**，
--   新增的 Skill 在界面上「看不见」（这正是用户 2026-10-03 反馈的现象）。
--
-- ★ 只注册**已实现**的 4 个。`39` 号 §2.3 列了 8 个，其余 4 个
--   （src_semantic / src_ai_field / src_ai_table / src_dict）**代码尚不存在** ——
--   若提前注册，`SkillExecutor.ResolveType` 会返回 null ⇒ 报「无法找到类型」。
--   待实现后再补注册行。
-- =============================================================================

-- ① 技能分类字典补 3 项 -------------------------------------------------------
--    字典：Sys_Dictionary.DicNo='skill_category'（Code=63a1d466b99d11f1877ec60431dd7713）
INSERT INTO `Sys_DictionaryList`
  (`Code`, `DicCode`, `DicName`, `DicValue`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('doccat_doc_src_scalar', '63a1d466b99d11f1877ec60431dd7713', '文档取值·标量', 'doc_src_scalar', 60, 1, 0, NOW(), 'seed_doc_fill_skill'),
  ('doccat_doc_src_table',  '63a1d466b99d11f1877ec60431dd7713', '文档取值·表格', 'doc_src_table',  61, 1, 0, NOW(), 'seed_doc_fill_skill'),
  ('doccat_doc_fill',       '63a1d466b99d11f1877ec60431dd7713', '文档填写',       'doc_fill',       62, 1, 0, NOW(), 'seed_doc_fill_skill')
AS new
ON DUPLICATE KEY UPDATE
  `DicName` = new.`DicName`,
  `DicValue` = new.`DicValue`,
  `OrderNo` = new.`OrderNo`,
  `IsValid` = 1,
  `IsDeleted` = 0;


-- ② wf_skill 注册（4 个已实现） -----------------------------------------------
INSERT INTO `wf_skill`
  (`Code`, `SkillCode`, `Name`, `SkillType`, `CategoryCode`, `SideEffect`, `ReturnType`,
   `Description`, `IsActive`, `OutputStrict`, `Version`, `SortOrder`, `Icon`, `Color`,
   `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('SK_SRC_GLOBAL_PARAM', 'src_global_param', '全局参数取值', 'method', 'doc_src_scalar', 0, 'json',
   '按参数编码取全局参数值（标准/阶段特化，取最特异一条）。产出 FillValue。', 1, 1, '1.0', 100, 'el-icon-set-up', '#409EFF', 1, 0, NOW(), 'seed_doc_fill_skill'),

  ('SK_SRC_MANUAL', 'src_manual', '人工待办声明', 'method', 'doc_src_scalar', 0, 'json',
   '声明「该锚点需人工填写」，产出待办标记（不产值）。', 1, 1, '1.0', 101, 'el-icon-edit-outline', '#909399', 1, 0, NOW(), 'seed_doc_fill_skill'),

  ('SK_FILL_CELL', 'fill_cell', '单元格填写', 'method', 'doc_fill', 1, 'json',
   '把值装配成单元格填充指令（含页眉）。★ 不落盘。', 1, 1, '1.0', 200, 'el-icon-edit', '#E6A23C', 1, 0, NOW(), 'seed_doc_fill_skill'),

  ('SK_FILL_TABLE', 'fill_table', '表格填写', 'method', 'doc_fill', 1, 'json',
   '把表格数据装配成区域填充指令（Word 表格 / Excel 区域）。★ 不落盘。', 1, 1, '1.0', 201, 'el-icon-grid', '#E6A23C', 1, 0, NOW(), 'seed_doc_fill_skill')
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


-- ③ wf_skill_reflection 反射登记（★ ClassPath 必须逐字正确，否则运行期「无法找到类型」）
--    ⚠️ 命名空间含 `.Fill` 段（39 号 §2.3 裁定，以该文为准）
INSERT INTO `wf_skill_reflection`
  (`Code`, `SkillCode`, `ClassPath`, `MethodName`, `Status`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('SKR_SRC_GLOBAL_PARAM', 'src_global_param',
   'CertPlatform.Admin.Services.Workflow.Skills.Fill.SrcGlobalParamSkill', 'ExecuteAsync', 'active', 1, 0, NOW(), 'seed_doc_fill_skill'),

  ('SKR_SRC_MANUAL', 'src_manual',
   'CertPlatform.Admin.Services.Workflow.Skills.Fill.SrcManualSkill', 'ExecuteAsync', 'active', 1, 0, NOW(), 'seed_doc_fill_skill'),

  ('SKR_FILL_CELL', 'fill_cell',
   'CertPlatform.Admin.Services.Workflow.Skills.Fill.FillCellSkill', 'ExecuteAsync', 'active', 1, 0, NOW(), 'seed_doc_fill_skill'),

  ('SKR_FILL_TABLE', 'fill_table',
   'CertPlatform.Admin.Services.Workflow.Skills.Fill.FillTableSkill', 'ExecuteAsync', 'active', 1, 0, NOW(), 'seed_doc_fill_skill')
AS new
ON DUPLICATE KEY UPDATE
  `ClassPath` = new.`ClassPath`,
  `MethodName` = new.`MethodName`,
  `Status` = 'active',
  `IsValid` = 1,
  `IsDeleted` = 0;


-- ④ 执行后自检（应输出 3 / 4 / 4） --------------------------------------------
SELECT '① 技能分类新增项' AS item, COUNT(*) AS cnt
  FROM `Sys_DictionaryList`
 WHERE `DicCode` = '63a1d466b99d11f1877ec60431dd7713'
   AND `DicValue` IN ('doc_src_scalar', 'doc_src_table', 'doc_fill') AND `IsDeleted` = 0
UNION ALL
SELECT '② wf_skill 填写类', COUNT(*)
  FROM `wf_skill` WHERE `SkillCode` IN ('src_global_param','src_manual','fill_cell','fill_table') AND `IsDeleted` = 0
UNION ALL
SELECT '③ wf_skill_reflection 填写类', COUNT(*)
  FROM `wf_skill_reflection` WHERE `SkillCode` IN ('src_global_param','src_manual','fill_cell','fill_table') AND `IsDeleted` = 0;
