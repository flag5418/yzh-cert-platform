-- ============================================================
-- skill_category_to_dictionary_V1.sql
-- 技能分类字典化迁移（2026-09-26）
--
-- 背景：
--   · wf_skill_category.Code 为 GUID，页面/关联不语义（用户裁决：分类改字典管理）
--   · 分类改由字典 DicNo='skill_category' 唯一维护（字典管理页面）
--   · skill.CategoryCode 关联值 = 字典项 DicValue（data_access 等，iso_category 同款先例）
--
-- 内容（按序执行）：
--   ① 建字典类型 skill_category（挂 cert_dict 根分类下）
--   ② 迁移 5 条分类 → 字典项（DicName/Color/SortOrder 从旧表取）
--   ③ wf_skill.CategoryCode：旧分类 GUID → DicValue 语义值
--   ④ 验证输出（数量 + 无 GUID 残留）
--
-- ⚠️ 与 drop_wf_skill_category_V1.sql 配套：本脚本先跑，确认无误后跑 DROP 脚本
-- 执行：docker exec -i -e MYSQL_PWD=... yzh-mysql mysql -uroot yzh_cert_platform < 本文件
-- ============================================================

-- 幂等：重跑先清理本字典（字典项随 DicCode 一并清理，skill 关联更新不回滚）
DELETE l FROM Sys_DictionaryList l
JOIN Sys_Dictionary d ON l.DicCode = d.Code
WHERE d.DicNo = 'skill_category';

DELETE FROM Sys_Dictionary WHERE DicNo = 'skill_category';

-- ① 建字典类型（挂「认证平台字典」根分类下，与 iso_category / doc_skill 同级）
SET @parent_code = (SELECT Code FROM Sys_Dictionary WHERE DicNo = 'cert_dict' LIMIT 1);
SET @dic_code = REPLACE(UUID(), '-', '');

INSERT INTO Sys_Dictionary (Code, DicName, DicNo, ParentCode, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy, Remark)
VALUES (@dic_code, '技能分类', 'skill_category', @parent_code, 210, 1, 0, NOW(), 'migration', '技能管理左树分类的数据源（原 wf_skill_category 迁入）');

-- ② 迁移分类 → 字典项（DicValue = 原业务编码 data_access 等 = skill 关联值）
INSERT INTO Sys_DictionaryList (Code, DicName, DicValue, DicCode, Color, OrderNo, Remark, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT REPLACE(UUID(), '-', ''),
       Name,
       CategoryCode,
       @dic_code,
       Color,
       SortOrder * 10,
       '原 wf_skill_category 迁移（2026-09-26）',
       1,
       0,
       NOW(),
       'migration'
FROM wf_skill_category
WHERE IsDeleted = 0
ORDER BY SortOrder, Id;

-- ③ 技能关联值：分类 GUID → DicValue（幂等：已迁移行 join 不上，0 行影响）
UPDATE wf_skill s
JOIN wf_skill_category c ON s.CategoryCode = c.Code
SET s.CategoryCode = c.CategoryCode
WHERE s.CategoryCode <> c.CategoryCode;

-- ④ 验证（人工确认：① 5 行 ② 0 行）
SELECT 'VERIFY_dict_items' AS chk, COUNT(*) AS cnt FROM Sys_DictionaryList WHERE DicCode = @dic_code;
SELECT 'VERIFY_skill_guid_residual' AS chk, COUNT(*) AS cnt FROM wf_skill
WHERE CategoryCode REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$';
SELECT 'VERIFY_skill_category_dist' AS chk, CategoryCode, COUNT(*) AS cnt FROM wf_skill GROUP BY CategoryCode;
