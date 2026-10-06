-- ============================================================================
-- fix_doc_fill_comment_V1.sql
--   目的：修正「标准文档填写规则」模块 3 处**列注释与代码/设计定稿不一致**的问题。
--         列类型 / 默认值一律保持原样，⛔ 只改 COMMENT（不动数据、不动结构语义）。
--
--   背景（2026-10-05 用户裁定「数据库结构错误必须修复」）：
--     ① cert_standard_doc_contract.FixedDocSubtype
--        DDL 注释写的是 platform_generated（= 平台自动生成），
--        但 49-V3 §2.2 已裁定（D-AA1）枚举为 standard_provided / enterprise_provided，
--        代码（StandardDocContractController 白名单）与全部原型（V7 / V8 / 51）也都用 standard_provided。
--        ⇒ 注释是错的，会让后来者按注释实现出一个永远匹配不上的值。
--        ⚠️ 默认值 enterprise_provided **保留不动**（49-V3 §2.2 明确「DB 不动，页面让用户显式确认」）。
--
--     ② cert_standard_doc_contract.AnalyzeStatus
--        DDL 注释缺 partial（2026-10-05 新增：两跳提示词只成功一跳 ⇒ 结论部分可用）。
--        ⇒ 注释缺取值会让「标签拿到了、作用没拿到」与「什么都没拿到」在文档上变成同一种状态。
--
--     ③ cert_doc_template_anchor.IsLocked
--        旧注释写「已认可…换模板重扫时保留配置」= 只描述了一个副作用。
--        用户 2026-10-05 裁定「锁定的问题，就是不能再修改配置」
--        ⇒ 注释必须表达**主语义**（配置冻结、保存一律拒绝），否则与后端强制行为不符。
--
--   幂等：MODIFY COLUMN 重复执行结果相同，可安全重跑。
--   执行：
--     docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 \
--       yzh_cert_platform < scripts/db/fix_doc_fill_comment_V1.sql
-- ============================================================================

-- ① cert_standard_doc_contract.FixedDocSubtype --------------------------------
ALTER TABLE `cert_standard_doc_contract`
  MODIFY COLUMN `FixedDocSubtype` varchar(20) NOT NULL DEFAULT 'enterprise_provided'
  COMMENT '【fixed 专用】可替换性（人工判断，D-AA1 裁定，⛔ 程序不推导）：standard_provided=标准自带(不向企业索取) / enterprise_provided=企业提供(必须给匹配依据)。非 fixed 行忽略此列';

-- ② cert_standard_doc_contract.AnalyzeStatus ---------------------------------
ALTER TABLE `cert_standard_doc_contract`
  MODIFY COLUMN `AnalyzeStatus` varchar(20) NOT NULL DEFAULT 'pending'
  COMMENT '语义分析状态：pending / running / completed / partial / failed / manual（人工直接写入）。partial=两跳提示词(doc_group/doc_content)只成功一跳，结论部分可用，失败原因落 AnalyzeMessage';

-- ③ cert_doc_template_anchor.IsLocked ---------------------------------------
ALTER TABLE `cert_doc_template_anchor`
  MODIFY COLUMN `IsLocked` tinyint(1) NOT NULL DEFAULT 0
  COMMENT '★锚点锁定=实施人员已认可、配置就此冻结：⛔ 不可再修改配置（save-batch/UpdateCore 命中锁定行且配置有变化一律拒绝，Clear 跳过锁定行），解锁走 lock 端点。换版重扫时同唯一键锚点由软删+复活自动保留配置';

-- ============================================================================
-- 回读验证：三列的类型 / 可空 / 默认值 / 注释
-- ============================================================================
SELECT '[1] FixedDocSubtype' AS step, TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE,
       COLUMN_DEFAULT, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_standard_doc_contract' AND COLUMN_NAME = 'FixedDocSubtype';

SELECT '[2] AnalyzeStatus' AS step, TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE,
       COLUMN_DEFAULT, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_standard_doc_contract' AND COLUMN_NAME = 'AnalyzeStatus';

SELECT '[3] IsLocked' AS step, TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE,
       COLUMN_DEFAULT, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_doc_template_anchor' AND COLUMN_NAME = 'IsLocked';
