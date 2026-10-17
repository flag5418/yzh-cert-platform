-- ============================================================================
--  20261016_contract_role_to_directory_V1.sql
--
--  依据：`docs/40-实施/企业资料规范化/67-文档约束与依赖企业文档-字段收敛设计-V1.md` §7
--  触发：2026-10-16 用户裁决①「是否依赖企业文档 = 是否可替换，同一件事」
--
--  ════════════════════════════════════════════════════════════════════════════
--  目的
--  ════════════════════════════════════════════════════════════════════════════
--  把「是否依赖企业文档」的**权威值**从【契约表】迁到【标准目录文件表】。
--
--  背景（代码实测，非推测）：
--    · 页面（`ContractTab.vue`「是否可替换」）写的是
--      `cert_standard_doc_contract.FixedDocSubtype`；
--    · 编排器（`DocumentFillOrchestrator.cs` 四路分流）读的是
--      `cert_standard_directory_file.Replaceable`。
--    ⇒ 两列**永不同步** ⇒ 「页面上配了、运行期不生效」的静默失效
--      （本项目最坏的失败形态：不报错、不抛异常，只是结果与配置无关）。
--
--  本次把契约表的既有值搬到目录表（**标准行**），随后**物理删除**契约表两列。
--
--  ★ 顺序即安全：**先迁（③）→ 再删（⑤）**。两段在同一个脚本里，不可拆开单独执行。
--
--  ════════════════════════════════════════════════════════════════════════════
--  执行前置
--  ════════════════════════════════════════════════════════════════════════════
--  ⚠️ 先备份：`mysqldump ... cert_standard_doc_contract cert_standard_directory_file`
--  ⚠️ **幂等**：可重复执行，结果不变（按值重算，非增量）。
--  ⚠️ **只更新标准行**（`EnterpriseCode` 为空）—— 企业实例行不覆盖。
--  ⚠️ 归属模型 V2：判据 = `EnterpriseCode` 非空 ⇒ 企业行；两列为空 ⇒ 平台标准级。
-- ============================================================================

-- ────────────────────────────────────────────────────────────────────────────
--  ① 核对（只读，先看再改）
--     期望：能看到 standard_provided / enterprise_provided 的分布。
--     ★ 若返回 0 行 ⇒ 从未有人在页面上配过该字段，② 会是 0 行更新，属正常。
-- ────────────────────────────────────────────────────────────────────────────
SELECT c.FixedDocSubtype,
       COUNT(*) AS Cnt
FROM cert_standard_doc_contract c
WHERE c.IsDeleted = 0
GROUP BY c.FixedDocSubtype;

-- ② 顺带看清「契约行 ↔ 目录标准行」的对应关系（迁移前对账用）
SELECT COUNT(*) AS 可迁移行数
FROM cert_standard_doc_contract c
JOIN cert_standard_directory_file f
  ON f.Code = c.StandardFileCode
WHERE c.IsDeleted = 0
  AND f.IsDeleted = 0
  AND (f.EnterpriseCode IS NULL OR f.EnterpriseCode = '')
  AND c.FixedDocSubtype IN ('standard_provided', 'enterprise_provided');

-- ────────────────────────────────────────────────────────────────────────────
--  ③ 迁移（幂等）
--
--     口径映射：`enterprise_provided` ⇒ `Replaceable = 1`（依赖企业文档）
--               `standard_provided`   ⇒ `Replaceable = 0`（不依赖，标准自带）
--
--     ⚠️ 只命中标准行；企业实例行的 `Replaceable` 保持原值不动
--        （企业行不该配这个维度 —— 它按 `StandardFileCode` 回标准行读）。
-- ────────────────────────────────────────────────────────────────────────────
UPDATE cert_standard_directory_file f
JOIN cert_standard_doc_contract c
  ON c.StandardFileCode = f.Code
 AND c.IsDeleted = 0
SET f.Replaceable = (c.FixedDocSubtype = 'enterprise_provided'),
    f.UpdateTime  = NOW(),
    f.UpdateBy    = 'migration_20261016_contract_role'
WHERE f.IsDeleted = 0
  AND (f.EnterpriseCode IS NULL OR f.EnterpriseCode = '')
  AND c.FixedDocSubtype IN ('standard_provided', 'enterprise_provided');

-- ────────────────────────────────────────────────────────────────────────────
--  ④ 校验（迁移后）
--     · 第一段：目录标准行的两档分布
--     · 第二段：**分叉对账** —— 期望 `分叉行数 = 0`
--       （若 > 0，说明有契约行指向了不存在的目录行，需人工排查）
-- ────────────────────────────────────────────────────────────────────────────
SELECT SUM(f.Replaceable = 1) AS 依赖企业文档,
       SUM(f.Replaceable = 0) AS 不依赖企业文档,
       COUNT(*)               AS 标准行总数
FROM cert_standard_directory_file f
WHERE f.IsDeleted = 0
  AND (f.EnterpriseCode IS NULL OR f.EnterpriseCode = '');

SELECT COUNT(*) AS 分叉行数
FROM cert_standard_doc_contract c
WHERE c.IsDeleted = 0
  AND c.FixedDocSubtype IN ('standard_provided', 'enterprise_provided')
  AND NOT EXISTS (
        SELECT 1 FROM cert_standard_directory_file f
        WHERE f.Code = c.StandardFileCode
          AND f.IsDeleted = 0
          AND (f.EnterpriseCode IS NULL OR f.EnterpriseCode = '')
      );

-- ────────────────────────────────────────────────────────────────────────────
--  ⑤ **物理删除**两列（2026-10-16 用户裁决②：「删除，保留了可能误导」）
--
--     ⛔ 不采用「留一版 + 改注释」——
--        留着的列**就是下一次踩坑的诱饵**：新人照列名写代码，会再次造出
--        「页面写契约表、编排器读目录表」的分叉。删除才是真正的收敛。
--
--     ★ 删除前置（本脚本已保证顺序）：
--        ③ 已把 `FixedDocSubtype` 的值迁到 `Replaceable` ⇒ **先迁后删，不丢数据**。
--        ⚠️ 若跳过 ③ 直接跑本段，会丢失「是否依赖企业文档」的历史配置。
--
--     ⚠️ `DocRole` 的 4 值**不做迁移**（`reference` / `attachment` 语义已取消，
--        与新的 2 值「必要 / 非必要」不完全等价）⇒ 交人工在页面重配。
--        现状核对：契约表**仅 3 行**（2026-10-16 实测），人工重配成本可忽略。
--
--     ⚠️ 幂等：MySQL 8 的 `DROP COLUMN` **不支持** `IF EXISTS`
--        （那是 MariaDB 语法）⇒ 用 `information_schema` 预检 + 动态 SQL。
-- ────────────────────────────────────────────────────────────────────────────

-- ⑤-1 DocRole
SET @ddl := (
  SELECT IF(COUNT(*) > 0,
    'ALTER TABLE `cert_standard_doc_contract` DROP COLUMN `DocRole`',
    'SELECT ''[skip] DocRole 已不存在'' AS msg')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_standard_doc_contract'
    AND COLUMN_NAME  = 'DocRole'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ⑤-2 FixedDocSubtype
SET @ddl := (
  SELECT IF(COUNT(*) > 0,
    'ALTER TABLE `cert_standard_doc_contract` DROP COLUMN `FixedDocSubtype`',
    'SELECT ''[skip] FixedDocSubtype 已不存在'' AS msg')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_standard_doc_contract'
    AND COLUMN_NAME  = 'FixedDocSubtype'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ⑤-3 回读验证：期望 **0 行**（两列已从物理表消失）
SELECT COLUMN_NAME AS 残留列
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME   = 'cert_standard_doc_contract'
  AND COLUMN_NAME IN ('DocRole', 'FixedDocSubtype');

-- ============================================================================
--  执行示例
-- ============================================================================
--  docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 \
--    yzh_cert_platform < scripts/db/fix/20261016_contract_role_to_directory_V1.sql
-- ============================================================================
