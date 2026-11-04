-- =============================================================================
-- 2026-11-03 cert_check_result.Conclusion 值域统一（落库四态 pass/ofi/nc/na）
-- -----------------------------------------------------------------------------
-- 背景：
--   前端历史上下传 LLM 原始结论值（conformant / observation / minor_nc / major_nc），
--   SaveResultAsync 原样落库 ⇒ 同一列出现两套词表：
--     ① LLM 路径写 pass/ofi/nc/na
--     ② 人工改判写 conformant/minor_nc/... ⇒ 「按结论筛选」与 NC 统计静默失效
--
-- 处置：
--   ① 后端 SaveResultAsync 已改为落库前过 CertVerdictMapper（唯一出口）
--   ② 前端下拉 / 统计 / 展示已统一为 pass/ofi/nc/na
--   ③ 本脚本：把存量脏值归一（幂等 + 先备份）
--
-- 【幂等】备份表已存在则复用；UPDATE 只命中非四态值，重跑为空操作。
-- =============================================================================
USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- 1. 备份（仅非四态值；幂等：表已存在则 INSERT IGNORE 补齐）
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `_bak_conclusion_20261103` LIKE `cert_check_result`;

INSERT IGNORE INTO `_bak_conclusion_20261103`
SELECT * FROM `cert_check_result`
WHERE `Conclusion` IS NOT NULL
  AND `Conclusion` NOT IN ('pass', 'ofi', 'nc', 'na');

-- -----------------------------------------------------------------------------
-- 2. 归一：LLM 原始值 → 落库四态（严重度缺失时按原结论反推）
-- -----------------------------------------------------------------------------
UPDATE `cert_check_result`
SET `Conclusion` = 'pass'
WHERE `Conclusion` IN ('conformant', 'conform', '符合');

UPDATE `cert_check_result`
SET `Conclusion` = 'ofi'
WHERE `Conclusion` IN ('observation', '观察项');

UPDATE `cert_check_result`
SET `Conclusion` = 'nc',
    `Severity`   = COALESCE(`Severity`, IF(`Conclusion` = 'minor_nc', 'minor', 'major'))
WHERE `Conclusion` IN ('minor_nc', 'major_nc', 'nonconform', '不符合');

-- 「待定」= 未判定，保持 NULL（四态之外没有「未知」这个合法值）
UPDATE `cert_check_result`
SET `Conclusion` = NULL
WHERE `Conclusion` IN ('unknown', '待定');

-- -----------------------------------------------------------------------------
-- 3. 验证（期望 C_违规 = 0）
-- -----------------------------------------------------------------------------
SELECT '结论分布（归一后）' AS Step;
SELECT IFNULL(`Conclusion`, '(NULL)') AS Conclusion,
       IFNULL(`Severity`, '(NULL)')   AS Severity,
       COUNT(*)                       AS C
FROM `cert_check_result`
GROUP BY `Conclusion`, `Severity`
ORDER BY C DESC;

SELECT '非法结论值（期望 0）' AS Step;
SELECT COUNT(*) AS C_违规
FROM `cert_check_result`
WHERE `Conclusion` IS NOT NULL
  AND `Conclusion` NOT IN ('pass', 'ofi', 'nc', 'na');
