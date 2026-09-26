-- =============================================================================
-- 只读门禁：企业机构树修复前置条件（2026-09-26）
-- -----------------------------------------------------------------------------
-- 【作用】用**一条 SQL 返回一行**，把「修复脚本能否安全执行」变成机器可判定：
--         `PeopleOnEnterpriseNodes` = 0 才允许执行修复（其余列仅作参考）。
--         本文件是 `verify-enterprise-org-tree.sql`（人读全量诊断）的**机器门禁版**，
--         两者判据必须一致；改判据时**同时改两处**。
--
-- 【用法】./fix-enterprise-org-tree-2026-09-26.sh 内部自动调用（无需手工跑）
--         手工：docker exec -i yzh-mysql mysql -uroot -p*** -N -B \
--                 --default-character-set=utf8mb4 yzh_cert_platform < 本文件
--         ⚠️ 本文件**不含 `USE`** —— 目标库由调用方给出
--
-- 【列含义】
--   PeopleOnEnterpriseNodes  挂在 `OrgType='Enterprise'` 节点上的人员数
--                            ★ 修复门禁：> 0 → 拒绝执行（软删节点会让这些人 OrgCode 悬空）
--   GhostNodes               幽灵节点数（企业已软删/不存在，节点还在）
--   DuplicateNodeGroups      同一企业挂多个节点的组数
--   MissingFolders           有企业但缺「企业信息」文件夹的工作区数
--   LegacyFolders            仍叫「企业用户」的旧文件夹数（应为 0）
--   MisplacedNodes           企业节点未挂在「企业信息」下的数量
--   NameDesyncedNodes        节点名与企业名不一致的数量
--   ValidDesyncedNodes       节点 IsValid 与企业 IsValid 不一致的数量
--
-- 【依赖】docker 容器 yzh-mysql；表 `Sys_Organization` / `cert_enterprise` / `Sys_User`
-- 【维护人】脚本规范 V1（2026-09-26）
-- 【铁律十 B4】只读 —— 本文件**只 SELECT**，可随时执行
-- =============================================================================

SELECT
  -- ★ 门禁：挂在企业节点上的人员
  (SELECT COUNT(*)
     FROM `Sys_User` u
     JOIN `Sys_Organization` o ON o.`Code` = u.`OrgCode`
    WHERE o.`OrgType` = 'Enterprise'
      AND u.`IsDeleted` = 0)                                        AS PeopleOnEnterpriseNodes,

  -- 幽灵节点：企业不存在 或 已软删，但节点仍在
  (SELECT COUNT(*)
     FROM `Sys_Organization` n
     LEFT JOIN `cert_enterprise` e ON e.`Code` = n.`OrgCode`
    WHERE n.`OrgType` = 'Enterprise'
      AND n.`IsDeleted` = 0
      AND (e.`Code` IS NULL OR e.`IsDeleted` = 1))                  AS GhostNodes,

  -- 同一企业多个节点
  (SELECT COUNT(*)
     FROM (SELECT `OrgCode`
             FROM `Sys_Organization`
            WHERE `OrgType` = 'Enterprise' AND `IsDeleted` = 0 AND `OrgCode` IS NOT NULL
            GROUP BY `OrgCode`
           HAVING COUNT(*) > 1) d)                                  AS DuplicateNodeGroups,

  -- 有企业但缺「企业信息」文件夹的工作区
  (SELECT COUNT(*)
     FROM `Sys_Organization` ws
     JOIN `cert_enterprise` e ON e.`OrgCode` = ws.`Code` AND e.`IsDeleted` = 0
    WHERE ws.`OrgType` = 'VirtualOrg' AND ws.`OrgLevel` = 2 AND ws.`IsDeleted` = 0
      AND NOT EXISTS (SELECT 1 FROM `Sys_Organization` g
                       WHERE g.`ParentCode` = ws.`Code`
                         AND g.`OrgName` = '企业信息'
                         AND g.`OrgType` = 'Dept'
                         AND g.`IsDeleted` = 0))                    AS MissingFolders,

  -- 仍叫「企业用户」的旧文件夹
  (SELECT COUNT(*)
     FROM `Sys_Organization`
    WHERE `OrgName` = '企业用户' AND `OrgType` = 'Dept' AND `IsDeleted` = 0) AS LegacyFolders,

  -- 企业节点未挂在「企业信息」下
  (SELECT COUNT(*)
     FROM `Sys_Organization` n
     LEFT JOIN `Sys_Organization` p ON p.`Code` = n.`ParentCode`
    WHERE n.`OrgType` = 'Enterprise' AND n.`IsDeleted` = 0
      AND (p.`OrgName` IS NULL OR p.`OrgName` <> '企业信息' OR p.`OrgType` <> 'Dept')) AS MisplacedNodes,

  -- 节点名与企业名不一致
  (SELECT COUNT(*)
     FROM `Sys_Organization` n
     JOIN `cert_enterprise` e ON e.`Code` = n.`OrgCode`
    WHERE n.`OrgType` = 'Enterprise' AND n.`IsDeleted` = 0
      AND n.`OrgName` <> LEFT(e.`Name`, 200))                       AS NameDesyncedNodes,

  -- 节点 IsValid 与企业 IsValid 不一致
  (SELECT COUNT(*)
     FROM `Sys_Organization` n
     JOIN `cert_enterprise` e ON e.`Code` = n.`OrgCode`
    WHERE n.`OrgType` = 'Enterprise' AND n.`IsDeleted` = 0
      AND n.`IsValid` <> e.`IsValid`)                               AS ValidDesyncedNodes;
