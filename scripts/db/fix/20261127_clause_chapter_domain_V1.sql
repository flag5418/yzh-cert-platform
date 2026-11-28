-- ══════════════════════════════════════════════════════════════════════════
-- 批 3-1：条款侧「大章节域」确定性回填（27 号执行计划 §五 批3）
--
-- 目标：给 cert_clause_attr 补 AttrCode='ChapterDomain' 一行/条款，
--       由现有 Role（r1–r7，与国标 4–10 章 1:1）确定性推导。
--       这是 2 段式召回第一段「章节 ∧ 部门」初过滤的**条款侧判据**。
--
-- 映射（唯一判据 = ChapterDomain.RoleByDomain，见 ChapterDomain.cs）：
--   r1→CD_ENV(4 组织环境)  r2→CD_LEAD(5 领导)  r3→CD_DECIDE(6 策划)  r4→CD_SUPPORT(7 支持)
--   r5→CD_RUN(8 运行)      r6→CD_ASSESS(9 绩效评价)                  r7→CD_IMPROVE(10 改进)
--
-- 幂等：仅当 (StandardCode, ClauseCode, AttrCode='ChapterDomain', IsDeleted=0) 不存在时插入。
-- 前置：备份到 _bak_clause_attr_chapterdomain_20261127（首次执行时建立）。
-- 审计人：CreateBy/UpdateBy = 'U_ADMIN'（= sys_user.Code，合规）。
--
-- 用法：
--   docker exec -i -e MYSQL_PWD='CHANGE_ME' yzh-mysql mysql -uroot --default-character-set=utf8mb4 \
--     yzh_cert_platform < scripts/db/fix/20261127_clause_chapter_domain_V1.sql
-- ══════════════════════════════════════════════════════════════════════════
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- 0. 备份（只建一次） ----------
CREATE TABLE IF NOT EXISTS `_bak_clause_attr_chapterdomain_20261127` AS
SELECT * FROM `cert_clause_attr` WHERE 1 = 0;

INSERT INTO `_bak_clause_attr_chapterdomain_20261127`
SELECT r.* FROM `cert_clause_attr` r
 WHERE r.AttrCode = 'ChapterDomain'
   AND NOT EXISTS (
     SELECT 1 FROM `_bak_clause_attr_chapterdomain_20261127` b
      WHERE b.Id = r.Id);

-- ---------- 1. 回填 ChapterDomain（幂等） ----------
INSERT INTO `cert_clause_attr`
  (`StandardCode`, `ClauseCode`, `ClauseNumber`, `AttrCode`, `ValueSource`, `AttrValue`,
   `Sort`, `IsValid`, `IsDeleted`, `Status`, `Code`, `CreateTime`, `CreateBy`, `UpdateTime`, `UpdateBy`)
SELECT
  r.`StandardCode`,
  r.`ClauseCode`,
  r.`ClauseNumber`,
  'ChapterDomain',
  'fixed',
  CASE LOWER(TRIM(r.`AttrValue`))
    WHEN 'r1' THEN 'CD_ENV'
    WHEN 'r2' THEN 'CD_LEAD'
    WHEN 'r3' THEN 'CD_DECIDE'
    WHEN 'r4' THEN 'CD_SUPPORT'
    WHEN 'r5' THEN 'CD_RUN'
    WHEN 'r6' THEN 'CD_ASSESS'
    WHEN 'r7' THEN 'CD_IMPROVE'
    ELSE 'CD_UNKNOWN'
  END,
  r.`Sort`,
  1, 0, 'active',
  REPLACE(UUID(), '-', ''),
  NOW(), 'U_ADMIN', NOW(), 'U_ADMIN'
FROM `cert_clause_attr` r
WHERE r.AttrCode = 'Role'
  AND r.IsDeleted = 0
  AND NOT EXISTS (
    SELECT 1 FROM `cert_clause_attr` x
     WHERE x.StandardCode = r.StandardCode
       AND x.ClauseCode   = r.ClauseCode
       AND x.AttrCode     = 'ChapterDomain'
       AND x.IsDeleted    = 0);

-- ---------- 2. 验证 ----------
-- 2.1 章域分布
SELECT AttrValue AS ChapterDomain, COUNT(*) AS Clauses
  FROM `cert_clause_attr`
 WHERE AttrCode = 'ChapterDomain' AND IsDeleted = 0
 GROUP BY AttrValue ORDER BY Clauses DESC;

-- 2.2 未回填条款 / CD_UNKNOWN（两者期望均为 0）
SELECT '未回填条款（Role有·ChapterDomain无）' AS CheckItem, COUNT(*) AS C_违规
  FROM `cert_clause_attr` r
 WHERE r.AttrCode = 'Role' AND r.IsDeleted = 0
   AND NOT EXISTS (SELECT 1 FROM `cert_clause_attr` x
                    WHERE x.StandardCode = r.StandardCode AND x.ClauseCode = r.ClauseCode
                      AND x.AttrCode = 'ChapterDomain' AND x.IsDeleted = 0)
UNION ALL
SELECT 'CD_UNKNOWN 条款', COUNT(*)
  FROM `cert_clause_attr`
 WHERE AttrCode = 'ChapterDomain' AND IsDeleted = 0 AND AttrValue = 'CD_UNKNOWN';
