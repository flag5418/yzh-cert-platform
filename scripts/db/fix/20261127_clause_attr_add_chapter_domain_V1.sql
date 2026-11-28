-- ══════════════════════════════════════════════════════════════════════════
-- 2026-11-27 · 条款属性池补「大章节」（ChapterDomain）—— 让 clause_attr_gen 具备大章节概念
--
-- 背景（用户裁定 2026-11-27）：
--   `clause_attr_gen` 此前只产 `Role`（r1–r7 功能角色），正文写着「章节只用于排版顺序，不作判据」，
--   提示词里**没有显式大章节概念**。大章节 = 跨标准通用抽象（国标 4–10 章），
--   「大章节 + 部门」是 2 段式召回第一段初筛的关键判据。
--
-- 做法（不改提示词正文，最小改动）：
--   `clause_attr_gen` 正文只写通用规则 + `{{attr_pool}}` 占位符，属性清单由
--   `ClauseAttrPromptService.BuildPoolSectionAsync` 按 `cert_clause_attr_def` **实时渲染**。
--   ⇒ 只要往属性池加一行 `ChapterDomain`（dict），提示词自动长出该字段，模型即产出 `CD_*`；
--      `ClauseContext.ChapterDomainCode = ChapterDomain.ResolveClause(Attr("ChapterDomain"), Role, 章号)`
--      会**优先**采用该属性（见 ChapterDomain.cs）。
--
-- 与既有数据的关系：
--   批 3-1 已用确定性脚本 `20261127_clause_chapter_domain_V1.sql` 回填 65 行
--   `cert_clause_attr.AttrCode='ChapterDomain'`（由 Role 推导）。本脚本补齐**属性池定义**，
--   使后续「条款属性预生成」也由 LLM 产出同一字段（口径一致、可覆盖）。
--   ⛔ 不删 `Role`（仍被 RecallPathResolver / ClauseCategoryResolver 消费）。
--
-- 幂等：全部 NOT EXISTS 保护，重复执行结果一致。
-- 用法：
--   docker exec -i -e MYSQL_PWD='CHANGE_ME' yzh-mysql mysql -uroot --default-character-set=utf8mb4 \
--     yzh_cert_platform < scripts/db/fix/20261127_clause_attr_add_chapter_domain_V1.sql
-- ══════════════════════════════════════════════════════════════════════════
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- 1. 数据字典 clause_chapter_domain（大章节枚举） ----------
INSERT INTO `Sys_Dictionary` (`Code`, `DicName`, `DicNo`, `OrderNo`, `Remark`, `IsValid`, `IsDeleted`, `CreateTime`)
SELECT 'CLAUSE_CHAPTER_DOMAIN', '条款大章节', 'clause_chapter_domain', 40,
       'ISO 管理体系标准第 4–10 章功能域（跨标准通用）', 1, 0, NOW()
 WHERE NOT EXISTS (SELECT 1 FROM `Sys_Dictionary`
                    WHERE `DicNo` = 'clause_chapter_domain' AND `IsDeleted` = 0);

INSERT INTO `Sys_DictionaryList` (`Code`, `DicCode`, `DicName`, `DicValue`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`)
SELECT v.`Code`, 'CLAUSE_CHAPTER_DOMAIN', v.`DicName`, v.`DicValue`, v.`OrderNo`, 1, 0, NOW()
  FROM (
    SELECT 'CCD_ENV'     AS `Code`, '4 组织环境'     AS `DicName`, 'CD_ENV'     AS `DicValue`, 10 AS `OrderNo` UNION ALL
    SELECT 'CCD_LEAD',    '5 领导作用',   'CD_LEAD',    20 UNION ALL
    SELECT 'CCD_DECIDE',  '6 策划',       'CD_DECIDE',  30 UNION ALL
    SELECT 'CCD_SUPPORT', '7 支持',       'CD_SUPPORT', 40 UNION ALL
    SELECT 'CCD_RUN',     '8 运行',       'CD_RUN',     50 UNION ALL
    SELECT 'CCD_ASSESS',  '9 绩效评价',   'CD_ASSESS',  60 UNION ALL
    SELECT 'CCD_IMPROVE', '10 改进',      'CD_IMPROVE', 70 UNION ALL
    SELECT 'CCD_UNKNOWN', '未判定',       'CD_UNKNOWN', 80
  ) v
 WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList`
                    WHERE `DicCode` = 'CLAUSE_CHAPTER_DOMAIN' AND `DicValue` = v.`DicValue` AND `IsDeleted` = 0);

-- ---------- 2. 属性池加一行「大章节」（Sort 置于 Role(110) 与 证据契约(120) 之间） ----------
INSERT INTO `cert_clause_attr_def`
  (`AttrName`, `ValueType`, `DictCode`, `Remark`, `PromptHint`, `Sort`,
   `IsValid`, `IsDeleted`, `Status`, `Code`, `CreateTime`)
SELECT
  '大章节', 'dict', 'clause_chapter_domain',
  'ISO 管理体系标准第 4–10 章功能域（跨标准通用抽象）',
  '按条款功能与内容判这条款属于哪一大章节（跨标准通用）：4 组织环境⇒CD_ENV、5 领导作用⇒CD_LEAD、6 策划⇒CD_DECIDE、7 支持⇒CD_SUPPORT、8 运行⇒CD_RUN、9 绩效评价⇒CD_ASSESS、10 改进⇒CD_IMPROVE；判不出⇒CD_UNKNOWN。⛔ 只填允许取值里的一个码。',
  115, 1, 0, 'active', 'ChapterDomain', NOW()
 WHERE NOT EXISTS (SELECT 1 FROM `cert_clause_attr_def`
                    WHERE `Code` = 'ChapterDomain' AND `IsDeleted` = 0);

-- ---------- 3. 验证 ----------
-- 3.1 字典型（期望：1 行 8 项）
SELECT '字典项数' AS CheckItem, COUNT(*) AS Cnt
  FROM `Sys_DictionaryList`
 WHERE `DicCode` = 'CLAUSE_CHAPTER_DOMAIN' AND `IsDeleted` = 0;

-- 3.2 属性池（期望：ChapterDomain 一行，dict + clause_chapter_domain）
SELECT `AttrName`, `Code`, `ValueType`, `DictCode`, `Sort`, `IsValid`
  FROM `cert_clause_attr_def`
 WHERE `Code` = 'ChapterDomain' AND `IsDeleted` = 0;

-- 3.3 属性池总数（期望 13 → 14）
SELECT '属性池条数' AS CheckItem, COUNT(*) AS Cnt
  FROM `cert_clause_attr_def` WHERE `IsValid` = 1 AND `IsDeleted` = 0;
