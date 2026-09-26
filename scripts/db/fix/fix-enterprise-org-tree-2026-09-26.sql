-- =============================================================================
-- 修复：专家端「企业信息」机构树收敛（2026-09-26）
-- -----------------------------------------------------------------------------
-- 【作用】把历史遗留的 Sys_Organization 企业节点收敛到统一形态：
--           虚拟体系机构(L1) → 专家注册人员(L2) → 企业信息(L3) → 具体企业(L4)
--         并软删「幽灵节点」（企业已删但机构节点还在）与「重复节点」。
--
-- 【用法】★ 不要直接执行本文件 —— 用同目录包装脚本（含自锁 + 自动备份）：
--           ./fix-enterprise-org-tree-2026-09-26.sh          # 只诊断，不写库
--           ./fix-enterprise-org-tree-2026-09-26.sh --apply  # 备份后执行修复
--         手工执行（不推荐）：
--           docker exec -i yzh-mysql mysql -uroot -p*** \
--             --default-character-set=utf8mb4 yzh_cert_platform < 本文件
--         ⚠️ 本文件**不含 `USE`** —— 目标库由调用方给出，否则库名白名单自锁形同虚设。
--
-- 【依赖】docker 容器 yzh-mysql；表 `Sys_Organization` / `cert_enterprise`
--        前置只读诊断：`scripts/db/verify/verify-enterprise-org-tree.sql`（人读）
--        前置机器门禁：`scripts/db/verify/enterprise-org-gate.sql`（单行计数，供包装脚本判定）
--
-- 【维护人】脚本规范 V1（2026-09-26）
--
-- ⛔ 安全约束（scripts/README.md 铁律 B6）
--   本脚本会 `UPDATE Sys_Organization`（改挂靠 / 改名 / 同步状态）并**软删**节点。
--   · 执行前必须先跑 `verify-enterprise-org-tree.sql`，**特别看第 6 节**：
--     若有人员挂在企业节点上，软删后其 `OrgCode` 将指向不存在的节点
--     → **必须先人工决定这些人改挂到哪里**（改挂「管理员」分组 / 保留）。
--   · 步骤 4/5 是**软删**（`IsDeleted=1`）→ 改回 0、`IsValid` 改回 1 即可恢复。
--   · 包装脚本在写库前会自动 `mysqldump` 备份到 `scripts/db/backup/`。
--
-- 【背景】用户 2026-09-26 报告两个问题：
--   ① 机构树少了「企业信息」这一层 —— 期望
--      虚拟体系机构 → 专家注册人员 → **企业信息** → 具体企业，
--      实际 虚拟体系机构 → 专家注册人员 → 具体企业
--   ② 「我删除了企业信息，但真正的机构表中的虚拟体系机构中该企业信息还存在」
--      → 删企业没同步删机构节点（幽灵节点）
--
-- 【根因】
--   · 早期 EnterpriseController.AttachEnterpriseOrgNodeAsync 是「**就地改造**」策略：
--     把工作区下名为「企业用户」的 Dept 占位节点**本身**改成企业节点（OrgType=Enterprise, L3）
--     → 于是既没有「企业信息」文件夹，企业节点也挂错在 L2 下
--   · EnterpriseController 未覆写 DeleteCore / UpdateCore
--     → 删企业、改企业名都不同步 Sys_Organization
--
-- 【代码侧已修】src/certplatform-api/CertPlatform.Auditor/Controllers/EnterpriseController.cs
--   · 新策略：确保「企业信息」文件夹（L3）+ 企业节点挂其下（L4）
--   · 覆写 AddCore / UpdateCore / DeleteCore，全部自开事务 + 同步机构节点
--   · RegisterRowAction("disable"/"enable") → 行按钮禁用/启用
--   → **新建/改名/禁用/启用/删除 之后不会再产生新的不一致**；
--     本脚本只负责把**历史遗留**的存量数据收敛到同一形态。
--
-- 【幂等】全脚本可重复执行；步骤 3 带 `NOT (a <=> b)` 守卫，二次执行影响 0 行。
-- 【可逆】步骤 4/5 是**软删**（IsDeleted=1）→ 改回 0 即可恢复。
-- 【铁律十】SQL 外置（不写进 .sh）；B4 只读/写入分离；B7 校验失败看「验证」段。
-- =============================================================================

-- =============================================================================
-- 步骤 1：旧名「企业用户」→「企业信息」（只改 Dept 文件夹，不碰已改造成企业节点的）
--         保留 Code / OrgPath，故挂靠其下的人不受影响
-- =============================================================================
UPDATE `Sys_Organization`
SET `OrgName`    = '企业信息',
    `UpdateBy`   = 'repair-20260926',
    `UpdateTime` = NOW()
WHERE `OrgName` = '企业用户'
  AND `OrgType` = 'Dept'
  AND `IsDeleted` = 0;

-- =============================================================================
-- 步骤 2：为「有企业但缺「企业信息」文件夹」的工作区补建文件夹（L3, Sort=40）
--         用临时表先固化 UUID，避免在 INSERT..SELECT 里两次调用 UUID() 得到不同值
-- =============================================================================
DROP TEMPORARY TABLE IF EXISTS `tmp_ent_group`;

CREATE TEMPORARY TABLE `tmp_ent_group` AS
SELECT ws.`Code`                    AS WsCode,
       REPLACE(UUID(), '-', '')     AS GroupCode,
       ws.`OrgPath`                 AS WsPath
FROM `Sys_Organization` ws
WHERE ws.`OrgType` = 'VirtualOrg'
  AND ws.`OrgLevel` = 2
  AND ws.`IsDeleted` = 0
  AND EXISTS (SELECT 1 FROM `cert_enterprise` e
              WHERE e.`OrgCode` = ws.`Code` AND e.`IsDeleted` = 0)
  AND NOT EXISTS (SELECT 1 FROM `Sys_Organization` g
                  WHERE g.`ParentCode` = ws.`Code`
                    AND g.`OrgName` = '企业信息'
                    AND g.`OrgType` = 'Dept'
                    AND g.`IsDeleted` = 0);

INSERT INTO `Sys_Organization`
  (`Code`, `OrgName`, `OrgCode`, `ParentCode`, `OrgType`, `OrgLevel`, `OrgPath`, `Sort`,
   `IsValid`, `Remark`, `CreateBy`, `CreateTime`)
SELECT g.`GroupCode`,
       '企业信息',
       g.`GroupCode`,
       g.`WsCode`,
       'Dept',
       3,
       CONCAT(IFNULL(g.`WsPath`, CONCAT('/', g.`WsCode`)), '/', g.`GroupCode`),
       40,
       1,
       '企业文件夹｜企业节点挂靠于此（2026-09-26 修复脚本创建）',
       'repair-20260926',
       NOW()
FROM `tmp_ent_group` g;

DROP TEMPORARY TABLE IF EXISTS `tmp_ent_group`;

-- =============================================================================
-- 步骤 3：企业节点收敛 —— 改挂「企业信息」+ OrgLevel=4 + OrgPath + 名称/对接人/状态同步
--         ★ 这是修「虚拟体系机构 → 专家注册人员 → 企业信息 → 具体企业」的关键一步
-- =============================================================================
UPDATE `Sys_Organization` n
JOIN `cert_enterprise`   e ON e.`Code` = n.`OrgCode`
JOIN `Sys_Organization`  g ON g.`ParentCode` = e.`OrgCode`
                          AND g.`OrgName`    = '企业信息'
                          AND g.`OrgType`    = 'Dept'
                          AND g.`IsDeleted`  = 0
SET n.`ParentCode`  = g.`Code`,
    n.`OrgType`     = 'Enterprise',
    n.`OrgLevel`    = 4,
    n.`OrgPath`     = CONCAT(IFNULL(g.`OrgPath`, CONCAT('/', g.`Code`)), '/', n.`Code`),
    n.`OrgName`     = LEFT(e.`Name`, 200),
    n.`LeaderName`  = LEFT(e.`ContactName`, 50),
    n.`LeaderPhone` = LEFT(e.`ContactPhone`, 20),
    n.`IsValid`     = e.`IsValid`,
    n.`Remark`      = CONCAT('企业节点｜', e.`Name`, '（', IFNULL(e.`EnterpriseNo`, ''), '）'),
    n.`UpdateBy`    = 'repair-20260926',
    n.`UpdateTime`  = NOW()
WHERE n.`OrgType`   = 'Enterprise'
  AND n.`IsDeleted` = 0
  -- ★ 幂等守卫：只有确实不一致才动（<=> 是 NULL 安全等号）
  AND ( NOT (n.`ParentCode` <=> g.`Code`)
     OR NOT (n.`OrgLevel`   <=> 4)
     OR n.`OrgName` <> LEFT(e.`Name`, 200)
     OR n.`IsValid` <> e.`IsValid`
     OR IFNULL(n.`LeaderName`,  '') <> IFNULL(LEFT(e.`ContactName`,  50), '')
     OR IFNULL(n.`LeaderPhone`, '') <> IFNULL(LEFT(e.`ContactPhone`, 20), '')
     OR NOT (n.`OrgPath` <=> CONCAT(IFNULL(g.`OrgPath`, CONCAT('/', g.`Code`)), '/', n.`Code`)) );

-- =============================================================================
-- 步骤 4：同一企业多个节点 → 只留最早一个（Id 最小），其余软删
-- =============================================================================
UPDATE `Sys_Organization` n
JOIN (
    SELECT `OrgCode`, MIN(`Id`) AS KeepId
    FROM `Sys_Organization`
    WHERE `OrgType` = 'Enterprise' AND `IsDeleted` = 0 AND `OrgCode` IS NOT NULL
    GROUP BY `OrgCode`
    HAVING COUNT(*) > 1
) d ON d.`OrgCode` = n.`OrgCode` AND n.`Id` <> d.`KeepId`
SET n.`IsDeleted`  = 1,
    n.`IsValid`    = 0,
    n.`DeleteBy`   = 'repair-20260926',
    n.`DeleteTime` = NOW()
WHERE n.`OrgType` = 'Enterprise' AND n.`IsDeleted` = 0;

-- =============================================================================
-- 步骤 5：★ 幽灵节点软删（企业已软删 / 企业记录不存在）
-- -----------------------------------------------------------------------------
-- ⚠️⚠️ 执行前必读：先确认 verify 脚本第 6 节返回 0 行（没有人员挂在企业节点上）。
--      若有行 → 先决定这些人改挂到哪，否则软删后他们的 OrgCode 会指向不存在的节点。
--      软删可逆：把 IsDeleted 改回 0、IsValid 改回 1 即恢复。
-- =============================================================================
UPDATE `Sys_Organization` n
LEFT JOIN `cert_enterprise` e ON e.`Code` = n.`OrgCode`
SET n.`IsDeleted`  = 1,
    n.`IsValid`    = 0,
    n.`DeleteBy`   = 'repair-20260926',
    n.`DeleteTime` = NOW()
WHERE n.`OrgType`   = 'Enterprise'
  AND n.`IsDeleted` = 0
  AND (e.`Code` IS NULL OR e.`IsDeleted` = 1);

-- =============================================================================
-- 验证（逐段对照，全部应满足「期望」）
-- =============================================================================

-- 验证 1：机构树形态（期望：企业信息 L3 在企业节点 L4 之上，且企业节点的 ParentName = 企业信息）
SELECT '--- 验证 1. 虚拟体系机构子树 ---' AS Info;

SELECT
  CONCAT(REPEAT('    ', GREATEST(IFNULL(`OrgLevel`, 1) - 1, 0)), `OrgName`) AS Tree,
  `Code`, `OrgType`, `OrgLevel`, `ParentCode`, `OrgCode`, `Sort`, `IsValid`, `IsDeleted`
FROM `Sys_Organization`
WHERE `Code` = 'ORG_ROOT_003' OR `OrgPath` LIKE '/ORG_ROOT_003%'
ORDER BY IFNULL(`OrgPath`, `Code`), `Sort`, `OrgName`;

-- 验证 2：不一致清单（期望 0 行）—— 与 verify 脚本第 3 节同判据
SELECT '--- 验证 2. ★ 剩余不一致（期望 0 行）---' AS Info;

SELECT n.`Code` AS NodeCode, n.`OrgName` AS NodeName, n.`OrgLevel`,
       p.`OrgName` AS ParentName, e.`Name` AS EntName,
       CASE
         WHEN e.`Code` IS NULL          THEN '★ 幽灵节点'
         WHEN e.`IsDeleted` = 1         THEN '★ 幽灵节点'
         WHEN p.`OrgName` <> '企业信息' OR p.`OrgType` <> 'Dept' THEN '★ 挂错层'
         WHEN NOT (n.`OrgLevel` <=> 4)  THEN '★ 层级错'
         WHEN n.`OrgName` <> LEFT(e.`Name`, 200) THEN '★ 名称未同步'
         WHEN n.`IsValid` <> e.`IsValid` THEN '★ 状态未同步'
         ELSE 'OK'
       END AS Diagnosis
FROM `Sys_Organization` n
LEFT JOIN `Sys_Organization` p ON p.`Code` = n.`ParentCode`
LEFT JOIN `cert_enterprise`  e ON e.`Code` = n.`OrgCode`
WHERE n.`OrgType` = 'Enterprise' AND n.`IsDeleted` = 0
HAVING Diagnosis <> 'OK';

-- 验证 3：幽灵节点（期望 0 行）
SELECT '--- 验证 3. 幽灵节点（期望 0 行）---' AS Info;

SELECT n.`Code`, n.`OrgName`, n.`OrgCode`
FROM `Sys_Organization` n
LEFT JOIN `cert_enterprise` e ON e.`Code` = n.`OrgCode`
WHERE n.`OrgType` = 'Enterprise' AND n.`IsDeleted` = 0
  AND (e.`Code` IS NULL OR e.`IsDeleted` = 1);

-- 验证 4：缺「企业信息」文件夹的工作区（期望 0 行）
SELECT '--- 验证 4. 缺文件夹的工作区（期望 0 行）---' AS Info;

SELECT ws.`Code`, ws.`OrgName`
FROM `Sys_Organization` ws
JOIN `cert_enterprise` e ON e.`OrgCode` = ws.`Code` AND e.`IsDeleted` = 0
WHERE ws.`OrgType` = 'VirtualOrg' AND ws.`OrgLevel` = 2 AND ws.`IsDeleted` = 0
  AND NOT EXISTS (SELECT 1 FROM `Sys_Organization` g
                  WHERE g.`ParentCode` = ws.`Code` AND g.`OrgName` = '企业信息'
                    AND g.`OrgType` = 'Dept' AND g.`IsDeleted` = 0)
GROUP BY ws.`Code`, ws.`OrgName`;

-- 验证 5：本次操作影响的节点（审计留痕）
SELECT '--- 验证 5. 本次修复触碰的节点 ---' AS Info;

SELECT `Code`, `OrgName`, `OrgType`, `OrgLevel`, `IsValid`, `IsDeleted`, `UpdateBy`, `UpdateTime`
FROM `Sys_Organization`
WHERE `UpdateBy` = 'repair-20260926' OR `DeleteBy` = 'repair-20260926'
ORDER BY `UpdateTime`, `OrgName`;
