-- ====================================================================
-- 20261106_fix_zerodate_role_dict_V1.sql
--
-- 修复：角色树 / 字典列表读取报 "xxx bind error"，导致整表查不出
--   —— role-user 页面刷新后「任何角色都不显示」、Role/dictionary 相关
--      /filter /tree/root /tree/children /list 全部返回空列表。
--
-- 根因（与 20261105_fix_organization_zerodate_V1.sql 同源）：
--   种子脚本（20261105_disaster_seed_minimal_V1.sql 等）INSERT 时
--   只写 CreateTime，未写 UpdateTime / DeleteTime；而 CodeFirst 当时
--   把 C# DateTime? 列建成了 datetime NOT NULL ⇒ MySQL 写入零日期
--   0000-00-00 00:00:00 ⇒ SqlSugar 把该值映射到 DateTime? 时抛
--   「UpdateTime绑定到Sys_Role失败」⇒ EntityService.GetListAsync 整体
--   失败并返回空列表（异常被 catch 吞掉，接口仍 success=true）。
--
-- 影响表（全库扫描结果，2026-11-06）：
--   Sys_Role.UpdateTime(4) Sys_Role.DeleteTime(4)
--   Sys_Dictionary.UpdateTime(2) Sys_Dictionary.DeleteTime(2)
--   Sys_DictionaryList.UpdateTime(4) Sys_DictionaryList.DeleteTime(4)
--
-- 幂等：先 ALTER 放宽 NULL（当前已是 NULL，重复运行无副作用）
--       → 备份 → SET sql_mode='' 清零日期 → 恢复严格模式 → 验评。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ① 放宽列 NULL 属性（DateTime? 对应的列必须允许 NULL）
ALTER TABLE Sys_Role MODIFY UpdateTime datetime NULL, MODIFY DeleteTime datetime NULL;
ALTER TABLE Sys_Dictionary MODIFY UpdateTime datetime NULL, MODIFY DeleteTime datetime NULL;
ALTER TABLE Sys_DictionaryList MODIFY UpdateTime datetime NULL, MODIFY DeleteTime datetime NULL;

-- ② 备份原始行（便于回滚核对；已存在则先删旧备份）
--    ★ 必须先 SET sql_mode=''：严格模式下 CREATE TABLE ... AS SELECT 读到零日期
--      会直接报 1292 Incorrect datetime value（2026-11-06 实测）。
SET sql_mode = '';

DROP TABLE IF EXISTS _bak_zerodate_20261106_role;
DROP TABLE IF EXISTS _bak_zerodate_20261106_dict;
DROP TABLE IF EXISTS _bak_zerodate_20261106_dictlist;
CREATE TABLE _bak_zerodate_20261106_role AS SELECT * FROM Sys_Role;
CREATE TABLE _bak_zerodate_20261106_dict AS SELECT * FROM Sys_Dictionary;
CREATE TABLE _bak_zerodate_20261106_dictlist AS SELECT * FROM Sys_DictionaryList;

-- ③ 清零日期（严格模式下 WHERE col < '1000-01-01' 会被拒绝，故临时清空 sql_mode）

UPDATE Sys_Role SET UpdateTime = NULL WHERE UpdateTime < '1000-01-01';
UPDATE Sys_Role SET DeleteTime = NULL WHERE DeleteTime < '1000-01-01';
UPDATE Sys_Dictionary SET UpdateTime = NULL WHERE UpdateTime < '1000-01-01';
UPDATE Sys_Dictionary SET DeleteTime = NULL WHERE DeleteTime < '1000-01-01';
UPDATE Sys_DictionaryList SET UpdateTime = NULL WHERE UpdateTime < '1000-01-01';
UPDATE Sys_DictionaryList SET DeleteTime = NULL WHERE DeleteTime < '1000-01-01';

-- ④ 恢复严格模式（后续 INSERT 若仍不显式写时间，框架会 DateTime.Now 填补，不会再生零日期）
SET sql_mode = 'STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

-- ⑤ 验评：三张表零日期计数必须为 0
SELECT 'Sys_Role' AS TableName,
       CONVERT(COUNT(*) USING utf8mb4) COLLATE utf8mb4_bin AS ZeroDateRows
FROM Sys_Role
WHERE (UpdateTime IS NOT NULL AND UpdateTime < '1000-01-01')
   OR (DeleteTime IS NOT NULL AND DeleteTime < '1000-01-01')
UNION ALL
SELECT 'Sys_Dictionary',
       CONVERT(COUNT(*) USING utf8mb4) COLLATE utf8mb4_bin
FROM Sys_Dictionary
WHERE (UpdateTime IS NOT NULL AND UpdateTime < '1000-01-01')
   OR (DeleteTime IS NOT NULL AND DeleteTime < '1000-01-01')
UNION ALL
SELECT 'Sys_DictionaryList',
       CONVERT(COUNT(*) USING utf8mb4) COLLATE utf8mb4_bin
FROM Sys_DictionaryList
WHERE (UpdateTime IS NOT NULL AND UpdateTime < '1000-01-01')
   OR (DeleteTime IS NOT NULL AND DeleteTime < '1000-01-01');

-- ⑥ 验评：当前角色清单（含重复数据，等待人工确认清理）
SELECT Code, RoleName, ParentCode, IsValid, IsDeleted, CreateTime, UpdateTime
FROM Sys_Role
ORDER BY Id;
