-- ============================================================================
-- verify_dict_softdelete.sql — 数据字典 · 软删除语义核对（★ 只读）
-- ============================================================================
-- 用途：API 端到端测试需要核对「删除接口是否只做软删除」——
--       这是 API 副作用，必须看库才能验证，属**正当的只读核对**。
--
-- 占位符：__DICT_CODE__ / __ITEM_CODE__
--         由 verify_dict_softdelete.sh 用 sed 注入（脚本只传参，不持有 SQL）。
--         遵循 scripts/README.md 铁律 B3（SQL 一律外置）。
--
-- 输出（制表符分隔）：
--   第 1 行：字典   IsDeleted|DeleteBy|DeleteTime
--   第 2 行：字典项 IsDeleted
-- ============================================================================

SELECT CONCAT(IsDeleted, '|', IFNULL(DeleteBy, 'NULL'), '|', IFNULL(DeleteTime, 'NULL'))
  FROM Sys_Dictionary
 WHERE Code = '__DICT_CODE__';

SELECT IsDeleted
  FROM Sys_DictionaryList
 WHERE Code = '__ITEM_CODE__';
