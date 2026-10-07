-- =============================================================================
-- 机构树 ↔ 认证机构业务表 一致性清理 V1
-- 生成日期：2026-10-07
-- 目标库：yzh_cert_platform（MySQL 8.0 @127.0.0.1:3307 / 容器 yzh-mysql）
--
-- 【背景】
--   左树读 Sys_Organization，认证机构管理列表读 cert_certification_body。
--   二者靠 Sys_Organization.Code = cert_certification_body.OrgCode 一一对应：
--   只有「从认证机构管理页新增」的记录才会在同一事务里同时写两张表
--   （CertificationBodyController.AddCore → BuildOrgNode）。
--   而「认证机构」根（ORG_ROOT_002）下有 8 个节点来自早期 demo seed
--   （scripts/db/backup/org_tree_before_fix_*.sql 一脉）、联调残留
--   （接口驱动测试 / E2E_F03_probe_*）与手工插入 → 业务表里没有对应记录，
--   表现为「树里有一堆认证机构、列表里只有 2 个」。
--
-- 【清理范围】（2026-10-07 用户裁决：只清孤儿认证机构节点）
--   ① 软删 8 个孤儿节点（IsDeleted=1，可回滚）
--   ② 唯一受影响的人员 wqq(USER_003376) 迁到真实机构节点 906e8b2a…
--   ③ 修复 2 处数据漂移（名称 / 编号未同步）
--   ⛔ 不动 44 / 444 / 555444 等手输测试机构；不动「专家系统」工作区
--      （ORG_ROOT_003 → 尚龙认证 · 王卿权 → 企业信息/审核员/…）
--
-- 【回滚】
--   UPDATE Sys_Organization SET IsDeleted=0 WHERE Code IN ('ORG_001_001', …);
--   UPDATE Sys_User SET OrgCode='5833c904-…' WHERE Code='USER_003376';
--   完整备份：scripts/db/backup/org_before_orphan_cleanup_<ts>.sql
-- =============================================================================

SET NAMES utf8mb4;

-- -----------------------------------------------------------------------------
-- 0. 执行前核对：应返回 8 行「孤儿」节点（业务表中无对应记录）
-- -----------------------------------------------------------------------------
SELECT o.Code, o.OrgName, o.OrgType, o.IsValid,
       (SELECT COUNT(*) FROM Sys_User u WHERE u.OrgCode = o.Code AND u.IsDeleted = 0) AS 人员数
FROM Sys_Organization o
WHERE o.ParentCode = 'ORG_ROOT_002'
  AND o.IsDeleted = 0
  AND NOT EXISTS (
        SELECT 1 FROM cert_certification_body c
        WHERE c.OrgCode = o.Code AND c.IsDeleted = 0
      )
ORDER BY o.OrgName;

-- -----------------------------------------------------------------------------
-- 1. 人员迁移：把「河北雄安尚龙认证有限公司」目录下的人员挪到真实机构节点
--    5833c904-… 是手工插入的重复节点（其 OrgCode=CB001 与业务表 Id=9 撞号），即将软删；
--    906e8b2a… 才是业务表 Id=9（河北雄安尚龙认证有限公司 / CB001）对应的机构节点。
-- -----------------------------------------------------------------------------
UPDATE Sys_User
SET OrgCode    = '906e8b2a962c4062b21144af4cc4abc0',
    UpdateBy   = 'orphan_cleanup_20261007',
    UpdateTime = NOW()
WHERE OrgCode = '5833c904-a802-11f1-9d10-96af2c2a4a1a'
  AND IsDeleted = 0;

-- -----------------------------------------------------------------------------
-- 2. 修复漂移 1：机构节点 906e8b2a… 的 OrgName / OrgCode 与业务表同步
--    业务表（Id=9）为源：Name=河北雄安尚龙认证有限公司，CbCode=CB001
--    （业务侧曾被 manual_fix_20260925 直接 SQL 修改 → 树侧名称/编号未跟上）
-- -----------------------------------------------------------------------------
UPDATE Sys_Organization o
JOIN cert_certification_body c
  ON c.OrgCode = o.Code AND c.IsDeleted = 0
SET o.OrgName    = c.Name,
    o.OrgCode    = c.CbCode,
    o.UpdateBy   = 'orphan_cleanup_20261007',
    o.UpdateTime = NOW()
WHERE o.Code IN ('906e8b2a962c4062b21144af4cc4abc0', '1579641b3d61498bba78dcbc959bd4ba')
  AND o.IsDeleted = 0
  AND (BINARY o.OrgName <> BINARY c.Name OR NOT (o.OrgCode <=> c.CbCode));

-- -----------------------------------------------------------------------------
-- 3. 软删 8 个孤儿节点（保留数据，只置 IsDeleted=1；DeleteBy/DeleteTime 留痕）
-- -----------------------------------------------------------------------------
UPDATE Sys_Organization
SET IsDeleted  = 1,
    DeleteBy   = 'orphan_cleanup_20261007',
    DeleteTime = NOW()
WHERE Code IN (
        'ORG_001_001',                             -- 北京认证中心（demo seed）
        'ORG_001_002',                             -- 上海认证中心（demo seed）
        'ORG_001_003',                             -- 广州审核小组（demo seed，OrgType=VirtualOrg）
        'b7d5e6f94f3a4bc6b73a961b737fa9a0',        -- Test Org（手输）
        '5833c904-a802-11f1-9d10-96af2c2a4a1a',    -- 河北雄安尚龙认证有限公司（与业务表 CbCode=CB001 撞号的重复节点）
        'f348ca9181454bfcb5d8fb3df14ac5cc',        -- 接口驱动测试（联调残留）
        'ad51a7609fee4adba1305813fccd5c33',        -- 6（手输）
        '136c18938f4747cd8f95a44ab05a1473'         -- E2E_F03_probe_1790314220（E2E 残留）
      )
  AND IsDeleted = 0;

-- -----------------------------------------------------------------------------
-- 4. 执行后验证（三条都应与预期一致）
-- -----------------------------------------------------------------------------

-- 4.1 认证机构根下「树节点数」应等于「业务表记录数」= 2
SELECT '4.1 树节点 vs 业务表' AS 检查项,
       (SELECT COUNT(*) FROM Sys_Organization
         WHERE ParentCode = 'ORG_ROOT_002' AND IsDeleted = 0 AND OrgType = 'CertBody') AS 树节点数,
       (SELECT COUNT(*) FROM cert_certification_body WHERE IsDeleted = 0)             AS 业务表数;

-- 4.2 名称/编号漂移应清零（期望 0 行）
SELECT o.Code, o.OrgName, o.OrgCode AS 树编号, c.Name, c.CbCode AS 业务编号
FROM Sys_Organization o
JOIN cert_certification_body c ON c.OrgCode = o.Code AND c.IsDeleted = 0
WHERE o.IsDeleted = 0
  AND (BINARY o.OrgName <> BINARY c.Name OR NOT (o.OrgCode <=> c.CbCode));

-- 4.3 不应存在「挂在已软删机构下」的未删人员（期望 0 行；
--     ⚠️ 直接 NOT REGEXP 匹配列名在本库会因排序规则失效，故此处只做数据核对）
SELECT u.Code, u.UserName, u.OrgCode, o.OrgName, o.IsDeleted
FROM Sys_User u
JOIN Sys_Organization o ON o.Code = u.OrgCode
WHERE u.IsDeleted = 0 AND o.IsDeleted = 1;
