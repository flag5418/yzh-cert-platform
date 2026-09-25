-- ============================================================================
-- cleanup_dict_test_data.sql — 测试夹具清理：物理删除 __T__ 前缀行（★ 写库）
-- ============================================================================
-- 用途：API 端到端测试会创建 `__T__` 前缀的临时数据（软删除后行仍保留），
--       本 SQL 负责**物理删除**这些测试残留，使测试可重复运行。
--
-- ⛔ 安全约束（铁律 B6：防止误删业务数据）：只删「字典 Name 以 __T__ 开头」的行。
--    必须放在 SQL 而不是 shell —— tree/add 未传 Code 时自动生成 32 位 GUID，
--    调用方拿到的 Code 不带 `__T__` 前缀，shell 按 Code 校验会恒拒绝（2026-09-25）。
--
-- 占位符：__DICT_CODE__ / __ITEM_CODE__
--         由 cleanup_dict_test_data.sh 用 sed 注入（铁律 B3：SQL 一律外置）。
--
-- 输出：最后一条 SELECT 给出「残留行数」，调用方据此判定清理是否彻底
--       （0 = 已清理；非 0 = 仍有残留，脚本须 exit 1）。
-- ============================================================================

DELETE l FROM Sys_DictionaryList l
 WHERE l.Code = '__ITEM_CODE__'
   AND EXISTS (SELECT 1 FROM Sys_Dictionary d
                WHERE d.Code = '__DICT_CODE__'
                  AND LEFT(d.DicName, 5) = '__T__');

DELETE d FROM Sys_Dictionary d
 WHERE d.Code = '__DICT_CODE__'
   AND LEFT(d.DicName, 5) = '__T__';

SELECT (SELECT COUNT(*) FROM Sys_Dictionary     WHERE Code = '__DICT_CODE__')
     + (SELECT COUNT(*) FROM Sys_DictionaryList WHERE Code = '__ITEM_CODE__') AS Remaining;
