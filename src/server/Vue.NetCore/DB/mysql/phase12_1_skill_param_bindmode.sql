-- ============================================================
-- Skill V2.1 优化：参数绑定模式 + compare 重构 + get_field/get_table 移除
-- 1. 从 Skill 体系移除 get_field / get_table（改为前端特殊节点）
-- 2. 重构 compare 反射信息
-- 3. 新增 compare_operator 字典
-- ============================================================

-- ① 移除 get_field / get_table
DELETE FROM wf_skill_reflection WHERE skill_code IN ('get_field', 'get_table');
DELETE FROM wf_skill_input WHERE skill_code IN ('get_field', 'get_table');
DELETE FROM wf_skill_output WHERE skill_code IN ('get_field', 'get_table');
DELETE FROM wf_skill WHERE skill_code IN ('get_field', 'get_table');

-- ② 更新 compare 的反射信息（classPath 不变，methodName 不变，确保唯一索引不冲突）
DELETE FROM wf_skill_reflection WHERE skill_code = 'compare';
INSERT INTO wf_skill_reflection (code, skill_code, class_path, method_name, param_binding, enable, status, create_date, creator)
VALUES (UUID(), 'compare', 'YZH.Core.Skills.CompareSkill', 'ExecuteAsync', NULL, 1, 'active', NOW(), 'system');

-- ③ 更新 compare 主表信息
UPDATE wf_skill SET
  skill_name = '值比较',
  description = '确定性比较：支持数值比较（> >= < <= == !=）和日期比较（自动解析日期格式，按天计算差值）',
  return_type = 'boolean'
WHERE skill_code = 'compare';

-- ④ 新增 compare_operator 字典（挂载在 cert_dict 分类下，ParentId=107）
-- 创建字典分类（防重复）
INSERT INTO `Sys_Dictionary` (`DicNo`, `DicName`, `OrderNo`, `Remark`, `Enable`, `CreateDate`, `ParentId`)
SELECT 'compare_operator', '比较运算符', 210, 'compare Skill 的 operator 参数字典', 1, NOW(), 107
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `Sys_Dictionary` WHERE `DicNo` = 'compare_operator');

SET @dict_id = (SELECT `Dic_ID` FROM `Sys_Dictionary` WHERE `DicNo` = 'compare_operator');

-- 创建字典项（逐条防重复）
INSERT INTO `Sys_DictionaryList` (`Dic_ID`, `DicName`, `DicValue`, `OrderNo`, `Remark`, `Enable`, `CreateDate`)
SELECT @dict_id, '大于', '>', 10, 'a > b', 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicValue` = '>')
AND NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicName` = '大于');

INSERT INTO `Sys_DictionaryList` (`Dic_ID`, `DicName`, `DicValue`, `OrderNo`, `Remark`, `Enable`, `CreateDate`)
SELECT @dict_id, '大于等于', '>=', 20, 'a >= b', 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicValue` = '>=')
AND NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicName` = '大于等于');

INSERT INTO `Sys_DictionaryList` (`Dic_ID`, `DicName`, `DicValue`, `OrderNo`, `Remark`, `Enable`, `CreateDate`)
SELECT @dict_id, '小于', '<', 30, 'a < b', 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicValue` = '<')
AND NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicName` = '小于');

INSERT INTO `Sys_DictionaryList` (`Dic_ID`, `DicName`, `DicValue`, `OrderNo`, `Remark`, `Enable`, `CreateDate`)
SELECT @dict_id, '小于等于', '<=', 40, 'a <= b', 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicValue` = '<=')
AND NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicName` = '小于等于');

INSERT INTO `Sys_DictionaryList` (`Dic_ID`, `DicName`, `DicValue`, `OrderNo`, `Remark`, `Enable`, `CreateDate`)
SELECT @dict_id, '等于', '==', 50, 'a == b', 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicValue` = '==')
AND NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicName` = '等于');

INSERT INTO `Sys_DictionaryList` (`Dic_ID`, `DicName`, `DicValue`, `OrderNo`, `Remark`, `Enable`, `CreateDate`)
SELECT @dict_id, '不等于', '!=', 60, 'a != b', 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicValue` = '!=')
AND NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `Dic_ID` = @dict_id AND `DicName` = '不等于');
