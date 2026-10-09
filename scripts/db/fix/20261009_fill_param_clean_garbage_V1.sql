-- ============================================================
-- 企业资料参数 · 垃圾数据清理 V1（2026-10-09）
--
-- 背景：`cert_fill_param_def` 里混入了两条**纯测试残留**，
--       它们会出现在专家端「企业资料参数」页的真实清单里（`merge-list` 只按
--       `IsDeleted=0 AND IsValid=1` 过滤，不区分是不是测试数据），
--       也会出现在后台「填写规则」页的「全局参数」下拉里。
--
--   ┌─ 行 1：ParamCode='3333' / ParamName='333' ─────────────────────┐
--   │  IsDeleted=0 · IsValid=1  ⇒ **当前是活行**，正出现在「基础信息」组里 │
--   │  GroupName='基础信息' · IsBuiltin=0 · SortOrder=0               │
--   │  StandardCode='846dec4b…'（iso9001）—— 一条挂在标准下的空壳测试行  │
--   │  实测：merge-list 对「测试企业b × iso9001」返回 9 项，其中 1 项就是它 │
--   └────────────────────────────────────────────────────────────────┘
--   ┌─ 行 2：ParamCode='TEST_BIND_CHECK2' / ParamName='绑定探针2' ────┐
--   │  IsDeleted=1 · IsValid=0  ⇒ 已是软删态，但**行还在**              │
--   │  GroupName='基础信息' · 名字自证是自动化测试的绑定探针             │
--   └────────────────────────────────────────────────────────────────┘
--
-- 处置：**硬删**（不是软删）。理由：
--   ① 两条都是测试残留，无任何业务语义，软删只会让表继续变脏；
--   ② 代码里存在「唯一键撞已删行 ⇒ 复活」的分支（见 FillParamValueController.Save），
--      留着软删行等于给将来留一个「测试数据复活」的暗门；
--   ③ `cert_fill_param_value` 实测 0 行 ⇒ 无值行引用它们，删除不会产生孤儿。
--
-- 影响面（2026-10-09 实测）：
--   - cert_fill_param_def：19 行 → 17 行（活行 9 → 8）
--   - cert_fill_param_value：0 行，不受影响
--   - ⛔ 不碰那 9 条「档案镜像参数」（company_name 等）—— 它们是 20261006 迁移
--     **有意软删**的，是否恢复属另一项裁定，见 `61-填写规则完整分析与实施计划-V1.md`
--   - 无外键：`information_schema.KEY_COLUMN_USAGE` 查无任何引用 cert_fill_param_def 的约束
--
-- 备份：`scripts/db/backup/20261009_fill_param_garbage_backup.sql`
--       （mysqldump --no-create-info --skip-extended-insert，含 2 条完整 INSERT，可直接回灌）
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -p'<root密码>' yzh_cert_platform < 本文件
-- 执行后：跑文末「验证 SQL」，期望 ① 0 行 ② 17 行
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ═════════════════════════════════════════════════════════
-- 第 0 节：执行前取证（留痕在输出里，便于事后对账）
-- ═════════════════════════════════════════════════════════
SELECT '【清理前】待删行' AS stage, Id, ParamCode, ParamName, GroupName,
       IsBuiltin, IsValid, IsDeleted, IFNULL(StandardCode, '') AS StandardCode
FROM cert_fill_param_def
WHERE ParamCode IN ('3333', 'TEST_BIND_CHECK2');

-- ═════════════════════════════════════════════════════════
-- 第 1 节：硬删两条测试残留
-- ⚠️ 用 ParamCode + ParamName 双条件 —— 单靠 ParamCode 万一撞上真实数据就误删，
--    名字一起对上才动手（'3333'+'333' 与 'TEST_BIND_CHECK2'+'绑定探针2' 都不可能撞）
-- ═════════════════════════════════════════════════════════
DELETE FROM cert_fill_param_def
WHERE (ParamCode = '3333'             AND ParamName = '333')
   OR (ParamCode = 'TEST_BIND_CHECK2' AND ParamName = '绑定探针2');

-- ═════════════════════════════════════════════════════════
-- 第 2 节：兜底 —— 清掉同族测试残留（自动化测试前缀）
-- 判据：ParamCode 以 'TEST_' 或 'AUTOTEST' 开头，且从未被任何值行引用
-- ═════════════════════════════════════════════════════════
DELETE d FROM cert_fill_param_def d
WHERE (d.ParamCode LIKE 'TEST\_%' OR d.ParamCode LIKE 'AUTOTEST%')
  AND NOT EXISTS (
      SELECT 1 FROM cert_fill_param_value v WHERE v.ParamCode = d.ParamCode
  );

-- ═════════════════════════════════════════════════════════
-- 验证 SQL（执行后逐条跑）
-- ═════════════════════════════════════════════════════════

-- ① 垃圾行不得残留 —— 期望 0 行
SELECT '① 垃圾行残留' AS chk, COUNT(*) AS expect_0 FROM cert_fill_param_def
WHERE ParamCode IN ('3333', 'TEST_BIND_CHECK2')
   OR ParamCode LIKE 'TEST\_%' OR ParamCode LIKE 'AUTOTEST%';

-- ② 全表应为 17 行 —— 期望 17
SELECT '② 全表行数' AS chk, COUNT(*) AS expect_17 FROM cert_fill_param_def;

-- ③ 活行应为 8 行 —— 期望 8
SELECT '③ 活行行数' AS chk, COUNT(*) AS expect_8 FROM cert_fill_param_def
WHERE IsDeleted = 0 AND IsValid = 1;

-- ④ 9 条档案镜像参数应**原样保留**（仍是软删态）—— 期望 9
SELECT '④ 档案镜像参数(应仍为9)' AS chk, COUNT(*) AS expect_9 FROM cert_fill_param_def
WHERE ParamCode IN ('company_name','company_short_name','credit_code','legal_person',
                    'company_address','industry_type','cert_scope','employee_count','contact_name');

-- ⑤ 清理后的完整清单（人工过一眼）
SELECT ParamCode, ParamName, GroupName, IsRequired, IsBuiltin, IsValid, IsDeleted,
       IFNULL(StandardCode, '') AS StandardCode
FROM cert_fill_param_def
ORDER BY IsDeleted, IsValid DESC, SortOrder, Id;
