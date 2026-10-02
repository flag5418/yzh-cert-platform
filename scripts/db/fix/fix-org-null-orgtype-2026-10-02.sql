-- =============================================================================
-- 修复 Sys_Organization.OrgType 空值脏数据 + 固化 OrgType 管理边界（2026-10-02）
-- -----------------------------------------------------------------------------
-- 【背景】用户裁决：Sys_Organization.OrgType 是**系统内部标识**，不允许在
--   机构管理页面（/system/organization）手工录入或编辑 —— 各机构类型由业务系统
--   按用途自动写入：
--     Platform   平台根节点（种子数据）
--     CertBody   认证机构（认证机构挂载时创建）
--     VirtualOrg 虚拟体系机构 / 专家工作区（专家系统「专家注册」创建）
--     Dept       部门/文件夹（管理端手工维护 + 业务系统建的角色分组）
--     Enterprise 企业（专家系统「新增企业」在「企业信息」分组下创建）
--
--   据此做了两件事：
--   ① 录入表单移除「机构类型」字段
--      → Assets/EntityConfigs/System/OrganizationForm.json 删除 OrgType 列
--      → OrganizationController.OnBeforeAddTree 强制 entity.OrgType = 'Dept'
--      → OrganizationController.OnBeforeUpdateTree 从原记录回填 OrgType
--        （OrgType 不再随表单回传，而 SqlSugar 更新是全列覆盖，不回填会清成 NULL）
--   ② 仅 OrgType='Dept' 的机构允许在管理端修改/删除
--      → 前端 OrgPageLogic.resolveTreeActions 隐藏「新增下级 / 编辑 / 删除」按钮
--      → 后端 OnBeforeUpdateTree / OnBeforeDeleteTree 硬拦截（防绕过前端直调 API）
--      → 「禁用 / 启用」保留：停用业务系统建的机构仍属管理员职责
--
-- 【本脚本职责】修复历史脏数据：OrgType 为 NULL / 空串的行。
--   成因（已修）：编辑弹窗回填取不到 OrgType（Extra 键是 camelCase `orgType`，
--   前端按 PascalCase `OrgType` 读 → miss），提交时 payload 不含该字段，
--   而 SqlSugar 更新为全列覆盖 → DB 中已有的 OrgType 被写空。
--
-- 幂等：UPDATE 带 WHERE 条件，重复执行无副作用。
-- =============================================================================

USE `yzh_cert_platform`;

-- ① 修复前快照（人工核对：这些行原本都是管理端建的根/部门层级节点）
SELECT 'BEFORE_null_orgtype' AS chk, Id, Code, OrgName, ParentCode, OrgLevel, IsValid, IsDeleted
FROM Sys_Organization
WHERE OrgType IS NULL OR TRIM(OrgType) = '';

-- ② 补齐为空值（DB 列默认值本就是 'Dept'）
UPDATE `Sys_Organization`
SET `OrgType` = 'Dept'
WHERE `OrgType` IS NULL OR TRIM(`OrgType`) = '';

-- ============================ 验证 ============================
-- ① 无空值残留（期望 0 行）
SELECT 'VERIFY_no_null_orgtype' AS chk, COUNT(*) AS cnt
FROM Sys_Organization
WHERE OrgType IS NULL OR TRIM(OrgType) = '';

-- ② 机构类型分布（管理端只读边界一览；仅 Dept 可在 /system/organization 改删）
SELECT 'VERIFY_orgtype_dist' AS chk, `OrgType`, COUNT(*) AS cnt
FROM Sys_Organization
WHERE `IsDeleted` = 0
GROUP BY `OrgType`
ORDER BY `OrgType`;

-- ③ v_sys_user 视图带的 OrgType 同步为非空
SELECT 'VERIFY_view_orgtype_null' AS chk, COUNT(*) AS cnt
FROM v_sys_user
WHERE OrgType IS NULL OR TRIM(OrgType) = '';