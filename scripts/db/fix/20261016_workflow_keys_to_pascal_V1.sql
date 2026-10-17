-- =====================================================================
-- 20261016_workflow_keys_to_pascal_V1.sql
-- 用途：工作流跨边界命名统一（O2）· 边界 A（rule_json 外层键）幂等迁移
-- 依据：docs/40-实施/开发计划/工作流跨边界命名统一方案-V1.md §2.2
--       docs/40-实施/开发计划/工作流跨边界命名统一-TODO清单-V1.md P9
-- 裁决：用户 2026-10-16「D1-b 先做 O2」（存量全 NULL ⇒ 唯一零迁移窗口）
-- =====================================================================
-- ★ 前置判定（实测 2026-10-16）：
--   cert_validation_rule : 1 行，RuleJson  = NULL
--   cert_report_section  : 8 行，LayoutJson = NULL
--   ⇒ **当前无任何存量**，本脚本为「条件执行」模板：§2 顶层改名自动空跑，
--     §3 嵌套改名**不实现**（见其说明），§4 校验必须 PASS。
--
-- ⛔ 铁律：
--   1. **禁止全文 REPLACE**（会把说明正文里的 "value"/"nodeId" 一起改烂，P9.4）；
--      只允许 `JSON_SET` / `JSON_REMOVE` 精确改键。
--   2. 边界 F（LayoutJson 的 `nodePositions`）**不翻** —— 本脚本不触碰 LayoutJson。
--   3. 幂等：所有改名前先判 `JSON_CONTAINS_PATH(..., '$.小写键')`，重复执行 = 第二次空跑。
-- =====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── §1 备份（幂等：存在则先丢弃重建）──────────────────────────────────
DROP TABLE IF EXISTS `_bak_wfkeys_20261016_validation_rule`;
CREATE TABLE `_bak_wfkeys_20261016_validation_rule` AS
  SELECT `id`, `Code`, `RuleJson`, `LayoutJson` FROM `cert_validation_rule`;

DROP TABLE IF EXISTS `_bak_wfkeys_20261016_report_section`;
CREATE TABLE `_bak_wfkeys_20261016_report_section` AS
  SELECT `id`, `Code`, `LayoutJson` FROM `cert_report_section`;

-- ── §2 顶层键：camelCase → PascalCase（幂等；无存量时空跑）───────────────
--    口径：version→Version / workflowType→WorkflowType / nodes→Nodes /
--          edges→Edges / outputConfig→OutputConfig / glossary→Glossary
UPDATE `cert_validation_rule`
SET `RuleJson` = JSON_REMOVE(
      JSON_SET(
        JSON_SET(
          JSON_SET(
            JSON_SET(
              JSON_SET(
                JSON_SET(`RuleJson`,
                  '$.Version',      JSON_EXTRACT(`RuleJson`, '$.version')),
                '$.WorkflowType',   JSON_EXTRACT(`RuleJson`, '$.workflowType')),
              '$.Nodes',        JSON_EXTRACT(`RuleJson`, '$.nodes')),
            '$.Edges',        JSON_EXTRACT(`RuleJson`, '$.edges')),
          '$.OutputConfig', JSON_EXTRACT(`RuleJson`, '$.outputConfig')),
        '$.Glossary',     JSON_EXTRACT(`RuleJson`, '$.glossary')),
      '$.version', '$.workflowType', '$.nodes', '$.edges', '$.outputConfig', '$.glossary')
WHERE `RuleJson` IS NOT NULL
  AND JSON_VALID(`RuleJson`)
  AND JSON_CONTAINS_PATH(`RuleJson`, 'one',
        '$.version', '$.workflowType', '$.nodes', '$.edges', '$.outputConfig', '$.glossary');

-- ── §3 嵌套键：nodes[] / edges[] 元素键（⚠️ 当前不实现，见下）───────────
--    需要 nodeId→NodeId / nodeType→NodeType / skillCode→SkillCode / title→Title /
--         description→Description / config→Config / inputs→Inputs / inputTypes→InputTypes /
--         outputs→Outputs / inputPorts→InputPorts / outputPorts→OutputPorts（nodes[] 内）
--    以及 source→Source / target→Target / sourceHandle→SourceHandle / targetHandle→TargetHandle（edges[] 内）。
--
--    ★ 为什么不写进本脚本：MySQL 8 的 `JSON_SET` **不支持数组通配路径**（`$.nodes[*].nodeId`），
--      唯一纯 SQL 路径是 `JSON_TABLE` 展开 → `JSON_ARRAYAGG` 重建 —— 而 `JSON_ARRAYAGG`
--      **无法保证元素顺序**（8.0 不支持 `ORDER BY`），会打乱 nodes/edges 次序。
--      workflow 落库虽不依赖数组次序（拓扑由 edges 决定），但重建风险 > 收益。
--
--    ★ 触发条件：仅当 §4 校验发现 `RuleJson LIKE '%"nodeId"%'` > 0 时才需要。
--      届时按方案 §2.2 用**应用侧迁移**（C# 读 JSON → 改名 → 写回，保序可控），
--      或升级到 MySQL 8.0.14+ 的 `JSON_TABLE` + 显式序号列排序后再 `JSON_ARRAYAGG`。
--      ⛔ 不得用 `REPLACE(RuleJson, '"nodeId"', '"NodeId"')` 之类的粗糙替换蒙混过关。
SELECT '§3 嵌套键迁移：当前未实现（存量=0 不需要）；若触发请走应用侧迁移' AS note;

-- ── §4 校验（P9.3；两项都必须为 0）───────────────────────────────────
SELECT
  (SELECT COUNT(*) FROM `cert_validation_rule`
     WHERE `RuleJson` IS NOT NULL
       AND (JSON_CONTAINS_PATH(`RuleJson`, 'one',
              '$.version', '$.workflowType', '$.nodes', '$.edges',
              '$.outputConfig', '$.glossary')
            OR `RuleJson` LIKE '%"nodeId"%' OR `RuleJson` LIKE '%"nodeType"%'
            OR `RuleJson` LIKE '%"skillCode"%' OR `RuleJson` LIKE '%"sourceHandle"%'))
  AS `C_rulejson_小写键残留_期望0`;

SELECT
  (SELECT COUNT(*) FROM `cert_validation_rule`
     WHERE `RuleJson` IS NOT NULL AND JSON_VALID(`RuleJson`) = 0)
  AS `C_rulejson_非法JSON_期望0`;

-- ── §5 回滚说明 ───────────────────────────────────────────────────────
--   回滚（仅在 §2 误改时）：
--     UPDATE cert_validation_rule r
--       JOIN _bak_wfkeys_20261016_validation_rule b ON b.id = r.id
--       SET r.RuleJson = b.RuleJson, r.LayoutJson = b.LayoutJson;
--     UPDATE cert_report_section s
--       JOIN _bak_wfkeys_20261016_report_section b ON b.id = s.id
--       SET s.LayoutJson = b.LayoutJson;
--     -- 确认无误后 DROP TABLE _bak_wfkeys_20261016_*;
