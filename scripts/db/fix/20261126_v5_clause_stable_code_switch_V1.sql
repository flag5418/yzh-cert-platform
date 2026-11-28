-- ================================================================
-- V5 条款稳定标识切换（2026-11-26）
-- 目的：把 cert_iso_clause.Code 从「旧随机码」切换为「稳定码」，
--       与 ClauseRefs.CreateStableCode(标准短码, 版本年, 条款号) 逐字一致，
--       解锁条款属性提取（ClauseAttrGenerateService 的稳定码校验）。
-- 依据：
--   src/certplatform-api/CertPlatform.Shared/Domain/Clauses/ClauseRef.cs
--   src/certplatform-api/CertPlatform.Admin/Services/Cert/ClauseAttrGenerateService.cs:183
--   src/certplatform-api/CertPlatform.Admin/Services/StandardDirectory/StandardSourceService.cs:187
-- 算法（与 C# 逐字一致，已验证 SHA2 可精确复现）：
--   identity = 'cert-clause-v1|' + len(lower(短码)) + ':' + lower(短码) + '|' + 版本年 + '|' + upper(trim(条款号))
--   code     = lower(substr(sha2(identity,256),1,32))
-- 影响面（已扫描全库引用 ClauseCode 的表，除本表外当前均 0 行）：
--   cert_iso_clause.Code + ParentCode（自引用）+ cert_audit_plan_detail.Clauses（56 行旧码）
-- 幂等：重跑时仅当 Code ≠ 稳定码才更新；备份表 IF NOT EXISTS 不覆盖首次备份。
-- 备份：cert_iso_clause_bak_20261126_stablecode / cert_audit_plan_detail_bak_20261126_stablecode
-- 执行：docker exec -i yzh-mysql mysql -uroot -pCHANGE_ME \
--        --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- ================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── ① 备份（首次执行时建立；重跑不覆盖）──
CREATE TABLE IF NOT EXISTS cert_iso_clause_bak_20261126_stablecode
    AS SELECT * FROM cert_iso_clause;
CREATE TABLE IF NOT EXISTS cert_audit_plan_detail_bak_20261126_stablecode
    AS SELECT * FROM cert_audit_plan_detail;

-- ── ② 计算映射（旧码 → 稳定码）──
DROP TEMPORARY TABLE IF EXISTS tmp_clause_map;
CREATE TEMPORARY TABLE tmp_clause_map (
    old_code VARCHAR(255) NOT NULL,
    new_code VARCHAR(255) NOT NULL,
    PRIMARY KEY (old_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO tmp_clause_map (old_code, new_code)
SELECT c.Code,
       LOWER(SUBSTRING(SHA2(CONCAT(
           'cert-clause-v1|',
           CHAR_LENGTH(LOWER(TRIM(s.StandardCode))), ':',
           LOWER(TRIM(s.StandardCode)), '|',
           s.VersionYear, '|',
           UPPER(TRIM(c.ClauseNumber))
       ), 256), 1, 32))
FROM cert_iso_clause c
JOIN cert_iso_standard s ON s.Code = c.StandardCode
WHERE c.Code IS NOT NULL AND c.Code <> '';

-- ── ③ 同步审核计划明细的条款码（旧码 → 稳定码）──
UPDATE cert_audit_plan_detail d
JOIN tmp_clause_map m ON m.old_code = d.Clauses
SET d.Clauses = m.new_code;

-- ── ④ 重键父码自引用（旧父码 → 新父码）──
UPDATE cert_iso_clause c
JOIN tmp_clause_map m ON m.old_code = c.ParentCode
SET c.ParentCode = m.new_code;

-- ── ⑤ 重键条款主码（旧码 → 稳定码，仅改未对齐行）──
UPDATE cert_iso_clause c
JOIN tmp_clause_map m ON m.old_code = c.Code
SET c.Code = m.new_code
WHERE c.Code <> m.new_code;

-- ── ⑥ 验证（期望：aligned=320，mismatch=0，plan_bad=0，parent_unresolved=0）──
SELECT 'aligned' AS chk, COUNT(*) AS n
FROM cert_iso_clause c JOIN cert_iso_standard s ON s.Code = c.StandardCode
WHERE c.IsDeleted = 0
  AND c.Code = LOWER(SUBSTRING(SHA2(CONCAT(
        'cert-clause-v1|', CHAR_LENGTH(LOWER(TRIM(s.StandardCode))), ':',
        LOWER(TRIM(s.StandardCode)), '|', s.VersionYear, '|',
        UPPER(TRIM(c.ClauseNumber))), 256), 1, 32));

SELECT 'mismatch' AS chk, COUNT(*) AS n
FROM cert_iso_clause c JOIN cert_iso_standard s ON s.Code = c.StandardCode
WHERE c.IsDeleted = 0
  AND c.Code <> LOWER(SUBSTRING(SHA2(CONCAT(
        'cert-clause-v1|', CHAR_LENGTH(LOWER(TRIM(s.StandardCode))), ':',
        LOWER(TRIM(s.StandardCode)), '|', s.VersionYear, '|',
        UPPER(TRIM(c.ClauseNumber))), 256), 1, 32));

SELECT 'plan_bad' AS chk, COUNT(*) AS n
FROM cert_audit_plan_detail d
WHERE d.IsDeleted = 0 AND (d.Clauses IS NOT NULL AND d.Clauses <> '')
  AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.Code = d.Clauses);

SELECT 'parent_unresolved' AS chk, COUNT(*) AS n
FROM cert_iso_clause c
WHERE c.IsDeleted = 0 AND c.ParentCode IS NOT NULL AND c.ParentCode <> ''
  AND NOT EXISTS (SELECT 1 FROM cert_iso_clause p WHERE p.Code = c.ParentCode);

-- 抽样对照：iso9001 条款 4.1 期望 2771154fd0b038377026169dc094b6d1
SELECT 'spot_4.1' AS chk, s.StandardCode, c.ClauseNumber, c.Code
FROM cert_iso_clause c JOIN cert_iso_standard s ON s.Code = c.StandardCode
WHERE c.IsDeleted = 0 AND s.StandardCode = 'iso9001' AND c.ClauseNumber = '4.1';

DROP TEMPORARY TABLE IF EXISTS tmp_clause_map;
