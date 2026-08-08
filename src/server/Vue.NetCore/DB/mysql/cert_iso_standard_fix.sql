-- ============================================================
-- ISO 标准管理 - 数据修复脚本 v4（最终版）
-- ============================================================

USE `yzh_cert_platform`;

-- Step 0: 获取父级 ID
SET @cert_dict_id = (SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'cert_dict' LIMIT 1);
SELECT CONCAT('✅ 父级字典 cert_dict ID=', @cert_dict_id) AS Info;

-- Step 1: standard_status 字典
INSERT IGNORE INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('ISO标准状态', 'standard_status', 1, @cert_dict_id, 35, NOW(), 1, '超级管理员');

SET @std_status_id = (SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'standard_status' LIMIT 1);

INSERT INTO `Sys_DictionaryList` (`DicName`, `DicValue`, `Dic_ID`, `Enable`, `OrderNo`, `Remark`, `CreateDate`, `CreateID`, `Creator`) VALUES
('草稿', 'draft', @std_status_id, 1, 10, '编辑中，未发布', NOW(), 1, '超级管理员'),
('已发布', 'published', @std_status_id, 1, 20, '已发布可使用', NOW(), 1, '超级管理员'),
('已停用', 'deprecated', @std_status_id, 1, 30, '已停用不可用', NOW(), 1, '超级管理员')
ON DUPLICATE KEY UPDATE DicName = VALUES(DicName);

SELECT CONCAT('✅ standard_status 字典完成, ID=', IFNULL(@std_status_id, 'NULL')) AS Result;

-- Step 2: cb_list 动态字典
INSERT IGNORE INTO `Sys_Dictionary` (`DicName`, `DicNo`, `Enable`, `ParentId`, `OrderNo`, `DbSql`, `CreateDate`, `CreateID`, `Creator`)
VALUES ('认证机构列表', 'cb_list', 1, @cert_dict_id, 36,
'SELECT Code as [key], Name as [value] FROM cert_certification_body WHERE Enable=1 ORDER BY Sort ASC, CreateDate ASC',
NOW(), 1, '超级管理员');

SELECT '✅ cb_list 动态字典完成' AS Result;

-- Step 3: 视图
DROP VIEW IF EXISTS `v_iso_standard`;

CREATE VIEW `v_iso_standard` AS
SELECT
    s.Id,
    s.CbCode,
    s.StandardCode,
    s.StandardName,
    s.VersionYear,
    s.Status,
    s.Remark,
    cb.Name AS CbName,
    cb.ShortName AS CbShortName
FROM cert_iso_standard s
LEFT JOIN cert_certification_body cb ON s.CbCode = cb.Code
WHERE s.Enable = 1;

SELECT '✅ v_iso_standard 视图创建完成' AS Result;

-- 验证
SELECT '=== 字典验证 ===' AS Info;
SELECT DicNo, DicName FROM Sys_Dictionary WHERE DicNo IN ('standard_status', 'cb_list');
SELECT '=== 视图验证 ===' AS Info;
SELECT Id, CbCode, CbName, StandardCode, StandardName FROM v_iso_standard LIMIT 5;
