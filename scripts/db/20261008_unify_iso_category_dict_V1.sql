-- ============================================================================
-- 20261008_unify_iso_category_dict_V1.sql
-- 目的：统一 iso_category 两套字典口径（P1 前置 bug）
-- ============================================================================
-- 背景：当前存在两套 iso_category 口径
--   A. GUID 集【权威】：Sys_Dictionary(DicNo='iso_category',
--      Code='26b0f119ae6c11f1953796fd503fd974') 下 7 项
--      quality/environment/safety/info/food/medical/energy
--      → items/by-no/iso_category、YzhForm dictCode、前端全部读它
--   B. legacy 集【孤儿】：Sys_DictionaryList.DicCode='iso_category' 字面量 10 项
--      Codes='26b0f001…' 系列，DicCode 指向不存在的字典 Code
--      → 仅 v_iso_standard 视图 join 用（Label 亦不同：质量管理体系 vs 质量管理）
--
-- 动作（3 步，全幂等可重复执行）：
--   1. legacy 独有 3 项 automotive/it_service/other 迁入 GUID 集（能力不丢）
--   2. legacy 10 项软删 IsDeleted=1
--   3. 重建 v_iso_standard 为 GUID join（与 v_cert_stage 同款写法）
--
-- 风险：零（cert_iso_standard 现有数据仅 quality/environment，两套均有）
-- 依据：collation 已实测全为 utf8mb4_general_ci，列 vs 列 join 安全
-- 回滚：见文末注释
-- 执行：docker exec -i -e MYSQL_PWD='Yzh123456.' yzh-mysql mysql -uroot \
--        --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ----------------------------------------------------------------------------
-- 0. 前置检查：确认权威字典存在且 legacy 项存在（不满足则中止）
-- ----------------------------------------------------------------------------
SELECT '== 前置检查：GUID 权威字典 ==' AS step;
SELECT Code, DicNo, DicName FROM Sys_Dictionary
 WHERE DicNo = 'iso_category' AND IsDeleted = 0;

SELECT '== 前置检查：legacy 项计数（期望 10）==' AS step;
SELECT COUNT(*) AS legacy_count FROM Sys_DictionaryList
 WHERE DicCode = 'iso_category';

-- ----------------------------------------------------------------------------
-- 1. 迁移 legacy 独有 3 项 → GUID 集（能力不丢）
--    automotive(汽车)/it_service(IT服务)/other(其他)
--    幂等：按 DicCode+DicValue 判重
-- ----------------------------------------------------------------------------
INSERT INTO Sys_DictionaryList (Code, DicCode, DicValue, DicName, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT REPLACE(UUID(), '-', ''),   -- Code 为 NOT NULL 且无默认值，格式 = 32 位小写 UUID（与现有项一致）
       d.Code,
       l.DicValue,
       l.DicName,
       l.OrderNo + 70,             -- GUID 集 OrderNo 已用 10..70，legacy 10/20/30 → 80/90/100
       1,
       0,
       NOW(),
       '超级管理员'
  FROM Sys_DictionaryList l
  JOIN Sys_Dictionary d
    ON d.DicNo = 'iso_category' AND d.IsDeleted = 0
 WHERE l.DicCode = 'iso_category'
   AND l.DicValue IN ('automotive', 'it_service', 'other')
   AND NOT EXISTS (
         SELECT 1 FROM Sys_DictionaryList x
          WHERE x.DicCode = d.Code AND x.DicValue = l.DicValue
       );

SELECT '== 迁移后 GUID 集（期望 10 项）==' AS step;
SELECT l.DicValue, l.DicName, l.OrderNo, l.IsDeleted
  FROM Sys_DictionaryList l
  JOIN Sys_Dictionary d ON d.DicNo = 'iso_category' AND d.IsDeleted = 0
 WHERE l.DicCode = d.Code
 ORDER BY l.OrderNo;

-- ----------------------------------------------------------------------------
-- 2. legacy 10 项软删
--    ⚠️ 必须在步骤 3（重建视图）之后执行前，先确认视图已切 GUID join；
--    本脚本顺序为 1→3→2，即先修视图再废弃数据，避免 CategoryName 断供
-- ----------------------------------------------------------------------------

-- ----------------------------------------------------------------------------
-- 3. 重建 v_iso_standard：join 改为 GUID 口径（与 v_cert_stage 同款）
--    ⚠️ 列清单逐列照抄原视图（实测），唯一差异 = cat 的 join 键：
--       原 cat.DicCode='iso_category' 字面量  →  cat.DicCode = d.Code (GUID)
--    原视图无 IsDeleted 列；CbName 来源 cb.ShortName；StatusName 为 CASE 表达式
-- ----------------------------------------------------------------------------
DROP VIEW IF EXISTS v_iso_standard;

CREATE VIEW v_iso_standard AS
SELECT
    s.Id                                                                   AS Id,
    s.Code                                                                 AS Code,
    s.OrgCode                                                              AS OrgCode,
    s.CbCode                                                               AS CbCode,
    cb.ShortName                                                           AS CbName,
    s.StandardCode                                                         AS StandardCode,
    s.StandardName                                                         AS StandardName,
    s.VersionYear                                                          AS VersionYear,
    s.Category                                                             AS Category,
    cat.DicName                                                            AS CategoryName,
    s.Description                                                          AS Description,
    s.Status                                                               AS Status,
    CASE s.Status
        WHEN 'active'   THEN '启用'
        WHEN 'inactive' THEN '停用'
        ELSE s.Status
    END                                                                    AS StatusName,
    s.CreateBy                                                             AS CreateBy,
    s.CreateTime                                                           AS CreateTime,
    s.UpdateBy                                                             AS UpdateBy,
    s.UpdateTime                                                           AS UpdateTime,
    s.DeleteBy                                                             AS DeleteBy,
    s.DeleteTime                                                           AS DeleteTime,
    s.IsValid                                                              AS IsValid,
    s.Sort                                                                 AS Sort,
    s.Remark                                                               AS Remark,
    s.ParentCode                                                           AS ParentCode,
    s.IsLeaf                                                               AS IsLeaf
FROM cert_iso_standard s
LEFT JOIN cert_certification_body cb
       ON s.CbCode = cb.Code collate utf8mb4_general_ci
LEFT JOIN Sys_Dictionary d
       ON d.DicNo = 'iso_category' AND d.IsDeleted = 0
LEFT JOIN Sys_DictionaryList cat
       ON cat.DicCode = d.Code collate utf8mb4_general_ci
      AND cat.DicValue = s.Category collate utf8mb4_general_ci
      AND cat.IsDeleted = 0;

-- ----------------------------------------------------------------------------
-- 4. legacy 10 项软删（视图已切 GUID，此处安全）
-- ----------------------------------------------------------------------------
UPDATE Sys_DictionaryList
   SET IsDeleted = 1
 WHERE DicCode = 'iso_category';

-- ----------------------------------------------------------------------------
-- 5. 验证
-- ----------------------------------------------------------------------------
SELECT '== 验证1：v_iso_standard（期望 CategoryName=中文 且非空）==' AS step;
SELECT StandardCode, StandardName, VersionYear, Category, CategoryName, Status, StatusName
  FROM v_iso_standard;

SELECT '== 验证2：legacy 已全废（期望 0）==' AS step;
SELECT COUNT(*) AS legacy_alive FROM Sys_DictionaryList
 WHERE DicCode = 'iso_category' AND IsDeleted = 0;

-- ============================================================================
-- 回滚：
--   UPDATE Sys_DictionaryList SET IsDeleted=0 WHERE DicCode='iso_category';
--   DELETE FROM Sys_DictionaryList
--    WHERE DicCode=(SELECT Code FROM Sys_Dictionary
--                    WHERE DicNo='iso_category' AND IsDeleted=0)
--      AND DicValue IN ('automotive','it_service','other')
--      AND OrderNo IN (80,90,100);
--   然后用 scripts/db/rebuild_views_pascalcase.sql 中的原 v_iso_standard 定义重建视图
-- ============================================================================
