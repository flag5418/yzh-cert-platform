-- ============================================================
-- 修复：cert_org_standard 存了业务编号 slug，而契约是 cert_iso_standard.Code（GUID）
--
-- 背景（2026-10-09 用户报障）：「勾选新的医疗器械标准，但没有保存成功」
--   根因 1：前端 save 传 StandardCode=item.StandardCode（slug，如 iso13485），
--           而 List / 专家端 EnterpriseStageController 均按 p.Code（GUID）比对
--           ⇒ 库里有行、回显永远未勾选。
--   根因 2：软删除 + uk_org_std(OrgCode,StandardCode) 全行唯一
--           ⇒ 「取消勾选后再勾选」必撞唯一键（实体已改硬删除，见 CertOrgStandard.cs）。
--
-- 执行：
--   docker exec -i yzh-mysql mysql -uroot -pYzh123456. \
--     --default-character-set=utf8mb4 yzh_cert_platform \
--     < scripts/db/fix/20261009_org_standard_slug_to_code_V1.sql
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── 1) iso13485 → cert_iso_standard.Code（无歧义，用户本次目标） ──
UPDATE cert_org_standard
SET StandardCode = 'dfca5483-f83c-4999-b773-3624de5f7cf7'
WHERE StandardCode = 'iso13485' AND IsDeleted = 0;

-- ── 2) iso9001 → 4eeb8cb0（推断：该机构已有 846dec4b 的 iso9001 关联行，
--        用户勾选的必然是当时显示为未勾选的另一条「质量体系认证」；
--        若推断有误，在页面取消勾选即可） ──
UPDATE cert_org_standard
SET StandardCode = '4eeb8cb0ce264a74aa022b68b275001d'
WHERE StandardCode = 'iso9001' AND IsDeleted = 0;

-- ── 3) 清理历史软删除行（硬删除改造后不再产生；软行占 uk 会挡住重新勾选） ──
DELETE FROM cert_org_standard WHERE IsDeleted = 1;

-- ── 4) 验证：应无 slug 残留、无软删行、总行数 = 有效 GUID 行 ──
SELECT Id, OrgCode, StandardCode, IsDeleted
FROM cert_org_standard ORDER BY Id;

SELECT COUNT(*) AS slug_residual
FROM cert_org_standard
WHERE StandardCode REGEXP '^(iso|cat_)';

SELECT COUNT(*) AS soft_deleted_rows
FROM cert_org_standard
WHERE IsDeleted = 1;
