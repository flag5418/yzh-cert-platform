-- =============================================================================
-- 只读诊断：专家端「企业信息」机构树一致性（2026-09-26）
-- -----------------------------------------------------------------------------
-- 【作用】回答四个问题，**不修改任何数据**：
--   ① 企业表里还有哪些真实企业（是否被误删）
--   ② 机构树里企业节点的**挂靠位置**对不对
--      期望：虚拟体系机构(L1) → 专家注册人员(L2) → 企业信息(L3) → 具体企业(L4)
--   ③ 有没有「幽灵节点」（企业已删但节点还在）—— 用户 2026-09-26 报告的问题
--   ④ ⚠️ 有没有**人员挂在企业节点上** —— 这是修复脚本能否安全执行的门禁
--
-- 【用法】./verify-enterprise-org-tree.sh                       # 推荐（包装脚本，只读）
--         docker exec -i yzh-mysql mysql -uroot -p*** \
--           --default-character-set=utf8mb4 yzh_cert_platform < 本文件
--         ⚠️ 本文件**不含 `USE`** —— 目标库由调用方给出（便于 YZH_DB_NAME 覆盖）
--
-- 【依赖】docker 容器 yzh-mysql；表 `Sys_Organization` / `cert_enterprise` / `Sys_User`
-- 【配套】修复脚本 `scripts/db/fix/fix-enterprise-org-tree-2026-09-26.sql`
--        机器门禁 `scripts/db/verify/enterprise-org-gate.sql`（单行计数，供包装脚本判定）
-- 【维护人】脚本规范 V1（2026-09-26）
--
-- 【铁律十 B4】只读放 `db/verify/`，写入放 `db/fix/` —— 本文件**只 SELECT**，可随时执行。
-- ⚠️ 【铁律八】本脚本**确实存在列 vs 列关联**（`n.OrgCode = e.Code`、`u.OrgCode = o.Code`
--    等跨表关联）→ 依赖全库已统一 `utf8mb4_general_ci`（2026-09-25 已执行 unify_collation）。
--    若报 `ERROR 1267`，说明有表未统一，先修 collation 再跑本脚本。
-- =============================================================================

-- =============================================================================
-- 1. 企业表现状（哪些企业还在）
--    ⛔ IsDeleted=1 是软删（Enterprise 是软删实体）；IsValid=0 是「禁用」不是「删除」
-- =============================================================================
SELECT '--- 1. cert_enterprise 全部行 ---' AS Info;

SELECT `Code`, `EnterpriseNo`, `Name`, `OrgCode`,
       `IsValid`, `IsDeleted`, `Status`, `CreateTime`
FROM `cert_enterprise`
ORDER BY `OrgCode`, `Sort`, `CreateTime`;

-- =============================================================================
-- 2. 虚拟体系机构子树全貌（缩进按 OrgPath 深度示意）
-- =============================================================================
SELECT '--- 2. 虚拟体系机构子树（含已软删，便于定位幽灵）---' AS Info;

SELECT
  CONCAT(REPEAT('    ', GREATEST(IFNULL(`OrgLevel`, 1) - 1, 0)), `OrgName`) AS Tree,
  `Code`,
  `OrgType`,
  `OrgLevel`,
  `ParentCode`,
  `OrgCode`,
  `Sort`,
  `IsValid`,
  `IsDeleted`,
  `OrgPath`
FROM `Sys_Organization`
WHERE `Code` = 'ORG_ROOT_003'
   OR `OrgPath` LIKE '/ORG_ROOT_003%'
   OR `OrgType` = 'Enterprise'          -- 挂错层的企业节点也捞出来
ORDER BY IFNULL(`OrgPath`, `Code`), `Sort`, `OrgName`;

-- =============================================================================
-- 3. ★ 核心诊断：每个企业节点的「挂靠位置 / 名称 / 状态」是否与企业表一致
--    期望 Diagnosis = 'OK'
-- =============================================================================
SELECT '--- 3. ★ 企业节点一致性诊断（期望全部 OK）---' AS Info;

SELECT
  n.`Code`        AS NodeCode,
  n.`OrgName`     AS NodeName,
  n.`OrgLevel`    AS NodeLevel,
  p.`OrgName`     AS ParentName,
  p.`OrgType`     AS ParentType,
  e.`Name`        AS EntName,
  e.`IsValid`     AS EntIsValid,
  e.`IsDeleted`   AS EntIsDeleted,
  CASE
    WHEN e.`Code` IS NULL                       THEN '★ 幽灵节点（企业记录已不存在）'
    WHEN e.`IsDeleted` = 1                      THEN '★ 幽灵节点（企业已软删）'
    WHEN p.`OrgName` IS NULL                    THEN '★ 父节点丢失'
    WHEN p.`OrgName` <> '企业信息'
      OR p.`OrgType` <> 'Dept'                  THEN '★ 挂错层（未挂在「企业信息」下）'
    WHEN n.`OrgLevel` <> 4                      THEN '★ 层级错（应为 4）'
    WHEN n.`OrgName` <> LEFT(e.`Name`, 200)     THEN '★ 名称未同步'
    WHEN IFNULL(n.`LeaderName`, '')  <> IFNULL(LEFT(e.`ContactName`, 50), '')
      OR IFNULL(n.`LeaderPhone`, '') <> IFNULL(LEFT(e.`ContactPhone`, 20), '')
                                                THEN '★ 对接人未同步'
    WHEN n.`IsValid` <> e.`IsValid`             THEN '★ 启用状态未同步'
    ELSE 'OK'
  END AS Diagnosis
FROM `Sys_Organization` n
LEFT JOIN `Sys_Organization` p ON p.`Code` = n.`ParentCode`
LEFT JOIN `cert_enterprise`  e ON e.`Code` = n.`OrgCode`
WHERE n.`OrgType` = 'Enterprise' AND n.`IsDeleted` = 0
ORDER BY Diagnosis, n.`OrgName`;

-- =============================================================================
-- 4. 幽灵节点清单（用户报告的问题）
-- =============================================================================
SELECT '--- 4. ★ 幽灵节点（企业已删但机构节点还在；期望 0 行）---' AS Info;

SELECT n.`Code`, n.`OrgName`, n.`ParentCode`, n.`OrgCode`, n.`OrgLevel`, n.`IsValid`, n.`CreateTime`
FROM `Sys_Organization` n
LEFT JOIN `cert_enterprise` e ON e.`Code` = n.`OrgCode`
WHERE n.`OrgType` = 'Enterprise'
  AND n.`IsDeleted` = 0
  AND (e.`Code` IS NULL OR e.`IsDeleted` = 1);

-- =============================================================================
-- 5. 缺少「企业信息」文件夹的工作区
-- =============================================================================
SELECT '--- 5. 缺「企业信息」文件夹的工作区（期望 0 行）---' AS Info;

SELECT ws.`Code` AS WsCode, ws.`OrgName` AS WsName, ws.`OrgCode` AS WsOrgCode,
       COUNT(e.`Code`) AS EntCount
FROM `Sys_Organization` ws
JOIN `cert_enterprise` e ON e.`OrgCode` = ws.`Code` AND e.`IsDeleted` = 0
WHERE ws.`OrgType` = 'VirtualOrg' AND ws.`OrgLevel` = 2 AND ws.`IsDeleted` = 0
  AND NOT EXISTS (
        SELECT 1 FROM `Sys_Organization` g
        WHERE g.`ParentCode` = ws.`Code`
          AND g.`OrgName` = '企业信息'
          AND g.`OrgType` = 'Dept'
          AND g.`IsDeleted` = 0)
GROUP BY ws.`Code`, ws.`OrgName`, ws.`OrgCode`;

-- =============================================================================
-- 6. ⚠️ 挂在「企业节点」上的人员
--    企业节点本不该挂人；若此处有行，说明历史「就地改造」把占位节点里的人员留在了企业节点上
--    → **软删节点前必须人工确认**（软删后这些人的 OrgCode 会指向不存在的节点）
-- =============================================================================
SELECT '--- 6. ⚠️ 挂在企业节点上的人员（期望 0 行；有行需人工决策）---' AS Info;

SELECT u.`Code` AS UserCode, u.`UserName`, u.`UserTrueName`,
       u.`OrgCode` AS UserOrgCode, o.`OrgName` AS NodeName, o.`IsDeleted` AS NodeDeleted
FROM `Sys_User` u
JOIN `Sys_Organization` o ON o.`Code` = u.`OrgCode`
WHERE o.`OrgType` = 'Enterprise'
  AND u.`IsDeleted` = 0;

-- =============================================================================
-- 7. 同一企业出现多个机构节点（期望 0 行）
-- =============================================================================
SELECT '--- 7. 企业节点重复（期望 0 行）---' AS Info;

SELECT `OrgCode`, COUNT(*) AS NodeCount, GROUP_CONCAT(`Code`) AS NodeCodes
FROM `Sys_Organization`
WHERE `OrgType` = 'Enterprise' AND `IsDeleted` = 0 AND `OrgCode` IS NOT NULL
GROUP BY `OrgCode`
HAVING COUNT(*) > 1;
