-- ============================================================
-- 2026-10-10 字典清理：删除 MES 业务字典 + NC 字典挂载到认证平台
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1. 删除 MES 业务的字典项（Sys_DictionaryList）
DELETE FROM Sys_DictionaryList
WHERE DicCode IN (
    '26b0ec50ae6c11f1953796fd503fd974',  -- mes业务（父字典）
    '26b0ec91ae6c11f1953796fd503fd974',  -- 仓库类型
    '26b0ecc9ae6c11f1953796fd503fd974',  -- 货位
    '26b0ed8eae6c11f1953796fd503fd974',  -- 仓库
    '26b0edcaae6c11f1953796fd503fd974',  -- 物料单位
    '26b0ee01ae6c11f1953796fd503fd974',  -- 设备列表
    '26b0ee40ae6c11f1953796fd503fd974',  -- 工序
    '26b0ee78ae6c11f1953796fd503fd974',  -- 物料分类
    '26b0eeb3ae6c11f1953796fd503fd974',  -- 物料列表
    '26b0eee2ae6c11f1953796fd503fd974',  -- 排产状态
    '26b0ef2dae6c11f1953796fd503fd974',  -- 优先级
    '26b0ef5cae6c11f1953796fd503fd974',  -- 用户列表
    '26b0ef8dae6c11f1953796fd503fd974'   -- 供应商
);

-- 2. 删除 MES 业务的字典定义（Sys_Dictionary）
-- 先删子级再删父级
DELETE FROM Sys_Dictionary
WHERE Code IN (
    '26b0ec91ae6c11f1953796fd503fd974',  -- 仓库类型
    '26b0ecc9ae6c11f1953796fd503fd974',  -- 货位
    '26b0ed8eae6c11f1953796fd503fd974',  -- 仓库
    '26b0edcaae6c11f1953796fd503fd974',  -- 物料单位
    '26b0ee01ae6c11f1953796fd503fd974',  -- 设备列表
    '26b0ee40ae6c11f1953796fd503fd974',  -- 工序
    '26b0ee78ae6c11f1953796fd503fd974',  -- 物料分类
    '26b0eeb3ae6c11f1953796fd503fd974',  -- 物料列表
    '26b0eee2ae6c11f1953796fd503fd974',  -- 排产状态
    '26b0ef2dae6c11f1953796fd503fd974',  -- 优先级
    '26b0ef5cae6c11f1953796fd503fd974',  -- 用户列表
    '26b0ef8dae6c11f1953796fd503fd974'   -- 供应商
);

DELETE FROM Sys_Dictionary
WHERE Code = '26b0ec50ae6c11f1953796fd503fd974';  -- mes业务（父字典）

-- 3. 将 NC Prompt 类型 和 NC 校验阶段 挂到认证平台字典下
UPDATE Sys_Dictionary
SET ParentCode = '26b0f1d2ae6c11f1953796fd503fd974',
    UpdateTime = NOW()
WHERE Code IN ('dct_nc_prompt_kind', 'dct_nc_validate')
  AND (ParentCode IS NULL OR ParentCode != '26b0f1d2ae6c11f1953796fd503fd974');

-- 4. 验证结果
SELECT '=== 剩余字典 ===' as '';
SELECT Id, DicName, DicNo, ParentCode, Code
FROM Sys_Dictionary
WHERE IsDeleted=0
ORDER BY Id;
