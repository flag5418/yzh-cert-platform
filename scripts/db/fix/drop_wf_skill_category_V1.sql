-- ============================================================
-- drop_wf_skill_category_V1.sql
-- 删除原技能分类表 wf_skill_category（2026-09-26）
--
-- 前置：skill_category_to_dictionary_V1.sql 已执行且验证通过
--   ① 备份表结构 + 数据（wf_skill_category_bak_20260926）
--   ② 删除表
--   ③ 验证（表不存在 + 字典数据在位）
--
-- 执行：docker exec -i -e MYSQL_PWD=... yzh-mysql mysql -uroot yzh_cert_platform < 本文件
-- ============================================================

-- ① 备份（结构 + 数据；重跑时先删旧备份）
DROP TABLE IF EXISTS wf_skill_category_bak_20260926;
CREATE TABLE wf_skill_category_bak_20260926 AS SELECT * FROM wf_skill_category;

-- ② 删表
DROP TABLE wf_skill_category;

-- ③ 验证（人工确认：① 0 行 ② 5 行）
SELECT 'VERIFY_table_dropped' AS chk, COUNT(*) AS cnt
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_skill_category';

SELECT 'VERIFY_dict_items' AS chk, COUNT(*) AS cnt
FROM Sys_DictionaryList l JOIN Sys_Dictionary d ON l.DicCode = d.Code
WHERE d.DicNo = 'skill_category';

SELECT 'VERIFY_backup_rows' AS chk, COUNT(*) AS cnt FROM wf_skill_category_bak_20260926;
