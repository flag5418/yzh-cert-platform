-- ══════════════════════════════════════════════════════════════════════
-- 20261124_clause_attr_add_clausetext_V1.sql
-- 目的：为 RM7（条款释义语义召回）灌入「条款正文」属性 ClauseText
--   ① cert_clause_attr_def 增加定义行（AttrCode = ClauseText）
--   ② cert_clause_attr 按 cert_iso_clause.Description 生成 ClauseText 行（值为 JSON）
-- 依据：docs/80-v4/16-条款释义匹配方案-V1.md §二 / §三
-- 幂等：删除 + 重灌；定义行用 NOT EXISTS 保护
-- ══════════════════════════════════════════════════════════════════════
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ① 属性定义行（AttrName=条款正文，值类型 json）
INSERT INTO cert_clause_attr_def
  (AttrName, ValueType, DictCode, Remark, PromptHint, Sort, IsValid, IsDeleted, Status, Code, CreateTime)
SELECT '条款正文', 'json', '',
       '标准条款全文（Title/Body/Items/Notes），供 RM7 语义召回',
       '值为 JSON 对象；字段可空，匹配取 Title+Body+Items+Notes 连接',
       130, 1, 0, 'active', 'ClauseText', NOW()
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM cert_clause_attr_def WHERE Code = 'ClauseText');

-- ② 清旧数据（幂等）
DELETE FROM cert_clause_attr WHERE AttrCode = 'ClauseText';

-- ③ 灌数据：cert_iso_clause.Description → JSON {Title,Body,Items,Notes}
--    Title = 条款号 + 标题；Body = 条款正文；Items/Notes 暂空（字段可扩展）
INSERT INTO cert_clause_attr
  (StandardCode, ClauseCode, ClauseNumber, AttrCode, ValueSource, AttrValue, Sort,
   IsValid, IsDeleted, Status, Code, CreateTime)
SELECT a.StandardCode, a.ClauseCode, a.ClauseNumber, 'ClauseText', 'fixed',
       JSON_OBJECT(
         'Title', CONCAT(c.ClauseNumber, ' ', COALESCE(c.Title, '')),
         'Body',  COALESCE(c.Description, ''),
         'Items', JSON_ARRAY(),
         'Notes', JSON_ARRAY()
       ),
       COALESCE(a.Sort, 0), 1, 0, 'active', REPLACE(UUID(), '-', ''), NOW()
FROM cert_clause_attr a
JOIN cert_iso_clause c ON c.Code = a.ClauseCode AND c.IsDeleted = 0
WHERE a.AttrCode = 'ClauseIntent'
  AND a.IsDeleted = 0
  AND COALESCE(c.Description, '') <> '';

-- ④ 验证：期望三标准均有数据（iso9001 约 68，合计 ≈ 185）
SELECT AttrCode, COUNT(*) AS C_行数 FROM cert_clause_attr WHERE AttrCode = 'ClauseText' GROUP BY AttrCode;
