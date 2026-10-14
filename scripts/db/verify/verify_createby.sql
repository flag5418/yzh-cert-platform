-- ══════════════════════════════════════════════════════════════════════════
-- CreateBy / UpdateBy / DeleteBy 口径校验（★ 只读，不做任何修改）
--
-- 规则（2026-10-11 建立）：
--   `CreateBy` / `UpdateBy` / `DeleteBy` 非空时 **必须等于某个 `sys_user.Code`**；
--   唯一例外是「已登记的脚本/种子标记」与「历史遗留无对应用户值」（白名单见下）。
--
-- 为什么要有它：`CreateBy` 曾经一列四义（Code / 登录名 / 显示名 / 脚本标记），
--   而**读侧都按 Code 解释** ⇒ 命不中时**不报错**，只表现为「姓名为空」「关联为 0」
--   （yzh_queue 曾是 79/79 行登录名 `wzm`，直接导致队列监控「创建人」显示登录名、
--     企业资料规范化「操作人」姓名永远解析不出来）。
--   本脚本把「非 Code 且有值」的行全部列出来，其中不在白名单内的 = **真违规**，期望 0。
--
-- 用法：
--   docker exec -i yzh-mysql mysql -uroot -p*** --default-character-set=utf8mb4 \
--     yzh_cert_platform < scripts/db/verify/verify_createby.sql
--
-- 期望：`C_违规` = 0；`B_已登记例外` 为白名单命中数（可见即可，非 0 正常）。
-- ⚠️ 与 `verify_naming.sql` 同族：**只读**；改了 CreateBy/UpdateBy 写入逻辑后必须重跑。
-- ⚠️ 本脚本自动枚举**所有**含 CreateBy/UpdateBy/DeleteBy 的基表列（含将来新增的），无需逐表维护；
--   视图（v_*）被排除 —— 它们跟随基表，且不可 UPDATE；
--   备份/台账表（`_bak_*` / `_mig_*`）也排除 —— 它们**有意**保存历史原值。
-- ══════════════════════════════════════════════════════════════════════════
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
SET SESSION group_concat_max_len = 4194304;

-- ---------- 动态拼「所有基表的 CreateBy/UpdateBy/DeleteBy 全集」（带列名，三列合并看） ----------
SET @union_sql = (
  SELECT GROUP_CONCAT(
           CONCAT('SELECT ''', c.TABLE_NAME, ''' AS SrcTable, ''', c.COLUMN_NAME,
                  ''' AS SrcColumn, t.`', c.COLUMN_NAME, '` AS V FROM `',
                  c.TABLE_NAME, '` t WHERE t.`', c.COLUMN_NAME, '` IS NOT NULL AND t.`',
                  c.COLUMN_NAME, '` <> ''''')
           SEPARATOR ' UNION ALL ')
    FROM information_schema.COLUMNS c
    JOIN information_schema.TABLES tb
      ON tb.TABLE_SCHEMA = c.TABLE_SCHEMA AND tb.TABLE_NAME = c.TABLE_NAME
   WHERE c.TABLE_SCHEMA = DATABASE()
     AND c.COLUMN_NAME IN ('CreateBy', 'UpdateBy', 'DeleteBy')
     AND tb.TABLE_TYPE = 'BASE TABLE'
     AND c.TABLE_NAME NOT LIKE '\_bak\_%'
     AND c.TABLE_NAME NOT LIKE '\_mig\_%'
);

-- ---------- 白名单谓词（同一份，三处复用；新增标记必须先登记在这里） ----------
--   A. 脚本 / 种子标记：记录「这行由哪个脚本创建」，改成用户 = 伪造审计留痕
--   B. 历史遗留无对应用户：测试管理员 / 测试超级管理员 / xxx（旧 Vol 时代把角色名写进了 CreateBy）
SET @is_exc = CONCAT(
  '   LEFT(x.V, 5) = ''seed_''',                        -- seed_*（含 seed_auditor / seed_fill_param_v2 …）
  '   OR UPPER(x.V) LIKE ''SYSTEM%''',                  -- system / system_init / SYSTEM / SYSTEM_FIX
  '   OR LEFT(x.V, 15) = ''fix_stage_align''',           -- fix_stage_align_20260930
  '   OR LEFT(x.V, 10) = ''manual_fix''',                -- manual_fix_20260925
  '   OR LEFT(x.V, 7)  = ''repair-''',                   -- repair-20260926
  '   OR LEFT(x.V, 10) = ''migration-''',               -- migration-20261012（⚠️ 与整词 migration 并存：整词是老标记，带日期后缀是 2026-10-12 起的迁移脚本标记）
  '   OR LEFT(x.V, 15) = ''orphan_cleanup_''',           -- orphan_cleanup_20261007（孤儿机构清理）
  '   OR x.V IN (''migration'', ''sys_migration'', ''restore'', ''auditor_reg'',',
  '               ''menu-restructure'', ''fix_20261010'', ''standard-family-20261008'',',
  '               ''menu_doc_semantic_rule'',',
  '               ''测试管理员'', ''测试超级管理员'', ''xxx'')'
);

-- ---------- ① 汇总（期望 C_违规 = 0） ----------
SET @summary = CONCAT(
  'SELECT ''A_合规_Code'' AS CheckItem, COUNT(*) AS Rows_ FROM (', @union_sql, ') x ',
  ' WHERE EXISTS (SELECT 1 FROM `sys_user` u WHERE u.Code = x.V)',
  ' UNION ALL SELECT ''B_已登记例外'' AS CheckItem, COUNT(*) FROM (', @union_sql, ') x ',
  ' WHERE NOT EXISTS (SELECT 1 FROM `sys_user` u WHERE u.Code = x.V) AND (', @is_exc, ')',
  ' UNION ALL SELECT ''C_违规（期望 0）'' AS CheckItem, COUNT(*) FROM (', @union_sql, ') x ',
  ' WHERE NOT EXISTS (SELECT 1 FROM `sys_user` u WHERE u.Code = x.V) AND NOT (', @is_exc, ')',
  ' UNION ALL SELECT ''D_两列总行数'' AS CheckItem, COUNT(*) FROM (', @union_sql, ') x'
);
PREPARE s1 FROM @summary; EXECUTE s1; DEALLOCATE PREPARE s1;

-- ---------- ② 已登记例外明细（谁在用这些非 Code 值） ----------
SET @whitelist = CONCAT(
  'SELECT x.V AS ExceptValue, x.SrcTable, x.SrcColumn, COUNT(*) AS Rows_ FROM (', @union_sql, ') x ',
  ' WHERE NOT EXISTS (SELECT 1 FROM `sys_user` u WHERE u.Code = x.V) AND (', @is_exc, ')',
  ' GROUP BY x.V, x.SrcTable, x.SrcColumn ORDER BY x.SrcTable, x.SrcColumn, x.V'
);
PREPARE s2 FROM @whitelist; EXECUTE s2; DEALLOCATE PREPARE s2;

-- ---------- ③ 违规明细（期望空集；非空 = 有一条 CreateBy/UpdateBy 写入路径没按契约走） ----------
SET @violations = CONCAT(
  'SELECT x.SrcTable, x.SrcColumn, x.V AS ViolationValue, COUNT(*) AS Rows_ FROM (', @union_sql, ') x ',
  ' WHERE NOT EXISTS (SELECT 1 FROM `sys_user` u WHERE u.Code = x.V) AND NOT (', @is_exc, ')',
  ' GROUP BY x.SrcTable, x.SrcColumn, x.V ORDER BY x.SrcTable, x.SrcColumn, x.V'
);
PREPARE s3 FROM @violations; EXECUTE s3; DEALLOCATE PREPARE s3;

-- ---------- ④ 列宽体检：Code 最长 36（GUID），列宽 < 36 ⇒ 严格模式 1406 / 非严格模式静默截断 ----------
SELECT c.TABLE_NAME, c.COLUMN_NAME, c.COLUMN_TYPE
  FROM information_schema.COLUMNS c
  JOIN information_schema.TABLES t
    ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
 WHERE c.TABLE_SCHEMA = DATABASE()
   AND c.COLUMN_NAME IN ('CreateBy', 'UpdateBy', 'DeleteBy')
   AND t.TABLE_TYPE = 'BASE TABLE'
   AND c.CHARACTER_MAXIMUM_LENGTH IS NOT NULL
   AND c.CHARACTER_MAXIMUM_LENGTH < 36          -- 期望空集
 ORDER BY c.TABLE_NAME, c.COLUMN_NAME;
