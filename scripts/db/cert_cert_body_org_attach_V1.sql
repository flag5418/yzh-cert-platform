-- ============================================================
-- 认证机构（机构-Attach 试点）数据库补齐脚本 V1
--
-- 作用：
--   1. cert_certification_body.OrgCode 建索引
--   2. Status 空值补 'active'
--   3. 存量记录回填 OrgCode = Code（单 Code 契约）
--   4. 为缺失系统机构记录的认证机构补建 Sys_Organization 记录
--
-- 机构-Attach 契约：
--   cert_certification_body.Code == .OrgCode == Sys_Organization.Code
--   挂载点：Sys_Organization 中 OrgType='CertBody' 且无父节点的根节点
--
-- 幂等：可重复执行
--
-- 用法：
--   docker exec -i yzh-mysql mysql -uroot -p'Yzh123456.' yzh_cert_platform \
--     < scripts/db/cert_cert_body_org_attach_V1.sql
-- ============================================================

-- ------------------------------------------------------------
-- 1. OrgCode 索引（MySQL 8.0 不支持 ADD INDEX IF NOT EXISTS，用动态 SQL 兜底）
-- ------------------------------------------------------------
SET @idx_exists := (
    SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'cert_certification_body'
      AND INDEX_NAME = 'idx_org_code'
);
SET @ddl := IF(@idx_exists = 0,
    'ALTER TABLE cert_certification_body ADD INDEX idx_org_code (OrgCode)',
    'SELECT ''idx_org_code 已存在，跳过'' AS msg');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 2. Status 空值补默认值
-- ------------------------------------------------------------
UPDATE cert_certification_body
SET Status = 'active'
WHERE Status IS NULL OR Status = '';

-- ------------------------------------------------------------
-- 3. 存量回填：OrgCode = Code（单 Code 契约）
-- ------------------------------------------------------------
UPDATE cert_certification_body
SET OrgCode = Code
WHERE OrgCode IS NULL OR OrgCode = '';

-- ------------------------------------------------------------
-- 4. 为缺失系统机构记录的认证机构补建 Sys_Organization 记录
--    注：Sys_Organization 的审计列使用历史命名（Creator/CreateDate），Id 为自增
-- ------------------------------------------------------------
SET @certbody_root := (
    SELECT Code FROM Sys_Organization
    WHERE OrgType = 'CertBody'
      AND (ParentCode IS NULL OR ParentCode = '')
      AND IsDeleted = 0
    ORDER BY Sort ASC, Id ASC
    LIMIT 1
);

INSERT INTO Sys_Organization
    (Code, OrgName, OrgCode, ParentCode, OrgType, OrgLevel, OrgPath,
     LeaderName, LeaderPhone, Sort, Enable, IsValid, Remark, Creator, CreateDate, IsDeleted)
SELECT
    c.Code,
    c.Name,
    c.CbCode,
    @certbody_root,
    'CertBody',
    2,
    CONCAT('/', @certbody_root, '/', c.Code),
    c.ContactName,
    c.ContactPhone,
    COALESCE(c.Sort, 0),
    CASE WHEN COALESCE(c.IsValid, 1) = 1 THEN 1 ELSE 0 END,
    1,
    c.Remark,
    'SYSTEM',
    NOW(),
    0
FROM cert_certification_body c
WHERE c.IsDeleted = 0
  AND @certbody_root IS NOT NULL
  -- 两表排序规则不同（general_ci vs unicode_ci），比较时需显式 COLLATE
  AND NOT EXISTS (
        SELECT 1 FROM Sys_Organization o
        WHERE o.Code COLLATE utf8mb4_unicode_ci = c.Code COLLATE utf8mb4_unicode_ci
      );

-- ------------------------------------------------------------
-- 5. 校验输出
-- ------------------------------------------------------------
SELECT '认证机构数' AS label, COUNT(*) AS cnt
FROM cert_certification_body WHERE IsDeleted = 0
UNION ALL
SELECT '已挂接机构数', COUNT(*)
FROM cert_certification_body c
WHERE c.IsDeleted = 0
  AND EXISTS (
        SELECT 1 FROM Sys_Organization o
        WHERE o.Code COLLATE utf8mb4_unicode_ci = c.Code COLLATE utf8mb4_unicode_ci
          AND o.IsDeleted = 0
      );
