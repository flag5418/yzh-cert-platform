-- ============================================================================
-- 2026-10-12 · 跨端共享层收敛 阶段 1.A：OrgCode 判据铁律落地（数据侧）
--
-- 依据（唯一权威）：
--   · docs/30-项目规则/跨端共享与核心资源检索规则-V1.md   §二
--   · docs/50-任务/迁移计划/跨端共享层收敛-实施计划-V2.md  阶段 1.A + §五 裁决 E/F/G
--
-- 做什么（五件事）：
--   ① 新建虚拟机构 yzh-std-env —— 「后台建立标准级数据」的机构归属        （裁决 E）
--   ② 迁移「后台标准级」历史数据 196 行 OrgCode → 'yzh-std-env'           （裁决 F）
--   ③ 迁移 NC 规则族 3 行（后台建的「全局复用」默认规则）→ 'yzh-std-env'  （裁决 G）
--   ④ 写入侧默认值对齐（防「不经过 OnBeforeAdd 的批量插入」落到旧形态）
--   ⑤ 体检 SQL（执行后自证）
--
-- 为什么是「新建」而不是「改 ORG_ROOT_003 的 Code」（实测修正 裁决 E）：
--   ORG_ROOT_003 的 Remark = 「专家注册工作区根节点（由 AuditorRegisterService 幂等创建）」
--   —— 它的用途是「专家注册工作区」，与「后台标准建立」是两件事；
--   且 AuditorRegisterService 硬编码引用它。⇒ 新建，不动它。
--
-- 裁决 G 的核实结论（2026-10-12 实测）：
--   · cert_conclusion_rule   CR-GENERIC-0001   Status=draft   CreateBy=system_init
--       Remark='★占位规则：阈值未经认证机构确认，不可用于真实认证结论'
--   · cert_nc_judge_prompt   JC-PROMPT-0001    Status=active  CreateBy=system_init
--       PromptName='NC判断 · 判定框架（全局复用）'
--   · cert_nc_conclude_prompt CC-PROMPT-0001   Status=active  CreateBy=system_init
--       PromptName='NC结论 · 理由撰写（全局复用）'
--     ⇒ 三者都是「后台（平台方）建的默认规则」，语义 = 后台标准级 ⇒ 迁 'yzh-std-env'。
--       配套：读取侧已加「机构专属优先 → 后台标准级回落」回落（见
--       BuildNcPromptSkill / NcConclusionCalcSkill）。
--   · cert_doc_extraction_rule c8c0e278-…      Status=failed  CreateBy=NULL
--     ⇒ ⛔ **不迁、不删**：实体注释明确 `OrgCode` = 规则键所属文件行
--       ConfigCode → cert_standard_directory_config.OrgCode（机构级，设计如此），
--       且被 cert_extraction_result 27 行 + cert_table_extraction_result 68 行引用。
--
-- 幂等：可重复执行（INSERT ... WHERE NOT EXISTS；UPDATE 带值过滤）
-- 反向脚本：见文末（⛔ 只回滚数据，不删机构行 —— 避免二次执行时 Id 漂移）
--
-- ⚠️ 执行后必须重建后端 + 重启：实体新增了 OrgCode 字段（ISOStandard / ISOClause），
--    且判据由「空值」改为「常量比较」（d.OrgCode == CoreOrgCodes.StdEnv）。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- ① 新建虚拟机构 yzh-std-env
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO sys_organization
    (Code, OrgName, OrgCode, ParentCode, OrgType, OrgLevel, OrgPath, Sort,
     IsValid, IsDeleted, Remark, CreateBy, CreateTime)
SELECT 'yzh-std-env',
       '后台标准建立虚拟机构',
       NULL, NULL, 'VirtualOrg', 1, '/yzh-std-env', 0,
       1, 0,
       '跨端共享层收敛（2026-10-12）：后台标准级数据的机构归属。见 docs/30-项目规则/跨端共享与核心资源检索规则-V1.md §二',
       'migration-20261012', NOW()
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM sys_organization WHERE Code = 'yzh-std-env');

-- ────────────────────────────────────────────────────────────────────────────
-- ② 迁移「后台标准级」历史数据 196 行（裁决 F）
--    形态（2026-10-12 实测）：
--      cert_iso_standard    NULL × 14
--      cert_iso_clause      NULL × 163
--      cert_fill_param_def  ''   × 17
--      cert_doc_fill_prompt ''   × 2
-- ────────────────────────────────────────────────────────────────────────────
UPDATE cert_iso_standard    SET OrgCode = 'yzh-std-env' WHERE OrgCode IS NULL;
UPDATE cert_iso_clause      SET OrgCode = 'yzh-std-env' WHERE OrgCode IS NULL;
UPDATE cert_fill_param_def  SET OrgCode = 'yzh-std-env' WHERE OrgCode IS NULL OR OrgCode = '';
UPDATE cert_doc_fill_prompt SET OrgCode = 'yzh-std-env' WHERE OrgCode IS NULL OR OrgCode = '';

-- ────────────────────────────────────────────────────────────────────────────
-- ③ 迁移 NC 规则族 3 行（裁决 G）
--    ⛔ 只迁这 3 张表；cert_doc_extraction_rule 是机构级（实体注释明载）⇒ 不动
-- ────────────────────────────────────────────────────────────────────────────
UPDATE cert_conclusion_rule   SET OrgCode = 'yzh-std-env'
    WHERE Code = 'CR-GENERIC-0001'   AND OrgCode = '906e8b2a962c4062b21144af4cc4abc0';
UPDATE cert_nc_judge_prompt   SET OrgCode = 'yzh-std-env'
    WHERE Code = 'JC-PROMPT-0001'    AND OrgCode = '906e8b2a962c4062b21144af4cc4abc0';
UPDATE cert_nc_conclude_prompt SET OrgCode = 'yzh-std-env'
    WHERE Code = 'CC-PROMPT-0001'    AND OrgCode = '906e8b2a962c4062b21144af4cc4abc0';

-- ────────────────────────────────────────────────────────────────────────────
-- ④ 写入侧默认值对齐
-- ────────────────────────────────────────────────────────────────────────────
ALTER TABLE cert_fill_param_def  ALTER COLUMN OrgCode SET DEFAULT 'yzh-std-env';
ALTER TABLE cert_doc_fill_prompt ALTER COLUMN OrgCode SET DEFAULT 'yzh-std-env';

-- ────────────────────────────────────────────────────────────────────────────
-- ⑤ 体检（执行后自证 —— 取消注释单独跑，第 1 组必须全部返回 0）
-- ────────────────────────────────────────────────────────────────────────────
-- SELECT 'cert_iso_standard' t, COUNT(*) c FROM cert_iso_standard WHERE OrgCode IS NULL OR OrgCode = ''
-- UNION ALL SELECT 'cert_iso_clause', COUNT(*) FROM cert_iso_clause WHERE OrgCode IS NULL OR OrgCode = ''
-- UNION ALL SELECT 'cert_fill_param_def', COUNT(*) FROM cert_fill_param_def WHERE OrgCode IS NULL OR OrgCode = ''
-- UNION ALL SELECT 'cert_doc_fill_prompt', COUNT(*) FROM cert_doc_fill_prompt WHERE OrgCode IS NULL OR OrgCode = ''
-- UNION ALL SELECT 'cert_conclusion_rule', COUNT(*) FROM cert_conclusion_rule WHERE OrgCode <> 'yzh-std-env'
-- UNION ALL SELECT 'cert_nc_judge_prompt', COUNT(*) FROM cert_nc_judge_prompt WHERE OrgCode <> 'yzh-std-env'
-- UNION ALL SELECT 'cert_nc_conclude_prompt', COUNT(*) FROM cert_nc_conclude_prompt WHERE OrgCode <> 'yzh-std-env';
--
-- -- 第 2 组：迁移后「后台标准级」条数应为 199（196 + 3）
-- SELECT 'backend_built_total' t, (
--     (SELECT COUNT(*) FROM cert_iso_standard        WHERE OrgCode = 'yzh-std-env') +
--     (SELECT COUNT(*) FROM cert_iso_clause          WHERE OrgCode = 'yzh-std-env') +
--     (SELECT COUNT(*) FROM cert_fill_param_def      WHERE OrgCode = 'yzh-std-env') +
--     (SELECT COUNT(*) FROM cert_doc_fill_prompt     WHERE OrgCode = 'yzh-std-env') +
--     (SELECT COUNT(*) FROM cert_conclusion_rule     WHERE OrgCode = 'yzh-std-env') +
--     (SELECT COUNT(*) FROM cert_nc_judge_prompt     WHERE OrgCode = 'yzh-std-env') +
--     (SELECT COUNT(*) FROM cert_nc_conclude_prompt  WHERE OrgCode = 'yzh-std-env')
-- ) c;

-- ============================================================================
-- 反向（回滚）
-- ============================================================================
-- UPDATE cert_iso_standard    SET OrgCode = NULL WHERE OrgCode = 'yzh-std-env';
-- UPDATE cert_iso_clause      SET OrgCode = NULL WHERE OrgCode = 'yzh-std-env';
-- UPDATE cert_fill_param_def  SET OrgCode = ''   WHERE OrgCode = 'yzh-std-env';
-- UPDATE cert_doc_fill_prompt SET OrgCode = ''   WHERE OrgCode = 'yzh-std-env';
-- UPDATE cert_conclusion_rule   SET OrgCode = '906e8b2a962c4062b21144af4cc4abc0' WHERE Code = 'CR-GENERIC-0001';
-- UPDATE cert_nc_judge_prompt   SET OrgCode = '906e8b2a962c4062b21144af4cc4abc0' WHERE Code = 'JC-PROMPT-0001';
-- UPDATE cert_nc_conclude_prompt SET OrgCode = '906e8b2a962c4062b21144af4cc4abc0' WHERE Code = 'CC-PROMPT-0001';
-- ALTER TABLE cert_fill_param_def  ALTER COLUMN OrgCode SET DEFAULT '';
-- ALTER TABLE cert_doc_fill_prompt ALTER COLUMN OrgCode SET DEFAULT '';
